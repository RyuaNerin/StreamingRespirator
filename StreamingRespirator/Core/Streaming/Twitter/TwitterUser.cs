using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamingRespirator.Core.Streaming.Twitter
{
    [DebuggerDisplay("{Id} = {ScreenName}")]
    internal class TwitterUser : IEquatable<TwitterUser>
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty("screen_name", Required = Required.Always)]
        public string ScreenName { get; set; }

        [JsonProperty("profile_image_url")]
        public string ProfileFimageUrl { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        public override string ToString()
            => $"{this.Id} | {this.ScreenName}";

        public override bool Equals(object other)
            => other != null && other is TwitterUser u ? this.Equals(u) : false;

        bool IEquatable<TwitterUser>.Equals(TwitterUser other)
            => this.Id == other.Id;

        public override int GetHashCode()
            => this.Id.GetHashCode();
    }
}
