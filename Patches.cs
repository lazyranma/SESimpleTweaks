using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Data;
using Data.ScriptableObject;
using Extensions;
using Game;
using Game.Info;
using Game.ObjectInfoDataScripts;
using Game.ObjectInfoDataScripts.CustomFacilitiesAndModules;
using Game.UI;
using Game.UI.Windows.Elements;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Game.UI.Windows.Elements.PlanMissionElements;
using Game.UI.Windows.Elements.SearchObjectElements;
using Game.UI.Windows.Windows;
using HarmonyLib;
using Language;
using UIPlanMissionElements;
using Manager;
using ScriptableObjectScripts;
using TMPro;
using UnityEngine;

namespace SimpleTweaks
{
    // ─────────────────────────────────────────────────────────────────────────
    // Shared transpiler helper: extends any method that already has
    //   if (Input.GetKey(LeftShift)) { ... += 10; }
    // to also trigger on LeftControl, replacing the literal 10 with
    // a call to `getCountMethod` (which returns 100 if Ctrl, 10 if Shift).
    // ─────────────────────────────────────────────────────────────────────────
    internal static class TranspilerHelper
    {
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

    // ─────────────────────────────────────────────────────────────────────────
    // Localisation: intercept LEManager.Get for SimpleTweaks.* keys.
    // Returns a translation from LocalisationData using the current locale,
    // falling back to en-US when no locale-specific translation is present.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(LEManager), nameof(LEManager.Get))]
    public static class Patch_LEManager_Get_CustomKeys
    {
        static bool Prefix(string key, ref string __result)
        {
            if (key == null || !key.StartsWith("SimpleTweaks.")) return true;
            string locale = MonoBehaviourSingleton<LEManager>.InstanceIsNull
                ? "en-US"
                : MonoBehaviourSingleton<LEManager>.Instance.CurrentLocSet;
            __result = LocalisationData.Get(locale, key);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 1: Show deposit-specific tooltip when hovering a resource icon
    //            in the Object Search list.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ToolTipManager), nameof(ToolTipManager.ShowToolTip))]
    public static class Patch_ToolTipManager_ShowToolTip
    {
        static void Prefix(MonoBehaviourOnDisable _mb, ref string tooltipString)
        {
            try
            {
                if (_mb is LabelLinksHandler llh)
                    TryHandleDepositTooltip(llh, ref tooltipString);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ToolTipManager_ShowToolTip: " + ex);
            }
        }

        private static void TryHandleDepositTooltip(LabelLinksHandler llh, ref string tooltipString)
        {
            TMP_Text label = llh.GetComponent<TMP_Text>();
            if (label == null || label.textInfo == null)
                return;

            SearchRow searchRow = llh.GetComponentInParent<SearchRow>();
            if (searchRow == null || searchRow.ObjectInfo == null)
                return;

            int linkIdx = TMP_TextUtilities.FindIntersectingLink(label, Input.mousePosition, null);
            if (linkIdx < 0 || linkIdx >= label.textInfo.linkCount)
                return;

            string linkID = label.textInfo.linkInfo[linkIdx].GetLinkID();
            string[] parts = linkID.Split(':');
            if (parts.Length != 2 || parts[0] != "ResourceDefinition")
                return;

            string rdID = parts[1];
            ResourceDefinition rd = SerializedMonoBehaviourSingleton<AllScriptableObjectManager>.Instance
                .AllResourceDefinitions.ListNotEmpty
                .FirstOrDefault(d => d.ID == rdID);
            if (rd == null)
                return;

            ObjectInfoData oid = searchRow.ObjectInfo.GetObjectInfoData(
                MonoBehaviourSingleton<GameManager>.Instance.Player);
            if (oid == null)
                return;

            RowExploredResourcesData explored = oid.listExploredResourcesRows
                .Where(r => r.ResourceType == rd && r.ExploredInAnyCapacity)
                .OrderByDescending(r => r.ObservedData?.Value ?? 0.0)
                .FirstOrDefault();
            if (explored == null)
                return;

            tooltipString = BuildDepositTooltip(explored, rd, searchRow.ObjectInfo);
        }

        private static string BuildDepositTooltip(
            RowExploredResourcesData explored,
            ResourceDefinition rd,
            ObjectInfo objectInfo)
        {
            string massFormat = LEManager.Get("UI.MassFormat");

            string depositSize;
            if (explored.Value >= 1.0)
                depositSize = explored.ObservedData.Value.ToPostfixString(massFormat);
            else if (explored.PreliminaryExplored)
                depositSize = explored.ObservedData.Value.ToPostfixString("~" + massFormat);
            else
                depositSize = "?";

            string miningFactor;
            if (explored.Value >= 1.0)
            {
                float mf = explored.ObservedData.MiningFactor ?? 0f;
                string mfStr = mf < 0.01f ? "<0.01" : (mf < 0.1f ? mf.ToString("F2") : mf.ToString("F1"));
                miningFactor = LEManager.Get("UI.MultiplierFormat").MyFormat(mfStr);
            }
            else
            {
                miningFactor = "?";
            }

            int pct = Mathd.FloorToInt(explored.Value * 100.0);
            string stateName = LEManager.Get(
                "UIRowExploredResources.ResourceState." + explored.ObservedData?.ResourceState.ToString());

            string tooltip = rd.TooltipStart
                + LEManager.Get("Tooltip.ObjectInfoWindow.ExploredResourceInfo").MyFormat(
                    rd.TooltipShort, pct, depositSize, miningFactor, stateName);

            if (explored.Value < 1.0)
                tooltip += "\n\n" + LEManager.Get("Tooltip.ObjectInfoWindow.SendAProbeToAnalyze");

            if (objectInfo.ResourceMiningLicenseFeePerT.TryGetValue(rd, out float fee) && fee > 0f)
                tooltip += "\n\n" + LEManager.Get(
                    "Tooltip.ObjectInfoWindow.ExploredResourceInfo.MiningLicenceFee").MyFormat(fee);

            if (explored.Value >= 1.0 && explored.ObservedData?.Balance < 0.0)
            {
                var (units, vTmp) = UIRowExploredResources.FormatDepletionTime(
                    explored.ObservedData.Value / (0.0 - explored.ObservedData.Balance));
                tooltip += "\n\n" + LEManager.Get(
                    "Tooltip.ObjectInfoWindow.ExploredResourceInfo.ExcavationTime")
                    .MyFormat(vTmp.ToString("0.#"), units);
            }

            return tooltip;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 2: Show Atlas / Asteroid-Engine tow requirements in search rows.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SearchRow), "Start")]
    public static class Patch_SearchRow_Start
    {
        private static readonly FieldInfo MoonsField =
            typeof(SearchRow).GetField("moonsTextMeshPro",
                BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(SearchRow __instance)
        {
            try
            {
                ObjectInfo oi = __instance.ObjectInfo;
                if (oi == null)
                    return;
                if (oi.objectTypes != EObjectTypes.Asteroid && oi.objectTypes != EObjectTypes.Comet)
                    return;
                if (!oi.PushableAsteroid2)
                    return;

                TextMeshProUGUI moonsTmp = MoonsField?.GetValue(__instance) as TextMeshProUGUI;
                if (moonsTmp == null)
                    return;

                var allSc = SerializedMonoBehaviourSingleton<AllScriptableObjectManager>.Instance.AllSpacecraftType;
                SpacecraftType atlas  = allSc.GetByID("spacecraft_asteroid_puller");
                SpacecraftType engine = allSc.GetByID("Spacecraft_build_asteroid_engine_facilityModule");
                if (atlas == null || engine == null)
                    return;

                Company player = MonoBehaviourSingleton<GameManager>.Instance.Player;
                int atlasCount  = oi.AsteroidCanBePushHowMuch(player, atlas);
                int engineCount = oi.AsteroidCanBePushHowMuch(player, engine);

                // Create a dedicated TMP element for tow requirements, placed to the LEFT
                // of moonsTextMeshPro as a sibling. moonsTextMeshPro is left completely
                // untouched so the game's original asteroid-type tooltip keeps working.
                var moonsRT = moonsTmp.rectTransform;
                var towGo = new GameObject("TowRequirements");
                towGo.transform.SetParent(moonsTmp.transform.parent, false);
                towGo.transform.SetAsLastSibling(); // render on top of resource icons

                var towTmp = towGo.AddComponent<TextMeshProUGUI>();
                towTmp.font = moonsTmp.font;
                towTmp.fontSharedMaterial = moonsTmp.fontSharedMaterial;
                towTmp.fontSize = moonsTmp.fontSize;
                towTmp.color = moonsTmp.color;
                towTmp.enableWordWrapping = false;
                towTmp.alignment = TextAlignmentOptions.MidlineRight;
                towTmp.text = atlasCount + "A/" + engineCount + "E";

                // Measure actual text width so the hitbox is tight around the text only.
                towTmp.ForceMeshUpdate();
                float towWidth = Mathf.Ceil(towTmp.preferredWidth) + 4f;

                // Right-align tow label immediately left of the type-letter column (moonsRT),
                // with only a small gap. The hitbox is exactly as wide as the text.
                var towRT = towGo.GetComponent<RectTransform>();
                towRT.anchorMin = moonsRT.anchorMin;
                towRT.anchorMax = moonsRT.anchorMax;
                towRT.pivot     = new Vector2(1f, moonsRT.pivot.y);
                towRT.sizeDelta = new Vector2(towWidth, moonsRT.sizeDelta.y);
                // Place tow text centred in the free space inside the OBJECTS column:
                // right edge at the midpoint of moonsRT (halfway between left and right edges).
                float moonsRightEdge = moonsRT.anchoredPosition.x + moonsRT.sizeDelta.x * (1f - moonsRT.pivot.x);
                towRT.anchoredPosition = new Vector2(moonsRightEdge - moonsRT.sizeDelta.x * 0.5f, moonsRT.anchoredPosition.y);

                var oiRef     = oi;
                var atlasRef  = atlas;
                var engineRef = engine;

                var tt = towGo.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 = () =>
                {
                    Company p = MonoBehaviourSingleton<GameManager>.Instance.Player;
                    int a = oiRef.AsteroidCanBePushHowMuch(p, atlasRef);
                    int e = oiRef.AsteroidCanBePushHowMuch(p, engineRef);
                    return LEManager.Get("Tooltip.UIBasicInfoObjectInfoMass")
                        .MyFormat(a, atlasRef.GetText(), e, engineRef.GetText());
                };
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SearchRow_Start: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 3: Ctrl+100 in the cycle-count spinner (TextIntUpDown).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(TextIntUpDown), "AddClick")]
    public static class Patch_TextIntUpDown_AddClick
    {
        static bool Prefix(TextIntUpDown __instance)
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return true;
            __instance.INTValue += 100;
            return false;
        }
    }

