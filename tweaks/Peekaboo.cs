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
        [HarmonyPrepare] static bool Prepare() => Plugin.Peekaboo.Value;

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
        [HarmonyPrepare] static bool Prepare() => Plugin.Peekaboo.Value;

        static void Postfix(bool value)
        {
            try
            {
                Peekaboo.SetVersionVisible(!value);
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
        [HarmonyPrepare] static bool Prepare() => Plugin.Peekaboo.Value;

        static void Postfix(bool inGame)
        {
            try
            {
                Peekaboo.SetVersionVisible(!inGame || !Peekaboo.IsUiHidden);
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

        internal static void Toggle(UIManager ui)
        {
            Canvas canvas = ui.GetComponent<Canvas>()
                ?? throw new MissingComponentException("UIManager has no Canvas component");

            // Derive the new state from the live Canvas so a toggle after the console command
            // always brings both the UI and the label to the same state.
            bool show = !canvas.enabled;
            canvas.enabled = show;
            SetVersionVisible(show);
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
    }
}
