using System;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Twitter.Streaming
{
    internal class StreamingEvent
    {
        [JsonProperty("target")]
        public object Target { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("event")]
        public string Event { get; set; }
                
        [JsonProperty("source")]
        public object Source { get; set; }
    }
}
