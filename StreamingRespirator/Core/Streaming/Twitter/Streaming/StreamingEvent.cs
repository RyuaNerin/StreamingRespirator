using System;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core.Twitter.Streaming
{
    internal class StreamingEvent : IStreamingData
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
