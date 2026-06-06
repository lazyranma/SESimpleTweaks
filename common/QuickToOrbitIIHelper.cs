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
    internal static class QuickToOrbitIIHelper
    {
        public static bool IsCtrlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        public static ObjectInfo GetOrbitObjectInfo(ObjectInfo bodyInfo)
        {
            return bodyInfo?.LowOrbitCustom?.GetObjectInfo();
        }

        public static void ApplyOrbitToWindow(ObjectInfo orbitInfo, bool secondary)
        {
            if (secondary)
            {
                var w = SerializedMonoBehaviourSingleton<UIManager>.Instance.GetSecondWindow<ObjectInfoWindow>();
                if (w.Open && w.ObjectInfoCurrent != orbitInfo)
                    w.SetData(orbitInfo);
                else if (w.Open && w.ObjectInfoCurrent == orbitInfo)
                    MonoBehaviourSingleton<MyCameraController>.Instance.ChangeTarget(orbitInfo.gameObject.transform);
                else if (!w.Open)
                    SerializedMonoBehaviourSingleton<UIManager>.Instance.OpenSecondWindow(EWindowType.ObjectInfo, orbitInfo);
            }
            else
            {
                var w = SerializedMonoBehaviourSingleton<UIManager>.Instance.GetWindow<ObjectInfoWindow>();
                if (w.Open && w.ObjectInfoCurrent != orbitInfo)
                    w.SetData(orbitInfo);
                else if (w.Open && w.ObjectInfoCurrent == orbitInfo)
                    MonoBehaviourSingleton<MyCameraController>.Instance.ChangeTarget(orbitInfo.gameObject.transform);
                else if (!w.Open)
                    SerializedMonoBehaviourSingleton<UIManager>.Instance.Open(EWindowType.ObjectInfo, orbitInfo);
            }
        }
    }
}
