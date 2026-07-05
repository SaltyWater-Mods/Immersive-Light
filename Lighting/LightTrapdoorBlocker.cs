using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveLight.Lighting
{
    internal static class LightTrapdoorBlocker
    {
        internal static bool BlocksRay(IBlockAccessor blockAccess, Block block, BlockPos pos, double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            BlockBehaviorTrapDoor trapdoorBh = block.GetBehavior<BlockBehaviorTrapDoor>();
            if (trapdoorBh == null)
            {
                return false;
            }

            BEBehaviorTrapDoor trapdoor = blockAccess.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>();
            if (trapdoor == null || !TrapdoorCanBlockRay(trapdoor, trapdoorBh))
            {
                return false;
            }

            return LightRayGeometry.SegmentHitsCollisionBoxes(blockAccess, block, pos, startX, startY, startZ, endX, endY, endZ);
        }

        internal static void MarkTrapdoorMoved(BEBehaviorTrapDoor trapdoor, bool wasOpened, bool nowOpened)
        {
            if (wasOpened == nowOpened)
            {
                return;
            }

            // same reason as doors moved hitbox means clean the old light first then let the new scene settle
            KickTrapdoor(trapdoor, 0, LightDoorBlocker.FakeBlockerAbsorption);
        }

        internal static void MarkTrapdoorPlaced(BEBehaviorTrapDoor trapdoor)
        {
            KickTrapdoor(trapdoor, 0, LightDoorBlocker.FakeBlockerAbsorption);
        }

        internal static void MarkTrapdoorRemoved(BEBehaviorTrapDoor trapdoor)
        {
            KickTrapdoor(trapdoor, LightDoorBlocker.FakeBlockerAbsorption, 0);
        }

        private static void KickTrapdoor(BEBehaviorTrapDoor trapdoor, int oldAbsorption, int newAbsorption)
        {
            if (trapdoor?.Api == null || !TrapdoorCanBlockRay(trapdoor, trapdoor.Blockentity.Block.GetBehavior<BlockBehaviorTrapDoor>()))
            {
                return;
            }

            // trapdoors get the same fake blocker kick not because their block changed but because the blocker shape did
            trapdoor.Api.World.BlockAccessor.MarkAbsorptionChanged(oldAbsorption, newAbsorption, trapdoor.Pos.Copy());
        }

        private static bool TrapdoorCanBlockRay(BEBehaviorTrapDoor trapdoor, BlockBehaviorTrapDoor trapdoorBh)
        {
            return trapdoorBh?.airtight == true;
        }
    }
}
