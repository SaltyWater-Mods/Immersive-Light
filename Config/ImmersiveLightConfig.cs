using ProtoBuf;

namespace ImmersiveLight
{
    internal class ImmersiveLightConfig
    {
        public bool EnableDirectionalSunlight = false;
    }

    [ProtoContract]
    internal class DirectionalSunlightConfigPacket
    {
        [ProtoMember(1)]
        public bool Enabled;
    }
}
