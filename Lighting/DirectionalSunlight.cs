using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace ImmersiveLight.Lighting
{
    internal enum DirectionalSunlightWork
    {
        Prepare,
        Flood
    }

    internal readonly struct DirectionalSunlightJob
    {
        internal readonly int ChunkX;
        internal readonly int ChunkZ;
        internal readonly int Generation;
        internal readonly double PhaseDays;

        internal DirectionalSunlightJob(int chunkX, int chunkZ, int generation, double phaseDays)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Generation = generation;
            PhaseDays = phaseDays;
        }
    }

    internal static class DirectionalSunlight
    {
        private sealed class PhaseState
        {
            internal readonly int Phase;
            internal readonly int Generation;
            internal readonly double PhaseDays;

            internal PhaseState(int phase, int generation, double phaseDays)
            {
                Phase = phase;
                Generation = generation;
                PhaseDays = phaseDays;
            }
        }

        private static readonly object QueueLock = new();
        private static readonly Queue<DirectionalSunlightJob> ClientPrepareColumns = new();
        private static readonly Queue<DirectionalSunlightJob> ClientFloodColumns = new();
        private static readonly Queue<DirectionalSunlightJob> ServerPrepareColumns = new();
        private static readonly Queue<DirectionalSunlightJob> ServerFloodColumns = new();
        private static bool clientEnabled;
        private static bool serverEnabled;

        // calendar and relight run on different threads publish hour as one value so relight thread cant mixx phase fields
        private static PhaseState clientState;
        private static PhaseState serverState;

        [ThreadStatic]
        private static Vec3d sunPosition;

        internal static void UpdatePhase(IWorldAccessor world)
        {
            bool client = world.Side == EnumAppSide.Client;
            if (!IsEnabled(client))
            {
                return;
            }

            IGameCalendar calendar = world.Calendar;
            IBlockAccessor blockAccessor = world.BlockAccessor;
            if (calendar == null || blockAccessor == null)
            {
                return;
            }

            int nextPhase = (int)Math.Floor(calendar.TotalHours);
            PhaseState state = GetState(client);
            if (state?.Phase == nextPhase)
            {
                return;
            }

            lock (QueueLock)
            {
                state = GetState(client);
                if (state?.Phase == nextPhase)
                {
                    return;
                }

                int chunkMapSizeX = blockAccessor.MapSizeX / GlobalConstants.ChunkSize;
                PhaseState nextState = new(
                    nextPhase,
                    (state?.Generation ?? 0) + 1,
                    (nextPhase + 0.5) / calendar.HoursPerDay
                );
                SetState(client, nextState);

                Queue<DirectionalSunlightJob> prepare = client ? ClientPrepareColumns : ServerPrepareColumns;
                Queue<DirectionalSunlightJob> flood = client ? ClientFloodColumns : ServerFloodColumns;
                prepare.Clear();
                flood.Clear();

                long[] loadedColumns = world.LoadedMapChunkIndices;
                if (client && world is IClientWorldAccessor clientWorld && clientWorld.Player?.Entity != null)
                {
                    int playerChunkX = (int)clientWorld.Player.Entity.Pos.X / GlobalConstants.ChunkSize;
                    int playerChunkZ = (int)clientWorld.Player.Entity.Pos.Z / GlobalConstants.ChunkSize;
                    Array.Sort(loadedColumns, (a, b) =>
                    {
                        long ax = a % chunkMapSizeX - playerChunkX;
                        long az = a / chunkMapSizeX - playerChunkZ;
                        long bx = b % chunkMapSizeX - playerChunkX;
                        long bz = b / chunkMapSizeX - playerChunkZ;
                        return (ax * ax + az * az).CompareTo(bx * bx + bz * bz);
                    });
                }

                for (int i = 0; i < loadedColumns.Length; i++)
                {
                    long index = loadedColumns[i];
                    prepare.Enqueue(new DirectionalSunlightJob(
                        (int)(index % chunkMapSizeX),
                        (int)(index / chunkMapSizeX),
                        nextState.Generation,
                        nextState.PhaseDays
                    ));
                }
            }
        }

        internal static void Configure(EnumAppSide side, bool enabled)
        {
            bool client = side == EnumAppSide.Client;
            lock (QueueLock)
            {
                if (client)
                {
                    Volatile.Write(ref clientEnabled, enabled);
                    ClientPrepareColumns.Clear();
                    ClientFloodColumns.Clear();
                }
                else
                {
                    Volatile.Write(ref serverEnabled, enabled);
                    ServerPrepareColumns.Clear();
                    ServerFloodColumns.Clear();
                }

                SetState(client, null);
            }
        }

        internal static bool IsEnabled(IBlockAccessor blockAccessor)
        {
            IWorldAccessor world = ChunkIlluminatorAccess.WorldAccessor((BlockAccessorBase)blockAccessor);
            return IsEnabled(world.Side == EnumAppSide.Client);
        }

        internal static bool IsEnabled(EnumAppSide side)
        {
            return IsEnabled(side == EnumAppSide.Client);
        }

        internal static Vec3f GetSunDirection(IBlockAccessor blockAccessor, int x, int y, int z)
        {
            if (!TryGetCurrentPhaseDays(blockAccessor, out double phaseDays))
            {
                return null;
            }

            return GetSunDirection(blockAccessor, x, y, z, phaseDays);
        }

        internal static Vec3f GetSunDirection(IBlockAccessor blockAccessor, int x, int y, int z, double phaseDays)
        {
            IWorldAccessor world = ChunkIlluminatorAccess.WorldAccessor((BlockAccessorBase)blockAccessor);
            if (world.Calendar == null)
            {
                return null;
            }

            sunPosition ??= new Vec3d();
            return world.Calendar.GetSunPosition(sunPosition.Set(x + 0.5, y + 0.5, z + 0.5), phaseDays);
        }

        internal static bool TryGetCurrentPhaseDays(IBlockAccessor blockAccessor, out double phaseDays)
        {
            IWorldAccessor world = ChunkIlluminatorAccess.WorldAccessor((BlockAccessorBase)blockAccessor);
            PhaseState state = GetState(world.Side == EnumAppSide.Client);
            if (state != null)
            {
                phaseDays = state.PhaseDays;
                return true;
            }

            IGameCalendar calendar = world.Calendar;
            if (calendar == null)
            {
                phaseDays = 0;
                return false;
            }

            int phase = (int)Math.Floor(calendar.TotalHours);
            phaseDays = (phase + 0.5) / calendar.HoursPerDay;
            return true;
        }

        internal static bool TryDequeueClient(out DirectionalSunlightWork work, out DirectionalSunlightJob job)
        {
            lock (QueueLock)
            {
                if (ClientPrepareColumns.Count > 0)
                {
                    work = DirectionalSunlightWork.Prepare;
                    job = ClientPrepareColumns.Dequeue();
                    return true;
                }

                if (ClientFloodColumns.Count > 0)
                {
                    work = DirectionalSunlightWork.Flood;
                    job = ClientFloodColumns.Dequeue();
                    return true;
                }
            }

            work = default;
            job = default;
            return false;
        }

        internal static bool TryDequeueServer(out DirectionalSunlightWork work, out DirectionalSunlightJob job)
        {
            lock (QueueLock)
            {
                if (ServerPrepareColumns.Count > 0)
                {
                    work = DirectionalSunlightWork.Prepare;
                    job = ServerPrepareColumns.Dequeue();
                    return true;
                }

                if (ServerFloodColumns.Count > 0)
                {
                    work = DirectionalSunlightWork.Flood;
                    job = ServerFloodColumns.Dequeue();
                    return true;
                }
            }

            work = default;
            job = default;
            return false;
        }

        internal static bool IsCurrentClient(DirectionalSunlightJob job)
        {
            return IsEnabled(true) && GetState(true)?.Generation == job.Generation;
        }

        internal static bool IsCurrentServer(DirectionalSunlightJob job)
        {
            return IsEnabled(false) && GetState(false)?.Generation == job.Generation;
        }

        internal static void QueueClientFlood(DirectionalSunlightJob job)
        {
            lock (QueueLock)
            {
                if (IsEnabled(true) && GetState(true)?.Generation == job.Generation)
                {
                    ClientFloodColumns.Enqueue(job);
                }
            }
        }

        internal static void QueueServerFlood(DirectionalSunlightJob job)
        {
            lock (QueueLock)
            {
                if (IsEnabled(false) && GetState(false)?.Generation == job.Generation)
                {
                    ServerFloodColumns.Enqueue(job);
                }
            }
        }

        private static PhaseState GetState(bool client)
        {
            return client ? Volatile.Read(ref clientState) : Volatile.Read(ref serverState);
        }

        private static bool IsEnabled(bool client)
        {
            return client ? Volatile.Read(ref clientEnabled) : Volatile.Read(ref serverEnabled);
        }

        private static void SetState(bool client, PhaseState state)
        {
            if (client)
            {
                Volatile.Write(ref clientState, state);
            }
            else
            {
                Volatile.Write(ref serverState, state);
            }
        }
    }
}
