using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter.Packet
{
    internal class PacketFriends : IPacket
    {
        [JsonProperty("friends")]
        public long[] Friends { get; set; }
    }
}
