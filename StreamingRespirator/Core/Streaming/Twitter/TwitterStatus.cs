using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Streaming.Twitter
{
    [DebuggerDisplay("{Count}")]
    internal class TwitterStatusList : List<TwitterStatus>
    {
    }

    [DebuggerDisplay("{Id} | @{User.ScreenName}: {Text}")]
    internal class TwitterStatus : IPacket
    {
        [JsonProperty("display_text_range")]
        public int[] DisplayTextRange { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("user", Required = Required.Always)]
        public TwitterUser User { get; set; }

        [JsonIgnore]
        public string Text
            => (
                this.AdditionalData.ContainsKey("full_text")
                ? this.AdditionalData["full_text"]
                : (
                    this.AdditionalData.ContainsKey("text")
                    ? this.AdditionalData["text"]
                    : null
                    )
                )?.Value<string>().Replace("\n", "");

        [JsonProperty("retweeted_status", NullValueHandling = NullValueHandling.Ignore)]
        public TwitterStatus RetweetedStatus { get; set; }

        [JsonProperty("quoted_status", NullValueHandling = NullValueHandling.Ignore)]
        public TwitterStatus QuotedStatus { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        public override string ToString()
            => $"{this.Id} | @{this.User.ScreenName}: {this.Text}";
    }
}
