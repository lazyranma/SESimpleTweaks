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
}
