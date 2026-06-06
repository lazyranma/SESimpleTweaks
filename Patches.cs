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
    // Shared transpiler helper: extends any method that already has
    //   if (Input.GetKey(LeftShift)) { ... += 10; }
    // to also trigger on LeftControl, replacing the literal 10 with
    // a call to `getCountMethod` (which returns 100 if Ctrl, 10 if Shift).
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

    /// <summary>
    /// Shared transpiler helper for Fleet Scales patches.
    /// Finds calls to a target method in IL, skips the first N occurrences,
    /// and after each remaining call injects IL that multiplies the return
    /// value (double) by a count loaded via the provided countLoaders.
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
    // Deposit Tooltips: Show deposit-specific tooltip when hovering a resource icon
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
                            if (img.sprite.name == "trash_delete_deconstruction")
                            {
                                _trashSprite = img.sprite;
                                break;
                            }
                        }
                    }
                }
            }
            if (_trashSprite != null) _trashSpriteLookedUp = true;
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
        }
    }

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
    // Ctrl+Click x100: Ctrl+100 in CountToAdd (SC/LV selection +/-).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(CountToAdd), "OnClickAdd")]
    public static class Patch_CountToAdd_OnClickAdd
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "CountToAdd.OnClickAdd");
    }

    [HarmonyPatch(typeof(CountToAdd), "OnClickRemove")]
    public static class Patch_CountToAdd_OnClickRemove
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "CountToAdd.OnClickRemove");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ctrl+Click x100: Ctrl+100 for module-cargo + button (ResourcesList).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ResourcesList), "OnClickMultiAdd")]
    public static class Patch_ResourcesList_OnClickMultiAdd
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "ResourcesList.OnClickMultiAdd");
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
    // Ctrl+Click x100: Ctrl+100 facility builds in ObjectInfoWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ObjectInfoWindow), "FacilityListOnOnClickCreateFacility")]
    public static class Patch_ObjectInfoWindow_FacilityBuildCount
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "ObjectInfoWindow.FacilityBuildCount");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ctrl+Click x100: Ctrl+100 SC/LV builds in SpaceCraftConstructionWindow.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpaceCraftConstructionWindow), "OnClickAccept")]
    public static class Patch_SpaceCraftConstructionWindow_BuildCount
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => TranspilerHelper.PatchShiftPlusTen(instructions, typeof(TranspilerHelper), nameof(TranspilerHelper.GetShiftCtrlCount), "SpaceCraftConstructionWindow.OnClickAccept");
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
                var btnAccept = __instance.RectTransformBtnAccept?.GetComponent<UnityEngine.UI.Button>();
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
                var btnAccept = __instance.RectTransformBtnAccept?.GetComponent<UnityEngine.UI.Button>();
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
    // Clear Build Queue: Shift+Click on "Cancel Building" cancels ALL facilities
    //            under construction on the same body.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(FacilityInfoWindow), "OnActionButtonClick")]
    public static class Patch_FacilityInfoWindow_CancelAllBuildings
    {
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
    // Clear Build Queue: Shift+Click on the X button (upper-right corner of a
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

    // Clear Build Queue: Shift+Click on the X (cross) button in a Spacecraft or
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
    // Clear Build Queue: Shift+Click on the CANCEL BUILDING button in the Spacecraft
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
    // Quick to Orbit: Plan Mission — "↑ ORBIT / ↓ SURFACE" quick-destination button.
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
        internal static readonly FieldInfo StartInputField =
            typeof(PMTabDestination).GetField("startInput",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // instance → button+label — used by the refresh patches below
        internal sealed class DestShortcutEntry
        {
            public readonly UnityEngine.UI.Button Btn;
            public readonly TextMeshProUGUI Lbl;
            public DestShortcutEntry(UnityEngine.UI.Button btn, TextMeshProUGUI lbl)
            { Btn = btn; Lbl = lbl; }
        }
        internal static readonly ConditionalWeakTable<PMTabDestination, DestShortcutEntry> Registry =
            new ConditionalWeakTable<PMTabDestination, DestShortcutEntry>();

        private const float BtnWidth = 22f;
        private const float BtnInset = 2f;

        static void Postfix(PMTabDestination __instance)
        {
            try
            {
                var destInput = DestInputField?.GetValue(__instance) as ObjectSearchInputField;
                var startInput = StartInputField?.GetValue(__instance) as ObjectSearchInputField;
                if (destInput == null || startInput == null) return;
                var searchBtn = destInput?.SearchButtonRectTransform?.GetComponent<UnityEngine.UI.Button>();

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

                Registry.Add(__instance, new DestShortcutEntry(btn, lbl));
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

        internal static void RefreshForInstance(PMTabDestination instance, string logTag)
        {
            try
            {
                if (!Registry.TryGetValue(instance, out var entry)) return;
                var startInput = StartInputField.GetValue(instance) as ObjectSearchInputField;
                if (startInput == null) return;
                RefreshButton(startInput, entry.Btn, entry.Lbl);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] " + logTag + ": " + ex);
            }
        }
    }

    // Refresh when origin selection changes.
    [HarmonyPatch(typeof(PMTabDestination), "StartInputOnObjectSelect")]
    public static class Patch_PMTabDestination_StartInputOnObjectSelect
    {
        static void Postfix(PMTabDestination __instance) =>
            Patch_PMTabDestination_DestShortcut.RefreshForInstance(
                __instance, "Patch_PMTabDestination_StartInputOnObjectSelect");
    }

    // Refresh when the destination tab is activated (origin pre-filled by the game).
    [HarmonyPatch(typeof(PMTabDestination), "ActiveTab")]
    public static class Patch_PMTabDestination_ActiveTab
    {
        static void Postfix(PMTabDestination __instance) =>
            Patch_PMTabDestination_DestShortcut.RefreshForInstance(
                __instance, "Patch_PMTabDestination_ActiveTab");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unsticky Crew: Mission Planner Cargo — crew slider unlocked on all
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
                var dd = __instance.ResorceDropDownModuleRectTransform?.GetComponent<DropDownEnum>();
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
    // Full Cycle: Rich cyclical mission display in Planet/Orbit view.
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

    // ─────────────────────────────────────────────────────────────────────
    // Leave No Trace — Eliminate floating-point noise from construction
    // cost discounts by rounding at key arithmetic points, and remove
    // deposits that fall below the precision threshold.
    //
    // Patch 1: Round the multiplier before multiplication.
    //   Kills float noise at the earliest point. 0.730000019f → 0.73.
    // ─────────────────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────
    // Fleet Scales — fixes the "perfect DV" cargo limit and drag-and-drop
    // cargo amounts to account for the number of selected spacecraft (SCCount)
    // instead of always using a single ship's capacity.
    //
    // Three single-unit sites, all missing `× scCount`:
    // 1. MaxValueSliderFuelToCalculateLoadLimit2 — every GetFuelCapacity call.
    //    Transpiler injects × SCCount after each.
    //    Postfix also floors result at fleet cargo capacity so the search range
    //    in CalculateLoadLimit2ToBeOkayMinFuelCost covers the full cargo range.
    //    (This replaces a former Transpiler on that method — which was wrapped
    //    in a try-catch in the May 2026 beta, causing any transpiler exception
    //    to be silently swallowed and the method to return 0.)
    // 2. AddCargoOrbit — drag-and-drop handler for surface starts.
    //    Transpiler injects × SCCount into its GetCargoCapacity call.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fleet Scales — multiply every GetFuelCapacity result by SCCount,
    /// fixing the comparison, the cap, and the ignoreLimit path at the source.
    /// Also floors the result at fleet cargo capacity so the binary-search
    /// upper bound in CalculateLoadLimit2ToBeOkayMinFuelCost covers the full
    /// cargo range when fleet cargo exceeds fleet fuel.
    /// </summary>
    [HarmonyPatch(typeof(PMMissionParameter), "MaxValueSliderFuelToCalculateLoadLimit2")]
    public static class Patch_FleetScale_FuelCap
    {
        // Enabled for 0.26.5.x (stable) only — beta compensates at the call site.
        private static readonly bool IsStable =
            UnityEngine.Application.version.StartsWith("0.26.5.");

        [HarmonyPrepare]
        static bool Prepare() => IsStable;

        private static MethodInfo _getFuelCap = AccessTools.Method(
            typeof(SpacecraftType), nameof(SpacecraftType.GetFuelCapacity));
        private static MethodInfo _getScCount = AccessTools.PropertyGetter(
            typeof(PMMissionParameter), nameof(PMMissionParameter.SCCount));

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var countLoaders = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, _getScCount),
            };
            FleetScaleTranspiler.Patch(codes, _getFuelCap, countLoaders, skipCount: 0, expectedMin: 3);
            return codes;
        }

        [HarmonyPostfix]
        static void Postfix(PMMissionParameter __instance, ref double __result)
        {
            // Floor at fleet cargo capacity so CalculateLoadLimit2ToBeOkayMinFuelCost
            // searches the full cargo range when fleet cargo > fleet fuel.
            // Doing this here (rather than transpiling the try-catch method directly)
            // avoids any risk of our injection being caught and silently returning 0.
            var sc = __instance.SC;
            if (sc == null) return;
            var sct = sc.GetTypeSpaceCraft();
            if (sct == null) return;
            double fleetCargoCap = sct.GetCargoCapacity(__instance.FlyCompany) * __instance.SCCount;
            if (__result < fleetCargoCap)
                __result = fleetCargoCap;
        }
    }

    /// <summary>
    /// Fleet Scales — AddCargoOrbit (drag-and-drop to orbit) uses single-ship
    /// cargo capacity without multiplying by SCCount.
    /// </summary>
    [HarmonyPatch(typeof(PMTabCargo), "AddCargoOrbit", new System.Type[] { typeof(ResourceDefinition) })]
    public static class Patch_FleetScale_AddCargoOrbit
    {
        private static MethodInfo _getCargoCap = AccessTools.Method(
            typeof(SpacecraftType), nameof(SpacecraftType.GetCargoCapacity));
        private static MethodInfo _get_planMissionWindow = AccessTools.PropertyGetter(
            typeof(PMTab), nameof(PMTab.PlanMissionWindow));
        private static MethodInfo _get_PMMParameter = AccessTools.PropertyGetter(
            typeof(PlanMissionWindow), nameof(PlanMissionWindow.PMMissionParameter));
        private static MethodInfo _get_ScCount = AccessTools.PropertyGetter(
            typeof(PMMissionParameter), nameof(PMMissionParameter.SCCount));

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var countLoaders = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Callvirt, _get_planMissionWindow),
                new CodeInstruction(OpCodes.Callvirt, _get_PMMParameter),
                new CodeInstruction(OpCodes.Callvirt, _get_ScCount),
            };
            FleetScaleTranspiler.Patch(codes, _getCargoCap, countLoaders, skipCount: 0, expectedMin: 1);
            return codes;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Feature: Quick to Orbit II — in the quick-access body bar, Ctrl+Click
    // opens a body's orbit ObjectInfo window instead of the body itself, and
    // Ctrl+Shift+Click does the same in the secondary window.
    // Ctrl+drop of modules/resources/ships redirects mission planner target to
    // that body's orbit. Falls back to default behaviour when no orbit exists
    // (e.g. the Sun / Solar Orbit entries).
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

    [HarmonyPatch(typeof(HighlightHoverObject), "ChangeTarget")]
    public static class Patch_HighlightHoverObject_CtrlClickOrbit
    {
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

    // ─────────────────────────────────────────────────────────────────────────
    // Feature: Mass Effect — fixes negative solid phase fractions.
    //
    // In UpdateDepositStates, the solid persistence formula can produce
    // negative solidFraction when gasFraction + liquidAngle/π > 1.
    // This clamps liquidFraction to 1−gasFraction so solidFraction ≥ 0.
    // Mass is strictly conserved (fractions always sum to 1).
    // Existing saves self-correct within one monthly tick.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Data.ScriptableObject.Terraformation.TerraformationConfig.HabitabilityParametersNew), "UpdateDepositStates")]
    public static class Patch_UpdateDepositStates_MassEffect
    {
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

    // ─────────────────────────────────────────────────────────────────────────
    // Feature: Keep Scanning — idle telescopes and observatories auto-start discovering asteroids
    // when a slot opens up; survey resources when all asteroids are discovered.
    //
    // Two complementary triggers:
    //
    // Trigger A — Asteroid destroyed (VirtualDestroy postfix):
    //   When any asteroid/comet is removed, SpawnAsteroidIfNeed() has already
    //   run synchronously, potentially adding a new undiscovered asteroid.
    //   Every idle player telescope or observatory is sent to discover asteroids (up to the
    //   count of unclaimed undiscovered asteroids), or to survey resources if
    //   no discovery slots remain.
    //
    // Trigger B — Discovery completes (OnObjectDiscovered postfix):
    //   The game already calls FindNextObjectForDiscovery() inside
    //   OnObjectDiscovered, so if another undiscovered asteroid exists the
    //   telescope/observatory continues in Discovery mode automatically.  Our Postfix only
    //   fires the survey fallback for THIS telescope/observatory when
    //   FindNextObjectForDiscovery returned false (WorkMode went Idle), meaning
    //   all asteroids are now discovered.
    //
    // In both cases OrderDiscovery / OrderExploration respect all game
    // constraints: IsBeingCurrentlyDiscovered prevents double-booking, and
    // FindNextTargetRecurrently picks the right survey target.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ObjectInfo), nameof(ObjectInfo.VirtualDestroy))]
    public static class Patch_AutoActivateObservatoriesOnAsteroidDestroy
    {
        static void Postfix(ObjectInfo __instance)
        {
            if (__instance.objectTypes != EObjectTypes.Asteroid &&
                __instance.objectTypes != EObjectTypes.Comet) return;

            try { TryActivateIdleObservatories(); }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] AutoActivateObservatories: " + ex);
            }
        }

        private static void TryActivateIdleObservatories()
        {
            if (LoadSaveManager.OnExtractAllFromSaveData) return;
            if (MonoBehaviourSingleton<GameManager>.InstanceIsNull) return;

            Company player = MonoBehaviourSingleton<GameManager>.Instance.Player;
            if (player == null) return;

            if (MonoBehaviourSingleton<ObjectInfoManager>.InstanceIsNull) return;
            var mgr = MonoBehaviourSingleton<ObjectInfoManager>.Instance;

            // Count undiscovered asteroids that no telescope or observatory has claimed yet.
            // This is the natural cap on how many discovery assignments we can make.
            int freeSlots = CountFreeDiscoverySlots(mgr, player);

            foreach (ObjectInfo oi in mgr.allObjectInfos.ToList())
            {
                ObjectInfoData data = oi.GetObjectInfoData(player);
                if (data == null) continue;

                foreach (Facility fac in data.ListFacility.ToList())
                {
                    if (!(fac is ObservatoryFacility obs)) continue;
                    if (obs.Quantity < 1) continue;
                    if (obs.WorkMode != ObservatoryFacility.EWorkMode.Idle) continue;

                    if (freeSlots > 0)
                    {
                        if (obs.OrderDiscovery(null, true))
                            freeSlots--;
                    }
                    else
                    {
                        // No undiscovered asteroids left — survey resources instead.
                        TryOrderSurvey(obs, mgr);
                    }
                }
            }
        }

        private static int CountFreeDiscoverySlots(ObjectInfoManager mgr, Company player)
        {
            int count = 0;
            foreach (ObjectInfo info in mgr.allObjectInfos)
            {
                if (info.objectTypes != EObjectTypes.Asteroid) continue;
                ObjectInfoData d = info.GetObjectInfoData(player);
                if (d == null) continue;
                if (!d.IsDiscoveredOrNotAsteroid && !d.IsBeingCurrentlyDiscovered && !d.IsHideToDiscover)
                    count++;
            }
            return count;
        }

        // FindNextTargetRecurrently is private; cache the MethodInfo once so the
        // name lookup only happens at class-init time, not on every invocation.
        private static readonly MethodInfo _findNextTargetRecurrently =
            AccessTools.Method(typeof(ObservatoryFacility), "FindNextTargetRecurrently",
                new[] { typeof(object), typeof(object), typeof(ObjectInfo).MakeByRefType(), typeof(bool) });

        // Calls the game's own FindNextTargetRecurrently starting from the Sun so the
        // traversal order and target-validity logic are identical to what the game does
        // when a telescope or observatory finishes surveying one body and moves to the next.
        internal static bool TryOrderSurvey(ObservatoryFacility obs, ObjectInfoManager mgr)
        {
            if (_findNextTargetRecurrently == null) return false;
            var args = new object[] { (object)mgr.mainObjectInfoSun, null, null, true };
            if ((bool)_findNextTargetRecurrently.Invoke(obs, args))
                return obs.OrderExploration((ObjectInfo)args[2], true);
            return false;
        }
    }

    // Keep Scanning — Trigger B: survey fallback when a telescope or observatory finishes its last discovery.
    // The game already auto-requeues the telescope/observatory via FindNextObjectForDiscovery()
    // inside OnObjectDiscovered; we only need to act when that returns false
    // (WorkMode went Idle = no more undiscovered asteroids).
    [HarmonyPatch(typeof(ObservatoryFacility), "OnObjectDiscovered")]
    public static class Patch_SurveyFallbackAfterDiscoveryComplete
    {
        static void Postfix(ObservatoryFacility __instance)
        {
            if (__instance.WorkMode != ObservatoryFacility.EWorkMode.Idle) return;
            if (LoadSaveManager.OnExtractAllFromSaveData) return;
            if (MonoBehaviourSingleton<GameManager>.InstanceIsNull) return;

            Company player = MonoBehaviourSingleton<GameManager>.Instance.Player;
            if (player == null || !__instance.Company.Equals(player)) return;

            if (MonoBehaviourSingleton<ObjectInfoManager>.InstanceIsNull) return;
            var mgr = MonoBehaviourSingleton<ObjectInfoManager>.Instance;
            Patch_AutoActivateObservatoriesOnAsteroidDestroy.TryOrderSurvey(__instance, mgr);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Lift Me Off — Fix zero load limit for LowOrbitContainer
    //
    // When planning a mission with an orbital payload container spacecraft and
    // no launch vehicle is selected yet, the game's CalculateLoadLimit2 method
    // returns 0 because its fallback check (LowOrbitContainer && lvType != null)
    // fails when lvType is null.  This causes "Max capacity for optimal
    // transfer" to display 0 T until the user adds cargo manually.
    //
    // The fix: when the result is ≤ 0 for a LowOrbitContainer with a null LV,
    // fall back to the best available LV via LVTypeBest().
    //
    // Cross-version: TargetMethod() selects the correct overload at runtime.
    // Stable: 3-arg (lvType, dV1, dV2).  Beta: 4-arg (lvType, dV1, dV2, lvCount).
    // On stable the Postfix receives lvCount=0 (Harmony default-fills unmatched
    // Postfix params), and LVTypeBest is called via reflection to handle the
    // signature change (stable: no args, beta: out int).
    // ═════════════════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(PMTabSchedule))]
    public static class Patch_LiftMeOff
    {
        static MethodBase TargetMethod()
        {
            // Beta: 4-arg (LaunchVehicleType, double, double, int lvCount)
            var m = AccessTools.Method(typeof(PMTabSchedule), "CalculateLoadLimit2ToBeOkayMinFuelCost",
                new[] { typeof(LaunchVehicleType), typeof(double), typeof(double), typeof(int) });
            if (m != null) return m;

            // Stable: 3-arg (LaunchVehicleType, double, double)
            return AccessTools.Method(typeof(PMTabSchedule), "CalculateLoadLimit2ToBeOkayMinFuelCost",
                new[] { typeof(LaunchVehicleType), typeof(double), typeof(double) });
        }

        static void Postfix(PMTabSchedule __instance, LaunchVehicleType lvType, double dV1, double dV2, int lvCount, ref double __result)
        {
            // On stable (3-arg method), Harmony fills unmatched Postfix params
            // with their default value, so lvCount will be 0.
            try
            {
                if (lvType != null || __result > 0)
                    return;

                var pm = __instance.PlanMissionWindow?.PMMissionParameter;
                if (pm == null) return;

                var sct = pm.SC?.GetTypeSpaceCraft();
                if (sct == null || !sct.LowOrbitContainer)
                    return;

                // LVTypeBest() changed signature in beta (added out int).
                // Use reflection to call the right overload on either version.
                var selectLv = __instance.PlanMissionWindow?.PMTabSelectLV;
                if (selectLv == null) return;
                var lvTypeBestMethod = AccessTools.Method(typeof(PMTabSelectLV), "LVTypeBest", Type.EmptyTypes)
                                    ?? AccessTools.Method(typeof(PMTabSelectLV), "LVTypeBest", new[] { typeof(int).MakeByRefType() });
                if (lvTypeBestMethod == null) return;

                var args = lvTypeBestMethod.GetParameters().Length == 0 ? null : new object[] { 0 };
                var bestLvType = (LaunchVehicleType)lvTypeBestMethod.Invoke(selectLv, args);
                // Extract the out-param count from beta's LVTypeBest(out int).
                int bestLvCount = (args != null) ? (int)args[0] : 1;

                if (bestLvType == null) return;

                double fallback = bestLvType.MaxPayloadOnThisObject(pm.Start, pm.FlyCompany);
                fallback *= bestLvCount;
                if (fallback > 0)
                    __result = fallback;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[LiftMeOff] error: {ex}");
            }
        }
    }
    // ═════════════════════════════════════════════════════════════════════════
    // Rapid Scheduled Disassembly
    //
    // The vanilla game shows a YES/NO dialog when scrapping a spacecraft or
    // launch vehicle, allowing only one instance at a time.  This patch
    // replaces it with a number-input dialog (like the one used for scrapping
    // multiple facility instances) when more than one identical idle
    // spacecraft/LV of the same type exist on the same body.
    //
    // The patch counts identical idle (not under construction, not on a
    // mission) items of the same type on the same body, and if the count
    // exceeds 1, presents the input dialog so the player can scrap any
    // number up to that count in one operation.
    // ═════════════════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(SpaceCraftInfoWindow), "OnScrapButtonClick")]
    public static class Patch_SpaceCraftInfoWindow_ScrapMulti
    {
        private static readonly FieldInfo _rowRocketDataField =
            AccessTools.Field(typeof(SpaceCraftInfoWindow), "rowRocketData");

        static bool Prefix(SpaceCraftInfoWindow __instance)
        {
            try
            {
                var rowRocketData = (RowRocketData)_rowRocketDataField.GetValue(__instance);
                if (rowRocketData == null) return true;

                // Only handle real instances (not fakes from the info panel)
                if (rowRocketData.spacecraft == null && rowRocketData.launchVehicle == null)
                    return true;

                var prodItem = rowRocketData.GetProductionItem();
                if (prodItem == null) return true;

                // Determine body and company from the real instance
                ObjectInfo body = null;
                Company company = null;
                bool isSpacecraft = false;

                if (rowRocketData.spacecraft != null)
                {
                    body = rowRocketData.spacecraft.CurrentlyOnThisObject;
                    company = rowRocketData.spacecraft.GetCompany();
                    isSpacecraft = true;
                }
                else if (rowRocketData.launchVehicle != null)
                {
                    body = rowRocketData.launchVehicle.objectInfo;
                    company = rowRocketData.launchVehicle.GetCompany();
                    isSpacecraft = false;
                }

                if (body == null || company == null) return true;
                if (!company.IsPlayer) return true;

                // Get all items of this type on this body, then count idle ones
                var oid = body.GetObjectInfoData(company);
                if (oid == null) return true;

                List<RowRocketData> allItems;
                if (isSpacecraft)
                    allItems = oid.GetListSpacecraftAndConstructed();
                else
                    allItems = oid.GetListLaunchVehicle();

                var matchingItems = allItems.Where(rrd =>
                    rrd.rConstruct == null &&
                    rrd.GetProductionItem() == prodItem &&
                    // Exclude items currently on a mission
                    (rrd.spacecraft == null || rrd.spacecraft.GetMissionInfo() == null) &&
                    (rrd.launchVehicle == null || rrd.launchVehicle.spacecraft == null ||
                     rrd.launchVehicle.spacecraft.GetMissionInfo() == null)
                ).ToList();

                int count = matchingItems.Count;
                if (count <= 1) return true; // Let the original YES/NO dialog handle it

                // Calculate the refund price for a single item
                var economic = MonoBehaviourSingleton<GameManager>.Instance.Economic;
                var player = MonoBehaviourSingleton<GameManager>.Instance.Player;

                ResourcePrice singlePrice = null;
                if (rowRocketData.spacecraft != null)
                    singlePrice = rowRocketData.spacecraft.spacecraftType.spaceCraftConstructDefault.Price;
                else if (rowRocketData.launchVehicle != null)
                    singlePrice = rowRocketData.launchVehicle.launchVehicleType.spaceCraftConstructDefault.Price;

                if (singlePrice == null) return true;

                singlePrice *= economic.GetScrapFinishedProductionMultiplier(player);

                string shipName = rowRocketData.GetShipName();
                long maxToScrap = count;

                // Reuse the facility "ScrapDialogHowMany" localisation key so we
                // don't need to add new translation entries for every language.
                SerializedMonoBehaviourSingleton<UIManager>.Instance.ShowInput(
                    LEManager.Get("Game.UI.Windows.Windows.FacilityInfoWindow.ScrapDialogHowMany")
                        .MyFormat(shipName,
                            (singlePrice * maxToScrap).ToStringTranslation(" <color=grey>/</color> ")),
                    onOk: (string s) =>
                    {
                        if (long.TryParse(s, out var result))
                        {
                            long toScrap = System.Math.Min(result, count);
                            for (int i = 0; i < toScrap && i < matchingItems.Count; i++)
                            {
                                var item = matchingItems[i];
                                if (item.spacecraft != null)
                                    item.spacecraft.Scrap();
                                else if (item.launchVehicle != null)
                                    item.launchVehicle.Scrap();
                            }
                            __instance.HideImmediately();
                        }
                    },
                    onNo: null,
                    inputFieldDefault: maxToScrap.ToString(),
                    inputFieldCharacterValidation: TMP_InputField.CharacterValidation.Decimal,
                    onEndEdit: (string s) =>
                    {
                        long newQty = count;
                        if (int.TryParse(s, out var r))
                            newQty = System.Math.Max(1L, System.Math.Min((long)r, (long)count));
                        string newText =
                            LEManager.Get("Game.UI.Windows.Windows.FacilityInfoWindow.ScrapDialogHowMany")
                                .MyFormat(shipName,
                                    (singlePrice * newQty).ToStringTranslation(" <color=grey>/</color> "));
                        return (newValue: newQty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            newText: newText);
                    });

                return false; // Skip the original YES/NO dialog
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[RapidScheduledDisassembly] error: {ex}");
                return true; // Fall back to original behavior on error
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Torch It — Constant-acceleration transfers for cyclical missions.
    //
    // Adds a "Torch" checkbox to the cyclical mission planner that enables
    // constant-acceleration (Bezier) transfers for ships with ConstanceAcceleration=true.
    // Hidden for moon transfers (different ΔV model).
    //
    // Uses ETransferType as a bitmask: bit 3 (8) = Torch mode enabled.
    //   TransferType = 8|Optimal  → Torch-Tmax (slowest, far-right slider)
    //   TransferType = 8|Fastest  → Torch-Tmin (fastest, far-left slider)
    // Stored in CycleMissionsDataData.TransferType, survives saves natively
    // (Odin serializes the enum as int). Launch delay: 3d base + SC-specific.
    // Travel times computed via PMTabSchedule.CalculateMinMaxMissionLenght().
    // ═════════════════════════════════════════════════════════════════════════

    internal static class TorchIt
    {
        // Bit 3 set = Torch mode active. Base bits 0-1 hold Optimal(0) or Fastest(1).
        public const ETransferType BitTorch = (ETransferType)8;

        // Returns true if bit 3 is set in the TransferType value.
        public static bool IsTorchActive(ETransferType et) => ((int)et & (int)BitTorch) != 0;

        // Extracts the base transfer type (Optimal=0 or Fastest=1) by masking bit 3.
        public static ETransferType BaseType(ETransferType et) => (ETransferType)((int)et & 3);

        // Vanilla localisation key used on the normal mission planner's
        // "Constant acceleration" toggle. Reused so all 13 languages work automatically.
        public const string LabelKey = "Game.UI.Windows.Windows.PlanMissionWindow.CONSTANT_ACCELERATION";

        // Our lone checkbox and the TransferToggle that owns it.
        // The cyclical mission planner is a singleton window, so there's
        // only one of each. Both are set in Awake.
        public static Toggle Checkbox;
        public static TransferToggle TransferToggle;

        // Computes launch date and travel time for torch missions.
        // Returns (departure, tmin, tmax) — caller picks Tmin or Tmax.
        // Departure is now + 3d base delay + SC-specific delay.
        // Travel times come from PMTabSchedule.CalculateMinMaxMissionLenght(),
        // which uses the departure date to propagate orbits via CalculateDistance().
        public static (DateTime departure, TimeSpan tmin, TimeSpan tmax)
            ComputeTorchDates(PMTabSchedule pmTab, PMMissionParameter pmp)
        {
            // Minimum departure: now + base delay (3 days) + SC-specific delay.
            var now = MonoBehaviourSingleton<TimeController>.Instance.CurrentTime
                .AddDays(MonoBehaviourSingleton<GameManager>.Instance.Economic
                    .TimeAddToPlanMissionDays);
            if (pmp.SC?.GetTypeSpaceCraft()?.timeAddToPlanMissionDays.HasValue == true)
            {
                now = now.AddDays(pmp.SC.GetTypeSpaceCraft().timeAddToPlanMissionDays.Value);
            }
            // Must plant a departure date before calling CalculateMinMaxMissionLenght()
            // because its internal CalculateDistance() propagates orbits from
            // CurrentTime to DepartureTimeDate to compute interplanetary distance.
            pmp.SetTabDateFromPorkchope(now, now.AddDays(1));

            // Vanilla slider formula: Tmax = 2*sqrt(D/Amin), Tmin = 2*sqrt(D/Amax).
            var mm = pmTab.CalculateMinMaxMissionLenght();

            return (now, mm.Item1, mm.Item2);
        }
    }

    // ── Create the Torch checkbox on Awake ───────────────────────────
    // Awake runs once per TransferToggle. We clone whichever radio toggle
    // is currently OFF (cloning ON would fire OnToggleChange). The checkbox
    // is NOT in the ToggleGroup so it doesn't interfere with the radios.
    [HarmonyPatch(typeof(TransferToggle), "Awake")]
    public static class Patch_TransferToggle_Awake_TorchSetup
    {
        // Cached reflection for the PlanCyclicalMissionWindow field + Toggle fields.
        private static readonly FieldInfo _pcmwField = typeof(TransferToggle).GetField("pcmw", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _optimalField = typeof(TransferToggle).GetField("optimal", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _fastestField = typeof(TransferToggle).GetField("fastest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _transferTypeField = typeof(TransferToggle).GetField("transferType", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _pmLabelsField = typeof(PMTabSchedule).GetField("pmLabels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _drawTrajectoryMethod = typeof(PMTabSchedule).GetMethod("DrawTrajectoryConstantAcceleration", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _setDataMethod = typeof(Game.UI.Windows.Elements.PlanMissionElements.PMScheduleElements.PMLabels).GetMethod("SetData", new[] { typeof(DateTime), typeof(DateTime), typeof(TimeSpan), typeof(bool) });

        static void Postfix(TransferToggle __instance)
        {
            try
            {
                if (TorchIt.Checkbox != null) return;
                var t = __instance.transform;

                // Clone whichever radio toggle is currently OFF.
                // Cloning the ON toggle would trigger the ToggleGroup to fire
                // OnToggleChange, corrupting transferType.
                var fastT = _fastestField.GetValue(__instance) as Toggle;
                var sourceToggle = !fastT.isOn ? fastT : _optimalField.GetValue(__instance) as Toggle;

                // Instantiate the cloned toggle as a child of the outer container.
                // Insert before the radio row so it appears above the toggles.
                var outer = t.parent; // outer "Transfer Types"

                // Give the outer container a vertical layout so children stack.
                var vlg = outer.gameObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vlg ??= outer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                // 24px top gap accounts for the "Transfer Types:" label drawn on outer.
                // 20px bottom padding becomes the visual gap before MISSION ENDS — it
                // disappears automatically when the checkbox is hidden (see below).
                vlg.padding = new RectOffset(0, 0, 24, 20);

                // "EndsToggle" (MISSION ENDS) is a sibling of outer "Transfer Types" inside
                // "Dates". The "Dates" VLG (childControlHeight=false) stacks children using
                // sizeDelta.y. A ContentSizeFitter on outer drives that sizeDelta.y from the
                // VLG's preferred height, which excludes disabled children automatically.
                // So the gap before MISSION ENDS shrinks to just the bottom padding when the
                // checkbox is hidden — no manual resize callbacks needed.
                var csf = outer.gameObject.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                csf ??= outer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

                // Disable the fixed LayoutElement so "Dates" VLG reads outer's actual
                // sizeDelta (driven by the CSF above) rather than the stale prefab values.
                var outerLe = outer.GetComponent<UnityEngine.UI.LayoutElement>();
                outerLe?.enabled = false;

                // Rebuild from "Dates" upward so its ContentSizeFitter picks up the new size.
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    outer.parent.GetComponent<RectTransform>());

                var torchGo = GameObject.Instantiate(sourceToggle.gameObject, outer);
                torchGo.name = "ST_TorchToggle";
                torchGo.transform.SetSiblingIndex(0); // before radio row

                // Give the VLG a minimum height for the checkbox so it doesn't
                // get squashed when childControlHeight is on (default).
                var cbLe = torchGo.GetComponent<UnityEngine.UI.LayoutElement>();
                cbLe ??= torchGo.AddComponent<UnityEngine.UI.LayoutElement>();
                cbLe.minHeight = torchGo.GetComponent<RectTransform>().sizeDelta.y;

                // Inner radio row: reset its anchors so VLG can position it.
                var innerRt = outer.GetChild(1) as RectTransform; // radio row
                innerRt.anchorMin = new Vector2(0, 1);
                innerRt.anchorMax = new Vector2(1, 1);
                innerRt.pivot = new Vector2(0.5f, 1);

                var torchToggle = torchGo.GetComponent<Toggle>();
                torchToggle.SetIsOnWithoutNotify(false);

                TorchIt.Checkbox = torchToggle;
                TorchIt.TransferToggle = __instance;
                torchToggle.group = null;

                // The checkbox was just created — sync its checked state to
                // whatever TransferType the TransferToggle already holds
                // (may have been set by a prior mission or save load).
                var currentTT = _transferTypeField.GetValue(__instance);
                if (TorchIt.IsTorchActive((ETransferType)currentTT))
                {
                    torchToggle.SetIsOnWithoutNotify(true);
                }

                // Reuse the vanilla StaticTranslateText component with the game's
                // key for "CONSTANT ACCELERATION".
                var stt = torchGo.GetComponentInChildren<Language.StaticTranslateText>();
                stt.key = TorchIt.LabelKey;
                stt.translateText.text = LEManager.Get(TorchIt.LabelKey);

                // ── Torch checkbox handler ─────────────────────────────
                torchToggle.onValueChanged.AddListener(_ =>
                {
                    try
                    {
                        // Get the PlanCyclicalMissionWindow from the TransferToggle.
                        var pcmw = _pcmwField.GetValue(__instance) as PlanCyclicalMissionWindow;
                        var pmp = pcmw?.PMMissionParameter;
                        var pmTab = pcmw?.PmTabSchedule;

                        // Read which radio button is currently selected.
                        var fastestT = _fastestField.GetValue(__instance) as Toggle;
                        var isFastest = fastestT?.isOn == true;
                        var baseType = isFastest ? ETransferType.Fastest : ETransferType.Optimal;

                        if (torchToggle.isOn)
                        {
                            // ── Torch ENABLED ──────────────────────────
                            // Combine TorchIt.BitTorch with the current radio value.
                            var newType = (ETransferType)((int)baseType | (int)TorchIt.BitTorch);
                            _transferTypeField.SetValue(__instance, newType);
                            pcmw?.CycleMissionsDataData?.TransferType = newType;

                            // Switch to constant-acceleration mode.
                            pmp?.SetBurst(false);

                            RecomputeTorchDatesAndUpdateLabels(pmTab, pmp, isFastest);
                        }
                        else
                        {
                            // ── Torch DISABLED ─────────────────────────
                            // Revert to vanilla: strip bit 3, restore burst mode.
                            var newType = baseType;
                            _transferTypeField.SetValue(__instance, newType);
                            if (pcmw?.CycleMissionsDataData != null)
                                pcmw.CycleMissionsDataData.TransferType = newType;
                            pmp?.SetBurst(true);
                            // Trigger porkchop recomputation via the vanilla click handlers.
                            if (isFastest)
                                pcmw?.ClickFastest();
                            else
                                pcmw?.ClickOptimal();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("[SimpleTweaks] TorchIt checkbox: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_TransferToggle_Awake_TorchSetup: " + ex);
            }
        }

        // ── Helper: show/hide torch checkbox based on ship eligibility ──
        // Also handles save-load auto-activation and reverting when torch
        // becomes ineligible (e.g. ship or target changed).
        public static void SyncFromMissionData(TransferToggle __instance, PlanCyclicalMissionWindow pcmw)
        {
            try
            {
                var torchT = TorchIt.Checkbox;
                if (torchT == null) return;
                var torchGo = torchT.gameObject;

                // Show torch only when PMMP says constant-acceleration is valid
                // (checks SC.ConstanceAcceleration, moon case, orbit case).
                var canTorch = pcmw?.PMMissionParameter?.CanSetConstanceAcceleration() == true;
                torchGo.gameObject.SetActive(canTorch);

                // Sync checkbox to the current mission's TransferType.
                var savedType = (ETransferType)_transferTypeField.GetValue(__instance);
                var shouldBeOn = TorchIt.IsTorchActive(savedType);
                if (shouldBeOn)
                    torchT.isOn = true;
                else
                    torchT.SetIsOnWithoutNotify(false);

                // If torch became ineligible while enabled (ship/moon-case changed),
                // turn it off, preserve the Optimal/Fastest radio choice.
                if (!canTorch && TorchIt.IsTorchActive(savedType))
                {
                    _transferTypeField.SetValue(__instance, TorchIt.BaseType(savedType));
                    var win = _pcmwField.GetValue(__instance) as PlanCyclicalMissionWindow;
                    if (win?.CycleMissionsDataData != null)
                        win.CycleMissionsDataData.TransferType = TorchIt.BaseType(savedType);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] TorchIt SyncFromMissionData: " + ex);
            }
        }

        // Shared helper: compute torch dates and update PMLabels.
        // Called by both the checkbox lambda (Awake) and the OnToggleChange prefix.
        internal static void RecomputeTorchDatesAndUpdateLabels(PMTabSchedule pmTab, PMMissionParameter pmp, bool isFastest)
        {
            if (pmp == null || pmTab == null) return;
            var (dep, tmin, tmax) = TorchIt.ComputeTorchDates(pmTab, pmp);
            var travelTime = isFastest ? tmin : tmax;
            pmp.SetTabDateFromPorkchope(dep, dep + travelTime);
            var labels = _pmLabelsField.GetValue(pmTab);
            _setDataMethod.Invoke(labels, new object[] {
                pmp.DepartureTimeDate, pmp.Arrival,
                pmp.Arrival - pmp.DepartureTimeDate, true });
            _drawTrajectoryMethod.Invoke(pmTab, null);
        }
    }

    // ── Sync torch checkbox state when a mission is bound ────────────
    // SetData is called when a TransferToggle is bound to a PlanCyclicalMissionWindow.
    // We sync the existing Torch checkbox visibility and checked state to the new mission.
    [HarmonyPatch(typeof(TransferToggle), nameof(TransferToggle.SetData),
        new[] { typeof(PlanCyclicalMissionWindow) })]
    public static class Patch_TransferToggle_SetData_TorchSync
    {
        static void Postfix(TransferToggle __instance, PlanCyclicalMissionWindow _pcmw)
        {
            try
            {
                if (_pcmw == null) return;
                Patch_TransferToggle_Awake_TorchSetup.SyncFromMissionData(__instance, _pcmw);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_TransferToggle_SetData_TorchSync: " + ex);
            }
        }
    }

    // ── Mirror the checkbox interactable state in edit mode ──────────
    // The game calls SetToggleInteractable(false) to block the
    // Optimal/Fastest radio buttons. We mirror that to our checkbox.
    [HarmonyPatch(typeof(TransferToggle), nameof(TransferToggle.SetToggleInteractable))]
    public static class Patch_SetToggleInteractable_TorchIt
    {
        static void Postfix(TransferToggle __instance, bool interactable)
        {
            try
            {
                var torchT = TorchIt.Checkbox;
                torchT?.interactable = interactable;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] SetToggleInteractable: " + ex);
            }
        }
    }

    // ── Extend TransferType setter to handle bit 3 (8) ───────────────
    // Vanilla setter handles Fastest (check fastest) / else (check optimal).
    // When bit 3 is set, also check our torch checkbox.
    [HarmonyPatch(typeof(TransferToggle), "set_TransferType")]
    public static class Patch_TransferToggle_SetTransferType_TorchIt
    {
        private static readonly FieldInfo _optimalField = typeof(TransferToggle).GetField("optimal", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _fastestField = typeof(TransferToggle).GetField("fastest", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(TransferToggle __instance, ETransferType value)
        {
            // Only intervene when bit 3 is set.
            if (!TorchIt.IsTorchActive(value)) return;
            try
            {
                // Correct radios — vanilla setter sees 8/9 ≠ Fastest(1)
                // and picks Optimal. We correct based on actual base type.
                var baseType = TorchIt.BaseType(value);
                var fastT = _fastestField.GetValue(__instance) as Toggle;
                var optT = _optimalField.GetValue(__instance) as Toggle;
                var isFast = baseType == ETransferType.Fastest;
                fastT.SetIsOnWithoutNotify(isFast);
                optT.SetIsOnWithoutNotify(!isFast);

                // Sync the torch checkbox (if it exists yet).
                var torchT = TorchIt.Checkbox;
                if (torchT == null) return;
                torchT.SetIsOnWithoutNotify(true);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_TransferToggle_SetTransferType_TorchIt: " + ex);
            }
        }
    }

    // ── Refresh torch checkbox visibility on schedule tab ────────────
    // SetData fires when the player navigates to the Schedule tab.
    // Ship or moon-case may have changed since TransferToggle.SetData ran.
    [HarmonyPatch(typeof(ScheduleCycliaclMissionUiElements),
        nameof(ScheduleCycliaclMissionUiElements.SetData),
        new[] { typeof(PlanCyclicalMissionWindow) })]
    public static class Patch_ScheduleElements_SetData_TorchIt
    {
        private static readonly FieldInfo _transferToggleField = typeof(ScheduleCycliaclMissionUiElements).GetField("transferToggle", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ScheduleCycliaclMissionUiElements __instance,
            PlanCyclicalMissionWindow pcmw)
        {
            try
            {
                // Get the TransferToggle from ScheduleCycliaclMissionUiElements.
                var tt = _transferToggleField.GetValue(__instance) as TransferToggle;

                // Delegate to the full sync: visibility, checkbox state, ineligibility revert.
                Patch_TransferToggle_Awake_TorchSetup.SyncFromMissionData(tt, pcmw);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_ScheduleElements_SetData_TorchIt: " + ex);
            }
        }
    }

    // ── Set Burst=false before porkchop so CreateFly uses Bezier ─────
    // PlanFlyCode runs the porkchop then calls CreateFly().
    // Burst=false ensures ConstanceAcceleration && !Burst → Bezier trajectory.
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlanFlyCode))]
    public static class Patch_PlanFlyCode_TorchIt_SetBurst
    {
        static void Prefix(PMMissionParameter missionParameter)
        {
            try
            {
                if (!missionParameter.ForCyclicalMission) return;
                // TryPlanCycleMission creates a fresh PMMissionParameter, so
                // CycleMissionsDataData is null. Read TransferType from the
                // spacecraft's active CycleMissionsData instead.
                var sc = missionParameter.SC as global::CustomUpdate.Spacecraft;
                var tt = sc?.CycleMissionsData?.TransferType ?? 0;
                if (!TorchIt.IsTorchActive((ETransferType)tt)) return;

                missionParameter.SetBurst(false);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_PlanFlyCode_TorchIt_SetBurst: " + ex);
            }
        }
    }

    // ── Fix torch dates in CreateFly, force Bezier trajectory ────────
    // CreateFly() uses DepartureTimeDate + TimeSpanMissionLenght set by
    // the porkchop cursor (potentially centuries away). We override with
    // torch-appropriate dates and force Burst=false to ensure the
    // ConstanceAcceleration && !Burst branch → Bezier trajectory.
    [HarmonyPatch(typeof(PMTabSchedule), "CreateFly")]
    public static class Patch_CreateFly_TorchIt
    {
        static void Prefix(PMTabSchedule __instance)
        {
            try
            {
                var pmp = __instance.PlanMissionWindow?.PMMissionParameter;
                if (pmp == null) return;
                if (!pmp.ForCyclicalMission) return;

                // CycleMissionsDataData is null — same reason as PlanFlyCode above.
                var sc = pmp.SC as global::CustomUpdate.Spacecraft;
                var tt = sc?.CycleMissionsData?.TransferType ?? 0;
                if (!TorchIt.IsTorchActive((ETransferType)tt)) return;

                // Force Burst=false — ensures CreateFly takes the Bezier branch.
                pmp.SetBurst(false);

                // Determine Tmax (Optimal) or Tmin (Fastest) from the bitmask.
                var isFastest = TorchIt.BaseType((ETransferType)tt) == ETransferType.Fastest;

                // Compute dates using the game's slider formula.
                var (dep, tmin, tmax) = TorchIt.ComputeTorchDates(__instance, pmp);
                var travelTime = isFastest ? tmin : tmax;

                // Write the final departure/arrival dates that CreateFly will use.
                pmp.SetTabDateFromPorkchope(dep, dep + travelTime);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_CreateFly_TorchIt: " + ex);
            }
        }
    }

    // ── OnToggleChange Prefix: block when torch is on ────────────────
    // Vanilla OnToggleChange sets transferType = Optimal or Fastest (strips
    // bit 3) and calls ClickOptimal/ClickFastest. When torch is checked,
    // we block the original and handle the radio switch ourselves:
    // OR bit 3 into TransferType, recompute Tmax↔Tmin, update labels.
    // When torch is unchecked, let vanilla OnToggleChange run unchanged.
    [HarmonyPatch(typeof(TransferToggle), "OnToggleChange")]
    public static class Patch_TransferToggle_OnToggleChange_TorchPrefix
    {
        private static readonly FieldInfo _fastField = typeof(TransferToggle).GetField("fastest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _optField = typeof(TransferToggle).GetField("optimal", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _ttField = typeof(TransferToggle).GetField("transferType", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _pcmwField = typeof(TransferToggle).GetField("pcmw", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(TransferToggle __instance)
        {
            try
            {
                // Check if our torch checkbox is on.
                var torchT = TorchIt.Checkbox;
                if (torchT == null || !torchT.isOn) return true; // CA off → vanilla

                // CA on → handle the Optimal↔Fastest switch manually.
                // Read which radio was just selected.
                var fastestOn = _fastField.GetValue(__instance) is Toggle { isOn: true };

                var baseType = fastestOn
                    ? ETransferType.Fastest : ETransferType.Optimal;
                var newType = (ETransferType)((int)baseType | (int)TorchIt.BitTorch);

                // Set transferType with bit 3 OR'd in.
                _ttField.SetValue(__instance, newType);

                // Update the data model.
                var pcmw = _pcmwField.GetValue(__instance) as PlanCyclicalMissionWindow;
                if (pcmw?.CycleMissionsDataData != null)
                    pcmw.CycleMissionsDataData.TransferType = newType;

                // Recompute dates: Tmax for Optimal, Tmin for Fastest.
                Patch_TransferToggle_Awake_TorchSetup.RecomputeTorchDatesAndUpdateLabels(
                    pcmw?.PmTabSchedule, pcmw?.PMMissionParameter, fastestOn);

                return false; // Block original OnToggleChange
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] OnToggleChange TorchPrefix: " + ex);
                return true; // Fall back to vanilla on error
            }
        }
    }

    // ── Block ClickOptimal/ClickFastest when torch is active ─────────
    // Both methods reset the porkchop cursor and recompute burst-mode dates,
    // which would overwrite our torch travel time.
    [HarmonyPatch(typeof(PlanCyclicalMissionWindow), "ClickOptimal")]
    [HarmonyPatch(typeof(PlanCyclicalMissionWindow), "ClickFastest")]
    public static class Patch_BlockClickWhenTorch
    {
        private static readonly FieldInfo _ttField = typeof(TransferToggle).GetField("transferType", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(PlanCyclicalMissionWindow __instance)
        {
            try
            {
                var tt = TorchIt.TransferToggle;
                if (tt == null) return true;

                var currentType = (ETransferType)_ttField.GetValue(tt);
                if (TorchIt.IsTorchActive(currentType))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_BlockClickWhenTorch: " + ex);
                return true;
            }
        }
    }
}
