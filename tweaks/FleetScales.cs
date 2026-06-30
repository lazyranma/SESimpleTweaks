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
    /// <summary>
    /// Fleet Scales — AddCargoOrbit (drag-and-drop to orbit) uses single-ship
    /// cargo capacity without multiplying by SCCount.
    /// </summary>
    [HarmonyPatch(typeof(PMTabCargo), "AddCargoOrbit", new System.Type[] { typeof(ResourceDefinition) })]
    public static class Patch_FleetScale_AddCargoOrbit
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.FleetScales.Value;

        private static MethodInfo _getCargoCap = AccessTools.Method(
            typeof(SpacecraftType), nameof(SpacecraftType.GetCargoCapacity));
        private static MethodInfo _get_planMissionWindow = AccessTools.PropertyGetter(
            typeof(PMTab), nameof(PMTab.PlanMissionWindow));
        private static MethodInfo _get_PMMParameter = AccessTools.PropertyGetter(
            typeof(PlanMissionWindow), nameof(PlanMissionWindow.PMMissionParameter));
        private static MethodInfo _get_ScCount = AccessTools.PropertyGetter(
            typeof(PMMissionParameter), nameof(PMMissionParameter.SCCount));

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var countLoaders = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Callvirt, _get_planMissionWindow),
                new CodeInstruction(OpCodes.Callvirt, _get_PMMParameter),
                new CodeInstruction(OpCodes.Callvirt, _get_ScCount),
            };
            FleetScaleTranspiler.Patch(codes, _getCargoCap, countLoaders, skipCount: 0, expectedMin: 1);
            return codes;
        }
    }
}
