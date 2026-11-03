using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.UI.Windows.Elements;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Game.UI.Windows.Windows;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
#pragma warning disable IDE0051

namespace SimpleTweaks
{
    /// <summary>
    /// Don't Blink - stops the Object Info section scrollbars from blinking on every window refresh.
    /// <para>
    /// UIList.ConformSizeAndScrollbarsToVisibleContent() ends with
    /// <c>scrollView.verticalScrollbar.gameObject.SetActive(scrollView.vertical)</c>. ScrollRect.vertical
    /// only says whether vertical scrolling is enabled (true for these lists), so every list rebuild
    /// force-shows the bar. The sections' ScrollRects are serialized as AutoHideAndExpandViewport, and
    /// Unity re-hides the bar in ScrollRect.LateUpdate when the content fits. In the steady state
    /// ScrollRectDisabler keeps the ScrollRect disabled (it mirrors the bar's active state into
    /// ScrollRect.enabled), so that LateUpdate is skipped in the frame of the rebuild: the bar is
    /// rendered for one frame and hidden on the next - the blink, once per refresh (~1/s while the
    /// window is open and dirty).
    /// </para>
    /// <para>
    /// This postfix replaces the forced show with the state Unity's own AutoHide pass is about to
    /// arrive at, so the bar never changes state for a one-frame flash. When the content really
    /// overflows it still activates the bar, which is what lets ScrollRectDisabler re-enable the
    /// ScrollRect.
    /// </para>
    /// <para>
    /// If another mod replaced the native sizing (the postfix sees __runOriginal == false), nothing is
    /// touched. That is the case for "Panel Spreading - Planet UI Fix", which sizes sections to their
    /// content and hides the inner scrollbars itself, so maxRows-based assumptions do not apply.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    public static class Patch_UIList_Conform_DontBlink
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.DontBlink.Value;

        private static readonly Type[] ListTypes =
        {
            typeof(UIFacilityList),
            typeof(UIResorcesList),
            typeof(UIExploredResourcesList),
            typeof(UIRocketList),
            typeof(UIMissionsList),
        };

        private static readonly Dictionary<Type, ListRefs> Refs = new Dictionary<Type, ListRefs>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var seen = new List<MethodBase>(ListTypes.Length);
            foreach (Type listType in ListTypes)
            {
                MethodInfo method = FindConform(listType)
                    ?? throw new MissingMethodException(listType.FullName, "ConformSizeAndScrollbarsToVisibleContent");
                if (seen.Contains(method)) continue;
                seen.Add(method);
                yield return method;
            }
        }

        private static MethodInfo FindConform(Type listType)
        {
            for (Type type = listType; type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(
                    "ConformSizeAndScrollbarsToVisibleContent",
                    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null) return method;
            }
            return null;
        }

        private static void Postfix(object __instance, bool __runOriginal)
        {
            if (!__runOriginal) return;
            if (!(__instance is MonoBehaviour list)) return;

            ListRefs refs = GetRefs(list.GetType());
            if (refs == null) return;

            // Mirrors the native guards: the list was blocked during SetData, or it does not
            // manage its own view at all (maxRows == 0).
            if ((bool)refs.Blocked.GetValue(list)) return;
            int maxRows = (int)refs.MaxRows.GetValue(list);
            if (maxRows == 0) return;
            if (!(refs.ScrollView.GetValue(list) is ScrollRect scrollView) || scrollView == null) return;
            if (scrollView.verticalScrollbar == null || scrollView.content == null) return;

            // This tweak targets the planet panel; leave other users of these list types alone.
            if (scrollView.GetComponentInParent<ObjectInfoWindow>(true) == null) return;

            // A collapsed section keeps its scroll view inactive; leave the bar as the game set it
            // so it is there when the section is opened again.
            if (!scrollView.gameObject.activeInHierarchy) return;

            // Only AutoHide modes let us decide; Permanent is Unity's call, not ours.
            if (scrollView.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent) return;

            bool needed;
            if (!scrollView.vertical)
            {
                needed = false;
            }
            else if (!(bool)refs.ListViewExpanded.GetValue(list))
            {
                // Collapsed: the view was just sized to min(maxRows, rows) * rowHeight, so the
                // content overflows only when there are more visual rows than maxRows.
                int perRow = (int)refs.ItemsInARow.GetValue(list);
                if (perRow <= 0) return;

                int visualRows = Mathf.CeilToInt(CountActiveRows(refs.CreateRows.GetValue(list)) / (float)perRow);
                needed = visualRows > maxRows;
            }
            else
            {
                // Expanded sections keep the height set by ObjectInfoCollapseSections, so measure.
                RectTransform content = scrollView.content;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

                RectTransform view = scrollView.viewport != null
                    ? scrollView.viewport
                    : content.parent as RectTransform;
                if (view == null) return;

                needed = content.rect.height > view.rect.height + 0.01f;
            }

            GameObject bar = scrollView.verticalScrollbar.gameObject;
            if (bar.activeSelf != needed) bar.SetActive(needed);
        }

        private static int CountActiveRows(object rowsObject)
        {
            if (!(rowsObject is IList rows)) return 0;

            int active = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is Component row && row != null && row.gameObject.activeSelf) active++;
            }
            return active;
        }

        private static ListRefs GetRefs(Type listType)
        {
            lock (Refs)
            {
                if (Refs.TryGetValue(listType, out ListRefs refs)) return refs;

                Type baseType = listType;
                while (baseType != null &&
                       !(baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(UIList<,>)))
                {
                    baseType = baseType.BaseType;
                }
                if (baseType == null) return null;

                refs = new ListRefs
                {
                    ScrollView = Field(baseType, "scrollView"),
                    CreateRows = Field(baseType, "creatRows"),
                    MaxRows = Field(baseType, "maxRows"),
                    ItemsInARow = Field(baseType, "itemsInARow"),
                    ListViewExpanded = Field(baseType, "listViewExpanded"),
                    Blocked = baseType.GetProperty(
                        "BlockConformSizeAndScrollbarsToVisibleContent",
                        BindingFlags.Instance | BindingFlags.Public)
                        ?? throw new MissingMemberException(
                            baseType.FullName, "BlockConformSizeAndScrollbarsToVisibleContent"),
                };

                Refs[listType] = refs;
                return refs;
            }
        }

        private static FieldInfo Field(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, name);

        private sealed class ListRefs
        {
            public FieldInfo ScrollView;
            public FieldInfo CreateRows;
            public FieldInfo MaxRows;
            public FieldInfo ItemsInARow;
            public FieldInfo ListViewExpanded;
            public PropertyInfo Blocked;
        }
    }
}
