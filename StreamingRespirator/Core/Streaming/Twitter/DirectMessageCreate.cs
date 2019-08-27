using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Streaming.Twitter
{
    [DebuggerDisplay("{Data}")]
    public class DirectMessageNew
    {
        [JsonProperty("event")]
        public Event Data { get; set; } = new Event();

        [DebuggerDisplay("{MessagCreate}")]
        public class Event
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("message_create")]
            public MessagCreate MessageCreate { get; set; } = new MessagCreate();
        }

        [DebuggerDisplay("{Target} : {MessageData}")]
        public class MessagCreate
        {
            [JsonProperty("target")]
            public Target Target { get; set; } = new Target();

            [JsonProperty("message_data")]
            public MessageData MessageData { get; set; } = new MessageData();
        }

        [DebuggerDisplay("{RecipientId}")]
        public class Target
        {
            [JsonProperty("recipient_id")]
            public string RecipientId { get; set; }
        }

        [DebuggerDisplay("{Text}")]
        public class MessageData
        {
            [JsonProperty("text")]
            public string Text { get; set; }
        }
    }
}
