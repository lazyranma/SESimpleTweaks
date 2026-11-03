using System;
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
    /// Don't Tip Out - keeps a tooltip open while the Object Info window refreshes itself in the
    /// background.
    /// <para>
    /// While the game runs, the window periodically refreshes itself (habitability/facility
    /// summaries are recalculated, deposits are mined), and every refresh rebuilds its lists.
    /// UIList.SetData despawns and respawns all rows through LeanPool, so each recycled row
    /// fires MonoBehaviourOnDisable.OnDisable, which calls ToolTipManager.HideToolTip. The stable
    /// branch hides the tooltip no matter which element asks, so a row of *any* rebuilt list
    /// closes the tooltip even though the cursor never moved and the tooltip belongs to another
    /// row. That is why the deposit tooltip disappears a moment after it appears.
    /// </para>
    /// <para>
    /// Hide requests issued by an element that is being deactivated during a window refresh are
    /// therefore ignored: those are recycled rows, not the cursor leaving. A tooltip whose owner
    /// really is gone after the refresh (the row was dropped instead of recycled) is closed
    /// normally by the postfix. The rows that show a snapshot tooltip (ListElement.UpdateText is
    /// false for deposits and stored resources) are refreshed from the row's current data, so the
    /// open tooltip keeps showing live numbers.
    /// </para>
    /// <para>
    /// The open beta rebuilds lists without the "reverse before despawn" step the stable branch
    /// uses, so LeanPool hands the instances back mirrored: after each rebuild the row that owns
    /// the tooltip sits at a different list index and carries another entry's data. Its text is
    /// therefore not refreshed - the game's own pointer re-entry handles the row that ends up
    /// under the cursor, which is also what keeps the numbers live there.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ObjectInfoWindow), "RefreshUI")]
    public static class Patch_ObjectInfoWindow_RefreshUI_DontTipOut
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.DontTipOut.Value;

        private static readonly FieldInfo TooltipOwners =
            typeof(ToolTipManager).GetField("mbList", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ToolTipManager).FullName, "mbList");

        private static readonly MethodInfo GetTooltipMethod =
            typeof(ListElement).GetMethod("GetTooltip", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ListElement).FullName, "GetTooltip");

        private struct RefreshState
        {
            internal int OwnerIndex;
            internal int RowCount;
        }

        private static int refreshDepth;
        private static ListElement lastTooltipRow;
        private static string lastTooltipText;
        private static string lastTooltipFooter;

        internal static bool IsRefreshing => refreshDepth > 0;

        static void Prefix(out RefreshState __state)
        {
            refreshDepth++;
            __state = CaptureTooltipOwner();
        }

        static void Postfix(RefreshState __state)
        {
            refreshDepth--;
            if (refreshDepth < 0) refreshDepth = 0;

            if (refreshDepth != 0) return;
            if (CloseTooltipIfOwnerInactive()) return;
            RefreshTooltipContents(__state);
        }

        /// <summary>Remembers which list entry the open tooltip belongs to before it is rebuilt.</summary>
        private static RefreshState CaptureTooltipOwner()
        {
            RefreshState state = new RefreshState { OwnerIndex = -1, RowCount = -1 };
            try
            {
                ToolTipManager manager = MonoBehaviourSingleton<ToolTipManager>.Instance;
                if (manager == null || !manager.TooltipShow()) return state;
                if (!(TooltipOwners.GetValue(manager) is List<MonoBehaviourOnDisable> owners) || owners.Count == 0) return state;
                if (!(owners[owners.Count - 1] is ListElement row) || row == null) return state;

                Transform parent = row.transform.parent;
                state.OwnerIndex = row.Index;
                state.RowCount = parent != null ? parent.childCount : -1;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ObjectInfoWindow_RefreshUI_DontTipOut: " + ex);
                state.OwnerIndex = -1;
            }

            return state;
        }

        /// <summary>
        /// The rebuild suppresses the row deactivation hides; if the tooltip's own element did not
        /// come back, close the tooltip as usual.
        /// </summary>
        private static bool CloseTooltipIfOwnerInactive()
        {
            try
            {
                ToolTipManager manager = MonoBehaviourSingleton<ToolTipManager>.Instance;
                if (manager == null || !manager.TooltipShow()) return false;
                if (!(TooltipOwners.GetValue(manager) is List<MonoBehaviourOnDisable> owners) || owners.Count == 0) return false;

                MonoBehaviourOnDisable owner = owners[owners.Count - 1];
                if (owner == null || owner.gameObject.activeInHierarchy) return false;

                manager.HideToolTip(owner);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ObjectInfoWindow_RefreshUI_DontTipOut: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Deposit and stored-resource rows show a snapshot tooltip (they leave
        /// ListElement.UpdateText at its default false), so a refresh would leave stale numbers on
        /// screen. Rebuild the text from the row's current data and push it into the open tooltip
        /// window. Facility, spacecraft and mission rows already refresh themselves and are left
        /// alone.
        /// </summary>
        private static void RefreshTooltipContents(RefreshState state)
        {
            try
            {
                if (state.OwnerIndex < 0) return;

                ToolTipManager manager = MonoBehaviourSingleton<ToolTipManager>.Instance;
                if (manager == null || !manager.TooltipShow()) return;
                if (!(TooltipOwners.GetValue(manager) is List<MonoBehaviourOnDisable> owners) || owners.Count == 0) return;

                MonoBehaviourOnDisable owner = owners[owners.Count - 1];
                if (owner == null) return;
                if (!(owner is UIRowExploredResources) && !(owner is UIRowResources)) return;

                ListElement row = (ListElement)owner;

                // Bail out when the rebuild moved the owner to another list entry (beta pooling
                // order) or changed the number of rows: its data no longer describes the row
                // under the cursor, so leave the tooltip as it is until the game re-enters the
                // hovered row.
                Transform parent = row.transform.parent;
                if (parent == null || parent.childCount != state.RowCount) return;
                if (row.Index != state.OwnerIndex) return;

                var tooltip = ((string, List<(string, string)>, string))GetTooltipMethod.Invoke(row, null);
                if (string.IsNullOrEmpty(tooltip.Item1)) return;
                if (row == lastTooltipRow && tooltip.Item1 == lastTooltipText && tooltip.Item3 == lastTooltipFooter) return;

                ToolTipWindow window = manager.ToolTipWindow;
                window.SetText(tooltip.Item1);
                window.SetFooter(tooltip.Item3);
                window.SetStats(tooltip.Item2);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)window.transform);

                lastTooltipRow = row;
                lastTooltipText = tooltip.Item1;
                lastTooltipFooter = tooltip.Item3;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ObjectInfoWindow_RefreshUI_DontTipOut: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ToolTipManager), nameof(ToolTipManager.HideToolTip))]
    public static class Patch_ToolTipManager_HideToolTip_DontTipOut
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.DontTipOut.Value;

        static bool Prefix(MonoBehaviourOnDisable _mb)
        {
            if (!Patch_ObjectInfoWindow_RefreshUI_DontTipOut.IsRefreshing) return true;
            if (_mb == null || _mb.gameObject.activeInHierarchy) return true;

            return false;
        }
    }
}
