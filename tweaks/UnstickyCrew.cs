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
        [HarmonyPrepare] static bool Prepare() => Plugin.UnstickyCrew.Value;

        private static readonly FieldInfo ButonDeleteFieldB =
            typeof(ResorceRow).GetField("butonDelete",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AddMultiFieldB =
            typeof(ResorceRow).GetField("addMulti",
                BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(ResorceRow __instance)
        {
            if (!__instance.CrewModuleOn) return true; // non-crew rows: run original
            try
            {
                var dd = __instance.ResorceDropDownModuleRectTransform?.GetComponent<DropDownEnum>();
                if (dd?.dropDown != null) dd.dropDown.interactable = false;
                var del = ButonDeleteFieldB?.GetValue(__instance) as UnityEngine.UI.Button;
                if (del != null) del.interactable = false;
                var am = AddMultiFieldB?.GetValue(__instance) as UnityEngine.UI.Button;
                if (am != null) am.gameObject.SetActive(false);
                return false; // skip original (which would also lock the crew slider)
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_BlockDropDown_KeepCrewSlider: " + ex);
                return true; // fallback: run original
            }
        }
    }
}
