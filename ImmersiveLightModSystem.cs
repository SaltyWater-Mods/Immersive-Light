using HarmonyLib;
using ImmersiveLight.Debugging;
using ImmersiveLight.Lighting;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ImmersiveLight
{
    public sealed class ImmersiveLightModSystem : ModSystem
    {
        private const string HarmonyId = "saltywater.immersivelight";
        private Harmony harmony;
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private ImmersiveLightDebugRenderer debugRenderer;
        private long clientSunTick;
        private long serverSunTick;

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientSunTick = api.Event.RegisterGameTickListener(_ => DirectionalSunlight.UpdatePhase(api.World), 1000);

            debugRenderer = new ImmersiveLightDebugRenderer(api);
            api.Event.RegisterRenderer(debugRenderer, EnumRenderStage.AfterFinalComposition, "immersivelight-debug");
            ImmersiveLightDebugCommands.Register(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverSunTick = api.Event.RegisterGameTickListener(_ => DirectionalSunlight.UpdatePhase(api.World), 1000);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);

            if (clientSunTick != 0)
            {
                capi?.Event.UnregisterGameTickListener(clientSunTick);
            }

            if (serverSunTick != 0)
            {
                sapi?.Event.UnregisterGameTickListener(serverSunTick);
            }

            if (debugRenderer != null)
            {
                capi?.Event.UnregisterRenderer(debugRenderer, EnumRenderStage.AfterFinalComposition);
                debugRenderer.Dispose();
                debugRenderer = null;
            }
        }
    }
}
