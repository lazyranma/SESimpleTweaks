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
}
