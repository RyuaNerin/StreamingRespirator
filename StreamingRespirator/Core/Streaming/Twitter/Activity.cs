using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Streaming.Twitter
{
    [DebuggerDisplay("{Count}")]
    internal class ActivityList : List<Activity>
    {
    }

    [DebuggerDisplay("{Action} : {Targets.Count}")]
    internal class Activity
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("max_position")]
        public long MaxPosition { get; set; }

        [JsonProperty("sources", NullValueHandling = NullValueHandling.Ignore)]
        public TwitterUser[] Sources { get; set; }

        [JsonProperty("targets", NullValueHandling = NullValueHandling.Ignore)]
        public JObject[] Targets { get; set; }

        [JsonProperty("target_objects")]
        public TwitterStatus[] TargetObjects { get; set; }
    }
}
