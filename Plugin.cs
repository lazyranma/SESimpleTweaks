using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SimpleTweaks
{
    [BepInPlugin("com.simpletweaks", "Simple Tweaks", VersionConstants.PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> InControl;
        internal static ConfigEntry<bool> MassShift;
        internal static ConfigEntry<bool> MassShiftII;
        internal static ConfigEntry<bool> GoodTip;
        internal static ConfigEntry<bool> QuickToOrbit;
        internal static ConfigEntry<bool> QuickToOrbitII;
        internal static ConfigEntry<bool> UnstickyCrew;
        internal static ConfigEntry<bool> FullCycle;
        internal static ConfigEntry<bool> LeaveNoTrace;
        internal static ConfigEntry<bool> FleetScales;
        internal static ConfigEntry<bool> MassEffect;
        internal static ConfigEntry<bool> KeepScanning;
        internal static ConfigEntry<bool> LiftMeOff;
        internal static ConfigEntry<bool> RapidScheduledDisassembly;
        internal static ConfigEntry<bool> TorchCycle;
        internal static ConfigEntry<bool> RoadClosed;

        private void BindConfig()
        {
            InControl = Config.Bind("In Control", "Enabled", true,
                "Ctrl+Click to add/remove/queue 100 at a time. (restart to apply)");
            MassShift = Config.Bind("Mass Shift", "Enabled", true,
                "Shift+Click to cancel construction of all items of the same type. (restart to apply)");
            MassShiftII = Config.Bind("Mass Shift II", "Enabled", true,
                "Allows removing 10/100 modules at once from cargo in mission planner. (restart to apply)");
            GoodTip = Config.Bind("Good Tip", "Enabled", true,
                "Object Search resource tooltip shows actually useful info; adds Asteroid Tow info and the Space Bin trash button. (restart to apply)");
            QuickToOrbit = Config.Bind("Quick to Orbit", "Enabled", true,
                "Up/Down button inside the destination field to select body's orbit or an orbit's body. (restart to apply)");
            QuickToOrbitII = Config.Bind("Quick to Orbit II", "Enabled", true,
                "Ctrl+Click or Ctrl+Drag on the quick-access body bar to target orbit. (restart to apply)");
            UnstickyCrew = Config.Bind("Unsticky Crew", "Enabled", true,
                "The crew slider is unlocked for all modules. (restart to apply)");
            FullCycle = Config.Bind("Full Cycle", "Enabled", true,
                "Full info on cyclical mission in the Planet/Orbit view. (restart to apply)");
            LeaveNoTrace = Config.Bind("Leave No Trace", "Enabled", true,
                "Eliminate trace amounts of resources left when applying a build discount. (restart to apply)");
            FleetScales = Config.Bind("Fleet Scales", "Enabled", true,
                "Scales the amount of resources added to cargo at once based on the size of the fleet. (restart to apply)");
            MassEffect = Config.Bind("Mass Effect", "Enabled", true,
                "Removes negative-mass solid-phase fractions. (restart to apply)");
            KeepScanning = Config.Bind("Keep Scanning", "Enabled", true,
                "Telescopes and observatories will keep scanning for new asteroids and resources. (restart to apply)");
            LiftMeOff = Config.Bind("Lift Me Off", "Enabled", true,
                "Lifting cargo from the surface to orbit will not show \"Max capacity for optimal transfer\" = 0 T. (restart to apply)");
            RapidScheduledDisassembly = Config.Bind("Rapid Scheduled Disassembly", "Enabled", true,
                "Scrap multiple identical spacecraft or launch vehicles at once. (restart to apply)");
            TorchCycle = Config.Bind("Torch Cycle", "Enabled", true,
                "Enable constant-acceleration (torch) mode for cyclical missions. (restart to apply)");
            RoadClosed = Config.Bind("Road Closed", "Enabled", true,
                "Suppresses the roadmap window on startup. (restart to apply)");
        }

        private void Awake()
        {
            Log = Logger;

            BindConfig();

            var harmony = new Harmony("com.simpletweaks");

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

            Log.LogInfo("SimpleTweaks loaded.");
        }
    }
}
