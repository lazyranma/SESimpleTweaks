using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SimpleTweaks
{
    /// <summary>
    /// Records everything the plugin writes outside its Harmony patches so a ScriptEngine
    /// reload can put the game back into the state a vanilla install would have.
    /// <para>
    /// ScriptEngine destroys the plugin component and loads a fresh assembly, which leaves
    /// every GameObject, component and field the old instance created or changed behind.
    /// Harmony patches are removed separately in <see cref="Plugin.OnDestroy"/>; this
    /// registry covers the rest.
    /// </para>
    /// <para>
    /// Records for objects are weakly held. The game destroys and recreates search rows and
    /// other controls during normal play; a strong list would keep those dead wrappers (and
    /// the callbacks that capture their transforms) alive for the whole session.
    /// </para>
    /// </summary>
    internal static class Teardown
    {
        private static readonly List<Action> Restorers = new List<Action>();
        private static readonly List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();
        private static readonly LiveObjectList<Component> AddedComponents = new LiveObjectList<Component>();
        private static readonly LiveObjectList<GameObject> CreatedObjects = new LiveObjectList<GameObject>();
        private static readonly LiveObjectList<RectTransform> PositionKeys = new LiveObjectList<RectTransform>();
        private static readonly ConditionalWeakTable<RectTransform, PositionSnapshot> Positions =
            new ConditionalWeakTable<RectTransform, PositionSnapshot>();

        private sealed class PositionSnapshot
        {
            internal bool Taken;
            internal Vector2 Value;
        }

        private sealed class CoroutineHandle
        {
            internal MonoBehaviour Runner;
            internal Coroutine Handle;
        }

        /// <summary>Registers a callback that undoes a mutation of game state.</summary>
        internal static void TrackRestore(Action restore)
        {
            if (restore != null) Restorers.Add(restore);
        }

        /// <summary>Registers a component the plugin added to a game-owned object.</summary>
        internal static void TrackAddedComponent(Component component)
        {
            AddedComponents.Add(component);
        }

        /// <summary>Registers a GameObject the plugin created.</summary>
        internal static void TrackCreated(GameObject gameObject)
        {
            CreatedObjects.Add(gameObject);
        }

        /// <summary>
        /// Saves the current anchored position once per transform and restores it on teardown.
        /// Tweaks that shift a vanilla element to make room for an inserted button use this.
        /// </summary>
        internal static void TrackAnchoredPosition(RectTransform rect)
        {
            if (rect == null) return;

            PositionSnapshot snapshot = Positions.GetValue(rect, _ => new PositionSnapshot());
            if (snapshot.Taken) return;

            snapshot.Value = rect.anchoredPosition;
            snapshot.Taken = true;
            PositionKeys.Add(rect);
        }

        /// <summary>
        /// Starts a coroutine and stops it on teardown. The record removes itself when the
        /// coroutine finishes, so repeated UI rebuilds do not accumulate stale entries.
        /// </summary>
        internal static Coroutine StartCoroutine(MonoBehaviour runner, IEnumerator routine)
        {
            if (runner == null || routine == null) return null;

            var entry = new CoroutineHandle { Runner = runner };
            entry.Handle = runner.StartCoroutine(Wrap(routine, entry));
            Coroutines.Add(entry);
            return entry.Handle;
        }

        private static IEnumerator Wrap(IEnumerator routine, CoroutineHandle entry)
        {
            try
            {
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }
            }
            finally
            {
                Coroutines.Remove(entry);
            }
        }

        /// <summary>
        /// Undoes every tracked change. Each step is guarded independently so one failure
        /// cannot leave the remaining changes behind in the reloaded game.
        /// </summary>
        internal static void Run()
        {
            // Stop work that would otherwise keep running against the unloaded assembly.
            // Copy first: stopping a coroutine can run its finally block and modify the list.
            CoroutineHandle[] running = Coroutines.ToArray();
            Coroutines.Clear();
            foreach (CoroutineHandle coroutine in running)
            {
                try
                {
                    if (coroutine.Runner != null) coroutine.Runner.StopCoroutine(coroutine.Handle);
                }
                catch (Exception ex)
                {
                    Report("stop a tracked coroutine", ex);
                }
            }

            for (int i = Restorers.Count - 1; i >= 0; i--)
            {
                try
                {
                    Restorers[i]();
                }
                catch (Exception ex)
                {
                    Report("restore game state", ex);
                }
            }
            Restorers.Clear();

            TooltipHook.RestoreAll();

            PositionKeys.Visit(rect =>
            {
                try
                {
                    if (Positions.TryGetValue(rect, out PositionSnapshot snapshot))
                        rect.anchoredPosition = snapshot.Value;
                }
                catch (Exception ex)
                {
                    Report("restore a UI position", ex);
                }
            });

            AddedComponents.Visit(component =>
            {
                try
                {
                    UnityEngine.Object.Destroy(component);
                }
                catch (Exception ex)
                {
                    Report("remove an added component", ex);
                }
            });

            CreatedObjects.Visit(gameObject =>
            {
                try
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
                catch (Exception ex)
                {
                    Report("remove a created object", ex);
                }
            });
        }

        private static void Report(string step, Exception error)
        {
            if (Plugin.Log != null)
            {
                Plugin.Log.LogError($"[SimpleTweaks] Teardown could not {step}: {error}");
            }
        }
    }
}
