using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SimpleTweaks
{
    [BepInPlugin("com.simpletweaks", "Simple Tweaks", "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("SimpleTweaks loaded.");
            new Harmony("com.simpletweaks").PatchAll();
        }
    }
}
