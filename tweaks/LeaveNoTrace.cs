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
    [HarmonyPatch(typeof(ResourcePrice), "op_Multiply", new Type[] { typeof(double), typeof(ResourcePrice) })]
    public static class Patch_ResourcePrice_RoundMultiplier
    {
        static void Prefix(ref double a)
        {
            a = Math.Round(a, 6);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Patch 2: Round stockpile after every subtraction.
    //   Cleans up any floating-point residue immediately.
    //   4.500000000000001 → 4.5.  1.2e-15 → 0.0.
    // ─────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(RowResourcesData), nameof(RowResourcesData.Remove))]
    public static class Patch_RowResourcesData_RoundAfterRemove
    {
        static void Postfix(RowResourcesData __instance)
        {
            __instance.Value = Math.Round(__instance.Value, 6);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Patch 3: Raise the deposit cleanup threshold from 1e-7 to 1e-6.
    //   Combined with Patch 2, a fully-depleted deposit becomes 0.0 and
    //   is removed at the next monthly tick (or immediately if
    //   UpdateDepositStates fires).
    // ─────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(MyExtensions), nameof(MyExtensions.IsNearZero), new Type[] { typeof(double) })]
    public static class Patch_MyExtensions_IsNearZeroThreshold
    {
        static bool Prefix(double value, ref bool __result)
        {
            __result = Math.Abs(value) < 1E-06;
            return false; // skip original
        }
    }
}
