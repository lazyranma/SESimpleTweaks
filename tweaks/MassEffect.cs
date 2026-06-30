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
    [HarmonyPatch(typeof(Data.ScriptableObject.Terraformation.TerraformationConfig.HabitabilityParametersNew), "UpdateDepositStates")]
    public static class Patch_UpdateDepositStates_MassEffect
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassEffect.Value;

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Step 1 — find ldc.r8 0.9 (persistence factor, unique in this method)
            int idx09 = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R8 &&
                    codes[i].operand is double d &&
                    Math.Abs(d - 0.9) < 0.0001)
                {
                    idx09 = i;
                    break;
                }
            }

            if (idx09 < 0)
            {
                Plugin.Log.LogWarning("[MassEffect] Could not find ldc.r8 0.9 in UpdateDepositStates");
                return codes;
            }

            // Step 2 — find 1st stloc after 0.9 → liquidFraction (num13)
            int num13Idx = -1;
            int stloc1Pos = -1;
            for (int i = idx09 + 1; i < codes.Count; i++)
            {
                if (IsStloc(codes[i]))
                {
                    stloc1Pos = i;
                    num13Idx = GetLocalIndex(codes[i]);
                    break;
                }
            }

            if (num13Idx < 0)
            {
                Plugin.Log.LogWarning("[MassEffect] Could not find stloc num13 (liquidFraction)");
                return codes;
            }

            // Step 3 — find 2nd stloc after 0.9 → solidFraction (num14)
            int num14Idx = -1;
            int stloc2Pos = -1;
            for (int i = stloc1Pos + 1; i < codes.Count; i++)
            {
                if (IsStloc(codes[i]))
                {
                    stloc2Pos = i;
                    num14Idx = GetLocalIndex(codes[i]);
                    break;
                }
            }

            if (num14Idx < 0)
            {
                Plugin.Log.LogWarning("[MassEffect] Could not find stloc num14 (solidFraction)");
                return codes;
            }

            // Step 4 — inject fixup after stloc num14.
            //
            // When num14 (solidFraction) < 0, we shift the deficit from liquid:
            //   num13 += num14   (num13 was over-allocated by |num14|)
            //   num14 = 0
            // When num14 ≥ 0 this is a no-op.
            //
            // IL:
            //   ldloc num14;  ldc.r8 0;  bge.s AFTER;
            //   ldloc num13;  ldloc num14;  add;  stloc num13;
            //   ldc.r8 0;    stloc num14;
            // AFTER:

            var afterLabel = new Label();
            if (stloc2Pos + 1 < codes.Count)
                codes[stloc2Pos + 1].labels.Add(afterLabel);

            var fixup = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc_S, (byte)num14Idx),
                new CodeInstruction(OpCodes.Ldc_R8, 0.0),
                new CodeInstruction(OpCodes.Bge_S, afterLabel),
                new CodeInstruction(OpCodes.Ldloc_S, (byte)num13Idx),
                new CodeInstruction(OpCodes.Ldloc_S, (byte)num14Idx),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Stloc_S, (byte)num13Idx),
                new CodeInstruction(OpCodes.Ldc_R8, 0.0),
                new CodeInstruction(OpCodes.Stloc_S, (byte)num14Idx),
            };

            codes.InsertRange(stloc2Pos + 1, fixup);
            return codes;
        }

        static bool IsStloc(CodeInstruction ci)
        {
            var op = ci.opcode;
            return op == OpCodes.Stloc || op == OpCodes.Stloc_S
                || op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1
                || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3;
        }

        static int GetLocalIndex(CodeInstruction ci)
        {
            if (ci.operand is LocalVariableInfo lvi) return lvi.LocalIndex;
            if (ci.operand is LocalBuilder lb) return lb.LocalIndex;
            if (ci.operand is byte b) return b;
            if (ci.operand is int i) return i;
            if (ci.operand is short s) return s;
            if (ci.operand is sbyte sb) return sb;
            // Stloc_0..3 and Ldloc_0..3 encode the index in the opcode — operand is null
            var op = ci.opcode;
            if (op == OpCodes.Stloc_0 || op == OpCodes.Ldloc_0) return 0;
            if (op == OpCodes.Stloc_1 || op == OpCodes.Ldloc_1) return 1;
            if (op == OpCodes.Stloc_2 || op == OpCodes.Ldloc_2) return 2;
            if (op == OpCodes.Stloc_3 || op == OpCodes.Ldloc_3) return 3;
            return -1;
        }
    }
}
