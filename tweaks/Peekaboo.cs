using System;
using System.Reflection;
using BepInEx.Configuration;
using Game.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#pragma warning disable IDE0051

namespace SimpleTweaks
{
    /// <summary>
    /// Peekaboo - a configurable hotkey (ScrollLock by default) that hides the whole in-game UI
    /// for a clean view, and hides the version label together with it.
    /// <para>
    /// The vanilla dev-console command <c>HideUI</c> only disables the Canvas on UIManager. The
    /// version label belongs to a DontDestroyOnLoad <see cref="global::Version"/> object that is
    /// re-parented between its own menu and in-game anchors, so it lives outside that Canvas and
    /// stays on screen. This tweak toggles the same Canvas and additionally disables the label's
    /// TextMeshPro component.
    /// </para>
    /// <para>
    /// Input is polled from a prefix on UIManager.Update instead of a plugin Update() because
    /// plugin update callbacks do not run reliably in this game (see docs/BEPINEX_NOTES.md).
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UIManager), "Update")]
    public static class Patch_UIManager_Update_Peekaboo
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            if (!Plugin.Peekaboo.Value) return false;
            Peekaboo.RegisterTeardown();
            return true;
        }

        static void Prefix(UIManager __instance)
        {
            KeyboardShortcut shortcut = Plugin.ToggleUIHotkey.Value;
            KeyCode key = shortcut.MainKey;
            if (key == KeyCode.None || !Input.GetKeyDown(key)) return;

            // Check configured modifiers by hand instead of calling shortcut.IsDown(): that
            // also rejects the press when any other key is held, which would break the
            // toggle while the player is panning the camera with W/A/S/D.
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier)) return;
            }

            try
            {
                // Typing in Object Search, a save-name prompt or the dev console must not
                // hide the UI mid-entry. Only hiding is skipped; an already-hidden UI can
                // still be shown with the same key.
                if (Peekaboo.IsUiVisible(__instance) && Peekaboo.IsTextFieldFocused()) return;

                Peekaboo.Toggle(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Peekaboo toggle failed: " + ex);
            }
        }
    }

    // The vanilla console command has the same blind spot, so keep the version label in sync
    // when the UI is toggled from the dev console instead of the hotkey.
    [HarmonyPatch(typeof(DevConsoleCommands), "HideUI")]
    public static class Patch_DevConsoleCommands_HideUI_Peekaboo
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            if (!Plugin.Peekaboo.Value) return false;
            Peekaboo.RegisterTeardown();
            return true;
        }

        static void Postfix(bool value)
        {
            try
            {
                Peekaboo.OnConsoleHideUI(value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Peekaboo version sync failed: " + ex);
            }
        }
    }

    // The version object survives scene changes. When the game moves the label back to its menu
    // anchor, show it again, otherwise hiding the UI in game would leave it missing from the
    // main menu too.
    // Note: the game's Version type is in the global namespace, so it needs global:: here -
    // plain Version would bind to System.Version because of the using directive above.
    [HarmonyPatch(typeof(global::Version), "ChangePosition")]
    public static class Patch_Version_ChangePosition_Peekaboo
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            if (!Plugin.Peekaboo.Value) return false;
            Peekaboo.RegisterTeardown();
            return true;
        }

        static void Postfix(bool inGame)
        {
            try
            {
                Peekaboo.ChangeVersionVisibility(!inGame || !Peekaboo.IsUiHidden);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Peekaboo version sync failed: " + ex);
            }
        }
    }

    internal static class Peekaboo
    {
        private static readonly FieldInfo VersionTextField =
            AccessTools.Field(typeof(global::Version), "text")
            ?? throw new MissingFieldException(typeof(global::Version).FullName, "text");

        private static bool _teardownRegistered;

        // Tracks only what Peekaboo itself changed. The dev console can set the canvas
        // visibility too, and its state must survive a ScriptEngine reload untouched.
        private static bool _ownsCanvasState;
        private static Canvas _toggledCanvas;
        private static bool _visibleBeforePeekaboo;
        private static bool _visibleAfterPeekaboo;

        // The label is a DontDestroyOnLoad singleton outside the UIManager canvas, so its
        // change is tracked separately and undone on unload even when the canvas is not ours.
        private static bool _labelChanged;
        private static bool _labelVisibleBeforeChange;

        internal static void RegisterTeardown()
        {
            if (_teardownRegistered) return;
            _teardownRegistered = true;
            Teardown.TrackRestore(Restore);
        }

        internal static void Toggle(UIManager ui)
        {
            Canvas canvas = ui.GetComponent<Canvas>()
                ?? throw new MissingComponentException("UIManager has no Canvas component");

            // Remember what to go back to on unload. Repeated toggles on the same canvas keep
            // the baseline from the first change, so hide -> show -> hide still restores the
            // pre-tweak state. A different canvas (another game after a return to the menu)
            // starts a fresh baseline.
            if (!_ownsCanvasState || _toggledCanvas != canvas)
            {
                _toggledCanvas = canvas;
                _visibleBeforePeekaboo = canvas.enabled;
                _ownsCanvasState = true;
            }

            // Derive the new state from the live Canvas so a toggle after the console command
            // (or after a reload) always brings both the UI and the label to the same state.
            bool show = !canvas.enabled;
            canvas.enabled = show;
            _visibleAfterPeekaboo = show;
            ChangeVersionVisibility(show);
        }

        /// <summary>
        /// The dev console is authoritative for the canvas: whatever it set is now the
        /// baseline, and Peekaboo no longer owns the canvas state, so a later unload leaves
        /// the canvas alone. The label is still Peekaboo's change and is restored on unload.
        /// </summary>
        internal static void OnConsoleHideUI(bool hidden)
        {
            _ownsCanvasState = false;
            ChangeVersionVisibility(!hidden);
        }

        /// <summary>
        /// Applies a label visibility change made by the plugin. The state before the first
        /// such change is remembered for teardown.
        /// </summary>
        internal static void ChangeVersionVisibility(bool visible)
        {
            global::Version version = UnityEngine.Object.FindObjectOfType<global::Version>();
            if (version == null) return;

            var text = (TextMeshProUGUI)VersionTextField.GetValue(version);
            if (text == null || text.enabled == visible) return;

            if (!_labelChanged)
            {
                _labelVisibleBeforeChange = text.enabled;
                _labelChanged = true;
            }

            text.enabled = visible;
        }

        internal static void SetVersionVisible(bool visible)
        {
            global::Version version = UnityEngine.Object.FindObjectOfType<global::Version>();
            if (version == null) return;

            var text = (TextMeshProUGUI)VersionTextField.GetValue(version);
            if (text != null && text.enabled != visible) text.enabled = visible;
        }

        internal static bool IsUiVisible(UIManager ui)
        {
            Canvas canvas = ui.GetComponent<Canvas>();
            return canvas != null && canvas.enabled;
        }

        /// <summary>
        /// True while the player is typing. uGUI and TMP input fields (Object Search, save
        /// names, the F8 error reporter, GEConsole) are found through the EventSystem
        /// selection; the Opencoding developer console uses an IMGUI text field, so it never
        /// becomes the selection and is detected by the console being open.
        /// </summary>
        internal static bool IsTextFieldFocused()
        {
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                GameObject selected = current.currentSelectedGameObject;
                if (selected != null && selected.activeInHierarchy
                    && (selected.GetComponent<TMP_InputField>() != null
                        || selected.GetComponent<UnityEngine.UI.InputField>() != null))
                {
                    return true;
                }
            }

            // GUIUtility.keyboardControl cannot be used here: it can stay set after the
            // console closes, which would leave the hotkey dead for the rest of the session.
            return Opencoding.Console.DebugConsole.IsVisible;
        }

        internal static bool IsUiHidden
        {
            get
            {
                UIManager ui = SerializedMonoBehaviourSingleton<UIManager>.Instance;
                if (ui == null) return false;

                Canvas canvas = ui.GetComponent<Canvas>();
                return canvas != null && !canvas.enabled;
            }
        }

        private static void Restore()
        {
            try
            {
                UIManager ui = SerializedMonoBehaviourSingleton<UIManager>.Instance;
                Canvas canvas = ui != null ? ui.GetComponent<Canvas>() : null;

                // Only undo a canvas change Peekaboo made, on the same canvas instance, and
                // only if no one else wrote it since. A different canvas means another game
                // is loaded, and the console state set there must survive.
                if (canvas != null && _ownsCanvasState
                    && canvas == _toggledCanvas
                    && canvas.enabled == _visibleAfterPeekaboo)
                {
                    canvas.enabled = _visibleBeforePeekaboo;
                }

                // The label is restored on its own, so unloading preserves a canvas state
                // set by the console while still bringing the label back.
                if (_labelChanged)
                {
                    SetVersionVisible(_labelVisibleBeforeChange);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[SimpleTweaks] Peekaboo teardown failed: " + ex);
            }
        }
    }
}
