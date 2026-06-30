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
    [HarmonyPatch(typeof(HighlightHoverObject), "ChangeTarget")]
    public static class Patch_HighlightHoverObject_CtrlClickOrbit
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.QuickToOrbitII.Value;

        static bool Prefix(HighlightHoverObject __instance)
        {
            if (!QuickToOrbitIIHelper.IsCtrlPressed())
                return true; // not Ctrl — let the original method handle it

            if (!__instance.enabled)
                return false;

            ObjectInfo bodyInfo = __instance.MyTargetObjectInfo;

            if (bodyInfo == null || !HighlightHoverObject.clickObjectInfo)
                return true;

            ObjectInfo orbitInfo = QuickToOrbitIIHelper.GetOrbitObjectInfo(bodyInfo);
            if (orbitInfo == null)
                return true; // no orbit for this body — fall back to normal

            QuickToOrbitIIHelper.ApplyOrbitToWindow(orbitInfo, Input.GetKey(KeyCode.LeftShift));
            return false; // skip original
        }
    }

    [HarmonyPatch(typeof(HighlightHoverObject), "OnDragAndDrop")]
    public static class Patch_HighlightHoverObject_CtrlDropOrbit
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.QuickToOrbitII.Value;

        static bool Prefix(HighlightHoverObject __instance, DragAndDropTransactItem item, ref bool __result)
        {
            if (!QuickToOrbitIIHelper.IsCtrlPressed())
                return true; // not Ctrl — let the original method handle it

            ObjectInfo bodyInfo = __instance.MyTargetObjectInfo;
            if (bodyInfo == null)
                return true;

            ObjectInfo orbitInfo = QuickToOrbitIIHelper.GetOrbitObjectInfo(bodyInfo);
            if (orbitInfo == null)
                return true; // no orbit for this body — fall back to normal

            __result = orbitInfo.OnDragAndDrop(item);
            return false; // skip original
        }
    }
}
