using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.Common.Database;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace ImmersiveLight.Lighting
{
    internal static class DirectionalSunlightRelighter
    {
        private static readonly AccessTools.FieldRef<ClientSystemRelight, ChunkIlluminator> ClientIlluminator =
            AccessTools.FieldRefAccess<ClientSystemRelight, ChunkIlluminator>("chunkIlluminator");

        [ThreadStatic]
        private static ClientChunk[] clientColumn;

        [ThreadStatic]
        private static IWorldChunk[] serverColumn;

        [ThreadStatic]
        private static HashSet<long> fullRedraw;

        [ThreadStatic]
        private static HashSet<long> edgeRedraw;

        internal static void ProcessClient(ClientSystemRelight system, ClientMain game)
        {
            if (!DirectionalSunlight.IsEnabled(EnumAppSide.Client))
            {
                return;
            }

            lock (game.WorldMap.LightingTasksLock)
            {
                if (game.WorldMap.LightingTasks.Count > 0)
                {
                    return;
                }
            }

            if (!DirectionalSunlight.TryDequeueClient(out DirectionalSunlightWork work, out DirectionalSunlightJob job) || !DirectionalSunlight.IsCurrentClient(job))
            {
                return;
            }

            ClientChunk[] chunks = GetClientColumn(game, job.ChunkX, job.ChunkZ);
            if (chunks == null)
            {
                return;
            }

            ChunkIlluminator illuminator = ClientIlluminator(system);
            SunlightRay.BeginPass(job.PhaseDays);
            try
            {
                if (work == DirectionalSunlightWork.Prepare)
                {
                    DirectionalSunlightSeeder.Apply(illuminator, chunks, job.ChunkX, job.ChunkZ, job.PhaseDays);
                    illuminator.SunlightFlood(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ);
                    byte spreadFaces = illuminator.SunLightFloodNeighbourChunks(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ, Dimensions.NormalWorld);
                    RedrawClientColumns(game, job.ChunkX, job.ChunkZ, chunks.Length, spreadFaces);
                    DirectionalSunlight.QueueClientFlood(job);
                    return;
                }

                illuminator.SunlightFlood(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ);
                byte reconcileFaces = illuminator.SunLightFloodNeighbourChunks(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ, Dimensions.NormalWorld);
                RedrawClientColumns(game, job.ChunkX, job.ChunkZ, chunks.Length, reconcileFaces);
            }
            finally
            {
                SunlightRay.EndPass();
            }
        }

        internal static void ProcessServer(ServerSystemRelight system, ServerMain server)
        {
            if (!DirectionalSunlight.IsEnabled(EnumAppSide.Server))
            {
                return;
            }

            lock (server.WorldMap.LightingTasksLock)
            {
                if (server.WorldMap.LightingTasks.Count > 0)
                {
                    return;
                }
            }

            if (!DirectionalSunlight.TryDequeueServer(out DirectionalSunlightWork work, out DirectionalSunlightJob job) || !DirectionalSunlight.IsCurrentServer(job))
            {
                return;
            }

            IWorldChunk[] chunks = GetServerColumn(server, job.ChunkX, job.ChunkZ);
            if (chunks == null)
            {
                return;
            }

            SunlightRay.BeginPass(job.PhaseDays);
            try
            {
                if (work == DirectionalSunlightWork.Prepare)
                {
                    DirectionalSunlightSeeder.Apply(system.chunkIlluminator, chunks, job.ChunkX, job.ChunkZ, job.PhaseDays);
                    system.chunkIlluminator.SunlightFlood(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ);
                    system.chunkIlluminator.SunLightFloodNeighbourChunks(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ, Dimensions.NormalWorld);
                    DirectionalSunlight.QueueServerFlood(job);
                    return;
                }

                system.chunkIlluminator.SunlightFlood(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ);
                system.chunkIlluminator.SunLightFloodNeighbourChunks(chunks, job.ChunkX, chunks.Length - 1, job.ChunkZ, Dimensions.NormalWorld);
            }
            finally
            {
                SunlightRay.EndPass();
            }
        }

        private static ClientChunk[] GetClientColumn(ClientMain game, int chunkX, int chunkZ)
        {
            int height = game.WorldMap.ChunkMapSizeY;
            if (clientColumn == null || clientColumn.Length != height)
            {
                clientColumn = new ClientChunk[height];
            }

            for (int y = 0; y < height; y++)
            {
                ClientChunk chunk = game.WorldMap.GetChunk(chunkX, y, chunkZ) as ClientChunk;
                if (chunk == null)
                {
                    return null;
                }

                chunk.Unpack();
                clientColumn[y] = chunk;
            }

            return clientColumn;
        }

        private static IWorldChunk[] GetServerColumn(ServerMain server, int chunkX, int chunkZ)
        {
            int height = server.WorldMap.ChunkMapSizeY;
            if (serverColumn == null || serverColumn.Length != height)
            {
                serverColumn = new IWorldChunk[height];
            }

            for (int y = 0; y < height; y++)
            {
                IWorldChunk chunk = server.WorldMap.GetServerChunk(chunkX, y, chunkZ);
                if (chunk == null)
                {
                    return null;
                }

                chunk.Unpack();
                serverColumn[y] = chunk;
            }

            return serverColumn;
        }

        private static void RedrawClientColumns(ClientMain game, int chunkX, int chunkZ, int height, byte spreadFaces)
        {
            fullRedraw ??= new HashSet<long>();
            edgeRedraw ??= new HashSet<long>();
            fullRedraw.Clear();
            edgeRedraw.Clear();

            AddColumn(game.WorldMap, fullRedraw, chunkX, chunkZ, height);

            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                if ((spreadFaces & facing.Flag) != 0)
                {
                    AddColumn(game.WorldMap, fullRedraw, chunkX + facing.Normali.X, chunkZ + facing.Normali.Z, height);
                }
            }

            foreach (long chunkIndex in fullRedraw)
            {
                // do not let the hourly pass clog the queue block edits use
                game.WorldMap.SetChunkDirty(chunkIndex, false, false);
            }

            foreach (long chunkIndex in fullRedraw)
            {
                ChunkPos pos = game.WorldMap.ChunkPosFromChunkIndex3D(chunkIndex);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0)
                            {
                                continue;
                            }

                            int cy = pos.InternalY + dy;
                            if (!BlockAccessorMovable.ChunkCoordsInSameDimension(pos.InternalY, cy) || !game.WorldMap.IsValidChunkPos(pos.X + dx, cy, pos.Z + dz))
                            {
                                continue;
                            }

                            long neighbourIndex = game.WorldMap.ChunkIndex3D(pos.X + dx, cy, pos.Z + dz);
                            if (!fullRedraw.Contains(neighbourIndex))
                            {
                                edgeRedraw.Add(neighbourIndex);
                            }
                        }
                    }
                }
            }

            foreach (long chunkIndex in edgeRedraw)
            {
                ClientChunk chunk = game.WorldMap.GetChunk(chunkIndex) as ClientChunk;
                bool hasDoor = false;

                if (chunk != null)
                {
                    foreach (BlockEntity blockEntity in chunk.BlockEntities.Values)
                    {
                        if (blockEntity.GetBehavior<BEBehaviorDoor>() != null || blockEntity.GetBehavior<BEBehaviorTrapDoor>() != null)
                        {
                            hasDoor = true;
                            break;
                        }
                    }
                }

                game.WorldMap.SetChunkDirty(chunkIndex, false, false, !hasDoor);
            }
        }

        private static void AddColumn(ClientWorldMap worldMap, HashSet<long> chunks, int chunkX, int chunkZ, int height)
        {
            int lowestChunkY = SunlightRay.GetLowestSurfaceChunk(worldMap.GetMapChunk(chunkX, chunkZ), GlobalConstants.ChunkSize);
            for (int y = lowestChunkY; y < height; y++)
            {
                if (worldMap.IsValidChunkPos(chunkX, y, chunkZ) && worldMap.GetChunk(chunkX, y, chunkZ) != null)
                {
                    chunks.Add(worldMap.ChunkIndex3D(chunkX, y, chunkZ));
                }
            }
        }
    }
}
