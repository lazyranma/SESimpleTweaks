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
    [HarmonyPatch(typeof(SpaceCraftInfoWindow), "OnScrapButtonClick")]
    public static class Patch_SpaceCraftInfoWindow_ScrapMulti
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.RapidScheduledDisassembly.Value;

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
}
