using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleTweaks
{
    /// <summary>
    /// A list of Unity objects that keeps live entries reachable and drops entries once Unity
    /// reports them destroyed. Unity does not support weak references to
    /// <see cref="UnityEngine.Object"/> instances: the managed wrapper can be collected while
    /// the native object is still alive.
    /// </summary>
    internal sealed class LiveObjectList<T> where T : UnityEngine.Object
    {
        private const int PruneEvery = 32;
        private const int PruneEveryAddBelow = 128;

        private readonly List<T> Items = new List<T>();
        private int _addsSincePrune;

        internal void Add(T item)
        {
            if ((UnityEngine.Object)item == null) return;

            Items.Add(item);
            _addsSincePrune++;
            if (_addsSincePrune >= PruneEvery || Items.Count <= PruneEveryAddBelow) Prune();
        }

        internal void Visit(Action<T> action)
        {
            try
            {
                for (int i = Items.Count - 1; i >= 0; i--)
                {
                    T item = Items[i];
                    if ((UnityEngine.Object)item == null)
                    {
                        Items.RemoveAt(i);
                        continue;
                    }
                    action(item);
                }
            }
            finally
            {
                // Every caller runs during teardown. ScriptEngine keeps the old assembly (and
                // therefore these static lists) loaded, so release the processed controls.
                Items.Clear();
                _addsSincePrune = 0;
            }
        }

        private void Prune()
        {
            _addsSincePrune = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if ((UnityEngine.Object)Items[i] == null) Items.RemoveAt(i);
            }
        }
    }
}
