using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using CameraControl;
using Data;
using Data.ScriptableObject;
using Extensions;
using Game;
using Game.Info;
using Game.ObjectInfoDataScripts;
using Game.ObjectInfoDataScripts.CustomFacilitiesAndModules;
using Game.UI;
using Game.UI.DragAndDropSystem;
using Game.UI.Windows.Elements;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Game.UI.Windows.Elements.MissionsElements;
using Game.UI.Windows.Elements.PlanMissionElements;
using Game.UI.Windows.Elements.SearchObjectElements;
using Game.UI.Windows.Elements.SpaceCraftConstructElements;
using Game.UI.Windows.Windows;
using HarmonyLib;
using Language;
using UIPlanMissionElements;
using Manager;
using ScriptableObjectScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#pragma warning disable IDE0051

namespace SimpleTweaks
{
    [HarmonyPatch(typeof(ResorceRow), "BlockDropDown")]
    public static class Patch_ResorceRow_BlockDropDown_KeepCrewSlider
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            if (!_teardownRegistered)
            {
                _teardownRegistered = true;
                Teardown.TrackRestore(RestoreAll);
            }
            return Plugin.UnstickyCrew.Value;
        }

        private static readonly FieldInfo ButonDeleteFieldB =
            typeof(ResorceRow).GetField("butonDelete",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AddMultiFieldB =
            typeof(ResorceRow).GetField("addMulti",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ButonDeleteToolTipField =
            typeof(ResorceRow).GetField("butonDeleteToolTip",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(ResorceRow).FullName, "butonDeleteToolTip");

        private sealed class OriginalState
        {
            internal bool Listed;
            internal bool DropDownInteractable;
            internal bool DeleteInteractable;
            internal bool TooltipEnabled;
            internal bool AddMultiActive;
        }

        // Kept per row so a ScriptEngine reload can put the controls back the way they were.
        // The list holds rows strongly while they are alive; destroyed rows are pruned.
        private static readonly ConditionalWeakTable<ResorceRow, OriginalState> OriginalStates =
            new ConditionalWeakTable<ResorceRow, OriginalState>();
        private static readonly LiveObjectList<ResorceRow> TrackedRows = new LiveObjectList<ResorceRow>();
        private static bool _teardownRegistered;

        static bool Prefix(ResorceRow __instance)
        {
            if (!__instance.CrewModuleOn) return true; // non-crew rows: run original
            try
            {
                var dd = __instance.ResorceDropDownModuleRectTransform?.GetComponent<DropDownEnum>();
                var del = ButonDeleteFieldB?.GetValue(__instance) as UnityEngine.UI.Button;
                var deleteToolTip = (ShowToolTip)ButonDeleteToolTipField.GetValue(__instance);
                var am = AddMultiFieldB?.GetValue(__instance) as UnityEngine.UI.Button;

                OriginalState original = OriginalStates.GetValue(__instance, _ => new OriginalState());
                if (!original.Listed)
                {
                    original.Listed = true;
                    original.DropDownInteractable = dd == null || dd.dropDown == null || dd.dropDown.interactable;
                    original.DeleteInteractable = del == null || del.interactable;
                    original.TooltipEnabled = deleteToolTip == null || deleteToolTip.enabled;
                    original.AddMultiActive = am == null || am.gameObject.activeSelf;
                    TrackedRows.Add(__instance);
                }

                if (dd?.dropDown != null) dd.dropDown.interactable = false;
                if (del != null) del.interactable = false;
                if (deleteToolTip != null) deleteToolTip.enabled = false;
                if (am != null) am.gameObject.SetActive(false);
                return false; // skip original (which would also lock the crew slider)
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_BlockDropDown_KeepCrewSlider: " + ex);
                return true; // fallback: run original
            }
        }

        private static void RestoreAll()
        {
            TrackedRows.Visit(row =>
            {
                if (!OriginalStates.TryGetValue(row, out OriginalState original)) return;
                try
                {
                    Restore(row, original);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[SimpleTweaks] UnstickyCrew teardown: " + ex);
                }
            });
        }

        private static void Restore(ResorceRow row, OriginalState original)
        {
            if (row == null) return;

            var dd = row.ResorceDropDownModuleRectTransform?.GetComponent<DropDownEnum>();
            if (dd?.dropDown != null) dd.dropDown.interactable = original.DropDownInteractable;
            var del = ButonDeleteFieldB?.GetValue(row) as UnityEngine.UI.Button;
            if (del != null) del.interactable = original.DeleteInteractable;
            var deleteToolTip = (ShowToolTip)ButonDeleteToolTipField.GetValue(row);
            if (deleteToolTip != null) deleteToolTip.enabled = original.TooltipEnabled;
            var am = AddMultiFieldB?.GetValue(row) as UnityEngine.UI.Button;
            if (am != null) am.gameObject.SetActive(original.AddMultiActive);
        }
    }
}
