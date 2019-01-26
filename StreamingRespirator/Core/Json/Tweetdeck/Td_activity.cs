using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Json.Tweetdeck
{
    [DebuggerDisplay("{Count}")]
    internal class Td_activity : List<Td_activity_item>
    {
    }

    [DebuggerDisplay("{Action} : {Targets.Count}")]
    internal class Td_activity_item
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("max_position")]
        public long MaxPosition { get; set; }

        [JsonProperty("targets")]
        public Td_activity_item_targets[] Targets { get; set; }
    }

    [DebuggerDisplay("{Id} | @{User.ScreenName}: {Text}")]
    internal class Td_activity_item_targets
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
