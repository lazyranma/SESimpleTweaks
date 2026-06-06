using System.Reflection;
using HarmonyLib;
#pragma warning disable IDE0051

namespace SimpleTweaks
{
    [HarmonyPatch(typeof(MenuSceneUI), "Start")]
    public static class Patch_RoadClosed
    {
        private static readonly FieldInfo _roadmapWasDisplayed =
            AccessTools.Field(typeof(MenuSceneUI), "roadmapWasDisplayed");

        static void Prefix()
        {
            _roadmapWasDisplayed.SetValue(null, true);
        }
    }
}
