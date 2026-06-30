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
    [HarmonyPatch(typeof(PMTabSchedule), "CalculateLoadLimit2ToBeOkayMinFuelCost",
        new[] { typeof(LaunchVehicleType), typeof(double), typeof(double), typeof(int) })]
    public static class Patch_LiftMeOff
    {
        static void Postfix(PMTabSchedule __instance, LaunchVehicleType lvType, double dV1, double dV2, int lvCount, ref double __result)
        {
            try
            {
                if (lvType != null || __result > 0)
                    return;

                var pm = __instance.PlanMissionWindow?.PMMissionParameter;
                if (pm == null) return;

                var sct = pm.SC?.GetTypeSpaceCraft();
                if (sct == null || !sct.LowOrbitContainer)
                    return;

                var selectLv = __instance.PlanMissionWindow?.PMTabSelectLV;
                if (selectLv == null) return;
                var bestLvType = selectLv.LVTypeBest(out int bestLvCount);

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
