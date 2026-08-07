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
        // keep this at 3 any more and back to wall leaks
        private const int MaxLooseSteps = 3;
        private const int PathBrightnessBits = 5;
        private const int PathStateBits = (MaxLooseSteps + 1) * PathBrightnessBits;
        private const int VisibilityKnownFlag = 1 << PathStateBits;
        private const int SourceVisibleFlag = 1 << (PathStateBits + 1);
        private const int HasPathFlag = 1 << (PathStateBits + 2);

        [ThreadStatic]
        private static int[] pathStates;

        [ThreadStatic]
        private static List<int> reachedNodes;

        [ThreadStatic]
        private static Queue<LightNode> bfsQueue;

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
            LightBlockerKind[] blockerKinds = LightBlockerCache.Get(blockTypes);

            Block block = LightWorld.GetBlock(chunkProvider, blockTypes, chunkSize, posX, posY, posZ);
            if (block == null)
            {
                return;
            }

            pathStates ??= new int[currentVisited.Length];
            reachedNodes ??= new List<int>();
            bfsQueue ??= new Queue<LightNode>();
            reachedNodes.Clear();
            bfsQueue.Clear();

            byte[] lightHsv = block.GetLightHsv(readBlockAccess, tmpPos.Set(posX, posY, posZ));
            byte h = lightHsv[0];
            byte s = lightHsv[1];
            byte v = lightHsv[2];

            bfsQueue.Enqueue(new LightNode(MaxLightSpread, MaxLightSpread, MaxLightSpread, v, 0));

            bool nearMapEdge = posX < v - 1 || posZ < v - 1 || posX >= ChunkIlluminatorAccess.MapSizeX(illuminator) - v + 1 || posZ >= ChunkIlluminatorAccess.MapSizeZ(illuminator) - v + 1;

            ref int iterationValue = ref ChunkIlluminatorAccess.Iteration(illuminator);
            int iteration = ++iterationValue;

            int sourceX = posX;
            int sourceY = posY;
            int sourceZ = posZ;

            posX -= MaxLightSpread;
            posY -= MaxLightSpread;
            posZ -= MaxLightSpread;

            int centerIndex = (MaxLightSpread * VisitedWidth + MaxLightSpread) * VisitedWidth + MaxLightSpread;
            currentVisited[centerIndex] = iteration;
            pathStates[centerIndex] = HasPathFlag | VisibilityKnownFlag | SourceVisibleFlag | v;
            reachedNodes.Add(centerIndex);

            while (bfsQueue.Count > 0)
            {
                LightNode node = bfsQueue.Dequeue();
                int nodeIndex = (node.Y * VisitedWidth + node.Z) * VisitedWidth + node.X;
                if (GetPathBrightness(pathStates[nodeIndex], node.LooseSteps) != node.Brightness)
                {
                    continue;
                }

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
                    if (ny < 0 || ny % BlockPos.DimensionBoundary >= ChunkIlluminatorAccess.MapSizeY(illuminator) || nearMapEdge && (nx < 0 || nz < 0 || nx >= ChunkIlluminatorAccess.MapSizeX(illuminator) || nz >= ChunkIlluminatorAccess.MapSizeZ(illuminator)))
                    {
                        continue;
                    }

                    if (Math.Abs(nx - forPosX) + Math.Abs(ny - forPosY) + Math.Abs(nz - forPosZ) >= forRange + spreadBright)
                    {
                        continue;
                    }

                    int nextBright = spreadBright;
                    int nextLooseSteps = 0;

                    int pathState;
                    if (currentVisited[visitedIndex] == iteration)
                    {
                        pathState = pathStates[visitedIndex];
                    }
                    else
                    {
                        currentVisited[visitedIndex] = iteration;
                        pathState = 0;
                    }

                    // several bend paths can reach this block, dont raycast it every time
                    bool sourceVisible;
                    if ((pathState & VisibilityKnownFlag) == 0)
                    {
                        sourceVisible = LightRay.CanSeeSource(chunkProvider, readBlockAccess, blockTypes, blockerKinds, chunkSize, sourceX, sourceY, sourceZ, nx, ny, nz, rayPos);
                        pathState |= VisibilityKnownFlag;
                        if (sourceVisible)
                        {
                            pathState |= SourceVisibleFlag;
                        }

                        pathStates[visitedIndex] = pathState;
                    }
                    else
                    {
                        sourceVisible = (pathState & SourceVisibleFlag) != 0;
                    }

                    if (!sourceVisible)
                    {
                        if (face.Y > 0 || node.LooseSteps >= MaxLooseSteps)
                        {
                            continue;
                        }

                        nextBright -= LoosePenalty;
                        if (nextBright <= 0)
                        {
                            continue;
                        }

                        nextLooseSteps = node.LooseSteps + 1;
                    }

                    if (!TryImprovePath(currentVisited, iteration, visitedIndex, nextBright, nextLooseSteps))
                    {
                        continue;
                    }

                    if (!sourceVisible)
                    {
                        ImmersiveLightDebug.TraceLooseSpill(ox, oy, oz, nx, ny, nz);
                    }

                    bfsQueue.Enqueue(new LightNode(nx - posX, ny - posY, nz - posZ, nextBright, nextLooseSteps));
                }
            }

            for (int i = 0; i < reachedNodes.Count; i++)
            {
                int visitedIndex = reachedNodes[i];
                int pathState = pathStates[visitedIndex];
                int brightness = 0;
                for (int looseSteps = 0; looseSteps <= MaxLooseSteps; looseSteps++)
                {
                    brightness = Math.Max(brightness, GetPathBrightness(pathState, looseSteps));
                }

                int offsetX = visitedIndex % VisitedWidth;
                int yz = visitedIndex / VisitedWidth;
                int offsetZ = yz % VisitedWidth;
                int offsetY = yz / VisitedWidth;
                AddLight(visitedNodes, posX + offsetX, posY + offsetY, posZ + offsetZ, h, s, (byte)brightness);
            }
        }

        private static bool TryImprovePath(int[] currentVisited, int iteration, int visitedIndex, int brightness, int looseSteps)
        {
            int pathState;
            if (currentVisited[visitedIndex] != iteration)
            {
                currentVisited[visitedIndex] = iteration;
                pathState = 0;
            }
            else
            {
                pathState = pathStates[visitedIndex];
            }

            // first hit only made the bend change depending on N/E/S/W face order
            for (int state = 0; state <= looseSteps; state++)
            {
                if (GetPathBrightness(pathState, state) >= brightness)
                {
                    return false;
                }
            }

            if ((pathState & HasPathFlag) == 0)
            {
                pathState |= HasPathFlag;
                reachedNodes.Add(visitedIndex);
            }

            int shift = looseSteps * PathBrightnessBits;
            pathState = (pathState & ~(MaxLightSpread << shift)) | (brightness << shift);

            for (int state = looseSteps + 1; state <= MaxLooseSteps; state++)
            {
                shift = state * PathBrightnessBits;
                if (GetPathBrightness(pathState, state) <= brightness)
                {
                    pathState &= ~(MaxLightSpread << shift);
                }
            }

            pathStates[visitedIndex] = pathState;
            return true;
        }

        private static int GetPathBrightness(int pathState, int looseSteps)
        {
            return (pathState >> (looseSteps * PathBrightnessBits)) & MaxLightSpread;
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
            internal readonly byte X;
            internal readonly byte Y;
            internal readonly byte Z;
            internal readonly byte Brightness;
            internal readonly byte LooseSteps;

            internal LightNode(int x, int y, int z, int brightness, int looseSteps)
            {
                X = (byte)x;
                Y = (byte)y;
                Z = (byte)z;
                Brightness = (byte)brightness;
                LooseSteps = (byte)looseSteps;
            }
        }
    }
}

