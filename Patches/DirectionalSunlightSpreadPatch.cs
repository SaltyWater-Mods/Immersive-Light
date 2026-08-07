using System.Collections.Generic;
using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator))]
    internal static class DirectionalSunlightSpreadPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ChunkIlluminator.SpreadSunLightInColumn))]
        private static bool SpreadInColumnPrefix(ChunkIlluminator __instance, Stack<BlockPos> stack, IWorldChunk[] chunks)
        {
            if (!DirectionalSunlight.IsEnabled(ChunkIlluminatorAccess.BlockAccessor(__instance)))
            {
                return true;
            }

            if (stack.Count == 0 || stack.Peek().dimension != Dimensions.NormalWorld)
            {
                return true;
            }

            SunlightSpreader.SpreadInColumn(__instance, stack, chunks);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ChunkIlluminator.SpreadSunlightAt))]
        private static bool SpreadAtPrefix(ChunkIlluminator __instance, QueueOfInt unhandledPositions, BlockPos centerPos, bool isDirectlyIlluminated, FastSetOfLongs touchedChunks)
        {
            if (!DirectionalSunlight.IsEnabled(ChunkIlluminatorAccess.BlockAccessor(__instance)))
            {
                return true;
            }

            if (centerPos.dimension != Dimensions.NormalWorld)
            {
                return true;
            }

            SunlightSpreader.SpreadAt(__instance, unhandledPositions, centerPos, isDirectlyIlluminated, touchedChunks);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ChunkIlluminator.SunLightFloodNeighbourChunks))]
        private static bool SpreadBordersPrefix(ChunkIlluminator __instance, IWorldChunk[] curChunks, int chunkX, int chunkY, int chunkZ, int dimension, ref byte __result)
        {
            if (!DirectionalSunlight.IsEnabled(ChunkIlluminatorAccess.BlockAccessor(__instance)))
            {
                return true;
            }

            if (dimension != Dimensions.NormalWorld)
            {
                return true;
            }

            __result = SunlightBorderSpreader.Spread(__instance, curChunks, chunkX, chunkY, chunkZ);
            return false;
        }
    }
}
