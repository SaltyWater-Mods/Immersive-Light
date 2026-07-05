using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(BEBehaviorDoor), nameof(BEBehaviorDoor.ToggleDoorState))]
    internal static class DoorToggleRelightPatch
    {
        private static void Prefix(BEBehaviorDoor __instance, out bool __state)
        {
            __state = __instance.Opened;
        }

        private static void Postfix(BEBehaviorDoor __instance, bool __state)
        {
            // door state lives in the block entity so vanilla sees the same block with the same absorption
            // >> make the relight queue notice when my ray blocker changed or the old light just sits there
            LightDoorBlocker.MarkDoorMoved(__instance, __state, __instance.Opened);
        }
    }

    [HarmonyPatch(typeof(BEBehaviorDoor), nameof(BEBehaviorDoor.OnBlockPlaced))]
    internal static class DoorPlaceRelightPatch
    {
        private static void Postfix(BEBehaviorDoor __instance)
        {
            // kick light here because before this the shape is still guessing
            LightDoorBlocker.MarkDoorPlaced(__instance);
        }
    }

    [HarmonyPatch(typeof(BlockBehaviorDoor), nameof(BlockBehaviorDoor.OnBlockRemoved))]
    internal static class DoorRemoveRelightPatch
    {
        private static void Prefix(IWorldAccessor world, BlockPos pos)
        {
            LightDoorBlocker.MarkDoorRemoved(world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>());
        }
    }
}
