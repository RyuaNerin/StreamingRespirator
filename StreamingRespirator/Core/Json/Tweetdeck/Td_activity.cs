using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

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

    [DebuggerDisplay("{Id} / {Text}")]
    internal class Td_activity_item_targets : JExpendo
    {
        [JsonIgnore]
        public long Id => (long)(this["id"] ?? 0);

        [JsonIgnore]
        public string Text => ((this["full_text"] ?? this["text"]) as string)?.Replace("\n", "");
    }
}
