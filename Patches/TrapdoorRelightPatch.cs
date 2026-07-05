using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(BEBehaviorTrapDoor), nameof(BEBehaviorTrapDoor.ToggleDoorState))]
    internal static class TrapdoorToggleRelightPatch
    {
        private static void Prefix(BEBehaviorTrapDoor __instance, out bool __state)
        {
            __state = __instance.Opened;
        }

        private static void Postfix(BEBehaviorTrapDoor __instance, bool __state)
        {
            // same old absorption zero trick as doors
            // the hitbox moves but vanilla absorption stays innocent so force block light to look again
            LightTrapdoorBlocker.MarkTrapdoorMoved(__instance, __state, __instance.Opened);
        }
    }

    [HarmonyPatch(typeof(BEBehaviorTrapDoor), nameof(BEBehaviorTrapDoor.OnBlockPlaced))]
    internal static class TrapdoorPlaceRelightPatch
    {
        private static void Postfix(BEBehaviorTrapDoor __instance)
        {
            // by now vanilla rebuilt the closed hitbox so this is the first moment the ray blocker can trust it
            LightTrapdoorBlocker.MarkTrapdoorPlaced(__instance);
        }
    }

    [HarmonyPatch(typeof(Block), nameof(Block.OnBlockRemoved))]
    internal static class TrapdoorRemoveRelightPatch
    {
        private static void Prefix(Block __instance, IWorldAccessor world, BlockPos pos)
        {
            if (__instance.GetBehavior<BlockBehaviorTrapDoor>() == null)
            {
                return;
            }

            // trapdoor behavior does not own a removal override like doors do
            // patching the block here is ugly but at least the filters tiny
            LightTrapdoorBlocker.MarkTrapdoorRemoved(world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>());
        }
    }
}
