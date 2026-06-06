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
    /// </summary>
    internal static class FleetScaleTranspiler
    {
        public static int Patch(
            List<CodeInstruction> codes,
            MethodInfo targetMethod,
            CodeInstruction[] countLoaders,
            int skipCount,
            int expectedMin = 1)
        {
            if (targetMethod == null || countLoaders == null)
                return 0;

            int seen = 0;
            int patched = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt
                    && codes[i].operand is MethodInfo m
                    && m == targetMethod)
                {
                    seen++;
                    if (seen <= skipCount)
                        continue;

                    var injections = new List<CodeInstruction>(countLoaders);
                    injections.Add(new CodeInstruction(OpCodes.Conv_R8));
                    injections.Add(new CodeInstruction(OpCodes.Mul));

                    codes.InsertRange(i + 1, injections);
                    patched++;
                    i += injections.Count;
                }
            }

            if (patched < expectedMin)
            {
                Plugin.Log.LogWarning(
                    $"[FleetScales] transpiler: expected >= {expectedMin} patches " +
                    $"for {targetMethod.Name}, got {patched} (seen {seen} total calls)");
            }
            return patched;
        }
    }
}
