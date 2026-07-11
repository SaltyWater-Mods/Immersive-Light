using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ImmersiveLight.Lighting
{
    [Flags]
    internal enum LightBlockerKind : byte
    {
        None = 0,
        Door = 1,
        Trapdoor = 2
    }

    // rays were touching a lot of blocks and asking every one for door or trap door.. gets expensive fast so this keeps a tiny blockid lookup ready

    internal static class LightBlockerCache
    {
        private static readonly ConditionalWeakTable<IList<Block>, LightBlockerKind[]> Cache = new();

        internal static LightBlockerKind[] Get(IList<Block> blockTypes)
        {
            return Cache.GetValue(blockTypes, Build);
        }

        private static LightBlockerKind[] Build(IList<Block> blockTypes)
        {
            LightBlockerKind[] blockers = new LightBlockerKind[blockTypes.Count];

            for (int i = 0; i < blockTypes.Count; i++)
            {
                Block block = blockTypes[i];
                if (block == null)
                {
                    continue;
                }

                LightBlockerKind kind = LightBlockerKind.None;

                if (block.GetBehavior<BlockBehaviorDoor>() != null || block is BlockMultiblock)
                {
                    kind |= LightBlockerKind.Door;
                }

                if (block.GetBehavior<BlockBehaviorTrapDoor>() != null)
                {
                    kind |= LightBlockerKind.Trapdoor;
                }

                blockers[i] = kind;
            }

            return blockers;
        }
    }
}
