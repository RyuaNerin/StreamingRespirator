using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core.Twitter.Streaming
{
    internal class StreamingFriends : ITwitter
    {
        [JsonProperty("friends")]
        public long[] Friends { get; set; }
    }
}
