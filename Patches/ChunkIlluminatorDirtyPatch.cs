using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator))]
    internal static class ChunkIlluminatorDirtyPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("PlaceBlockLight")]
        private static void PlacePostfix(ChunkIlluminator __instance, FastSetOfLongs __result)
        {
            // expanding the result here cuz placing light can touch meshes outside the chunks vanilla reports 
            LightDirtyChunks.IncludeNeighbours(__instance, __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch("RemoveBlockLight")]
        private static void RemovePostfix(ChunkIlluminator __instance, FastSetOfLongs __result)
        {
            // removing light uses the same expanded area so old light does not stay baked nearby
            LightDirtyChunks.IncludeNeighbours(__instance, __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateBlockLight")]
        private static void UpdatePostfix(ChunkIlluminator __instance, FastSetOfLongs __result)
        {
            LightDirtyChunks.IncludeNeighbours(__instance, __result);
        }
    }
}

