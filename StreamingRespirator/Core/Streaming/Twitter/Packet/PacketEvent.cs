using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter.Packet
{
    internal abstract class PacketEvent : IPacket
    {
        [JsonProperty("event")]
        public abstract string Event { get; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("target")]
        public object Target { get; set; }

        [JsonProperty("source")]
        public object Source { get; set; }
    }

    [DebuggerDisplay("UserUpdated : {this.Target?.ScreenName}")]
    internal class PacketUserUpdated : PacketEvent
    {
        public override string Event => "user_update";
    }
}
