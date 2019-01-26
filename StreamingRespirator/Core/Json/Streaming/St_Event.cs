using System;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Json.Streaming
{
    internal class St_Event
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
