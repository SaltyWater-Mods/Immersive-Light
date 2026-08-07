using System;
using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ChunkIlluminator), nameof(ChunkIlluminator.IsDirectlyIlluminated))]
    internal static class DirectionalSunlightDirectPatch
    {
        [ThreadStatic]
        private static BlockPos tmpPos;

        private static bool Prefix(
            ChunkIlluminator __instance,
            int posX,
            int posY,
            int posZ,
            ref bool __result
        )
        {
            IBlockAccessor blockAccessor = ChunkIlluminatorAccess.BlockAccessor(__instance);
            if (!DirectionalSunlight.IsEnabled(blockAccessor))
            {
                return true;
            }

            if (posY >= BlockPos.DimensionBoundary)
            {
                return true;
            }

            int chunkSize = ChunkIlluminatorAccess.ChunkSize(__instance);
            if (!SunlightRay.UsesDirectionalSunlight(blockAccessor, chunkSize, posX, posY, posZ))
            {
                return true;
            }

            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            tmpPos.Set(posX, posY, posZ);

            __result = SunlightRay.CanSeeSun(
                ChunkIlluminatorAccess.ChunkProvider(__instance),
                blockAccessor,
                ChunkIlluminatorAccess.BlockTypes(__instance),
                chunkSize,
                posX,
                posY,
                posZ,
                tmpPos
            );

            return false;
        }
    }
}
