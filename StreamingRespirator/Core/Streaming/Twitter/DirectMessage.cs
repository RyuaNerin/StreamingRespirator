using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter
{
    [DebuggerDisplay("Item")]
    internal class DirectMessage
    {
        [JsonProperty("user_inbox")]
        public DirectMessageItem UserInbox { get; set; }

        [JsonProperty("user_events")]
        public DirectMessageItem UserEvents { get; set; }

        [JsonIgnore]
        public DirectMessageItem Item => this.UserEvents ?? this.UserInbox;
    }

    [DebuggerDisplay("Entries : {Entries?.Length}")]
    internal class DirectMessageItem
    {
        [JsonProperty("conversations")]
        public object Conversations { get; set; }

        [JsonProperty("cursor")]
        public string Cursor { get; set; }

        [JsonProperty("entries")]
        public DirectMessageEntry[] Entries { get; set; }

        [JsonProperty("users")]
        public Dictionary<string, TwitterUser> Users { get; set; }
    }

    [DebuggerDisplay("{Message}")]
    internal class DirectMessageEntry
    {
        [JsonProperty("message")]
        public DirectMessageMessageData Message { get; set; }
    }

    [DebuggerDisplay("{Data}")]
    internal class DirectMessageMessageData
    {
        [JsonProperty("message_data")]
        public DirectMessageMessage Data { get; set; }
    }

    [DebuggerDisplay("{Sender_Id} > {Recipiend_Id} : {Id} / {Text}")]
    internal class DirectMessageMessage
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonIgnore]
        public DateTime CreatedAt => new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(this.Time);

        [JsonProperty("recipient_id")]
        public string Recipiend_Id { get; set; }

        [JsonProperty("sender_id")]
        public string Sender_Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
