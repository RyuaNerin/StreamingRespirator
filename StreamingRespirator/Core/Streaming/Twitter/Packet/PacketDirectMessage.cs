using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter.Packet
{
    [DebuggerDisplay("{Item.Sender.ScreenName} -> {Item.Recipient.ScreenName} : {Item.Text}")]
    internal class PacketDirectMessage : IPacket
    {
        [JsonProperty("direct_message")]
        public PacketDirectMessageItem Item { get; set; } = new PacketDirectMessageItem();

        public override string ToString()
            => $"{this.Item.Sender.ScreenName} -> {this.Item.Recipient.ScreenName} : {this.Item.Text}";
    }

    internal class PacketDirectMessageItem
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("id_str")]
        public string IdStr { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [JsonProperty("sender")]
        public TwitterUser Sender { get; set; }

        [JsonProperty("sender_id")]
        public long SenderId { get; set; }

        [JsonProperty("sender_screen_name")]
        public string SenderScreenName { get; set; }
        
        [JsonProperty("recipient")]
        public TwitterUser Recipient { get; set; }

        [JsonProperty("recipient_id")]
        public long RecipientId { get; set; }

        [JsonProperty("recipient_screen_name")]
        public string RecipientScreenName { get; set; }

    }
}