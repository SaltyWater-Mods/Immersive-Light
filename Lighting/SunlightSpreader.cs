using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal static class SunlightSpreader
    {
        [ThreadStatic]
        private static Queue<SunNode> columnQueue;

        [ThreadStatic]
        private static Queue<long> runtimeQueue;

        [ThreadStatic]
        private static Dictionary<long, RuntimeSunNode> runtimePending;

        [ThreadStatic]
        private static BlockPos tmpPos;

        internal static void SpreadInColumn(ChunkIlluminator illuminator, Stack<BlockPos> stack, IWorldChunk[] chunks)
        {
            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            columnQueue ??= new Queue<SunNode>();
            columnQueue.Clear();

            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            IChunkProvider chunkProvider = ChunkIlluminatorAccess.ChunkProvider(illuminator);
            IBlockAccessor blockAccess = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);

            while (stack.Count > 0)
            {
                BlockPos pos = stack.Pop();
                bool direct = SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, pos.X, pos.InternalY, pos.Z, tmpPos);
                columnQueue.Enqueue(new SunNode(pos.X, pos.InternalY, pos.Z, direct));
            }

            SpreadColumnQueue(illuminator, chunks, chunkSize, chunkProvider, blockAccess, blockTypes);
        }

        internal static void SpreadInColumn(ChunkIlluminator illuminator, Stack<SunlightSeed> stack, IWorldChunk[] chunks)
        {
            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            columnQueue ??= new Queue<SunNode>();
            columnQueue.Clear();

            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            IChunkProvider chunkProvider = ChunkIlluminatorAccess.ChunkProvider(illuminator);
            IBlockAccessor blockAccess = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);

            while (stack.Count > 0)
            {
                SunlightSeed seed = stack.Pop();
                bool direct = SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, seed.X, seed.Y, seed.Z, tmpPos);
                columnQueue.Enqueue(new SunNode(seed.X, seed.Y, seed.Z, direct));
            }

            SpreadColumnQueue(illuminator, chunks, chunkSize, chunkProvider, blockAccess, blockTypes);
        }

        internal static void SpreadAt(ChunkIlluminator illuminator, QueueOfInt unhandledPositions, BlockPos centerPos, bool isDirectlyIlluminated, FastSetOfLongs touchedChunks)
        {
            int chunkSize = ChunkIlluminatorAccess.ChunkSize(illuminator);
            int ambientCeiling = GetAmbientCeiling(illuminator);
            int mapSizeX = ChunkIlluminatorAccess.MapSizeX(illuminator);
            int mapSizeY = ChunkIlluminatorAccess.MapSizeY(illuminator);
            int mapSizeZ = ChunkIlluminatorAccess.MapSizeZ(illuminator);
            IChunkProvider chunkProvider = ChunkIlluminatorAccess.ChunkProvider(illuminator);
            IBlockAccessor blockAccess = ChunkIlluminatorAccess.BlockAccessor(illuminator);
            IList<Block> blockTypes = ChunkIlluminatorAccess.BlockTypes(illuminator);

            tmpPos ??= new BlockPos(Dimensions.NormalWorld);
            runtimeQueue ??= new Queue<long>();
            runtimePending ??= new Dictionary<long, RuntimeSunNode>();
            runtimeQueue.Clear();
            runtimePending.Clear();

            while (unhandledPositions.Count > 0)
            {
                int packed = unhandledPositions.Dequeue();
                int light = packed >> 24 & 0x1F;
                if (light == 0)
                {
                    continue;
                }

                int x = (packed & 0xFF) - 128 + centerPos.X;
                int y = (packed >> 8 & 0xFF) - 128 + centerPos.Y;
                int z = (packed >> 16 & 0xFF) - 128 + centerPos.Z;
                bool direct = SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, x, y, z, tmpPos);
                EnqueueRuntime(new RuntimeSunNode(x, y, z, direct ? light : Math.Min(light, ambientCeiling), direct), mapSizeX, mapSizeZ);
            }

            while (runtimeQueue.Count > 0)
            {
                long nodeKey = runtimeQueue.Dequeue();
                RuntimeSunNode node = runtimePending[nodeKey];
                runtimePending.Remove(nodeKey);

                IWorldChunk chunk = chunkProvider.GetUnpackedChunkFast(node.X / chunkSize, node.Y / chunkSize, node.Z / chunkSize);
                if (chunk == null)
                {
                    continue;
                }

                int index3d = ((node.Y % chunkSize) * chunkSize + node.Z % chunkSize) * chunkSize + node.X % chunkSize;
                if (chunk.Lighting.GetSunlight(index3d) < node.Light)
                {
                    chunk.Lighting.SetSunlight_Buffered(index3d, node.Light);
                }

                int absorption = chunk.GetLightAbsorptionAt(index3d, tmpPos.Set(node.X, node.Y, node.Z), blockTypes);
                if (node.Light - absorption <= 0)
                {
                    continue;
                }

                for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
                {
                    Vec3i face = BlockFacing.ALLNORMALI[i];
                    int nx = node.X + face.X;
                    int ny = node.Y + face.Y;
                    int nz = node.Z + face.Z;
                    if ((nx | ny | nz) < 0 || nx >= mapSizeX || ny >= mapSizeY || nz >= mapSizeZ)
                    {
                        continue;
                    }

                    int spreadLight = node.Light - absorption - (isDirectlyIlluminated && nx == centerPos.X && nz == centerPos.Z && i == BlockFacing.indexDOWN ? 0 : 1);
                    if (spreadLight <= 0)
                    {
                        continue;
                    }

                    chunk = chunkProvider.GetUnpackedChunkFast(nx / chunkSize, ny / chunkSize, nz / chunkSize);
                    if (chunk == null)
                    {
                        continue;
                    }

                    touchedChunks.Add(chunkProvider.ChunkIndex3D(nx / chunkSize, ny / chunkSize, nz / chunkSize));
                    index3d = ((ny % chunkSize) * chunkSize + nz % chunkSize) * chunkSize + nx % chunkSize;
                    int currentLight = chunk.Lighting.GetSunlight(index3d);
                    if (currentLight >= spreadLight)
                    {
                        continue;
                    }

                    bool direct = node.Direct && SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, nx, ny, nz, tmpPos);
                    if (!direct)
                    {
                        spreadLight = Math.Min(spreadLight, ambientCeiling);
                    }

                    if (currentLight < spreadLight)
                    {
                        EnqueueRuntime(new RuntimeSunNode(nx, ny, nz, spreadLight, direct), mapSizeX, mapSizeZ);
                    }
                }
            }
        }

        internal static int GetAmbientCeiling(ChunkIlluminator illuminator)
        {
            // keep shade tied to the world sunlight setting
            return Math.Max(1, ChunkIlluminatorAccess.DefaultSunLight(illuminator) / 8);
        }

        private static void SpreadColumnQueue(ChunkIlluminator illuminator, IWorldChunk[] chunks, int chunkSize, IChunkProvider chunkProvider, IBlockAccessor blockAccess, IList<Block> blockTypes)
        {
            int ambientCeiling = GetAmbientCeiling(illuminator);
            int mapSizeY = ChunkIlluminatorAccess.MapSizeY(illuminator);

            while (columnQueue.Count > 0)
            {
                SunNode node = columnQueue.Dequeue();
                int cy = node.Y / chunkSize;
                int lx = node.X % chunkSize;
                int ly = node.Y % chunkSize;
                int lz = node.Z % chunkSize;
                IWorldChunk chunk = chunks[cy];
                int index3d = (ly * chunkSize + lz) * chunkSize + lx;
                int absorption = chunk.GetLightAbsorptionAt(index3d, tmpPos.Set(node.X, node.Y, node.Z), blockTypes);
                int spreadLight = chunk.Lighting.GetSunlight(index3d) - absorption - 1;
                if (spreadLight <= 0)
                {
                    continue;
                }

                for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
                {
                    Vec3i face = BlockFacing.ALLNORMALI[i];
                    int nx = node.X + face.X;
                    int ny = node.Y + face.Y;
                    int nz = node.Z + face.Z;
                    int nlx = lx + face.X;
                    int nlz = lz + face.Z;

                    if (nlx < 0 || ny < 0 || nlz < 0 || nlx >= chunkSize || ny >= mapSizeY || nlz >= chunkSize)
                    {
                        continue;
                    }

                    IWorldChunk nextChunk = chunks[ny / chunkSize];
                    if (nextChunk != chunk)
                    {
                        nextChunk.Unpack();
                    }
                    int nextIndex3d = ((ny % chunkSize) * chunkSize + nlz) * chunkSize + nlx;
                    int currentLight = nextChunk.Lighting.GetSunlight(nextIndex3d);
                    if (currentLight >= spreadLight)
                    {
                        continue;
                    }

                    bool direct = node.Direct && SunlightRay.CanSeeSun(chunkProvider, blockAccess, blockTypes, chunkSize, nx, ny, nz, tmpPos);
                    int nextLight = direct ? spreadLight : Math.Min(spreadLight, ambientCeiling);
                    if (currentLight >= nextLight)
                    {
                        continue;
                    }

                    nextChunk.Lighting.SetSunlight_Buffered(nextIndex3d, nextLight);
                    columnQueue.Enqueue(new SunNode(nx, ny, nz, direct));
                }
            }
        }

        private static void EnqueueRuntime(RuntimeSunNode node, int mapSizeX, int mapSizeZ)
        {
            // QueueOfInt does this too only keep the best light waiting for a block
            long key = MapUtil.Index3dL(node.X, node.Y, node.Z, mapSizeX, mapSizeZ);
            if (runtimePending.TryGetValue(key, out RuntimeSunNode pending))
            {
                if (pending.Light > node.Light || (pending.Light == node.Light && (pending.Direct || !node.Direct)))
                {
                    return;
                }

                runtimePending[key] = node;
                return;
            }

            runtimePending.Add(key, node);
            runtimeQueue.Enqueue(key);
        }

        internal readonly struct SunlightSeed
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Z;

            internal SunlightSeed(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private readonly struct SunNode
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Z;
            internal readonly bool Direct;

            internal SunNode(int x, int y, int z, bool direct)
            {
                X = x;
                Y = y;
                Z = z;
                Direct = direct;
            }
        }

        private readonly struct RuntimeSunNode
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Z;
            internal readonly int Light;
            internal readonly bool Direct;

            internal RuntimeSunNode(int x, int y, int z, int light, bool direct)
            {
                X = x;
                Y = y;
                Z = z;
                Light = light;
                Direct = direct;
            }
        }
    }
}
