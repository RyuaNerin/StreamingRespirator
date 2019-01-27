using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Twitter.Tweetdeck
{
    [DebuggerDisplay("{Count}")]
    internal class TwitterStatusList : List<TwitterStatus>
    {
    }
}
