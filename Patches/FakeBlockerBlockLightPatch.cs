using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator), nameof(ChunkIlluminator.UpdateSunLight))]
    internal static class FakeBlockerBlockLightPatch
    {
        private static void Postfix(ChunkIlluminator __instance, int posX, int posY, int posZ, int oldAbsorb, int newAbsorb, FastSetOfLongs __result)
        {
            if (!LightDoorBlocker.IsFakeBlockerAbsorptionChange(oldAbsorb, newAbsorb))
            {
                return;
            }

            // markabsorptionchanged only makes vanilla run the sunlight half, doors and trapdoors need block light too or placing one just redraws the wood
            foreach (long chunkIndex in __instance.UpdateBlockLight(oldAbsorb, newAbsorb, posX, posY, posZ))
            {
                __result.Add(chunkIndex);
            }
        }
    }
}
