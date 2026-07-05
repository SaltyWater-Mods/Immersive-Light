using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal static class ChunkIlluminatorAccess
    {
        // chunkilluminators walk state private so these refs are how we reach the vanilla data
        // uglier than a public api but oh well, less cursed than copying the whole relight path
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int> ChunkSize = AccessTools.FieldRefAccess<ChunkIlluminator, int>("chunkSize");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int> MapSizeX = AccessTools.FieldRefAccess<ChunkIlluminator, int>("mapsizex");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int> MapSizeY = AccessTools.FieldRefAccess<ChunkIlluminator, int>("mapsizey");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int> MapSizeZ = AccessTools.FieldRefAccess<ChunkIlluminator, int>("mapsizez");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, IChunkProvider> ChunkProvider = AccessTools.FieldRefAccess<ChunkIlluminator, IChunkProvider>("chunkProvider");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, IList<Block>> BlockTypes = AccessTools.FieldRefAccess<ChunkIlluminator, IList<Block>>("blockTypes");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, IBlockAccessor> BlockAccessor = AccessTools.FieldRefAccess<ChunkIlluminator, IBlockAccessor>("readBlockAccess");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, Dictionary<Vec3i, LightSourcesAtBlock>> VisitedNodes = AccessTools.FieldRefAccess<ChunkIlluminator, Dictionary<Vec3i, LightSourcesAtBlock>>("VisitedNodes");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int[]> CurrentVisited = AccessTools.FieldRefAccess<ChunkIlluminator, int[]>("currentVisited");
        internal static readonly AccessTools.FieldRef<ChunkIlluminator, int> Iteration = AccessTools.FieldRefAccess<ChunkIlluminator, int>("iteration");
    }
}

