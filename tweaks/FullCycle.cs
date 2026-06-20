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
    [HarmonyPatch(typeof(UIRowMission), "SetDataRowMissionDataCycleMissionsInfo")]
    public static class Patch_UIRowMission_CyclicRichDisplay
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.FullCycle.Value;

        private static MissionRowCyclicalNew _cachedPrefab;
        private static bool _prefabLookedUp;

        private static readonly FieldInfo IconField =
            typeof(UIRowMission).GetField("icon",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TitleField =
            typeof(UIRowMission).GetField("titleTextMeshPro",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DescField =
            typeof(UIRowMission).GetField("descriptionTextMeshPro",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DateField =
            typeof(UIRowMission).GetField("dateTextMeshPro",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CountField =
            typeof(UIRowMission).GetField("countOnIcon",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ButtonField =
            typeof(UIRowMission).GetField("buttonShowInfoMission",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CycleDataField =
            typeof(UIRowMission).GetField("cycleMissionsData",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool _maxRowsFixed;
        private static readonly FieldInfo MaxRowsField =
            typeof(UIList<UIRowMission, RowMissionData>).GetField("maxRows",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static MissionRowCyclicalNew GetPrefab()
        {
            if (_prefabLookedUp) return _cachedPrefab;

            try
            {
                var mw = SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .GetWindow<MissionsWindow>();
                if (mw == null)
                {
                    Plugin.Log.LogWarning("[SimpleTweaks] GetPrefab: MissionsWindow not found");
                    return null;
                }

                var cal = Traverse.Create(mw).Field("cycleMissionAllList")
                    .GetValue<CycleMissionAllList>();
                if (cal == null)
                {
                    Plugin.Log.LogWarning("[SimpleTweaks] GetPrefab: CycleMissionAllList not found");
                    return null;
                }

                _cachedPrefab = Traverse.Create(cal)
                    .Field("missionRowCyclicalPrefabNew")
                    .GetValue<MissionRowCyclicalNew>();
                if (_cachedPrefab == null)
                    Plugin.Log.LogWarning("[SimpleTweaks] GetPrefab: missionRowCyclicalPrefabNew not found");
                if (_cachedPrefab != null) _prefabLookedUp = true;
                return _cachedPrefab;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    "[SimpleTweaks] Failed to get MissionRowCyclicalNew prefab: " + ex.Message);
                return null;
            }
        }

        static bool Prefix(UIRowMission __instance)
        {
            try
            {
                var cmd = CycleDataField?.GetValue(__instance) as CycleMissionsData;
                if (cmd == null)
                    return true;

                // Ensure the parent UIMissionsList allows variable-height rows
                if (!_maxRowsFixed)
                {
                    _maxRowsFixed = true;
                    var ml = __instance.GetComponentInParent<UIMissionsList>();
                    if (ml != null && MaxRowsField != null)
                    {
                        MaxRowsField.SetValue(ml, 0);
                    }
                }

                var prefab = GetPrefab();
                if (prefab == null)
                {
                    Plugin.Log.LogWarning("[SimpleTweaks] Prefix: prefab is null, falling back to original");
                    return true; // fallback to original
                }

                // Hide the vanilla simple UI
                (IconField?.GetValue(__instance) as UnityEngine.UI.Image)?.gameObject.SetActive(false);
                (TitleField?.GetValue(__instance) as TextMeshProUGUI)?.gameObject.SetActive(false);
                (DescField?.GetValue(__instance) as TextMeshProUGUI)?.gameObject.SetActive(false);
                (DateField?.GetValue(__instance) as TextMeshProUGUI)?.gameObject.SetActive(false);
                (CountField?.GetValue(__instance) as TextMeshProUGUI)?.gameObject.SetActive(false);
                (ButtonField?.GetValue(__instance) as UnityEngine.UI.Button)?.gameObject.SetActive(false);

                // Clean up any previously created rich row
                var t = __instance.transform;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    if (t.GetChild(i).name.StartsWith("ST_CyclicRich"))
                        UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
                }

                // Create the rich display
                var rich = UnityEngine.Object.Instantiate(prefab, t);
                rich.name = "ST_CyclicRich";
                rich.SetData(cmd);

                // Recursively fix ALL LayoutElement minWidths and all
                // HorizontalLayoutGroup alignments in the rich row hierarchy.
                // The prefab has minW=550 on two levels AND center-aligned
                // content that creates ~115px of dead space on the left.
                FixLayoutRecursive(rich.transform);

                // Stretch the rich row to fill parent width
                var richRT = rich.GetComponent<RectTransform>();
                if (richRT != null)
                {
                    richRT.anchorMin = Vector2.zero;
                    richRT.anchorMax = Vector2.one;
                    richRT.offsetMin = Vector2.zero;
                    richRT.offsetMax = Vector2.zero;
                }

                // Let the parent row grow taller
                var le = __instance.GetComponent<UnityEngine.UI.LayoutElement>();
                if (le == null)
                    le = __instance.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredHeight = -1f;
                le.flexibleHeight = 1f;
                le.minWidth = -1f;

                return false; // skip original
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    "[SimpleTweaks] Patch_UIRowMission_CyclicRichDisplay: " + ex);
                return true; // fallback to original
            }
        }

        private static void FixLayoutRecursive(Transform t)
        {
            // Clear forced minimum widths on all LayoutElements
            var le = t.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null)
            {
                le.minWidth = 0;
                le.preferredWidth = -1;
            }

            // Force HorizontalLayoutGroup to left-align content
            var hlg = t.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childAlignment = TextAnchor.UpperLeft;
            }

            // Recurse
            for (int i = 0; i < t.childCount; i++)
                FixLayoutRecursive(t.GetChild(i));
        }
    }

    // Refresh the planet view's mission list after deleting a cyclical mission
    // from the rich row (the game only refreshes the MissionsWindow, not OIW).
    [HarmonyPatch(typeof(MissionRowCyclicalNew), "OnButtonDelete")]
    public static class Patch_MissionRowCyclicalNew_OnButtonDelete_RefreshOIW
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.FullCycle.Value;

        static void Postfix(MissionRowCyclicalNew __instance)
        {
            try
            {
                var oiw = __instance.GetComponentInParent<ObjectInfoWindow>();
                if (oiw != null && oiw.Open)
                {
                    // RefreshUI() is protected — call via reflection
                    var refreshMethod = typeof(ObjectInfoWindow).GetMethod("RefreshUI",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (refreshMethod != null)
                        refreshMethod.Invoke(oiw, null);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_OnButtonDelete: " + ex);
            }
        }
    }
}
