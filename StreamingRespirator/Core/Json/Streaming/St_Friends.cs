using Newtonsoft.Json;

namespace StreamingRespirator.Core.Json.Streaming
{
    internal class St_Friends
    {
        [JsonProperty("friends")]
        public long[] Friends { get; set; }
    }
}
