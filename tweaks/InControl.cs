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
    [HarmonyPatch(typeof(LEManager), nameof(LEManager.Get))]
    public static class Patch_LEManager_Get_CustomKeys
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static bool Prefix(string key, ref string __result)
        {
            if (key == null || !key.StartsWith("SimpleTweaks.")) return true;
            string locale = MonoBehaviourSingleton<LEManager>.InstanceIsNull
                ? "en-US"
                : MonoBehaviourSingleton<LEManager>.Instance.CurrentLocSet;
            __result = LocalisationData.Get(locale, key);
            return false;
        }
    }
    [HarmonyPatch(typeof(TextIntUpDown), "AddClick")]
    public static class Patch_TextIntUpDown_AddClick
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static bool Prefix(TextIntUpDown __instance)
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return true;
            __instance.INTValue += 100;
            return false;
        }
    }

    [HarmonyPatch(typeof(TextIntUpDown), "DownClick")]
    public static class Patch_TextIntUpDown_DownClick
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static bool Prefix(TextIntUpDown __instance)
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return true;
            __instance.INTValue -= 100;
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In Control: Ctrl+100 in CountToAdd (SC/LV selection +/-).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(CountToAdd), "OnClickAdd")]
    public static class Patch_CountToAdd_OnClickAdd
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "CountToAdd.OnClickAdd");
    }

    [HarmonyPatch(typeof(CountToAdd), "OnClickRemove")]
    public static class Patch_CountToAdd_OnClickRemove
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "CountToAdd.OnClickRemove");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In Control: Ctrl+100 for module-cargo + button (ResourcesList).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResourcesList), "OnClickMultiAdd")]
    public static class Patch_ResourcesList_OnClickMultiAdd
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "ResourcesList.OnClickMultiAdd");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In Control: localised Ctrl+Click hint appended to the existing
    // Shift+Click text already shown by the game.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(CountToAdd), "SetData",
        new Type[] { typeof(int), typeof(int), typeof(bool) })]
    public static class Patch_CountToAdd_SetData
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static void Postfix(CountToAdd __instance)
        {
            SetCtrlHint(__instance.addTooltip2);
            SetCtrlHint(__instance.removeToolTip2);
        }

        private static void SetCtrlHint(ShowToolTip tt)
        {
            if (tt == null) return;
            string baseText = tt.CustomTextFromCode;
            if (string.IsNullOrEmpty(baseText)) return;
            tt.CustomTextFromCodeRefreshText2 = () =>
                baseText + "\n<color=grey>" + LEManager.Get("SimpleTweaks.Tooltip.CtrlHint") + "</color>";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In Control: Ctrl+100 facility builds in ObjectInfoWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ObjectInfoWindow), "FacilityListOnOnClickCreateFacility")]
    public static class Patch_ObjectInfoWindow_FacilityBuildCount
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "ObjectInfoWindow.FacilityBuildCount");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In Control: Ctrl+100 SC/LV builds in SpaceCraftConstructionWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftConstructionWindow), "OnClickAccept")]
    public static class Patch_SpaceCraftConstructionWindow_BuildCount
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "SpaceCraftConstructionWindow.OnClickAccept");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on ACCEPT button in SpaceCraftConstructionWindow.
    // Added dynamically in Awake so the localized text is always current.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftConstructionWindow), "Awake")]
    public static class Patch_SpaceCraftConstructionWindow_Awake
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static void Postfix(SpaceCraftConstructionWindow __instance)
        {
            try
            {
                var btnAccept = __instance.RectTransformBtnAccept?.GetComponent<UnityEngine.UI.Button>();
                if (btnAccept == null) return;

                var tt = btnAccept.gameObject.GetComponent<ShowToolTip>()
                    ?? btnAccept.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.BuildShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SpaceCraftConstructionWindow_Awake: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on ACCEPT button in ChoseFacilityWindow (facilities & modules build).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ChoseFacilityWindow), "Awake")]
    public static class Patch_ChoseFacilityWindow_Awake
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.InControl.Value;

        static void Postfix(ChoseFacilityWindow __instance)
        {
            try
            {
                var btnAccept = __instance.RectTransformBtnAccept?.GetComponent<UnityEngine.UI.Button>();
                if (btnAccept == null) return;

                var tt = btnAccept.gameObject.GetComponent<ShowToolTip>()
                    ?? btnAccept.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.BuildShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ChoseFacilityWindow_Awake: " + ex);
            }
        }
    }
}
