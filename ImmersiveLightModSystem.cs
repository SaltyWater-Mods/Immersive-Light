using HarmonyLib;
using ImmersiveLight.Debugging;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ImmersiveLight
{
    public sealed class ImmersiveLightModSystem : ModSystem
    {
        private const string HarmonyId = "saltywater.immersivelight";
        private Harmony harmony;
        private ICoreClientAPI capi;
        private ImmersiveLightDebugRenderer debugRenderer;

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            debugRenderer = new ImmersiveLightDebugRenderer(api);
            api.Event.RegisterRenderer(debugRenderer, EnumRenderStage.AfterFinalComposition, "immersivelight-debug");
            ImmersiveLightDebugCommands.Register(api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);

            if (debugRenderer != null)
            {
                capi?.Event.UnregisterRenderer(debugRenderer, EnumRenderStage.AfterFinalComposition);
                debugRenderer.Dispose();
                debugRenderer = null;
            }
        }
    }
}
