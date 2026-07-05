using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImmersiveLight.Lighting
{
    internal static class LightDoorBlocker
    {
        internal const int FakeBlockerAbsorption = 33;

        internal static bool BlocksRay(IBlockAccessor blockAccess, Block block, BlockPos pos, double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            if (!TryGetDoor(blockAccess, block, pos, out BEBehaviorDoor door) || !DoorPieceCanBlockRay(door, pos))
            {
                return false;
            }

            return LightRayGeometry.SegmentHitsCollisionBoxes(blockAccess, block, pos, startX, startY, startZ, endX, endY, endZ);
        }

        internal static bool IsFakeBlockerAbsorptionChange(int oldAbsorb, int newAbsorb)
        {
            return oldAbsorb == FakeBlockerAbsorption || newAbsorb == FakeBlockerAbsorption;
        }

        internal static void MarkDoorMoved(BEBehaviorDoor door, bool wasOpened, bool nowOpened)
        {
            if (wasOpened == nowOpened)
            {
                return;
            }

            // always do the direction so old light gets cleaned before the new shape is asked again
            KickDoorAndBuddy(door, 0, FakeBlockerAbsorption);
        }

        internal static void MarkDoorPlaced(BEBehaviorDoor door)
        {
            KickDoorAndBuddy(door, 0, FakeBlockerAbsorption);
        }

        internal static void MarkDoorRemoved(BEBehaviorDoor door)
        {
            KickDoorAndBuddy(door, FakeBlockerAbsorption, 0);
        }

        private static void KickDoorAndBuddy(BEBehaviorDoor door, int oldAbsorption, int newAbsorption)
        {
            if (door?.Api == null)
            {
                return;
            }

            KickDoorPieces(door, oldAbsorption, newAbsorption);

            BEBehaviorDoor leftDoor = door.LeftDoor;
            if (leftDoor != null && !object.ReferenceEquals(leftDoor, door))
            {
                KickDoorPieces(leftDoor, oldAbsorption, newAbsorption);
            }

            BEBehaviorDoor rightDoor = door.RightDoor;
            if (rightDoor != null && !object.ReferenceEquals(rightDoor, door) && !object.ReferenceEquals(rightDoor, leftDoor))
            {
                KickDoorPieces(rightDoor, oldAbsorption, newAbsorption);
            }
        }

        private static void KickDoorPieces(BEBehaviorDoor door, int oldAbsorption, int newAbsorption)
        {
            if (door?.Api == null || door.doorBh == null)
            {
                return;
            }

            door.doorBh.IterateOverEach(door.Pos, door.RotateYRad, door.InvertHandles, pos =>
            {
                if (DoorPieceCanBlockRay(door, pos))
                {
                    // vanilla still thinks the door is absorption zero this fake number is just me poking block light until it admits it moved
                    door.Api.World.BlockAccessor.MarkAbsorptionChanged(oldAbsorption, newAbsorption, pos.Copy());
                }

                return true;
            });
        }

        private static bool DoorPieceCanBlockRay(BEBehaviorDoor door, BlockPos pos)
        {
            if (door.doorBh?.airtight != true)
            {
                return false;
            }

            if (HasUpperWindow(door, pos))
            {
                return false;
            }

            return true;
        }

        private static bool HasUpperWindow(BEBehaviorDoor door, BlockPos pos)
        {
            string style = door.Block.Variant?["style"];
            if (style == null || !style.Contains("windowed"))
            {
                return false;
            }

            // top half of windowed doors needs to let light pass but collision boxes do not know about the glass so theres this exception here
            return pos.InternalY - door.Pos.InternalY > 0;
        }

        private static bool TryGetDoor(IBlockAccessor blockAccess, Block block, BlockPos pos, out BEBehaviorDoor door)
        {
            door = null;

            if (block.GetBehavior<BlockBehaviorDoor>() == null && block is not BlockMultiblock)
            {
                return false;
            }

            door = blockAccess.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            if (door != null)
            {
                return true;
            }

            if (block is not BlockMultiblock multiblock)
            {
                return false;
            }

            BlockPos doorPos = pos.AddCopy(multiblock.OffsetInv);
            door = blockAccess.GetBlockEntity(doorPos)?.GetBehavior<BEBehaviorDoor>();
            return door != null;
        }
    }
}
