using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    // rebuild the surface cause clearing whole columns were making caves follow the sun whopsy
    internal static class DirectionalSunlightSeeder
    {
        private const int MaxShadowDistance = 384;

        [ThreadStatic]
        private static List<ShadowRayStep> raySteps;

        [ThreadStatic]
        private static BlockPos tmpPos;

        internal static void Apply(ChunkIlluminator illuminator, IWorldChunk[] chunks, int chunkX, int chunkZ, double phaseDays)
        {
            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            IBlockAccessor blockAccessor = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);
            ushort[] terrainHeights = blockAccessor.GetMapChunk(chunkX, chunkZ)?.WorldGenTerrainHeightMap;
            if (terrainHeights == null)
            {
                return;
            }

            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            Vec3f sun = DirectionalSunlight.GetSunDirection(blockAccessor, chunkX * chunkSize, blockAccessor.MapSizeY - 1, chunkZ * chunkSize, phaseDays);
            double horizontal = sun == null ? 0 : Math.Sqrt(sun.X * sun.X + sun.Z * sun.Z);
            bool projectShadows = sun != null && sun.Y > 0 && horizontal >= 0.001;
            if (projectShadows)
            {
                raySteps ??= new List<ShadowRayStep>(MaxShadowDistance);
                BuildRaySteps(raySteps, sun.X / horizontal, sun.Z / horizontal, sun.Y / horizontal);
            }

            int baseX = chunkX * chunkSize;
            int baseZ = chunkZ * chunkSize;
            int defaultSunlight = ChunkIlluminatorAccess.DefaultSunLight(illuminator);
            int foliageLight = SunlightSpreader.GetBounceCeiling(illuminator);
            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int lz = 0; lz < chunkSize; lz++)
                {
                    int terrainHeight = terrainHeights[lz * chunkSize + lx];
                    RestoreSurfaceColumn(chunks, chunkSize, baseX + lx, baseZ + lz, lx, lz, terrainHeight, defaultSunlight, blockTypes);
                    if (!projectShadows)
                    {
                        continue;
                    }

                    ShadowProjection shadow = GetShadowProjection(blockAccessor, baseX + lx, baseZ + lz, chunkSize, raySteps);
                    if (shadow.Height >= terrainHeight)
                    {
                        bool foliageShadow = blockAccessor.GetBlock(tmpPos.Set(shadow.CasterX, shadow.CasterY, shadow.CasterZ)).BlockMaterial == EnumBlockMaterial.Leaves;
                        ShadeSurface(chunks, chunkSize, lx, lz, terrainHeight, shadow.Height, foliageShadow ? foliageLight : 0, foliageLight, blockTypes);
                    }
                }
            }
        }

        private static void RestoreSurfaceColumn(
            IWorldChunk[] chunks,
            int chunkSize,
            int x,
            int z,
            int lx,
            int lz,
            int terrainHeight,
            int defaultSunlight,
            IList<Block> blockTypes
        )
        {
            int maxY = chunks.Length * chunkSize - 1;
            int sunlight = defaultSunlight;

            for (int y = maxY; y >= terrainHeight; y--)
            {
                IWorldChunk chunk = chunks[y / chunkSize];
                int index3d = (((y % chunkSize) * chunkSize) + lz) * chunkSize + lx;
                if (chunk.Lighting.GetSunlight(index3d) != sunlight)
                {
                    chunk.Lighting.SetSunlight_Buffered(index3d, sunlight);
                }

                if (sunlight == 0)
                {
                    continue;
                }

                int absorption = chunk.GetLightAbsorptionAt(index3d, tmpPos.Set(x, y, z), blockTypes);
                sunlight = absorption > sunlight ? 0 : sunlight - absorption;
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

        private static ShadowProjection GetShadowProjection(IBlockAccessor blockAccessor, int x, int z, int chunkSize, List<ShadowRayStep> steps)
        {
            int shadowHeight = -1;
            int casterX = 0;
            int casterY = 0;
            int casterZ = 0;
            int mapChunkX = int.MinValue;
            int mapChunkZ = int.MinValue;
            ushort[] rainHeights = null;

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
                    rainHeights = blockAccessor.GetMapChunk(mapChunkX, mapChunkZ)?.RainHeightMap;
                    if (rainHeights == null)
                    {
                        break;
                    }
                }

                int localX = sx % chunkSize;
                int localZ = sz % chunkSize;
                int terrainHeight = rainHeights[localZ * chunkSize + localX];
                int projectedHeight = (int)Math.Floor(terrainHeight - step.HeightDrop);
                if (projectedHeight > shadowHeight)
                {
                    shadowHeight = projectedHeight;
                    casterX = sx;
                    casterY = terrainHeight;
                    casterZ = sz;
                }

                if (projectedHeight < 0 && step.Distance > chunkSize)
                {
                    break;
                }
            }

            return new ShadowProjection(shadowHeight, casterX, casterY, casterZ);
        }

        private static void ShadeSurface(
            IWorldChunk[] chunks,
            int chunkSize,
            int lx,
            int lz,
            int terrainHeight,
            int shadowHeight,
            int shadowLight,
            int foliageLight,
            IList<Block> blockTypes
        )
        {
            int maxY = Math.Min(shadowHeight, chunks.Length * chunkSize - 1);
            for (int y = terrainHeight; y <= maxY; y++)
            {
                IWorldChunk chunk = chunks[y / chunkSize];
                int index3d = (((y % chunkSize) * chunkSize) + lz) * chunkSize + lx;
                int nextLight = blockTypes[chunk.Data[index3d]].BlockMaterial == EnumBlockMaterial.Leaves
                    ? Math.Max(shadowLight, foliageLight)
                    : shadowLight;
                if (chunk.Lighting.GetSunlight(index3d) != nextLight)
                {
                    chunk.Lighting.SetSunlight_Buffered(index3d, nextLight);
                }
            }
        }

        private readonly struct ShadowProjection
        {
            internal readonly int Height;
            internal readonly int CasterX;
            internal readonly int CasterY;
            internal readonly int CasterZ;

            internal ShadowProjection(int height, int casterX, int casterY, int casterZ)
            {
                Height = height;
                CasterX = casterX;
                CasterY = casterY;
                CasterZ = casterZ;
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
