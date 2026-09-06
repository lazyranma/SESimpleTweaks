using System;
using System.Reflection;
using Game.UI.Windows.Elements.PlanMissionElements;
using HarmonyLib;

#pragma warning disable IDE0051

namespace SimpleTweaks
{
    [HarmonyPatch(typeof(CycleMissionsData), MethodType.Constructor,
        new[] { typeof(CycleMissionManager.PMMissionParameterCyclicalDataSave) })]
    internal static class Patch_CycleMissionsData_MigrateLegacyTorchCycle
    {
        private const int LegacyTorchCycleBit = 8;

        [HarmonyPrepare]
        static bool Prepare() => Plugin.IsBetaGame;

        static void Prefix(CycleMissionManager.PMMissionParameterCyclicalDataSave data)
        {
            int transferType = (int)data.TransferType;
            if ((transferType & LegacyTorchCycleBit) == 0)
                return;

            data.TransferType = (ETransferType)(transferType & 3);

            FieldInfo typeAccelerationField = data.GetType().GetField(
                "TypeAcceleration", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingFieldException(data.GetType().FullName, "TypeAcceleration");
            int typeAcceleration = data.TransferType == ETransferType.Fastest ? 1 : 2;
            typeAccelerationField.SetValue(
                data, Enum.ToObject(typeAccelerationField.FieldType, typeAcceleration));
        }
    }
}
