using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter.Packet
{
    [DebuggerDisplay("{Delete.Status.UserId} / {Delete.Status.Id}")]
    internal class PacketDelete : IPacket
    {
        [JsonProperty("delete")]
        public DeleteItem Delete { get; } = new DeleteItem();
        
        public class DeleteItem
        {
            [JsonProperty("status")]
            public StatusItem Status { get; } = new StatusItem();
        }

        public class StatusItem
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("id_str")]
            public string IdStr => this.Id.ToString();

            [JsonProperty("user_id")]
            public long UserId { get; set; }

            [JsonProperty("user_id_str")]
            public string UserIdStr => this.UserId.ToString();
        }
    }
}
