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
            if (posY >= BlockPos.DimensionBoundary)
            {
                return true;
            }

            IBlockAccessor blockAccessor = ChunkIlluminatorAccess.BlockAccessor(__instance);
            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            tmpPos.Set(posX, posY, posZ);

            __result = SunlightRay.CanSeeSun(
                ChunkIlluminatorAccess.ChunkProvider(__instance),
                blockAccessor,
                ChunkIlluminatorAccess.BlockTypes(__instance),
                ChunkIlluminatorAccess.ChunkSize(__instance),
                posX,
                posY,
                posZ,
                tmpPos
            );

            return false;
        }
    }
}
