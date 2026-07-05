using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal static class LightDirtyChunks
    {
        internal static void IncludeNeighbours(ChunkIlluminator illuminator, FastSetOfLongs chunks)
        {
            if (chunks == null || chunks.Count == 0 || ChunkIlluminatorAccess.ChunkProvider(illuminator) is not WorldMap worldMap)
            {
                return;
            }

            // no reason to call the whole 3x3x3 cube light spreads through face neighbours so only wake chunks touching the changed ones
            // >> note: if this misses a border case fix should be smarter borders lets leave like this
            List<long> changedChunks = new();
            foreach (long chunkIndex in chunks)
            {
                changedChunks.Add(chunkIndex);
            }

            foreach (long chunkIndex in changedChunks)
            {
                // FUTURE ME wtf is this please remove this and use worldMap.ChunkPosFromChunkIndex3D<<
                // done. decoding chunks is a game problem now
                var chunkPos = worldMap.ChunkPosFromChunkIndex3D(chunkIndex);
                int cy = chunkPos.InternalY;

                AddChunkIfValid(worldMap, chunks, chunkPos.X - 1, cy, chunkPos.Z);
                AddChunkIfValid(worldMap, chunks, chunkPos.X + 1, cy, chunkPos.Z);
                AddChunkIfValid(worldMap, chunks, chunkPos.X, cy, chunkPos.Z - 1);
                AddChunkIfValid(worldMap, chunks, chunkPos.X, cy, chunkPos.Z + 1);

                if (BlockAccessorMovable.ChunkCoordsInSameDimension(cy, cy - 1))
                {
                    AddChunkIfValid(worldMap, chunks, chunkPos.X, cy - 1, chunkPos.Z);
                }

                if (BlockAccessorMovable.ChunkCoordsInSameDimension(cy, cy + 1))
                {
                    AddChunkIfValid(worldMap, chunks, chunkPos.X, cy + 1, chunkPos.Z);
                }
            }
        }

        private static void AddChunkIfValid(WorldMap worldMap, FastSetOfLongs chunks, int chunkX, int chunkY, int chunkZ)
        {
            int localY = chunkY % GlobalConstants.DimensionSizeInChunks;
            if (localY < 0 || localY >= worldMap.ChunkMapSizeY || !worldMap.IsValidChunkPos(chunkX, chunkY, chunkZ))
            {
                return;
            }

            chunks.Add(worldMap.ChunkIndex3D(chunkX, chunkY, chunkZ));
        }
    }
}

