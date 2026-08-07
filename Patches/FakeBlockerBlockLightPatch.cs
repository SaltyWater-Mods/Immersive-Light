using System;
using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator), nameof(ChunkIlluminator.UpdateSunLight))]
    internal static class FakeBlockerBlockLightPatch
    {
        private static bool Prefix(ChunkIlluminator __instance, int posX, int posY, int posZ, int oldAbsorb, int newAbsorb, ref FastSetOfLongs __result, out bool __state)
        {
            bool updateSunlight = !LightDoorBlocker.IsFakeBlockerAbsorptionChange(oldAbsorb, newAbsorb);
            __state = updateSunlight && DirectionalSunlight.IsEnabled(ChunkIlluminatorAccess.BlockAccessor(__instance));
            if (__state)
            {
                SunlightRay.BeginPass();
            }

            if (updateSunlight)
            {
                return true;
            }

            // fake absorption here because the hitbox moved sunlight didnt actually change
            __result = __instance.UpdateBlockLight(oldAbsorb, newAbsorb, posX, posY, posZ);
            return false;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(bool __state, Exception __exception)
        {
            if (__state)
            {
                SunlightRay.EndPass();
            }

            return __exception;
        }
    }
}
