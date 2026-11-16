using System;
using System.Linq;
using Game.ObjectInfoDataScripts;
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

        private static void Postfix(MonoBehaviour __instance, bool __runOriginal, ScrollRect ___scrollView,
            int ___maxRows, int ___itemsInARow, bool ___listViewExpanded)
        {
            if (!__runOriginal || ___scrollView == null || ___maxRows == 0) return;

            switch (__instance)
            {
                case UIFacilityList facilities: UpdateScrollbar(facilities); break;
                case UIResorcesList resources: UpdateScrollbar(resources); break;
                case UIExploredResourcesList deposits: UpdateScrollbar(deposits); break;
                case UIRocketList rockets: UpdateScrollbar(rockets); break;
                case UIMissionsList missions: UpdateScrollbar(missions); break;
            }

            void UpdateScrollbar<T, TData>(UIList<T, TData> list)
                where T : ListElement where TData : ListElementData
            {
                if (list.BlockConformSizeAndScrollbarsToVisibleContent) return;
                ScrollRect scrollView = ___scrollView;
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
                else if (!___listViewExpanded)
                {
                    // Collapsed: the view was just sized to min(maxRows, rows) * rowHeight, so the
                    // content overflows only when there are more visual rows than maxRows.
                    int perRow = ___itemsInARow;
                    if (perRow <= 0) return;

                    int visualRows = Mathf.CeilToInt(list.CreateRows.Count(row => row != null && row.gameObject.activeSelf) / (float)perRow);
                    needed = visualRows > ___maxRows;
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
        }
    }
}
