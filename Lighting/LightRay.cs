using System;
using System.Collections.Generic;
using ImmersiveLight.Debugging;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ImmersiveLight.Lighting
{
    internal static class LightRay
    {
        // use game full block light stop line softer blocks still use normal brightness falloff
        private const int FullyBlockingLightAbsorption = 32;
        private const double FaceRayNear = 0.2;
        private const double FaceRayFar = 0.8;

        internal static bool CanSeeSource(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, int chunkSize, int sourceX, int sourceY, int sourceZ, int targetX, int targetY, int targetZ, BlockPos tmpPos)
        {
            double startX = sourceX + 0.5;
            double startY = sourceY + 0.5;
            double startZ = sourceZ + 0.5;

            // center ray first since most blocks do not need the extra checks
            if (CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + 0.5, targetY + 0.5, targetZ + 0.5, tmpPos, LightDebugRayKind.Center))
            {
                return true;
            }

            int dx = targetX - sourceX;
            int dy = targetY - sourceY;
            int dz = targetZ - sourceZ;
            int adx = Math.Abs(dx);
            int ady = Math.Abs(dy);
            int adz = Math.Abs(dz);

            double near = FaceRayNear;
            double far = FaceRayFar;
            double faceX = 0.5;
            double faceY = 0.5;
            double faceZ = 0.5;
            
            // FUTURE ME DO NOT REWORK THIS it is expensive but removing it makes the mod feel grid snapping like first tests
            // center only gets too harsh around doors and thin gaps so try the face looking back at the source aswell (REMEMBER to give this a different color in debugging)
            if (adx >= ady && adx >= adz)
            {
                faceX = dx >= 0 ? near : far;
                return
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + faceX, targetY + near, targetZ + near, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + faceX, targetY + near, targetZ + far, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + faceX, targetY + far, targetZ + near, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + faceX, targetY + far, targetZ + far, tmpPos, LightDebugRayKind.Face);
            }

            if (ady >= adz)
            {
                faceY = dy >= 0 ? near : far;
                return
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + near, targetY + faceY, targetZ + near, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + near, targetY + faceY, targetZ + far, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + far, targetY + faceY, targetZ + near, tmpPos, LightDebugRayKind.Face) ||
                    CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + far, targetY + faceY, targetZ + far, tmpPos, LightDebugRayKind.Face);
            }

            faceZ = dz >= 0 ? near : far;
            return
                CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + near, targetY + near, targetZ + faceZ, tmpPos, LightDebugRayKind.Face) ||
                CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + near, targetY + far, targetZ + faceZ, tmpPos, LightDebugRayKind.Face) ||
                CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + far, targetY + near, targetZ + faceZ, tmpPos, LightDebugRayKind.Face) ||
                CanReachTarget(chunkProvider, blockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, targetX, targetY, targetZ, startX, startY, startZ, targetX + far, targetY + far, targetZ + faceZ, tmpPos, LightDebugRayKind.Face);
        }

        private static bool CanReachTarget(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, int chunkSize, int sourceX, int sourceY, int sourceZ, int targetX, int targetY, int targetZ, double startX, double startY, double startZ, double endX, double endY, double endZ, BlockPos tmpPos, LightDebugRayKind debugKind)
        {
            double deltaX = endX - startX;
            double deltaY = endY - startY;
            double deltaZ = endZ - startZ;

            int x = sourceX;
            int y = sourceY;
            int z = sourceZ;

            int stepX = Math.Sign(deltaX);
            int stepY = Math.Sign(deltaY);
            int stepZ = Math.Sign(deltaZ);

            double tMaxX = FirstVoxelCrossing(startX, deltaX, stepX);
            double tMaxY = FirstVoxelCrossing(startY, deltaY, stepY);
            double tMaxZ = FirstVoxelCrossing(startZ, deltaZ, stepZ);
            double tDeltaX = stepX == 0 ? double.PositiveInfinity : 1 / Math.Abs(deltaX);
            double tDeltaY = stepY == 0 ? double.PositiveInfinity : 1 / Math.Abs(deltaY);
            double tDeltaZ = stepZ == 0 ? double.PositiveInfinity : 1 / Math.Abs(deltaZ);

            while (x != targetX || y != targetY || z != targetZ)
            {
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxZ)
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }

                if (x == targetX && y == targetY && z == targetZ)
                {
                    ImmersiveLightDebug.TraceRay(debugKind, true, startX, startY, startZ, endX, endY, endZ);
                    return true;
                }

                if (BlocksRay(chunkProvider, blockAccess, blockTypes, chunkSize, x, y, z, tmpPos, startX, startY, startZ, endX, endY, endZ))
                {
                    ImmersiveLightDebug.TraceRay(debugKind, false, startX, startY, startZ, x + 0.5, y + 0.5, z + 0.5);
                    return false;
                }
            }

            ImmersiveLightDebug.TraceRay(debugKind, true, startX, startY, startZ, endX, endY, endZ);
            return true;
        }

        private static double FirstVoxelCrossing(double start, double delta, int step)
        {
            if (step == 0)
            {
                return double.PositiveInfinity;
            }

            double nextBoundary = step > 0 ? Math.Floor(start) + 1 : Math.Floor(start);
            return (nextBoundary - start) / delta;
        }

        private static bool BlocksRay(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, int chunkSize, int x, int y, int z, BlockPos tmpPos, double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            if ((x | y | z) < 0)
            {
                return true;
            }

            IWorldChunk chunk = chunkProvider.GetUnpackedChunkFast(x / chunkSize, y / chunkSize, z / chunkSize);
            if (chunk == null)
            {
                return true;
            }

            int index3d = ((y % chunkSize) * chunkSize + z % chunkSize) * chunkSize + x % chunkSize;
            tmpPos.SetAndCorrectDimension(x, y, z);
            Block block = blockTypes[chunk.Data[index3d]];

            // only blocks that fully stop block light should stop the ray
            // softer absorption stays with the normal brightness falloff
            if (chunk.GetLightAbsorptionAt(index3d, tmpPos, blockTypes) > FullyBlockingLightAbsorption)
            {
                return true;
            }

            // doors and trapdoors are the cursed exceptions vanilla keeps them absorption zero but my ray cares about the moved collision box
            return LightDoorBlocker.BlocksRay(blockAccess, block, tmpPos, startX, startY, startZ, endX, endY, endZ) || LightTrapdoorBlocker.BlocksRay(blockAccess, block, tmpPos, startX, startY, startZ, endX, endY, endZ);
        }
    }
}

