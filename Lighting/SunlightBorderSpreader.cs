using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal static class SunlightBorderSpreader
    {
        // reusable scratch data for client and server
        [ThreadStatic]
        private static IWorldChunk[] neighbourChunks;

        [ThreadStatic]
        private static Stack<SunlightSpreader.SunlightSeed> currentStack;

        [ThreadStatic]
        private static Stack<SunlightSpreader.SunlightSeed> neighbourStack;

        [ThreadStatic]
        private static BlockPos tmpPos;

        internal static byte Spread(ChunkIlluminator illuminator, IWorldChunk[] currentChunks, int chunkX, int chunkY, int chunkZ)
        {
            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            int ambientCeiling = SunlightSpreader.GetAmbientCeiling(illuminator);
            IChunkProvider chunkProvider = ChunkIlluminatorAccess.ChunkProvider(illuminator);
            IBlockAccessor blockAccess = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);

            if (neighbourChunks == null || neighbourChunks.Length != currentChunks.Length)
            {
                neighbourChunks = new IWorldChunk[currentChunks.Length];
            }

            currentStack ??= new Stack<SunlightSpreader.SunlightSeed>();
            neighbourStack ??= new Stack<SunlightSpreader.SunlightSeed>();
            tmpPos ??= new BlockPos(Dimensions.NormalWorld);

            bool TrySpreadAcrossBorder(
                IWorldChunk fromChunk,
                int fromIndex,
                int fromX,
                int y,
                int fromZ,
                IWorldChunk toChunk,
                int toIndex,
                int toX,
                int toZ,
                Stack<SunlightSpreader.SunlightSeed> stack
            )
            {
                int spreadLight = fromChunk.Lighting.GetSunlight(fromIndex) - 1;
                if (spreadLight <= 0)
                {
                    return false;
                }

                spreadLight -= fromChunk.GetLightAbsorptionAt(fromIndex, tmpPos.Set(fromX, y, fromZ), blockTypes);
                int currentLight = toChunk.Lighting.GetSunlight(toIndex);
                if (spreadLight <= currentLight)
                {
                    return false;
                }

                bool direct = SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, fromX, y, fromZ, tmpPos)
                    && SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, toX, y, toZ, tmpPos);
                if (!direct)
                {
                    spreadLight = Math.Min(spreadLight, ambientCeiling);
                }

                if (spreadLight <= currentLight)
                {
                    return false;
                }

                toChunk.Lighting.SetSunlight_Buffered(toIndex, spreadLight);
                stack.Push(new SunlightSpreader.SunlightSeed(toX, y, toZ));
                return true;
            }

            byte spreadFaces = 0;
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                int dx = facing.Normali.X;
                int dz = facing.Normali.Z;
                bool loaded = true;
                for (int cy = 0; cy < currentChunks.Length; cy++)
                {
                    neighbourChunks[cy] = chunkProvider.GetChunk(chunkX + dx, cy, chunkZ + dz);
                    if (neighbourChunks[cy] == null)
                    {
                        loaded = false;
                        break;
                    }

                    neighbourChunks[cy].Unpack();
                    currentChunks[cy].Unpack();
                }

                if (!loaded)
                {
                    continue;
                }

                currentStack.Clear();
                neighbourStack.Clear();
                int ownLx = dx > 0 ? chunkSize - 1 : 0;
                int ownLz = dz > 0 ? chunkSize - 1 : 0;
                int neighbourLx = dx > 0 ? 0 : chunkSize - 1;
                int neighbourLz = dz > 0 ? 0 : chunkSize - 1;

                for (int cy = chunkY; cy >= 0; cy--)
                {
                    IWorldChunk ownChunk = currentChunks[cy];
                    IWorldChunk neighbourChunk = neighbourChunks[cy];
                    for (int a = 0; a < chunkSize; a++)
                    {
                        for (int ly = 0; ly < chunkSize; ly++)
                        {
                            int lx = dx == 0 ? a : ownLx;
                            int lz = dz == 0 ? a : ownLz;
                            int nlx = dx == 0 ? a : neighbourLx;
                            int nlz = dz == 0 ? a : neighbourLz;
                            int y = cy * chunkSize + ly;
                            int ownX = chunkX * chunkSize + lx;
                            int ownZ = chunkZ * chunkSize + lz;
                            int neighbourX = (chunkX + dx) * chunkSize + nlx;
                            int neighbourZ = (chunkZ + dz) * chunkSize + nlz;
                            int ownIndex = (ly * chunkSize + lz) * chunkSize + lx;
                            int neighbourIndex = (ly * chunkSize + nlz) * chunkSize + nlx;

                            if (TrySpreadAcrossBorder(ownChunk, ownIndex, ownX, y, ownZ, neighbourChunk, neighbourIndex, neighbourX, neighbourZ, neighbourStack))
                            {
                                spreadFaces |= facing.Flag;
                            }

                            TrySpreadAcrossBorder(neighbourChunk, neighbourIndex, neighbourX, y, neighbourZ, ownChunk, ownIndex, ownX, ownZ, currentStack);
                        }
                    }
                }

                if (neighbourStack.Count > 0)
                {
                    SunlightSpreader.SpreadInColumn(illuminator, neighbourStack, neighbourChunks);
                }

                if (currentStack.Count > 0)
                {
                    SunlightSpreader.SpreadInColumn(illuminator, currentStack, currentChunks);
                }
            }

            return spreadFaces;
        }
    }
}
