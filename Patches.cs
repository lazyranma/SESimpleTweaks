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
using Game.UI.Windows.Elements.MissionsElements;
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
using UnityEngine.UI;

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
    [HarmonyPatch(typeof(SearchRow), "Start")]
    public static class Patch_SearchRow_Start
    {
        private static readonly FieldInfo MoonsField =
            typeof(SearchRow).GetField("moonsTextMeshPro",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private const float DeleteBtnWidth = 20f;
        private const float DeleteBtnGap = 4f;
        private const float TotalDeleteColumnWidth = DeleteBtnWidth + DeleteBtnGap;
        private static Sprite _trashSprite;
        private static bool _trashSpriteLookedUp;

        private static Sprite GetTrashSprite()
        {
            if (_trashSpriteLookedUp) return _trashSprite;
            _trashSpriteLookedUp = true;
            var oiw = SerializedMonoBehaviourSingleton<UIManager>.Instance
                .GetWindow<ObjectInfoWindow>();
            var uiObjName = oiw?.GetComponentInChildren<UIObjectName>(true);
            if (uiObjName != null)
            {
                var btn = Traverse.Create(uiObjName).Field("trashObjectInfo")
                    .GetValue<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    foreach (var img in btn.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        if (img.sprite != null)
                        {
                            var n = img.sprite.name.ToLower();
                            if (img.sprite.name == "trash_delete_deconstruction")
                            {
                                _trashSprite = img.sprite;
                                break;
                            }
                        }
                    }
                }
            }
            return _trashSprite;
        }

        static void Postfix(SearchRow __instance)
        {
            try
            {
                ObjectInfo oi = __instance.ObjectInfo;
                if (oi == null) return;
                if (oi.objectTypes != EObjectTypes.Asteroid && oi.objectTypes != EObjectTypes.Comet) return;

                TextMeshProUGUI moonsTmp = MoonsField?.GetValue(__instance) as TextMeshProUGUI;
                if (moonsTmp == null) return;

                Company player = MonoBehaviourSingleton<GameManager>.Instance.Player;
                var moonsRT = moonsTmp.rectTransform;
                bool canDelete = oi.CanPlayerForgotObject();

                // ── Create delete button / spacer ──
                var delGo = new GameObject(canDelete ? "AsteroidTrashBtn" : "AsteroidTrashSpacer");
                delGo.transform.SetParent(moonsTmp.transform.parent, false);
                delGo.transform.SetAsLastSibling();

                var delRT = delGo.AddComponent<RectTransform>();
                delRT.anchorMin = new Vector2(1f, 0.5f);
                delRT.anchorMax = new Vector2(1f, 0.5f);
                delRT.pivot = new Vector2(1f, 0.5f);
                delRT.sizeDelta = new Vector2(DeleteBtnWidth, DeleteBtnWidth);
                delRT.anchoredPosition = new Vector2(-2f, 0f);

                if (canDelete)
                {
                    var img = delGo.AddComponent<UnityEngine.UI.Image>();
                    var trashSprite = GetTrashSprite();
                    if (trashSprite != null) { img.sprite = trashSprite; img.type = UnityEngine.UI.Image.Type.Simple; img.color = Color.white; img.preserveAspect = true; }
                    else { img.color = new Color(0.15f, 0.15f, 0.15f, 0.7f); }

                    var btn = delGo.AddComponent<UnityEngine.UI.Button>();
                    btn.targetGraphic = img;
                    var oiDel = oi;
                    btn.onClick.AddListener(() =>
                    {
                        try
                        {
                            if (!oiDel.CanPlayerForgotObject()) return;
                            SerializedMonoBehaviourSingleton<UIManager>.Instance.ShowPopUP(
                                LEManager.Get("PopUPOnClickTrashObjectInfo"),
                                delegate
                                {
                                    ObjectInfo parentOI = oiDel.parentObjectInfo;
                                    ObjectInfoGroups parentGroup = oiDel.parentObjectInfoGropup;
                                    oiDel.VirtualDestroy();
                                    var w = SerializedMonoBehaviourSingleton<UIManager>.Instance
                                        .GetWindow<ObjectInfoWindow>();
                                    if (w.Open && w.ObjectInfoCurrent == oiDel)
                                        w.HideImmediately();
                                    w = SerializedMonoBehaviourSingleton<UIManager>.Instance
                                        .GetSecondWindow<ObjectInfoWindow>();
                                    if (w.Open && w.ObjectInfoCurrent == oiDel)
                                        w.HideImmediately();
                                    var sow = SerializedMonoBehaviourSingleton<UIManager>.Instance.GetWindow<SearchObjectWindow>();
                                    if (sow != null) sow.StartCoroutine(ReExpandParent(parentOI, parentGroup));
                                },
                                delegate { },
                                btnOkEnable: true, btnNoEnable: true,
                                blockerOn: true, yesNoMenu: true);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.LogError("[SimpleTweaks] AsteroidTrashBtn: " + ex);
                        }
                    });

                    // Fallback trash icon (only if no sprite found)
                    if (trashSprite == null)
                    {
                        var lblGo = new GameObject("Lbl");
                        lblGo.transform.SetParent(delGo.transform, false);
                        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                        lbl.font = moonsTmp.font;
                        lbl.fontSharedMaterial = moonsTmp.fontSharedMaterial;
                        lbl.fontSize = 12f;
                        lbl.color = new Color(0.9f, 0.3f, 0.3f, 1f);
                        lbl.alignment = TextAlignmentOptions.Center;
                        lbl.enableWordWrapping = false;
                        lbl.text = "\u2716";
                        var lblRT = lblGo.GetComponent<RectTransform>();
                        lblRT.anchorMin = Vector2.zero;
                        lblRT.anchorMax = Vector2.one;
                        lblRT.offsetMin = Vector2.zero;
                        lblRT.offsetMax = Vector2.zero;

                    }
                    // Tooltip
                    var tt = delGo.AddComponent<ShowToolTip>();
                    tt.CustomTextFromCode = LEManager.Get("SimpleTweaks.Tooltip.DeleteAsteroid");
                    tt.CustomTextFromCodeRefreshText2 =
                        () => LEManager.Get("SimpleTweaks.Tooltip.DeleteAsteroid");
                }

                // ── Shift moonsRT left ──
                moonsRT.anchoredPosition = new Vector2(
                    moonsRT.anchoredPosition.x - TotalDeleteColumnWidth,
                    moonsRT.anchoredPosition.y);

                // ── Tow info ──
                if (!oi.PushableAsteroid2) return;

                var allSc = SerializedMonoBehaviourSingleton<AllScriptableObjectManager>.Instance.AllSpacecraftType;
                SpacecraftType atlas = allSc.GetByID("spacecraft_asteroid_puller");
                SpacecraftType engine = allSc.GetByID("Spacecraft_build_asteroid_engine_facilityModule");
                if (atlas == null || engine == null) return;

                int atlasCount = oi.AsteroidCanBePushHowMuch(player, atlas);
                int engineCount = oi.AsteroidCanBePushHowMuch(player, engine);

                var towGo = new GameObject("TowRequirements");
                towGo.transform.SetParent(moonsTmp.transform.parent, false);
                towGo.transform.SetAsLastSibling();

                var towTmp = towGo.AddComponent<TextMeshProUGUI>();
                towTmp.font = moonsTmp.font;
                towTmp.fontSharedMaterial = moonsTmp.fontSharedMaterial;
                towTmp.fontSize = moonsTmp.fontSize;
                towTmp.color = moonsTmp.color;
                towTmp.enableWordWrapping = false;
                towTmp.alignment = TextAlignmentOptions.MidlineRight;
                towTmp.text = atlasCount + "A/" + engineCount + "E";

                towTmp.ForceMeshUpdate();
                float towWidth = Mathf.Ceil(towTmp.preferredWidth) + 4f;

                var towRT = towGo.GetComponent<RectTransform>();
                towRT.anchorMin = moonsRT.anchorMin;
                towRT.anchorMax = moonsRT.anchorMax;
                towRT.pivot = new Vector2(1f, moonsRT.pivot.y);
                towRT.sizeDelta = new Vector2(towWidth, moonsRT.sizeDelta.y);
                float edge = moonsRT.anchoredPosition.x + moonsRT.sizeDelta.x * (1f - moonsRT.pivot.x);
                towRT.anchoredPosition = new Vector2(edge - moonsRT.sizeDelta.x * 0.5f, moonsRT.anchoredPosition.y);

                var oiRef = oi;
                var atlasRef = atlas;
                var engineRef = engine;
                var tt2 = towGo.AddComponent<ShowToolTip>();
                tt2.CustomTextFromCodeRefreshText2 = () =>
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



        private static System.Collections.IEnumerator ReExpandParent(
            ObjectInfo parentOI, ObjectInfoGroups parentGroup)
        {
            yield return null;
            yield return null;
            var window = SerializedMonoBehaviourSingleton<UIManager>.Instance
                .GetWindow<SearchObjectWindow>();
            SearchRow parentRow = null;
            if (parentOI != null)
                parentRow = window.FindSearchRow(parentOI);
            if (parentRow == null && parentGroup != null)
                parentRow = window.FindSearchRow(parentGroup);
            if (parentRow != null && !parentRow.IsExpand)
            {
                parentRow.OnClickArrowButton();
            }
            else if (parentRow == null) { }
        }
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
        // Feature 9b: Shift+Click on the X button (upper-right corner of a
        // facility icon in the Object Info list) cancels all facilities of the
        // same type under construction on that body.
        // ─────────────────────────────────────────────────────────────────────────
        [HarmonyPatch(typeof(UIFacilityList), "CancelBuilding")]
        public static class Patch_UIFacilityList_CancelAllBuildings
        {
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
            static void Postfix(UIFacilityList __instance)
            {
                try
                {
                    string tip = LEManager.Get("SimpleTweaks.Tooltip.CancelAllBuildings");
                    foreach (var row in __instance.CreateRows)
                    {
                        var btn = row.ButtonCancel;
                        if (btn == null) continue;
                        var existing = btn.gameObject.GetComponents<ShowToolTip>();
                        foreach (var st in existing)
                            UnityEngine.Object.Destroy(st);
                        var tt = btn.gameObject.AddComponent<ShowToolTip>();
                        tt.CustomTextFromCode = tip;
                        tt.CustomTextFromCodeRefreshText2 = () => tip;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[SimpleTweaks] Patch_UIFacilityList_SetData_Tooltip: " + ex);
                }
            }

            // Feature 9c: Shift+Click on the X (cross) button in a Spacecraft or
            // Launch Vehicle construction queue row cancels all items of the same
            // type (e.g. same ship class) in the construction queue.
            // ─────────────────────────────────────────────────────────────────────────
            [HarmonyPatch(typeof(UIRowRocket), "OnCancelBuildClick")]
            public static class Patch_UIRowRocket_CancelAllConstruction
            {
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
                static void Postfix(UIRowRocket __instance)
                {
                    try
                    {
                        var btn = Traverse.Create(__instance)
                            .Field("buttonCancelConstruction")
                            .GetValue<UnityEngine.UI.Button>();
                        if (btn == null) return;
                        string tip = LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
                        var tt = btn.gameObject.GetComponent<ShowToolTip>()
                            ?? btn.gameObject.AddComponent<ShowToolTip>();
                        tt.CustomTextFromCode = tip;
                        tt.CustomTextFromCodeRefreshText2 = () => tip;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("[SimpleTweaks] Patch_UIRowRocket_Awake_Tooltip: " + ex);
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────────────
            // Feature 9d: Shift+Click on the CANCEL BUILDING button in the Spacecraft
            // / Launch Vehicle info window cancels all constructions of that exact
            // type (e.g. same ship class, not just SC vs LV) on the body.
            // ─────────────────────────────────────────────────────────────────────────
            [HarmonyPatch(typeof(SpaceCraftInfoWindow), "OnCancelBuildButtonClick")]
            public static class Patch_SpaceCraftInfoWindow_CancelAllConstruction
            {
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
                static void Postfix(SpaceCraftInfoWindow __instance)
                {
                    try
                    {
                        var btn = Traverse.Create(__instance)
                            .Field("cancelBuildButton")
                            .GetValue<UnityEngine.UI.Button>();
                        if (btn == null) return;
                        string tip = LEManager.Get("SimpleTweaks.Tooltip.CancelAllConstruction");
                        var tt = btn.gameObject.GetComponent<ShowToolTip>()
                            ?? btn.gameObject.AddComponent<ShowToolTip>();
                        tt.CustomTextFromCode = tip;
                        tt.CustomTextFromCodeRefreshText2 = () => tip;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("[SimpleTweaks] Patch_SpaceCraftInfoWindow_Awake_Tooltip: " + ex);
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
                        var destInput = DestInputField?.GetValue(__instance) as ObjectSearchInputField;
                        var startInput = StartInputField?.GetValue(__instance) as ObjectSearchInputField;
                        if (destInput == null || startInput == null) return;
                        var searchBtn = SearchBtnOnInput?.GetValue(destInput) as UnityEngine.UI.Button;

                        // ── build button game object ───────────────────────────────
                        var btnGo = new GameObject("ST_DestShortcut");
                        // Parent to the destination input; the button's rect extends
                        // outside the input's bounds to the right (no masking at root).
                        btnGo.transform.SetParent(destInput.transform, false);
                        btnGo.transform.SetAsLastSibling();

                        // Background – copy style from the ⇄ switch button
                        var img = btnGo.AddComponent<UnityEngine.UI.Image>();
                        var srcImg = searchBtn?.GetComponent<UnityEngine.UI.Image>();
                        if (srcImg != null)
                        {
                            img.sprite = srcImg.sprite;
                            img.color = srcImg.color;
                            img.type = srcImg.type;
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
                            lbl.font = refTmp.font;
                            lbl.fontSharedMaterial = refTmp.fontSharedMaterial;
                        }
                        lbl.fontSize = 14f;
                        lbl.alignment = TextAlignmentOptions.Center;
                        lbl.color = Color.white;
                        lbl.enableWordWrapping = false;

                        var lblRT = lblGo.GetComponent<RectTransform>();
                        lblRT.anchorMin = Vector2.zero;
                        lblRT.anchorMax = Vector2.one;
                        lblRT.offsetMin = Vector2.zero;
                        lblRT.offsetMax = Vector2.zero;

                        // ── position: inside destInput, flush on the right ─────────
                        var btnRT = btnGo.GetComponent<RectTransform>();
                        btnRT.anchorMin = new Vector2(1f, 0f);
                        btnRT.anchorMax = new Vector2(1f, 1f);
                        btnRT.pivot = new Vector2(1f, 0.5f);
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
                    var origin = startInput?.ObjectInfo;
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

            // ─────────────────────────────────────────────────────────────────────────
            // Feature 11: Mission Planner Cargo — crew slider unlocked on all
            // crew-module rows (not just the last).
            //
            // BlockDropDown normally locks the module dropdown, delete button, + button,
            // and crew slider on every row except the last.  This patch overrides
            // BlockDropDown for crew-module rows: it locks the module dropdown, delete
            // button, and + button as usual, but leaves sliderCrew.interactable
            // untouched so the crew slider remains editable on all rows.
            // ─────────────────────────────────────────────────────────────────────────

            // BlockDropDown for crew-module rows: lock the module dropdown,
            // delete button, and + button as usual, but leave sliderCrew.interactable
            // untouched so the crew slider remains editable on all rows.
            [HarmonyPatch(typeof(ResorceRow), "BlockDropDown")]
            public static class Patch_ResorceRow_BlockDropDown_KeepCrewSlider
            {
                private static readonly FieldInfo ModuleDropDownField =
                    typeof(ResorceRow).GetField("moduleDropDown",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                private static readonly FieldInfo ButonDeleteFieldB =
                    typeof(ResorceRow).GetField("butonDelete",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                private static readonly FieldInfo AddMultiFieldB =
                    typeof(ResorceRow).GetField("addMulti",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                static bool Prefix(ResorceRow __instance)
                {
                    if (!__instance.CrewModuleOn) return true; // non-crew rows: run original
                    try
                    {
                        var dd = ModuleDropDownField?.GetValue(__instance) as DropDownEnum;
                        if (dd?.dropDown != null) dd.dropDown.interactable = false;
                        var del = ButonDeleteFieldB?.GetValue(__instance) as UnityEngine.UI.Button;
                        if (del != null) del.interactable = false;
                        var am = AddMultiFieldB?.GetValue(__instance) as UnityEngine.UI.Button;
                        if (am != null) am.gameObject.SetActive(false);
                        return false; // skip original (which would also lock the crew slider)
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("[SimpleTweaks] Patch_ResorceRow_BlockDropDown_KeepCrewSlider: " + ex);
                        return true; // fallback: run original
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────────────
            // Feature 12: Rich cyclical mission display in Planet/Orbit view.
            //
            // In the ObjectInfo "PLANNED MISSIONS" list, replaces the bare-bones
            // cyclical mission row (just name + button that opens Mission List) with
            // the full MissionRowCyclicalNew rectangle from the Missions window,
            // complete with A→B / B→A legs, cargo info, and pause/edit/delete buttons.
            // ─────────────────────────────────────────────────────────────────────────

            [HarmonyPatch(typeof(UIRowMission), "SetDataRowMissionDataCycleMissionsInfo")]
            public static class Patch_UIRowMission_CyclicRichDisplay
            {
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
                    _prefabLookedUp = true;

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
                        // childAlignment is TextAnchor enum — use reflection to avoid
                        // needing UnityEngine.TextRenderingModule reference
                        var prop = typeof(HorizontalLayoutGroup).GetProperty("childAlignment");
                        if (prop != null)
                        {
                            // TextAnchor.UpperLeft = 0
                            prop.SetValue(hlg, 0);
                        }
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

        // ─────────────────────────────────────────────────────────────────────
        // Hide stockpile resources with amount < 0.01 in ObjectInfoWindow.
        // These trace amounts are floating-point artifacts from construction
        // cost discounts, not real resources the player can use.
        // ─────────────────────────────────────────────────────────────────────
        [HarmonyPatch(typeof(ObjectInfoWindow), nameof(ObjectInfoWindow.GetListRowResourcesDataToShowUI))]
        public static class Patch_ObjectInfoWindow_HideTraceResources
        {
            static void Postfix(ref List<RowResourcesData> __result)
            {
                try
                {
                    if (__result == null || __result.Count == 0) return;
                    __result = __result.Where(r => r.Value >= 0.01).ToList();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[SimpleTweaks] Patch_HideTraceResources: " + ex);
                }
            }
        }
    }
}
