using Newtonsoft.Json;

namespace StreamingRespirator.Core.Twitter.Streaming
{
    internal class StreamingFriends
    {
        [JsonProperty("friends")]
        public long[] Friends { get; set; }
    }
}
