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
    [HarmonyPatch(typeof(FacilityInfoWindow), "OnActionButtonClick")]
    public static class Patch_FacilityInfoWindow_CancelAllBuildings
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static bool Prefix(FacilityInfoWindow __instance)
        {
            try
            {
                Facility currentFacility = __instance.CurrentFacility;

                if (currentFacility == null || currentFacility.BuildProgress >= 1f)
                    return true;

                bool shiftHeld = Input.GetKey(KeyCode.LeftShift);

                var current = SerializedMonoBehaviourSingleton<UIManager>.Instance.Current;
                if (current is PlanMissionWindow pmw && pmw.Open)
                    return true;

                // Close stale second ObjectInfoWindow before navigating back.
                // The vanilla Open() call only manages the primary window, so
                // a second window (e.g. from PlanMission) would remain visible
                // with outdated data. Do this for both normal and Shift-clicks.
                var uiManager = SerializedMonoBehaviourSingleton<UIManager>.Instance;
                if (uiManager.ObjectInfoSecondWindow != null
                    && uiManager.ObjectInfoSecondWindow.Open)
                    uiManager.ObjectInfoSecondWindow.HideNoImmediately();

                if (!shiftHeld)
                    return true; // normal click: let vanilla handle single cancel

                ObjectInfoData oid = currentFacility.ObjectInfoData;
                var descriptor = currentFacility.facilityDescriptor;
                var toCancel = oid.ListFacility
                    .Where(f => f.BuildProgress < 1f && f.facilityDescriptor == descriptor)
                    .ToList();
                foreach (Facility f in toCancel)
                    f.CancelBuild();

                Traverse.Create(__instance).Field("currentFacility").SetValue(null);
                uiManager.Open(EWindowType.ObjectInfo, oid.ObjectInfo);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_FacilityInfoWindow_CancelAllBuildings: " + ex);
                return true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on the cancel-build action button in FacilityInfoWindow.
    // SetupButtons is called each time state changes, so the tooltip is
    // conditionally applied only when the button is in cancel-build mode.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(FacilityInfoWindow), "SetupButtons")]
    public static class Patch_FacilityInfoWindow_SetupButtons
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static void Postfix(FacilityInfoWindow __instance)
        {
            try
            {
                var currentFacility = __instance.CurrentFacility;
                var actionButton = Traverse.Create(__instance).Field("actionButton")
                    .GetValue<UnityEngine.UI.Button>();
                if (actionButton == null) return;

                var tt = actionButton.gameObject.GetComponent<ShowToolTip>()
                    ?? actionButton.gameObject.AddComponent<ShowToolTip>();

                bool inCancelBuildMode =
                    currentFacility != null
                    && currentFacility.BuildProgress < 1f
                    && currentFacility.Company == MonoBehaviourSingleton<GameManager>.Instance.Player;

                tt.CustomTextFromCodeRefreshText2 = inCancelBuildMode
                    ? (Func<string>)(() => LEManager.Get("SimpleTweaks.Tooltip.CancelAllBuildings"))
                    : null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_FacilityInfoWindow_SetupButtons: " + ex);
            }
        }
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Mass Shift: Shift+Click on the X button (upper-right corner of a
    // facility icon in the Object Info list) cancels all facilities of the
    // same type under construction on that body.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(UIFacilityList), "CancelBuilding")]
    public static class Patch_UIFacilityList_CancelAllBuildings
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static bool Prefix(UIFacilityList __instance, UIRowFacility element)
        {
            if (!Input.GetKey(KeyCode.LeftShift)) return true;
            try
            {
                Facility facility = element?.Facility;
                if (facility == null || facility.BuildProgress >= 1f) return true;

                ObjectInfoData oid = facility.ObjectInfoData;
                var descriptor = facility.facilityDescriptor;
                var toCancel = oid.ListFacility
                    .Where(f => f.BuildProgress < 1f && f.facilityDescriptor == descriptor)
                    .ToList();
                foreach (Facility f in toCancel)
                    f.CancelBuild();

                // Refresh all existing ObjectInfoWindows in place (don't reopen)
                var uiManager = SerializedMonoBehaviourSingleton<UIManager>.Instance;
                var window = uiManager.GetWindow<ObjectInfoWindow>();
                if (window != null && window.Open)
                    window.SetData(oid.ObjectInfo);
                var secondWindow = uiManager.GetSecondWindow<ObjectInfoWindow>();
                if (secondWindow != null && secondWindow.Open)
                    secondWindow.SetData(oid.ObjectInfo);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_UIFacilityList_CancelAllBuildings: " + ex);
                return true;
            }
        }
    }
    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on the facility icon X button.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(UIFacilityList), "SetData")]
    public static class Patch_UIFacilityList_SetData_Tooltip
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static void Postfix(UIFacilityList __instance)
        {
            try
            {
                foreach (var row in __instance.CreateRows)
                {
                    var btn = row.ButtonCancel;
                    if (btn == null) continue;
                    var existing = btn.gameObject.GetComponents<ShowToolTip>();
                    foreach (var st in existing)
                        UnityEngine.Object.Destroy(st);
                    var tt = btn.gameObject.AddComponent<ShowToolTip>();
                    tt.CustomTextFromCode = LEManager.Get("SimpleTweaks.Tooltip.CancelAllBuildings");
                    tt.CustomTextFromCodeRefreshText2 = () => LEManager.Get("SimpleTweaks.Tooltip.CancelAllBuildings");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_UIFacilityList_SetData_Tooltip: " + ex);
            }
        }
    }

    // Mass Shift: Shift+Click on the X (cross) button in a Spacecraft or
    // Launch Vehicle construction queue row cancels all items of the same
    // type (e.g. same ship class) in the construction queue.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(UIRowRocket), "OnCancelBuildClick")]
    public static class Patch_UIRowRocket_CancelAllConstruction
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static bool Prefix(UIRowRocket __instance)
        {
            if (!Input.GetKey(KeyCode.LeftShift)) return true;
            try
            {
                var rcd = __instance.CurrentRowRocketData?.rConstruct;
                if (rcd == null || rcd.BuildProgress >= 1f) return true;

                var oid = rcd.ObjectInfoData;
                var window = SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .GetWindow<ObjectInfoWindow>();
                var currentOI = window?.ObjectInfoCurrent;

                var constructions = oid.GetListRocketConstruct()
                    .Where(c => c.BuildProgress < 1f)
                    .Where(c =>
                        (rcd.SpacecraftType != null && c.SpacecraftType == rcd.SpacecraftType) ||
                        (rcd.LaunchVehicleType != null && c.LaunchVehicleType == rcd.LaunchVehicleType))
                    .ToList();
                foreach (var c in constructions)
                    c.CancelBuild();

                if (window != null && window.Open)
                    window.SetData(currentOI ?? oid.ObjectInfo);
                var secondWindow = SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .GetSecondWindow<ObjectInfoWindow>();
                if (secondWindow != null && secondWindow.Open)
                    secondWindow.SetData(currentOI ?? oid.ObjectInfo);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_UIRowRocket_CancelAllConstruction: " + ex);
                return true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on the SC/LC construction queue X button.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(UIRowRocket), "Awake")]
    public static class Patch_UIRowRocket_Awake_Tooltip
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static void Postfix(UIRowRocket __instance)
        {
            try
            {
                var btn = Traverse.Create(__instance)
                    .Field("buttonCancelConstruction")
                    .GetValue<UnityEngine.UI.Button>();
                if (btn == null) return;
                var tt = btn.gameObject.GetComponent<ShowToolTip>()
                    ?? btn.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCode = LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
                tt.CustomTextFromCodeRefreshText2 = () => LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_UIRowRocket_Awake_Tooltip: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mass Shift: Shift+Click on the CANCEL BUILDING button in the Spacecraft
    // / Launch Vehicle info window cancels all constructions of that exact
    // type (e.g. same ship class, not just SC vs LV) on the body.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftInfoWindow), "OnCancelBuildButtonClick")]
    public static class Patch_SpaceCraftInfoWindow_CancelAllConstruction
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static bool Prefix(SpaceCraftInfoWindow __instance)
        {
            if (!Input.GetKey(KeyCode.LeftShift)) return true;
            try
            {
                var rcd = Traverse.Create(__instance).Field("rowRocketData")
                    .GetValue<RowRocketData>()?.rConstruct;
                if (rcd == null || rcd.BuildProgress >= 1f) return true;

                var oid = rcd.ObjectInfoData;
                bool isSC = rcd.SpacecraftType != null;
                var window = SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .GetWindow<ObjectInfoWindow>();
                var currentOI = window?.ObjectInfoCurrent;

                var constructions = oid.GetListRocketConstruct()
                    .Where(c => c.BuildProgress < 1f)
                    .Where(c => isSC
                        ? c.SpacecraftType == rcd.SpacecraftType
                        : c.LaunchVehicleType == rcd.LaunchVehicleType)
                    .ToList();
                foreach (var c in constructions)
                    c.CancelBuild();

                __instance.HideImmediately();
                if (window != null && window.Open)
                    window.SetData(currentOI ?? oid.ObjectInfo);
                var secondWindow = SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .GetSecondWindow<ObjectInfoWindow>();
                if (secondWindow != null && secondWindow.Open)
                    secondWindow.SetData(currentOI ?? oid.ObjectInfo);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SpaceCraftInfoWindow_CancelAllConstruction: " + ex);
                return true;
            }
        }
    }
    // Tooltip on the CANCEL BUILDING button in SpacecraftInfoWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftInfoWindow), "Awake")]
    public static class Patch_SpaceCraftInfoWindow_Awake_Tooltip
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.MassShift.Value;

        static void Postfix(SpaceCraftInfoWindow __instance)
        {
            try
            {
                var btn = Traverse.Create(__instance)
                    .Field("cancelBuildButton")
                    .GetValue<UnityEngine.UI.Button>();
                if (btn == null) return;
                var tt = btn.gameObject.GetComponent<ShowToolTip>()
                    ?? btn.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCode = LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
                tt.CustomTextFromCodeRefreshText2 = () => LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SpaceCraftInfoWindow_Awake_Tooltip: " + ex);
            }
        }
    }
}
