using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Json.Tweetdeck
{
    [DebuggerDisplay("Item")]
    internal class Td_dm
    {
        [JsonProperty("user_inbox")]
        public Td_dm_item UserInbox { get; set; }

        [JsonProperty("user_events")]
        public Td_dm_item UserEvents { get; set; }

        [JsonIgnore]
        public Td_dm_item Item => this.UserEvents ?? this.UserInbox;
    }

    [DebuggerDisplay("Entries : {Entries?.Length}")]
    internal class Td_dm_item
    {
        [JsonProperty("conversations")]
        public object Conversations { get; set; }

        [JsonProperty("cursor")]
        public string Cursor { get; set; }

        [JsonProperty("entries")]
        public Td_dm_item_entry[] Entries { get; set; }

        [JsonProperty("users")]
        public Dictionary<string, TwitterUser> Users { get; set; }
    }

    [DebuggerDisplay("{Message}")]
    internal class Td_dm_item_entry
    {
        [JsonProperty("message")]
        public Td_dm_item_entry_message Message { get; set; }
    }

    [DebuggerDisplay("{Data}")]
    internal class Td_dm_item_entry_message
    {
        [JsonProperty("message_data")]
        public Td_dm_item_entry_data Data { get; set; }
    }

    [DebuggerDisplay("{Sender_Id} > {Recipiend_Id} : {Id} / {Text}")]
    internal class Td_dm_item_entry_data
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonIgnore]
        public DateTime CreatedAt => new DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(this.Time);

        [JsonProperty("recipient_id")]
        public string Recipiend_Id { get; set; }

        [JsonProperty("sender_id")]
        public string Sender_Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
