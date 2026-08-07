using HarmonyLib;
using ImmersiveLight.Lighting;
using Vintagestory.Client.NoObf;
using Vintagestory.Server;

namespace ImmersiveLight.Patches
{
    [HarmonyPatch(typeof(ClientSystemRelight), nameof(ClientSystemRelight.OnSeperateThreadGameTick))]
    internal static class ClientDirectionalSunlightRelightPatch
    {
        private static readonly AccessTools.FieldRef<ClientSystem, ClientMain> Game = AccessTools.FieldRefAccess<ClientSystem, ClientMain>("game");

        private static void Postfix(ClientSystemRelight __instance)
        {
            DirectionalSunlightRelighter.ProcessClient(__instance, Game(__instance));
        }
    }

    [HarmonyPatch(typeof(ServerSystemRelight), nameof(ServerSystemRelight.OnSeparateThreadTick))]
    internal static class ServerDirectionalSunlightRelightPatch
    {
        private static readonly AccessTools.FieldRef<ServerSystem, ServerMain> Server = AccessTools.FieldRefAccess<ServerSystem, ServerMain>("server");

        private static void Postfix(ServerSystemRelight __instance)
        {
            DirectionalSunlightRelighter.ProcessServer(__instance, Server(__instance));
        }
    }
}
