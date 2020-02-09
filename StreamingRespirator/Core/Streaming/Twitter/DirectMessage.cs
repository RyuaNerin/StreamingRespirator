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
        public UserItem UserInbox { get; set; }

        [JsonProperty("user_events")]
        public UserItem UserEvents { get; set; }

        [JsonIgnore]
        public UserItem Items => this.UserEvents ?? this.UserInbox;

        [DebuggerDisplay("Entries : {Entries?.Length}")]
        public class UserItem
        {
            [JsonProperty("conversations")]
            public object Conversations { get; set; }

            [JsonProperty("cursor")]
            public string Cursor { get; set; }

            [JsonProperty("entries")]
            public Entry[] Entries { get; set; }

            [JsonProperty("users")]
            public Dictionary<string, TwitterUser> Users { get; set; }
        }

        [DebuggerDisplay("{Message}")]
        public class Entry
        {
            [JsonProperty("message")]
            public Message Message { get; set; }
        }

        [DebuggerDisplay("{Data}")]
        public class Message
        {
            [JsonProperty("message_data")]
            public MessageData Data { get; set; }
        }

        [DebuggerDisplay("{Sender_Id} > {Recipiend_Id} : {Id} / {Text}")]
        public class MessageData
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
}