    [HarmonyPatch(typeof(TextIntUpDown), "DownClick")]
    public static class Patch_TextIntUpDown_DownClick
    {
        static bool Prefix(TextIntUpDown __instance)
        {
            if (!Input.GetKey(KeyCode.LeftControl)) return true;
            __instance.INTValue -= 100;
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 5 & 6: Ctrl+100 in CountToAdd (SC/LV selection +/-).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(CountToAdd), "OnClickAdd")]
    public static class Patch_CountToAdd_OnClickAdd
    {
        public static int GetAddCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(Patch_CountToAdd_OnClickAdd), nameof(GetAddCount), "CountToAdd.OnClickAdd");
    }

    [HarmonyPatch(typeof(CountToAdd), "OnClickRemove")]
    public static class Patch_CountToAdd_OnClickRemove
    {
        public static int GetAddCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(Patch_CountToAdd_OnClickRemove), nameof(GetAddCount), "CountToAdd.OnClickRemove");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 5b: Ctrl+100 for module-cargo + button (ResourcesList).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResourcesList), "OnClickMultiAdd")]
    public static class Patch_ResourcesList_OnClickMultiAdd
    {
        public static int GetAddCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(Patch_ResourcesList_OnClickMultiAdd), nameof(GetAddCount), "ResourcesList.OnClickMultiAdd");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip hint on CountToAdd +/- buttons: localised Ctrl+Click hint appended
    // to the existing Shift+Click text already shown by the game.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(CountToAdd), "SetData",
        new Type[] { typeof(int), typeof(int), typeof(bool) })]
    public static class Patch_CountToAdd_SetData
    {
        static void Postfix(CountToAdd __instance)
        {
            SetCtrlHint(__instance.addTooltip2);
            SetCtrlHint(__instance.removeToolTip2);
        }

        private static void SetCtrlHint(ShowToolTip tt)
        {
            if (tt == null) return;
            string baseText = tt.CustomTextFromCode;
            if (string.IsNullOrEmpty(baseText)) return;
            tt.CustomTextFromCodeRefreshText2 = () =>
                baseText + "\n<color=grey>" + LEManager.Get("SimpleTweaks.Tooltip.CtrlHint") + "</color>";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 7: Ctrl+100 facility builds in ObjectInfoWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ObjectInfoWindow), "FacilityListOnOnClickCreateFacility")]
    public static class Patch_ObjectInfoWindow_FacilityBuildCount
    {
        public static int GetBuildCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(Patch_ObjectInfoWindow_FacilityBuildCount), nameof(GetBuildCount), "ObjectInfoWindow.FacilityBuildCount");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 8: Ctrl+100 SC/LV builds in SpaceCraftConstructionWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftConstructionWindow), "OnClickAccept")]
    public static class Patch_SpaceCraftConstructionWindow_BuildCount
    {
        public static int GetBuildCount() => Input.GetKey(KeyCode.LeftControl) ? 100 : 10;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(Patch_SpaceCraftConstructionWindow_BuildCount), nameof(GetBuildCount), "SpaceCraftConstructionWindow.OnClickAccept");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on ACCEPT button in SpaceCraftConstructionWindow.
    // Added dynamically in Awake so the localized text is always current.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftConstructionWindow), "Awake")]
    public static class Patch_SpaceCraftConstructionWindow_Awake
    {
        static void Postfix(SpaceCraftConstructionWindow __instance)
        {
            try
            {
                var btnAccept = Traverse.Create(__instance).Field("btnAccept")
                    .GetValue<UnityEngine.UI.Button>();
                if (btnAccept == null) return;

                var tt = btnAccept.gameObject.GetComponent<ShowToolTip>()
                    ?? btnAccept.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.BuildShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SpaceCraftConstructionWindow_Awake: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on ACCEPT button in ChoseFacilityWindow (facilities & modules build).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ChoseFacilityWindow), "Awake")]
    public static class Patch_ChoseFacilityWindow_Awake
    {
        static void Postfix(ChoseFacilityWindow __instance)
        {
            try
            {
                var btnAccept = Traverse.Create(__instance).Field("btnAccept")
                    .GetValue<UnityEngine.UI.Button>();
                if (btnAccept == null) return;

                var tt = btnAccept.gameObject.GetComponent<ShowToolTip>()
                    ?? btnAccept.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.BuildShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ChoseFacilityWindow_Awake: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 9: Shift+Click on "Cancel Building" cancels ALL facilities
    //            under construction on the same body.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(FacilityInfoWindow), "OnActionButtonClick")]
    public static class Patch_FacilityInfoWindow_CancelAllBuildings
    {
        static bool Prefix(FacilityInfoWindow __instance)
        {
            try
            {
                Facility currentFacility = Traverse.Create(__instance)
                    .Field("currentFacility").GetValue<Facility>();

                if (currentFacility == null || currentFacility.BuildProgress >= 1f)
                    return true;
                if (!Input.GetKey(KeyCode.LeftShift))
                    return true;

                var current = SerializedMonoBehaviourSingleton<UIManager>.Instance.Current;
                if (current is PlanMissionWindow pmw && pmw.Open)
                    return true;

                ObjectInfoData oid = currentFacility.ObjectInfoData;
                var toCancel = oid.ListFacility.Where(f => f.BuildProgress < 1f).ToList();
                foreach (Facility f in toCancel)
                    f.CancelBuild();

                Traverse.Create(__instance).Field("currentFacility").SetValue(null);
                SerializedMonoBehaviourSingleton<UIManager>.Instance
                    .Open(EWindowType.ObjectInfo, oid.ObjectInfo);
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
        static void Postfix(FacilityInfoWindow __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var currentFacility = traverse.Field("currentFacility").GetValue<Facility>();
                var actionButton = traverse.Field("actionButton")
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
    // Tooltip on the addMulti (+) button for module cargo rows.
    // RefreshAddMulti() is called after SetData and whenever availability changes.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResorceRow), nameof(ResorceRow.RefreshAddMulti))]
    public static class Patch_ResorceRow_RefreshAddMulti
    {
        private static readonly FieldInfo AddMultiField =
            typeof(ResorceRow).GetField("addMulti", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ResorceRow __instance)
        {
            try
            {
                var addMulti = AddMultiField?.GetValue(__instance) as UnityEngine.UI.Button;
                if (addMulti == null || !addMulti.gameObject.activeSelf) return;

                var tt = addMulti.gameObject.GetComponent<ShowToolTip>()
                    ?? addMulti.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.AddModuleShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_RefreshAddMulti: " + ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tooltip on the delete (−) button for module cargo rows, and
    // Shift+Click / Ctrl+Click support to remove 10 / 100 rows at once.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResorceRow), "SetData",
        new Type[] { typeof(Cargo), typeof(float), typeof(ResourcesList), typeof(bool) })]
    public static class Patch_ResorceRow_SetData
    {
        private static readonly FieldInfo ButonDeleteField =
            typeof(ResorceRow).GetField("butonDelete", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ResorceRow __instance)
        {
            try
            {
                if (__instance.CargoResourceTypeType() != EResourceTypeType.modules) return;

                var butonDelete = ButonDeleteField?.GetValue(__instance) as UnityEngine.UI.Button;
                if (butonDelete == null) return;

                var tt = butonDelete.gameObject.GetComponent<ShowToolTip>()
                    ?? butonDelete.gameObject.AddComponent<ShowToolTip>();
                tt.CustomTextFromCodeRefreshText2 =
                    () => LEManager.Get("SimpleTweaks.Tooltip.RemoveModuleShiftCtrl");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_SetData: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ResorceRow), "OnButtonClickDelete")]
    public static class Patch_ResorceRow_OnButtonClickDelete_Multi
    {
        private static bool _inMultiDelete = false;

        static bool Prefix(ResorceRow __instance)
        {
            if (_inMultiDelete) return true;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl))
                return true;

            try
            {
                if (__instance.CargoResourceTypeType() != EResourceTypeType.modules)
                    return true;
            }
            catch { return true; }

            SpaceModuleDescriptor ft = __instance.FacilityType();
            if (ft == null) return true;

            var rList = Traverse.Create(__instance).Field("resourcesListParent")
                .GetValue<ResourcesList>();
            if (rList == null) return true;

            // count-1 additional deletes (the original delete handles __instance itself)
            int extra = (Input.GetKey(KeyCode.LeftControl) ? 100 : 10) - 1;

            _inMultiDelete = true;
            try
            {
                for (int i = 0; i < extra; i++)
                {
                    // Re-query each iteration: the list shrinks as rows are deleted
                    ResorceRow next = null;
                    foreach (var r in rList.listResorces)
                    {
                        if (r == __instance) continue;
                        SpaceModuleDescriptor rft = null;
                        try { rft = r.FacilityType(); } catch { continue; }
                        if (rft == ft) { next = r; break; }
                    }
                    if (next == null) break;
                    next.OnButtonClickDeletePublic();
                }
            }
            finally
            {
                _inMultiDelete = false;
            }

            return true; // let original handle __instance
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MirrorShadeBugFix: re-allocate mirrors/shades after a save is loaded.
    // Calling AllocateMirrorsAcrossTargets(false) twice is idempotent, so this
    // coexists safely with the standalone MirrorShadeBugFix plugin.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Facility), nameof(Facility.OnAfterLoadSave))]
    public static class Patch_Facility_OnAfterLoadSave
    {
        static void Postfix(Facility __instance)
        {
            SpaceMirrorOrShadeFacility mirror = __instance as SpaceMirrorOrShadeFacility;
            if (mirror == null || mirror.Enabled <= 0) return;
            mirror.AllocateMirrorsAcrossTargets(allocateExcess: false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature 10: Plan Mission — "↑ ORBIT / ↓ SURFACE" quick-destination button.
    //
    // When origin is a surface body (planet, moon, asteroid …) the button reads
    // "↑ ORBIT" and sets the destination to that body's low orbit.
    // When origin is an orbit it reads "↓ SURFACE" and sets the destination to
    // the parent body.  The button is hidden whenever no applicable counterpart
    // exists (e.g. unknown origin or a body with no associated orbit).
    //
    // The button is added as a child of the destination ObjectSearchInputField,
    // anchored to its right edge so it sits just outside the input box.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PMTabDestination), "Awake")]
    public static class Patch_PMTabDestination_DestShortcut
    {
        private static readonly FieldInfo DestInputField =
            typeof(PMTabDestination).GetField("destinationInput",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StartInputField =
            typeof(PMTabDestination).GetField("startInput",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SearchBtnOnInput =
            typeof(ObjectSearchInputField).GetField("searchButton",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // instance → (button, label) – used by the refresh patches below
        internal static readonly Dictionary<PMTabDestination, (UnityEngine.UI.Button btn, TextMeshProUGUI lbl)> Registry =
            new Dictionary<PMTabDestination, (UnityEngine.UI.Button, TextMeshProUGUI)>();

        private const float BtnWidth = 22f;
        private const float BtnInset = 2f;

        static void Postfix(PMTabDestination __instance)
        {
            try
            {
                var destInput  = DestInputField?.GetValue(__instance)  as ObjectSearchInputField;
                var startInput = StartInputField?.GetValue(__instance) as ObjectSearchInputField;
                if (destInput == null || startInput == null) return;
                var searchBtn  = SearchBtnOnInput?.GetValue(destInput) as UnityEngine.UI.Button;

                // ── build button game object ───────────────────────────────
                var btnGo = new GameObject("ST_DestShortcut");
                // Parent to the destination input; the button's rect extends
                // outside the input's bounds to the right (no masking at root).
                btnGo.transform.SetParent(destInput.transform, false);
                btnGo.transform.SetAsLastSibling();

                // Background – copy style from the ⇄ switch button
                var img    = btnGo.AddComponent<UnityEngine.UI.Image>();
                var srcImg = searchBtn?.GetComponent<UnityEngine.UI.Image>();
                if (srcImg != null)
                {
                    img.sprite   = srcImg.sprite;
                    img.color    = srcImg.color;
                    img.type     = srcImg.type;
                    img.material = srcImg.material;
                }

                var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
                if (searchBtn != null) btn.colors = searchBtn.colors;
                btn.targetGraphic = img;

                // ── label ─────────────────────────────────────────────────
                var lblGo = new GameObject("Lbl");
                lblGo.transform.SetParent(btnGo.transform, false);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();

                // Borrow font from the destination input's own TMP text
                var refTmp = destInput.GetComponentInChildren<TextMeshProUGUI>();
                if (refTmp != null)
                {
                    lbl.font               = refTmp.font;
                    lbl.fontSharedMaterial = refTmp.fontSharedMaterial;
                }
                lbl.fontSize         = 14f;
                lbl.alignment        = TextAlignmentOptions.Center;
                lbl.color            = Color.white;
                lbl.enableWordWrapping = false;

                var lblRT = lblGo.GetComponent<RectTransform>();
                lblRT.anchorMin  = Vector2.zero;
                lblRT.anchorMax  = Vector2.one;
                lblRT.offsetMin  = Vector2.zero;
                lblRT.offsetMax  = Vector2.zero;

                // ── position: inside destInput, flush on the right ─────────
                var btnRT = btnGo.GetComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(1f, 0f);
                btnRT.anchorMax = new Vector2(1f, 1f);
                btnRT.pivot     = new Vector2(1f, 0.5f);
                btnRT.offsetMin = new Vector2(-(BtnWidth + BtnInset), BtnInset);
                btnRT.offsetMax = new Vector2(-BtnInset, -BtnInset);

                // ── tooltip ───────────────────────────────────────────────
                var tt = btnGo.AddComponent<ShowToolTip>();
                var startRef = startInput;
                tt.CustomTextFromCodeRefreshText2 = () =>
                {
                    var orig = startRef.ObjectInfo;
                    if (orig == null) return string.Empty;
                    bool fromOrbit = orig.objectTypes == EObjectTypes.Orbit
                                  || orig.objectTypes == EObjectTypes.SolarOrbit;
                    return LEManager.Get(fromOrbit
                        ? "SimpleTweaks.Tooltip.GoToSurface"
                        : "SimpleTweaks.Tooltip.GoToOrbit");
                };

                // ── click ─────────────────────────────────────────────────
                var destRef = destInput;
                btn.onClick.AddListener(() =>
                {
                    try
                    {
                        var orig = startRef.ObjectInfo;
                        if (orig == null) return;
                        var target = GetCounterpart(orig);
                        if (target == null) return;
                        destRef.ObjectInfo = target;
                        destRef.InvokeOnObjectSelect();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("[SimpleTweaks] DestShortcut click: " + ex);
                    }
                });

                Registry[__instance] = (btn, lbl);
                RefreshButton(startInput, btn, lbl);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_PMTabDestination_Awake: " + ex);
            }
        }

        // Returns the orbital counterpart of a body, or null if none exists.
        internal static ObjectInfo GetCounterpart(ObjectInfo origin)
        {
            if (origin == null) return null;

            // Orbit around a planet/moon → parent body
            if (origin.objectTypes == EObjectTypes.Orbit)
                return origin.parentObjectInfo;

            // SolarOrbit (orbit around the Sun) → Sun is not a valid destination
            if (origin.objectTypes == EObjectTypes.SolarOrbit)
                return null;

            // Surface → prefer the low-orbit NBody reference on the body
            if (origin.LowOrbitCustom != null)
            {
                var oi = origin.LowOrbitCustom.GetObjectInfo();
                if (oi != null) return oi;
            }

            // Fallback: first Orbit-type child (not SolarOrbit)
            return origin.listChildren.FirstOrDefault(c =>
                c != null && c.objectTypes == EObjectTypes.Orbit);
        }

        // Show/hide the button and update its label text.
        internal static void RefreshButton(
            ObjectSearchInputField startInput,
            UnityEngine.UI.Button btn,
            TextMeshProUGUI lbl)
        {
            if (btn == null) return;
            var origin      = startInput?.ObjectInfo;
            var counterpart = GetCounterpart(origin);
            btn.gameObject.SetActive(counterpart != null);
            if (counterpart == null) return;

            bool fromOrbit = origin.objectTypes == EObjectTypes.Orbit
                          || origin.objectTypes == EObjectTypes.SolarOrbit;
            lbl.text = fromOrbit ? "\u2193" : "\u2191";
        }
    }

    // Refresh when origin selection changes.
    [HarmonyPatch(typeof(PMTabDestination), "StartInputOnObjectSelect")]
    public static class Patch_PMTabDestination_StartInputOnObjectSelect
    {
        private static readonly FieldInfo StartInputField =
            typeof(PMTabDestination).GetField("startInput",
                BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(PMTabDestination __instance)
        {
            try
            {
                if (!Patch_PMTabDestination_DestShortcut.Registry
                        .TryGetValue(__instance, out var pair)) return;
                var startInput = StartInputField?.GetValue(__instance) as ObjectSearchInputField;
                if (startInput == null) return;
                Patch_PMTabDestination_DestShortcut.RefreshButton(startInput, pair.btn, pair.lbl);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_PMTabDestination_StartInputOnObjectSelect: " + ex);
            }
        }
    }

    // Refresh when the destination tab is activated (origin pre-filled by the game).
    [HarmonyPatch(typeof(PMTabDestination), "ActiveTab")]
    public static class Patch_PMTabDestination_ActiveTab
    {
        private static readonly FieldInfo StartInputField =
            typeof(PMTabDestination).GetField("startInput",
                BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(PMTabDestination __instance)
        {
            try
            {
                if (!Patch_PMTabDestination_DestShortcut.Registry
                        .TryGetValue(__instance, out var pair)) return;
                var startInput = StartInputField?.GetValue(__instance) as ObjectSearchInputField;
                if (startInput == null) return;
                Patch_PMTabDestination_DestShortcut.RefreshButton(startInput, pair.btn, pair.lbl);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_PMTabDestination_ActiveTab: " + ex);
            }
        }
    }
}
