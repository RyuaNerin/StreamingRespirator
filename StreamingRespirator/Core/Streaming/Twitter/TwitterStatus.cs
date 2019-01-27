using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Twitter
{
    [DebuggerDisplay("{Count}")]
    internal class TwitterStatusList : List<TwitterStatus>
    {
    }

    [DebuggerDisplay("{Id} | @{User.ScreenName}: {Text}")]
    internal class TwitterStatus
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("user")]
        public TwitterUser User { get; set; }

        [JsonIgnore]
        public string Text => ((this.AdditionalData["full_text"] ?? this.AdditionalData["text"]).Value<string>())?.Replace("\n", "");

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        public override string ToString()
            => $"{this.Id} | @{this.User.ScreenName}: {this.Text}";
    }
}
