using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Json.Tweetdeck
{
    [DebuggerDisplay("{Count}")]
    internal class Td_statuses : List<TwitterStatus>
    {
    }
}
