using System;
using System.Collections.Generic;
using ImmersiveLight.Debugging;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal static class LightCollector
    {
        private const int MaxLightSpread = 31;
        private const int VisitedWidth = MaxLightSpread * 2 + 1;
        private const int LoosePenalty = 2;

        // now each block has to see the source
        // max spread stays vanilla on purpose. WE DONT WANT TO REWRITE LIGHT SALTY COME ON
        // loose spill is just here for doors and small openings
        // should this be a config?^

        internal static void Collect(ChunkIlluminator illuminator, int posX, int posY, int posZ, int forPosX, int forPosY, int forPosZ, int forRange)
        {
            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            IChunkProvider chunkProvider = ChunkIlluminatorAccess.ChunkProvider(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);
            IBlockAccessor readBlockAccess = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            Dictionary<Vec3i, LightSourcesAtBlock> visitedNodes = ChunkIlluminatorAccess.VisitedNodes(illuminator);
            int[] currentVisited = ChunkIlluminatorAccess.CurrentVisited(illuminator);
            BlockPos tmpPos = new(Dimensions.WillSetLater);
            BlockPos rayPos = new(Dimensions.WillSetLater);

            Block block = LightWorld.GetBlock(chunkProvider, blockTypes, chunkSize, posX, posY, posZ);
            if (block == null)
            {
                return;
            }

            byte[] lightHsv = block.GetLightHsv(readBlockAccess, tmpPos.Set(posX, posY, posZ));
            byte h = lightHsv[0];
            byte s = lightHsv[1];
            byte v = lightHsv[2];

            // vanilla packs this into ints for speed but keeping the node readable here because the rays are the real cost not the queue
            Queue<LightNode> bfsQueue = new();
            bfsQueue.Enqueue(new LightNode(MaxLightSpread, MaxLightSpread, MaxLightSpread, v, true));

            AddLight(visitedNodes, posX, posY, posZ, h, s, v);

            bool nearMapEdge = posX < v - 1 || posZ < v - 1 || posX >= ChunkIlluminatorAccess.MapSizeX(illuminator) - v + 1 || posZ >= ChunkIlluminatorAccess.MapSizeZ(illuminator) - v + 1;

            ref int iterationValue = ref ChunkIlluminatorAccess.Iteration(illuminator);
            int iteration = ++iterationValue;

            int sourceX = posX;
            int sourceY = posY;
            int sourceZ = posZ;

            posX -= MaxLightSpread;
            posY -= MaxLightSpread;
            posZ -= MaxLightSpread;

            currentVisited[(MaxLightSpread * VisitedWidth + MaxLightSpread) * VisitedWidth + MaxLightSpread] = iteration;

            while (bfsQueue.Count > 0)
            {
                LightNode node = bfsQueue.Dequeue();
                int ox = node.X + posX;
                int oy = node.Y + posY;
                int oz = node.Z + posZ;

                IWorldChunk chunk = chunkProvider.GetUnpackedChunkFast(ox / chunkSize, oy / chunkSize, oz / chunkSize);
                if (chunk == null)
                {
                    continue;
                }

                int index3d = ((oy % chunkSize) * chunkSize + oz % chunkSize) * chunkSize + ox % chunkSize;
                int spreadBright = node.Brightness - chunk.GetLightAbsorptionAt(index3d, tmpPos.Set(ox, oy, oz), blockTypes) - 1;
                if (spreadBright <= 0)
                {
                    continue;
                }

                for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
                {
                    Vec3i face = BlockFacing.ALLNORMALI[i];
                    int nx = ox + face.X;
                    int ny = oy + face.Y;
                    int nz = oz + face.Z;

                    int visitedIndex = ((ny - posY) * VisitedWidth + nz - posZ) * VisitedWidth + nx - posX;
                    if (currentVisited[visitedIndex] == iteration)
                    {
                        continue;
                    }

                    if (ny < 0 || ny % BlockPos.DimensionBoundary >= ChunkIlluminatorAccess.MapSizeY(illuminator) || nearMapEdge && (nx < 0 || nz < 0 || nx >= ChunkIlluminatorAccess.MapSizeX(illuminator) || nz >= ChunkIlluminatorAccess.MapSizeZ(illuminator)))
                    {
                        continue;
                    }

                    if (Math.Abs(nx - forPosX) + Math.Abs(ny - forPosY) + Math.Abs(nz - forPosZ) >= forRange + spreadBright)
                    {
                        continue;
                    }

                    int nextBright = spreadBright;

                    // the normal path only lights the block when it can see back to the source n visited is marked after this check so a bad route does not poison the block forever
                    bool sourceVisible = LightRay.CanSeeSource(chunkProvider, readBlockAccess, blockTypes, chunkSize, sourceX, sourceY, sourceZ, nx, ny, nz, rayPos);

                    if (!sourceVisible)
                    {
                        // hidden blocks get one short handoff from a block that saw the source
                        // this can't keep chaining or the wall leaks come back to haunt me
                        if (face.Y > 0 || !node.SourceVisible)
                        {
                            continue;
                        }

                        nextBright -= LoosePenalty;
                        if (nextBright <= 0)
                        {
                            continue;
                        }

                        ImmersiveLightDebug.TraceLooseSpill(ox, oy, oz, nx, ny, nz);
                    }

                    currentVisited[visitedIndex] = iteration;
                    bfsQueue.Enqueue(new LightNode(nx - posX, ny - posY, nz - posZ, nextBright, sourceVisible));
                    AddLight(visitedNodes, nx, ny, nz, h, s, (byte)nextBright);
                }
            }
        }

        private static void AddLight(Dictionary<Vec3i, LightSourcesAtBlock> visitedNodes, int x, int y, int z, byte h, byte s, byte v)
        {
            Vec3i pos = new(x, y, z);
            if (!visitedNodes.TryGetValue(pos, out LightSourcesAtBlock lights))
            {
                visitedNodes[pos] = lights = new LightSourcesAtBlock();
            }

            lights.AddHsv(h, s, v);
        }

        private readonly struct LightNode
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Z;
            internal readonly int Brightness;
            internal readonly bool SourceVisible;

            internal LightNode(int x, int y, int z, int brightness, bool sourceVisible)
            {
                X = x;
                Y = y;
                Z = z;
                Brightness = brightness;
                SourceVisible = sourceVisible;
            }
        }
    }
}

