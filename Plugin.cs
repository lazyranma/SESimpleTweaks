using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

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
        internal static ConfigEntry<bool> KeepScanning;
        internal static ConfigEntry<bool> LiftMeOff;
        internal static ConfigEntry<bool> RoadClosed;
        internal static ConfigEntry<bool> DontTipOut;
        internal static ConfigEntry<bool> DontBlink;

        private const string ConfigSection = "SimpleTweaks";

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
                "Telescopes and observatories will keep scanning for new asteroids and resources.");
            LiftMeOff = Config.Bind(ConfigSection, "LiftMeOff", true,
                "Lifting cargo from the surface to orbit will not show \"Max capacity for optimal transfer\" = 0 T.");
            RoadClosed = Config.Bind(ConfigSection, "RoadClosed", true,
                "Suppresses the roadmap window on startup.");
            DontTipOut = Config.Bind(ConfigSection, "DontTipOut", true,
                "Deposit and stored-resource tooltips no longer disappear when the Object Info window "
                + "refreshes, and their numbers stay live.");
            DontBlink = Config.Bind(ConfigSection, "DontBlink", true,
                "Section scrollbars in the Object Info window no longer blink when the window refreshes.");
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
