using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    // couldnt think of a better class name.. this mostly clears sunlight to seed the shadows
    internal static class DirectionalSunlightSeeder
    {
        private const int MaxShadowDistance = 384;

        [ThreadStatic]
        private static List<ShadowRayStep> raySteps;

        internal static void Apply(ChunkIlluminator illuminator, IWorldChunk[] chunks, int chunkX, int chunkZ, double phaseDays)
        {
            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            IBlockAccessor blockAccessor = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            Vec3f sun = DirectionalSunlight.GetSunDirection(blockAccessor, chunkX * chunkSize, blockAccessor.MapSizeY - 1, chunkZ * chunkSize, phaseDays);
            if (sun == null || sun.Y <= 0)
            {
                return;
            }

            double horizontal = Math.Sqrt(sun.X * sun.X + sun.Z * sun.Z);
            if (horizontal < 0.001)
            {
                return;
            }

            raySteps ??= new List<ShadowRayStep>(MaxShadowDistance);
            BuildRaySteps(raySteps, sun.X / horizontal, sun.Z / horizontal, sun.Y / horizontal);

            int baseX = chunkX * chunkSize;
            int baseZ = chunkZ * chunkSize;
            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int lz = 0; lz < chunkSize; lz++)
                {
                    int shadowHeight = GetShadowHeight(blockAccessor, baseX + lx, baseZ + lz, chunkSize, raySteps);
                    if (shadowHeight >= 0)
                    {
                        ClearBelow(chunks, chunkSize, lx, lz, shadowHeight);
                    }
                }
            }
        }

        private static void BuildRaySteps(List<ShadowRayStep> steps, double stepX, double stepZ, double risePerBlock)
        {
            steps.Clear();
            double sampleX = 0.5;
            double sampleZ = 0.5;
            int lastX = 0;
            int lastZ = 0;

            for (int distance = 1; distance <= MaxShadowDistance; distance++)
            {
                sampleX += stepX;
                sampleZ += stepZ;
                int offsetX = (int)Math.Floor(sampleX);
                int offsetZ = (int)Math.Floor(sampleZ);
                if (offsetX == lastX && offsetZ == lastZ)
                {
                    continue;
                }

                lastX = offsetX;
                lastZ = offsetZ;
                steps.Add(new ShadowRayStep(offsetX, offsetZ, distance, distance * risePerBlock));
            }
        }

        private static int GetShadowHeight(IBlockAccessor blockAccessor, int x, int z, int chunkSize, List<ShadowRayStep> steps)
        {
            int shadowHeight = -1;
            int mapChunkX = int.MinValue;
            int mapChunkZ = int.MinValue;
            IMapChunk mapChunk = null;

            for (int i = 0; i < steps.Count; i++)
            {
                ShadowRayStep step = steps[i];
                int sx = x + step.OffsetX;
                int sz = z + step.OffsetZ;
                if (sx < 0 || sz < 0 || sx >= blockAccessor.MapSizeX || sz >= blockAccessor.MapSizeZ)
                {
                    break;
                }

                int nextMapChunkX = sx / chunkSize;
                int nextMapChunkZ = sz / chunkSize;
                if (nextMapChunkX != mapChunkX || nextMapChunkZ != mapChunkZ)
                {
                    mapChunkX = nextMapChunkX;
                    mapChunkZ = nextMapChunkZ;
                    mapChunk = blockAccessor.GetMapChunk(mapChunkX, mapChunkZ);
                    if (mapChunk == null)
                    {
                        break;
                    }
                }

                int localX = sx % chunkSize;
                int localZ = sz % chunkSize;
                int terrainHeight = mapChunk.RainHeightMap[localZ * chunkSize + localX];
                // projecting each heightmap obstruction back onto this column using the sun slope
                int projectedHeight = (int)Math.Floor(terrainHeight - step.HeightDrop);
                if (projectedHeight > shadowHeight)
                {
                    shadowHeight = projectedHeight;
                }

                if (projectedHeight < 0 && step.Distance > chunkSize)
                {
                    break;
                }
            }

            return shadowHeight;
        }

        private static void ClearBelow(IWorldChunk[] chunks, int chunkSize, int lx, int lz, int shadowHeight)
        {
            int maxY = Math.Min(shadowHeight, chunks.Length * chunkSize - 1);
            for (int y = 0; y <= maxY; y++)
            {
                IWorldChunk chunk = chunks[y / chunkSize];
                int index3d = (((y % chunkSize) * chunkSize) + lz) * chunkSize + lx;
                chunk.Lighting.SetSunlight_Buffered(index3d, 0);
            }
        }

        private readonly struct ShadowRayStep
        {
            internal readonly int OffsetX;
            internal readonly int OffsetZ;
            internal readonly int Distance;
            internal readonly double HeightDrop;

            internal ShadowRayStep(int offsetX, int offsetZ, int distance, double heightDrop)
            {
                OffsetX = offsetX;
                OffsetZ = offsetZ;
                Distance = distance;
                HeightDrop = heightDrop;
            }
        }
    }
}
