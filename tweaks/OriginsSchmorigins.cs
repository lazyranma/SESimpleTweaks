using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Extensions;
using Game.ObjectInfoDataScripts;
using Game.UI.DragAndDropSystem;
using Game.UI.Windows.Elements.PlanMissionElements;
using HarmonyLib;
using ScriptableObjectScripts;

namespace SimpleTweaks
{
    [HarmonyPatch(typeof(CargoDropTarget), nameof(CargoDropTarget.OnDragAndDrop))]
    internal static class Patch_OriginsSchmorigins
    {
        internal static readonly ConditionalWeakTable<Cargo, ResourceDefinition> DraggedResources = new();

        [HarmonyPrepare] static bool Prepare() => Plugin.OriginsSchmorigins.Value;

        static bool Prefix(DragAndDropTransactItem item, PMTabCargo ___tabCargo, ref bool __result)
        {
            if (___tabCargo == null
                || !(item is DragAndDropScriptableObjectTransactItem drag)
                || !(drag.item is ResourceDefinition resource)
                || resource.ResourceType != ResourceDefinition.EResourceType.Normal)
                return true;

            // CanDrag also checks the active Add Resource button. Honor that part
            // directly so a disabled button doesn't produce a wrong-origin popup.
            var list = ___tabCargo.ResourcesList;
            var cargos = list.Cargos;
            if (cargos == null)
            {
                __result = false;
                return false;
            }
            bool toOrbit = list.ShowAddResourceToOrbit();
            // Vanilla AddResourceOnOrbit reads interactable from the hidden regular
            // button, which can be disabled even when surface cargo is available.
            __result = toOrbit
                ? list.addCargoToOrbit.gameObject.activeSelf && list.addCargoToOrbit.interactable
                : list.AddResourceOn();
            if (__result)
            {
                // The list already resolves Cargo A/B and hypothetical mission sources.
                var source = toOrbit ? list.ObjectInfo.GetObjectInfo().parentObjectInfo : list.ObjectInfo;
                var mission = ___tabCargo.PlanMissionWindow.PMMissionParameter;
                var inventory = source.GetObjectInfo().GetObjectInfoData(mission.FlyCompany);
                var capacity = (int)mission.SC.GetTypeSpaceCraft().GetCargoCapacity(mission.SC.GetCompany());
                if (!toOrbit || Plugin.FleetScales.Value)
                    capacity *= mission.SCCount;
                if (!toOrbit && list.LoadLimit2.HasValue)
                    capacity = (int)Math.Min(capacity, list.LoadLimit2.Value);

                double stock = cargos.hypothetical
                    ? new ObjectInfoFake(inventory).CheckResourcesInterface(resource)
                    : inventory.CheckResources(resource);
                if (!toOrbit)
                    stock = stock.IsNearOne() ? 1 : Math.Floor(stock);
                var cargo = new Cargo(cargos)
                {
                    objectInfo = source,
                    OrbitCargo = toOrbit,
                    resourceTypeType = EResourceTypeType.resorces,
                    resourceType = resource,
                    cargoMass = Math.Max(0, Math.Min(stock, capacity - cargos.CargoCurrent))
                };
                DraggedResources.Add(cargo, resource);
                (toOrbit ? cargos.listCargoToOrbit : cargos.listCargo).Add(cargo);
                inventory.ChangeCargoToMaxOnPlanet(cargos, cargo);
                ___tabCargo.SetDataResourcesList();
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ResorceRow), nameof(ResorceRow.SetData))]
    internal static class Patch_OriginsSchmorigins_Row
    {
        internal static readonly ConditionalWeakTable<DropDownEnum, ResourceDefinition> ExtraOptions = new();

        [HarmonyPrepare] static bool Prepare() => Plugin.OriginsSchmorigins.Value;

        static void Prefix(Cargo _cargo, DropDownEnum ___resorceDropDown)
        {
            ExtraOptions.Remove(___resorceDropDown);
            if (Patch_OriginsSchmorigins.DraggedResources.TryGetValue(_cargo, out var dragged))
                ExtraOptions.Add(___resorceDropDown, dragged);
        }
    }

    [HarmonyPatch(typeof(DropDownEnum), nameof(DropDownEnum.SetOptionsAwake))]
    internal static class Patch_OriginsSchmorigins_Options
    {
        [HarmonyPrepare] static bool Prepare() => Plugin.OriginsSchmorigins.Value;

        // Both row initialization and stock-change notifications build the visible
        // options here, then vanilla reselects the cargo's current resource.
        static void Prefix(DropDownEnum __instance, List<ResourceDefinition> ___listRDonlyExitsResources)
        {
            if (___listRDonlyExitsResources != null
                && Patch_OriginsSchmorigins_Row.ExtraOptions.TryGetValue(__instance, out var dragged)
                && !___listRDonlyExitsResources.Contains(dragged))
                ___listRDonlyExitsResources.Add(dragged);
        }
    }
}
