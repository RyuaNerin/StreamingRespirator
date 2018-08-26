using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StreamingRespirator.Core.Json.Tweetdeck
{
    [DebuggerDisplay("{Count}")]
    internal class Td_statuses : List<Td_statuses_status>
    {
    }

    [DebuggerDisplay("{Id} / {Text}")]
    internal class Td_statuses_status : JExpendo
    {
        [JsonIgnore]
        public long Id => (long)(this["id"] ?? 0);

        [JsonIgnore]
        public string Text => ((this["full_text"] ?? this["text"]) as string)?.Replace("\n", "");
    }
}
