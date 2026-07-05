using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ImmersiveLight.Lighting
{
    internal static class LightRayGeometry
    {
        private const double TinyRayNudge = 0.0000001;

        internal static bool SegmentHitsCollisionBoxes(IBlockAccessor blockAccess, Block block, BlockPos pos, double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            Cuboidf[] boxes = block.GetCollisionBoxes(blockAccess, pos);
            if (boxes == null)
            {
                return false;
            }

            double dx = endX - startX;
            double dy = endY - startY;
            double dz = endZ - startZ;

            for (int i = 0; i < boxes.Length; i++)
            {
                if (SegmentHitsBox(boxes[i], pos, startX, startY, startZ, dx, dy, dz))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentHitsBox(Cuboidf box, BlockPos pos, double startX, double startY, double startZ, double dx, double dy, double dz)
        {
            double tMin = 0;
            double tMax = 1;

            // doors and trapdoors already move their collision boxes around for vanilla use that instead of the open/closed state for a more reliable result
            return
                ClipAxis(startX, dx, pos.X + box.MinX, pos.X + box.MaxX, ref tMin, ref tMax) &&
                ClipAxis(startY, dy, pos.InternalY + box.MinY, pos.InternalY + box.MaxY, ref tMin, ref tMax) &&
                ClipAxis(startZ, dz, pos.Z + box.MinZ, pos.Z + box.MaxZ, ref tMin, ref tMax);
        }

        private static bool ClipAxis(double start, double delta, double min, double max, ref double tMin, ref double tMax)
        {
            if (Math.Abs(delta) < TinyRayNudge)
            {
                return start >= min && start <= max;
            }

            double t1 = (min - start) / delta;
            double t2 = (max - start) / delta;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            if (t1 > tMin)
            {
                tMin = t1;
            }

            if (t2 < tMax)
            {
                tMax = t2;
            }

            return tMin <= tMax && tMax >= 0 && tMin <= 1;
        }
    }
}
