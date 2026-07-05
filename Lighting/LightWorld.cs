using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ImmersiveLight.Lighting
{
    internal static class LightWorld
    {
        internal static Block GetBlock(IChunkProvider chunkProvider, IList<Block> blockTypes, int chunkSize, int x, int y, int z)
        {
            if ((x | y | z) < 0)
            {
                return null;
            }

            IWorldChunk chunk = chunkProvider.GetUnpackedChunkFast(x / chunkSize, y / chunkSize, z / chunkSize, true);
            if (chunk == null)
            {
                return null;
            }

            return blockTypes[chunk.Data[((y % chunkSize) * chunkSize + z % chunkSize) * chunkSize + x % chunkSize]];
        }
    }
}

