using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ImmersiveLight.Lighting
{
    internal static class SunlightRay
    {
        [ThreadStatic]
        private static Dictionary<long, bool> visibilityCache;

        [ThreadStatic]
        private static Dictionary<long, Vec3f> directionCache;

        [ThreadStatic]
        private static Dictionary<long, int> terrainHeightCache;

        [ThreadStatic]
        private static IList<Block> cachedBlockTypes;

        [ThreadStatic]
        private static LightBlockerKind[] cachedBlockerKinds;

        [ThreadStatic]
        private static bool passActive;

        [ThreadStatic]
        private static bool hasPassPhase;

        [ThreadStatic]
        private static double passPhaseDays;

        // block updates call BeginPass() too they must not get the hourly cave cutoff
        [ThreadStatic]
        private static bool scheduledRelight;

        private const int FullyBlockingLightAbsorption = 32;
        private const double FaceRayNear = 0.2;
        private const double FaceRayFar = 0.8;

        internal static void BeginPass(double? phaseDays = null)
        {
            visibilityCache ??= new Dictionary<long, bool>();
            directionCache ??= new Dictionary<long, Vec3f>();
            terrainHeightCache ??= new Dictionary<long, int>();
            visibilityCache.Clear();
            directionCache.Clear();
            terrainHeightCache.Clear();
            passActive = true;
            hasPassPhase = phaseDays.HasValue;
            passPhaseDays = phaseDays.GetValueOrDefault();
            scheduledRelight = phaseDays.HasValue;
        }

        internal static void EndPass()
        {
            passActive = false;
            hasPassPhase = false;
            scheduledRelight = false;
        }

        internal static bool IsScheduledRelight => scheduledRelight;

        internal static int GetLowestSurfaceChunk(IMapChunk mapChunk, int chunkSize)
        {
            ushort[] heights = mapChunk?.WorldGenTerrainHeightMap;
            if (heights == null)
            {
                return 0;
            }

            int lowest = heights[0];
            for (int i = 1; i < heights.Length; i++)
            {
                lowest = Math.Min(lowest, heights[i]);
            }

            return lowest / chunkSize;
        }

        internal static bool UsesDirectionalSunlight(IBlockAccessor blockAccess, int chunkSize, int x, int y, int z)
        {
            if (x < 0 || z < 0 || x >= blockAccess.MapSizeX || z >= blockAccess.MapSizeZ)
            {
                return false;
            }

            long key = MapUtil.Index2dL(x, z, blockAccess.MapSizeX);
            if (!passActive || !terrainHeightCache.TryGetValue(key, out int terrainHeight))
            {
                ushort[] heights = blockAccess.GetMapChunk(x / chunkSize, z / chunkSize)?.WorldGenTerrainHeightMap;
                if (heights == null)
                {
                    // no heightmap, let vanilla handle it
                    return false;
                }

                terrainHeight = heights[z % chunkSize * chunkSize + x % chunkSize];
                if (passActive)
                {
                    terrainHeightCache[key] = terrainHeight;
                }
            }

            return y >= terrainHeight;
        }

        internal static bool CanSeeSun(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, int chunkSize, int x, int y, int z, BlockPos tmpPos)
        {
            long key = MapUtil.Index3dL(x, y, z, blockAccess.MapSizeX, blockAccess.MapSizeZ);
            if (passActive && visibilityCache.TryGetValue(key, out bool cached))
            {
                return cached;
            }

            bool result = CanSeeSunUncached(chunkProvider, blockAccess, blockTypes, chunkSize, x, y, z, tmpPos);
            if (passActive)
            {
                visibilityCache[key] = result;
            }

            return result;
        }

        private static bool CanSeeSunUncached(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, int chunkSize, int x, int y, int z, BlockPos tmpPos)
        {
            Vec3f sun = GetSunDirection(blockAccess, x, y, z);
            if (sun == null)
            {
                return false;
            }
            if (sun.Y <= 0)
            {
                return false;
            }

            LightBlockerKind[] blockerKinds = GetBlockerKinds(blockTypes);

            if (CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + 0.5, y + 0.5, z + 0.5, sun, tmpPos))
            {
                return true;
            }

            double near = FaceRayNear;
            double far = FaceRayFar;
            float absX = Math.Abs(sun.X);
            float absY = Math.Abs(sun.Y);
            float absZ = Math.Abs(sun.Z);

            // center only gets too harsh around windows so try face looking at the sun
            if (absX >= absY && absX >= absZ)
            {
                double faceX = sun.X >= 0 ? far : near;
                return
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + faceX, y + near, z + near, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + faceX, y + near, z + far, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + faceX, y + far, z + near, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + faceX, y + far, z + far, sun, tmpPos);
            }

            if (absY >= absZ)
            {
                double faceY = sun.Y >= 0 ? far : near;
                return
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + near, y + faceY, z + near, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + near, y + faceY, z + far, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + far, y + faceY, z + near, sun, tmpPos) ||
                    CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + far, y + faceY, z + far, sun, tmpPos);
            }

            double faceZ = sun.Z >= 0 ? far : near;
            return
                CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + near, y + near, z + faceZ, sun, tmpPos) ||
                CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + near, y + far, z + faceZ, sun, tmpPos) ||
                CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + far, y + near, z + faceZ, sun, tmpPos) ||
                CanReachSky(chunkProvider, blockAccess, blockTypes, blockerKinds, chunkSize, x + far, y + far, z + faceZ, sun, tmpPos);
        }

        private static Vec3f GetSunDirection(IBlockAccessor blockAccess, int x, int y, int z)
        {
            if (!passActive)
            {
                return DirectionalSunlight.GetSunDirection(blockAccess, x, y, z);
            }

            if (!hasPassPhase)
            {
                if (!DirectionalSunlight.TryGetCurrentPhaseDays(blockAccess, out passPhaseDays))
                {
                    return null;
                }

                hasPassPhase = true;
            }

            // y does nothing for the calendar here so dont calculate the same angle for every block above it
            long key = MapUtil.Index2dL(x, z, blockAccess.MapSizeX);
            if (!directionCache.TryGetValue(key, out Vec3f sun))
            {
                sun = DirectionalSunlight.GetSunDirection(blockAccess, x, y, z, passPhaseDays);
                directionCache[key] = sun;
            }

            return sun;
        }

        private static LightBlockerKind[] GetBlockerKinds(IList<Block> blockTypes)
        {
            if (!ReferenceEquals(cachedBlockTypes, blockTypes))
            {
                cachedBlockTypes = blockTypes;
                cachedBlockerKinds = LightBlockerCache.Get(blockTypes);
            }

            return cachedBlockerKinds;
        }

        private static bool CanReachSky(IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes, LightBlockerKind[] blockerKinds, int chunkSize, double startX, double startY, double startZ, Vec3f sun, BlockPos tmpPos)
        {
            int cellX = (int)Math.Floor(startX);
            int cellY = (int)Math.Floor(startY);
            int cellZ = (int)Math.Floor(startZ);
            int stepX = Math.Sign(sun.X);
            int stepY = Math.Sign(sun.Y);
            int stepZ = Math.Sign(sun.Z);
            double tMaxX = FirstVoxelCrossing(startX, sun.X, stepX);
            double tMaxY = FirstVoxelCrossing(startY, sun.Y, stepY);
            double tMaxZ = FirstVoxelCrossing(startZ, sun.Z, stepZ);
            double tDeltaX = stepX == 0 ? double.PositiveInfinity : 1 / Math.Abs(sun.X);
            double tDeltaY = stepY == 0 ? double.PositiveInfinity : 1 / Math.Abs(sun.Y);
            double tDeltaZ = stepZ == 0 ? double.PositiveInfinity : 1 / Math.Abs(sun.Z);
            double rayLength = (blockAccess.MapSizeY - startY) / sun.Y;
            if (sun.X > 0)
            {
                rayLength = Math.Min(rayLength, (blockAccess.MapSizeX - startX) / sun.X);
            }
            else if (sun.X < 0)
            {
                rayLength = Math.Min(rayLength, -startX / sun.X);
            }

            if (sun.Z > 0)
            {
                rayLength = Math.Min(rayLength, (blockAccess.MapSizeZ - startZ) / sun.Z);
            }
            else if (sun.Z < 0)
            {
                rayLength = Math.Min(rayLength, -startZ / sun.Z);
            }

            double endX = startX + sun.X * rayLength;
            double endY = startY + sun.Y * rayLength;
            double endZ = startZ + sun.Z * rayLength;

            int mapChunkX = int.MinValue;
            int mapChunkZ = int.MinValue;
            ushort[] rainHeights = null;
            int chunkX = int.MinValue;
            int chunkY = int.MinValue;
            int chunkZ = int.MinValue;
            IWorldChunk chunk = null;

            while (cellY < blockAccess.MapSizeY)
            {
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    cellX += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxZ)
                {
                    cellY += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    cellZ += stepZ;
                    tMaxZ += tDeltaZ;
                }

                if (cellX < 0 || cellZ < 0 || cellX >= blockAccess.MapSizeX || cellZ >= blockAccess.MapSizeZ)
                {
                    return true;
                }

                int nextMapChunkX = cellX / chunkSize;
                int nextMapChunkZ = cellZ / chunkSize;
                if (nextMapChunkX != mapChunkX || nextMapChunkZ != mapChunkZ)
                {
                    mapChunkX = nextMapChunkX;
                    mapChunkZ = nextMapChunkZ;
                    rainHeights = blockAccess.GetMapChunk(mapChunkX, mapChunkZ)?.RainHeightMap;
                    if (rainHeights == null)
                    {
                        // mapchunk can be here before this array
                        return false;
                    }
                }

                int localIndex = cellZ % chunkSize * chunkSize + cellX % chunkSize;
                if (cellY > rainHeights[localIndex])
                {
                    return true;
                }

                int nextChunkY = cellY / chunkSize;
                if (mapChunkX != chunkX || nextChunkY != chunkY || mapChunkZ != chunkZ)
                {
                    chunkX = mapChunkX;
                    chunkY = nextChunkY;
                    chunkZ = mapChunkZ;
                    chunk = chunkProvider.GetUnpackedChunkFast(chunkX, chunkY, chunkZ);
                    if (chunk == null)
                    {
                        return false;
                    }
                }

                int index3d = ((cellY % chunkSize) * chunkSize + cellZ % chunkSize) * chunkSize + cellX % chunkSize;
                tmpPos.Set(cellX, cellY, cellZ);
                if (chunk.GetLightAbsorptionAt(index3d, tmpPos, blockTypes) > FullyBlockingLightAbsorption)
                {
                    return false;
                }

                int blockId = chunk.Data[index3d];
                LightBlockerKind blockerKind = blockerKinds[blockId];
                if (blockerKind == LightBlockerKind.None)
                {
                    continue;
                }

                Block block = blockTypes[blockId];
                if (
                    (blockerKind & LightBlockerKind.Door) != 0 && LightDoorBlocker.BlocksRay(blockAccess, block, tmpPos, startX, startY, startZ, endX, endY, endZ) ||
                    (blockerKind & LightBlockerKind.Trapdoor) != 0 && LightTrapdoorBlocker.BlocksRay(blockAccess, block, tmpPos, startX, startY, startZ, endX, endY, endZ)
                )
                {
                    return false;
                }
            }

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
    }
}
