using System.Collections.Generic;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Game.UI.Windows.Elements.PlanMissionElements;
using Game.UI.Windows.Elements.SearchObjectElements;
using Game.UI.Windows.Windows;
using UnityEngine;

namespace SimpleTweaks
{
    /// <summary>
    /// Re-applies idempotent UI setup after a ScriptEngine reload. Awake/Start postfixes do
    /// not run again for controls that already exist, so without this pass the previous
    /// instance's teardown would permanently remove its buttons and tooltips.
    /// </summary>
    internal static class Reinitialize
    {
        internal static void Run()
        {
            foreach (PMTabDestination tab in SceneObjects<PMTabDestination>())
                Patch_PMTabDestination_DestShortcut.Apply(tab);

            foreach (SearchRow row in SceneObjects<SearchRow>())
                Patch_SearchRow_Start.Apply(row);

            foreach (SpaceCraftConstructionWindow window in SceneObjects<SpaceCraftConstructionWindow>())
                Patch_SpaceCraftConstructionWindow_Awake.Apply(window);

            foreach (ChoseFacilityWindow window in SceneObjects<ChoseFacilityWindow>())
                Patch_ChoseFacilityWindow_Awake.Apply(window);

            foreach (UIRowRocket row in SceneObjects<UIRowRocket>())
                Patch_UIRowRocket_Awake_Tooltip.Apply(row);

            foreach (SpaceCraftInfoWindow window in SceneObjects<SpaceCraftInfoWindow>())
                Patch_SpaceCraftInfoWindow_Awake_Tooltip.Apply(window);
        }

        // Prefabs and other assets also come back from FindObjectsOfTypeAll; only live scene
        // instances can own the runtime UI the plugin augments.
        private static IEnumerable<T> SceneObjects<T>() where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item == null) continue;

                var scene = item.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded) yield return item;
            }
        }
    }
}
