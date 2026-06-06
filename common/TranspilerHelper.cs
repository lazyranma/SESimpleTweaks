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
    // ─────────────────────────────────────────────────────────────────────────
    internal static class TranspilerHelper
    {
        public static int GetShiftCtrlCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        public static IEnumerable<CodeInstruction> PatchShiftPlusTen(
            IEnumerable<CodeInstruction> instructions,
            Type helperClass,
            string getCountMethodName,
            string debugLabel)
        {
            var getKeyMethod = AccessTools.Method(
                typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) });
            var getCountMethod = AccessTools.Method(helperClass, getCountMethodName);

            var codes = new List<CodeInstruction>(instructions);
            bool foundCondition = false;
            bool foundBound = false;

            for (int i = 0; i < codes.Count; i++)
            {
                // Extend: Input.GetKey(LeftShift) → … | Input.GetKey(LeftControl)
                if (!foundCondition
                    && codes[i].opcode == OpCodes.Call
                    && codes[i].operand is MethodInfo mi && mi == getKeyMethod
                    && i > 0
                    && codes[i - 1].opcode == OpCodes.Ldc_I4
                    && (int)codes[i - 1].operand == (int)KeyCode.LeftShift)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4, (int)KeyCode.LeftControl),
                        new CodeInstruction(OpCodes.Call, getKeyMethod),
                        new CodeInstruction(OpCodes.Or),
                    });
                    foundCondition = true;
                    i += 3;
                }

                // Replace literal 10 with GetAddCount()
                if (!foundBound
                    && codes[i].opcode == OpCodes.Ldc_I4_S
                    && (sbyte)codes[i].operand == (sbyte)10)
                {
                    codes[i] = new CodeInstruction(OpCodes.Call, getCountMethod);
                    foundBound = true;
                }

                if (foundCondition && foundBound) break;
            }

            if (!foundCondition)
                Plugin.Log.LogWarning($"[SimpleTweaks] {debugLabel}: LeftShift check not found");
            if (!foundBound)
                Plugin.Log.LogWarning($"[SimpleTweaks] {debugLabel}: loop bound 10 not found");

            return codes;
        }
    }
}
