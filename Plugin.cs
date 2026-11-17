using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SimpleTweaks
{
    [BepInPlugin(PluginGuid, "Simple Tweaks", VersionConstants.PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.lazyranma.simpletweaks";

        internal static ManualLogSource Log;

        private static Harmony HarmonyInstance;

        internal static ConfigEntry<bool> InControl;
        internal static ConfigEntry<bool> MassShift;
        internal static ConfigEntry<bool> GoodTip;
        internal static ConfigEntry<bool> AsteroidTow;
        internal static ConfigEntry<bool> QuickToOrbit;
        internal static ConfigEntry<bool> QuickToOrbitII;
        internal static ConfigEntry<bool> UnstickyCrew;
        internal static ConfigEntry<bool> SpaceBin;
        internal static ConfigEntry<bool> FullCycle;
        internal static ConfigEntry<bool> LeaveNoTrace;
        internal static ConfigEntry<bool> FleetScales;
        internal static ConfigEntry<bool> KeepScanning;
        internal static ConfigEntry<bool> LiftMeOff;
        internal static ConfigEntry<bool> RoadClosed;
        internal static ConfigEntry<bool> DontBlink;
        internal static ConfigEntry<bool> Peekaboo;
        internal static ConfigEntry<KeyboardShortcut> ToggleUIHotkey;

        private const string ConfigSection = "SimpleTweaks";
        private const string HotkeysSection = "Hotkeys";

        private void BindConfig()
        {
            InControl = Config.Bind(ConfigSection, "InControl", true,
                "Ctrl+Click to add/remove/queue 100 at a time "
                + "(module cargo +/-, SC/LV counts, facility builds, SC/LV construction, number fields).");
            MassShift = Config.Bind(ConfigSection, "MassShift", true,
                "Shift+Click to cancel construction of all items of the same type.");
            GoodTip = Config.Bind(ConfigSection, "GoodTip", true,
                "Object Search resource tooltip shows actually useful info.");
            AsteroidTow = Config.Bind(ConfigSection, "AsteroidTow", true,
                "Display Atlas/Engine requirements in Object Search.");
            QuickToOrbit = Config.Bind(ConfigSection, "QuickToOrbit", true,
                "↑/↓ button inside the destination field to select body's orbit or an orbit's body.");
            QuickToOrbitII = Config.Bind(ConfigSection, "QuickToOrbitII", true,
                "Ctrl+Click or Ctrl+Drag on the quick-access body bar to target orbit.");
            UnstickyCrew = Config.Bind(ConfigSection, "UnstickyCrew", true,
                "The crew slider is unlocked for all modules.");
            SpaceBin = Config.Bind(ConfigSection, "SpaceBin", true,
                "Trash bin button in the Object Search list.");
            FullCycle = Config.Bind(ConfigSection, "FullCycle", true,
                "Full info on cyclical mission in the Planet/Orbit view.");
            LeaveNoTrace = Config.Bind(ConfigSection, "LeaveNoTrace", true,
                "Eliminate trace amounts of resources left when applying a build discount.");
            FleetScales = Config.Bind(ConfigSection, "FleetScales", true,
                "Scales the amount of resources added to cargo at once based on the size of the fleet.");
            KeepScanning = Config.Bind(ConfigSection, "KeepScanning", true,
                "Telescopes and observatories keep scanning when there is something to scan for.");
            LiftMeOff = Config.Bind(ConfigSection, "LiftMeOff", true,
                "Lifting cargo from the surface to orbit will not show \"Max capacity for optimal transfer\" = 0 T.");
            RoadClosed = Config.Bind(ConfigSection, "RoadClosed", true,
                "Suppresses the roadmap window on startup.");
            DontBlink = Config.Bind(ConfigSection, "DontBlink", true,
                "Section scrollbars in the Object Info window no longer blink when the window refreshes.");
            Peekaboo = Config.Bind(ConfigSection, "Peekaboo", true,
                "Hide or show the whole UI, version label included, with the key configured in ToggleUIHotkey.");
            ToggleUIHotkey = Config.Bind(HotkeysSection, "ToggleUIHotkey",
                new KeyboardShortcut(KeyCode.ScrollLock),
                "Press this key (or key combination, e.g. ScrollLock + LeftControl) to hide or show "
                + "the whole UI. Set to None to disable the hotkey.");
        }

        private void Awake()
        {
            Log = Logger;

            BindConfig();

            // Solar Expanse destroys BepInEx_Manager at frame 0 unless the object carries
            // HideFlags.DontSave, and the resulting OnDestroy would unpatch Harmony before
            // the first frame. A released install runs with HideManagerGameObject = false,
            // so protect the manager ourselves. A ScriptEngine host already has
            // HideAndDontSave and is left alone.
            if (gameObject == Chainloader.ManagerObject
                && (gameObject.hideFlags & HideFlags.DontSave) == 0)
            {
                gameObject.hideFlags |= HideFlags.DontSave;
            }

            HarmonyInstance = new Harmony(PluginGuid);

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attrs = type.GetCustomAttributes<HarmonyPatch>();
                if (!attrs.Any()) continue;

                try
                {
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Log.LogError($"[SimpleTweaks] Failed to patch {type.FullName}: {ex.Message}");
                    Log.LogDebug($"[SimpleTweaks] {ex}");
                }
            }

            Log.LogInfo($"SimpleTweaks loaded.");

            // ScriptEngine reloads mid-game, when the windows and list rows that Awake/Start
            // already initialized are still alive. Their setup hooks will not run again, so
            // run the idempotent setup here to replace what the previous instance removed.
            try
            {
                Reinitialize.Run();
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleTweaks] Failed to reinitialize existing UI: {ex}");
            }
        }

        /// <summary>
        /// Detaches every hook and undoes the UI changes so ScriptEngine can replace this
        /// instance with a fresh assembly. Harmony identifies patches by MethodInfo, so the
        /// old assembly's patches would otherwise run alongside the new ones and leak into
        /// the game state.
        /// </summary>
        private void OnDestroy()
        {
            Log.LogInfo("SimpleTweaks unloading.");

            try
            {
                HarmonyInstance?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleTweaks] Failed to unpatch Harmony: {ex}");
            }
            finally
            {
                HarmonyInstance = null;
            }

            try
            {
                Teardown.Run();
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleTweaks] Teardown failed: {ex}");
            }
        }
    }
}
