using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator), "CollectLightValuesForLightSource")]
    internal static class ChunkIlluminatorCollectPatch
    {
        private static bool Prefix(ChunkIlluminator __instance, int posX, int posY, int posZ, int forPosX, int forPosY, int forPosZ, int forRange)
        {
            // replace vanilla collection with the same walk plus the visibility check
            LightCollector.Collect(__instance, posX, posY, posZ, forPosX, forPosY, forPosZ, forRange);
            return false;
        }
    }
}

