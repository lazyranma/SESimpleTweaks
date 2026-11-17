using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SimpleTweaks
{
    [BepInPlugin("com.lazyranma.simpletweaks", "Simple Tweaks", VersionConstants.PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

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
        internal static ConfigEntry<bool> OriginsSchmorigins;
        internal static ConfigEntry<bool> KeepScanning;
        internal static ConfigEntry<bool> LiftMeOff;
        internal static ConfigEntry<bool> RoadClosed;
        internal static ConfigEntry<bool> DontTipOut;
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
            OriginsSchmorigins = Config.Bind(ConfigSection, "OriginsSchmorigins", true,
                "Drag a resource from any body into the mission planner. The mission origin still supplies the cargo.");
            KeepScanning = Config.Bind(ConfigSection, "KeepScanning", true,
                "Telescopes and observatories keep scanning when there is something to scan for.");
            LiftMeOff = Config.Bind(ConfigSection, "LiftMeOff", true,
                "Lifting cargo from the surface to orbit will not show \"Max capacity for optimal transfer\" = 0 T.");
            RoadClosed = Config.Bind(ConfigSection, "RoadClosed", true,
                "Suppresses the roadmap window on startup.");
            DontTipOut = Config.Bind(ConfigSection, "DontTipOut", true,
                "Deposit and stored-resource tooltips no longer disappear when the Object Info window "
                + "refreshes, and their numbers stay live.");
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

            var harmony = new Harmony("com.lazyranma.simpletweaks");

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attrs = type.GetCustomAttributes<HarmonyPatch>();
                if (!attrs.Any()) continue;

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Log.LogError($"[SimpleTweaks] Failed to patch {type.FullName}: {ex.Message}");
                    Log.LogDebug($"[SimpleTweaks] {ex}");
                }
            }

            Log.LogInfo($"SimpleTweaks loaded.");
        }
    }
}
