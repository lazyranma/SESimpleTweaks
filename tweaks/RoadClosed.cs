using System.Reflection;
using HarmonyLib;
#pragma warning disable IDE0051

namespace SimpleTweaks
{
    [HarmonyPatch(typeof(MenuSceneUI), "Start")]
    public static class Patch_RoadClosed
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.RoadClosed.Value;

        private static readonly FieldInfo _roadmapWasDisplayed =
            AccessTools.Field(typeof(MenuSceneUI), "roadmapWasDisplayed");
        private static bool _originalCaptured;

        static void Prefix()
        {
            if (!_originalCaptured)
            {
                _originalCaptured = true;
                object original = _roadmapWasDisplayed.GetValue(null);
                Teardown.TrackRestore(() => _roadmapWasDisplayed.SetValue(null, original));
            }
            _roadmapWasDisplayed.SetValue(null, true);
        }
    }
}
