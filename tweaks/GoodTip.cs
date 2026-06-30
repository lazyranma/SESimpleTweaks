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
    [HarmonyPatch(typeof(ToolTipManager), nameof(ToolTipManager.ShowToolTip))]
    public static class Patch_ToolTipManager_ShowToolTip
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.GoodTip.Value;

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
        [HarmonyPrepare]
        static bool Prepare() => Plugin.AsteroidTow.Value || Plugin.SpaceBin.Value;

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

                if (Plugin.SpaceBin.Value)
                    AddTrashButton(oi, moonsTmp);

                if (Plugin.AsteroidTow.Value && oi.PushableAsteroid2)
                    AddTowInfo(oi, moonsTmp);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Patch_SearchRow_Start: " + ex);
            }
        }

        // Space Bin: trash button for asteroids/comets in Object Search.
        private static void AddTrashButton(ObjectInfo oi, TextMeshProUGUI moonsTmp)
        {
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
        }

        // Asteroid Tow: Atlas/Engine requirements readout in Object Search.
        private static void AddTowInfo(ObjectInfo oi, TextMeshProUGUI moonsTmp)
        {
            Company player = MonoBehaviourSingleton<GameManager>.Instance.Player;

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

            var moonsRT = moonsTmp.rectTransform;
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
}
