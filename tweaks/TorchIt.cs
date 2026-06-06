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
