using System;
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
        private const string ConfigFileName = "ImmersiveLight.json";
        private const string HarmonyId = "saltywater.immersivelight";
        private const string NetworkChannelName = "immersivelight";
        private Harmony harmony;
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private IServerNetworkChannel serverChannel;
        private ImmersiveLightConfig config;
        private ImmersiveLightDebugRenderer debugRenderer;
        private long clientSunTick;
        private long serverSunTick;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel(NetworkChannelName).RegisterMessageType<DirectionalSunlightConfigPacket>();

            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            DirectionalSunlight.Configure(EnumAppSide.Client, false);
            api.Network.GetChannel(NetworkChannelName).SetMessageHandler<DirectionalSunlightConfigPacket>(OnSunlightConfig);
            api.Event.LeaveWorld += OnLeaveWorld;
            clientSunTick = api.Event.RegisterGameTickListener(_ => DirectionalSunlight.UpdatePhase(api.World), 1000);

            debugRenderer = new ImmersiveLightDebugRenderer(api);
            api.Event.RegisterRenderer(debugRenderer, EnumRenderStage.AfterFinalComposition, "immersivelight-debug");
            ImmersiveLightDebugCommands.Register(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            config = LoadConfig(api);
            serverChannel = api.Network.GetChannel(NetworkChannelName);
            DirectionalSunlight.Configure(EnumAppSide.Server, config.EnableDirectionalSunlight);
            api.Event.PlayerJoin += OnPlayerJoin;
            serverSunTick = api.Event.RegisterGameTickListener(_ => DirectionalSunlight.UpdatePhase(api.World), 1000);
        }

        private static ImmersiveLightConfig LoadConfig(ICoreServerAPI api)
        {
            try
            {
                ImmersiveLightConfig loaded = api.LoadModConfig<ImmersiveLightConfig>(ConfigFileName);
                if (loaded != null)
                {
                    return loaded;
                }

                loaded = new ImmersiveLightConfig();
                api.StoreModConfig(loaded, ConfigFileName);
                return loaded;
            }
            catch (Exception exception)
            {
                api.Logger.Error("Could not load or create {0}, using defaults. {1}", ConfigFileName, exception);
                return new ImmersiveLightConfig();
            }
        }

        private void OnSunlightConfig(DirectionalSunlightConfigPacket packet)
        {
            DirectionalSunlight.Configure(EnumAppSide.Client, packet.Enabled);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            serverChannel.SendPacket(new DirectionalSunlightConfigPacket
            {
                Enabled = config.EnableDirectionalSunlight
            }, player);
        }

        private static void OnLeaveWorld()
        {
            DirectionalSunlight.Configure(EnumAppSide.Client, false);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);

            if (capi != null)
            {
                capi.Event.LeaveWorld -= OnLeaveWorld;
                DirectionalSunlight.Configure(EnumAppSide.Client, false);
            }

            if (sapi != null)
            {
                sapi.Event.PlayerJoin -= OnPlayerJoin;
                DirectionalSunlight.Configure(EnumAppSide.Server, false);
            }

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
