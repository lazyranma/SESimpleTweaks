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
    [HarmonyPatch(typeof(PMTabSchedule))]
    public static class Patch_LiftMeOff
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.LiftMeOff.Value;

        static MethodBase TargetMethod()
        {
            // Beta: 4-arg (LaunchVehicleType, double, double, int lvCount)
            var m = AccessTools.Method(typeof(PMTabSchedule), "CalculateLoadLimit2ToBeOkayMinFuelCost",
                new[] { typeof(LaunchVehicleType), typeof(double), typeof(double), typeof(int) });
            if (m != null) return m;

            // Stable: 3-arg (LaunchVehicleType, double, double)
            return AccessTools.Method(typeof(PMTabSchedule), "CalculateLoadLimit2ToBeOkayMinFuelCost",
                new[] { typeof(LaunchVehicleType), typeof(double), typeof(double) });
        }

        static void Postfix(PMTabSchedule __instance, LaunchVehicleType lvType, double dV1, double dV2, int lvCount, ref double __result)
        {
            // On stable (3-arg method), Harmony fills unmatched Postfix params
            // with their default value, so lvCount will be 0.
            try
            {
                if (lvType != null || __result > 0)
                    return;

                var pm = __instance.PlanMissionWindow?.PMMissionParameter;
                if (pm == null) return;

                var sct = pm.SC?.GetTypeSpaceCraft();
                if (sct == null || !sct.LowOrbitContainer)
                    return;

                // LVTypeBest() changed signature in beta (added out int).
                // Use reflection to call the right overload on either version.
                var selectLv = __instance.PlanMissionWindow?.PMTabSelectLV;
                if (selectLv == null) return;
                var lvTypeBestMethod = AccessTools.Method(typeof(PMTabSelectLV), "LVTypeBest", Type.EmptyTypes)
                                    ?? AccessTools.Method(typeof(PMTabSelectLV), "LVTypeBest", new[] { typeof(int).MakeByRefType() });
                if (lvTypeBestMethod == null) return;

                var args = lvTypeBestMethod.GetParameters().Length == 0 ? null : new object[] { 0 };
                var bestLvType = (LaunchVehicleType)lvTypeBestMethod.Invoke(selectLv, args);
                // Extract the out-param count from beta's LVTypeBest(out int).
                int bestLvCount = (args != null) ? (int)args[0] : 1;

                if (bestLvType == null) return;

                double fallback = bestLvType.MaxPayloadOnThisObject(pm.Start, pm.FlyCompany);
                fallback *= bestLvCount;
                if (fallback > 0)
                    __result = fallback;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[LiftMeOff] error: {ex}");
            }
        }
    }
}
