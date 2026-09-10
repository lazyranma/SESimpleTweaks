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
    [HarmonyPatch(typeof(ResorceRow), nameof(ResorceRow.RefreshAddMulti))]
    public static class Patch_ResorceRow_RefreshAddMulti
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShiftII.Value;

        private static readonly FieldInfo AddMultiField =
            typeof(ResorceRow).GetField("addMulti", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ResorceRow __instance)
        {
            try
            {
                var addMulti = AddMultiField?.GetValue(__instance) as UnityEngine.UI.Button;
                if (addMulti == null || !addMulti.gameObject.activeSelf) return;

                var tt = addMulti.gameObject.GetComponent<ShowToolTip>()
                    ?? addMulti.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => (LEManager.Get("SimpleTweaks.Tooltip.AddModuleShiftCtrl"), null, "");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_RefreshAddMulti: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mass Shift II: Tooltip on the delete (−) button for module cargo rows,
    // and Shift+Click / Ctrl+Click support to remove 10 / 100 rows at once.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResorceRow), "SetData",
        new Type[] { typeof(Cargo), typeof(float), typeof(ResourcesList), typeof(bool) })]
    public static class Patch_ResorceRow_SetData
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShiftII.Value;

        private static readonly FieldInfo ButonDeleteField =
            typeof(ResorceRow).GetField("butonDelete", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ResorceRow __instance)
        {
            try
            {
                if (__instance.CargoResourceTypeType() != EResourceTypeType.modules) return;

                var butonDelete = ButonDeleteField?.GetValue(__instance) as UnityEngine.UI.Button;
                if (butonDelete == null) return;

                var tt = butonDelete.gameObject.GetComponent<ShowToolTip>()
                    ?? butonDelete.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => (LEManager.Get("SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"), null, "");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_SetData: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ResourcesList), nameof(ResourcesList.OnClickMultiRemove),
        new[] { typeof(SpaceModuleDescriptor), typeof(int) })]
    public static class Patch_ResourcesList_OnClickMultiRemove
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShiftII.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper),
                nameof(TranspilerHelper.GetShiftCtrlCount), "ResourcesList.OnClickMultiRemove");
    }
}
