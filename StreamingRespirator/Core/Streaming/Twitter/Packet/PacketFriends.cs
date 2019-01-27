using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter.Packet
{
    [DebuggerDisplay("Friends : {this.Friends?.Length}")]
    internal class PacketFriends : IPacket
    {
        [JsonProperty("friends")]
        public long[] Friends { get; set; }

        public override string ToString()
            => $"Friends : {this.Friends?.Length}";
    }
}
