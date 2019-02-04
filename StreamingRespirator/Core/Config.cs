using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal static class Config
    {
        public static bool StartWithWindows { get; set; }

        public static ConfigFilter Filter { get; } = new ConfigFilter();
        public class ConfigFilter
        {
            [JsonProperty("retweeted")]
            public bool ShowRetweetedMyStatus { get; set; } = true;

            [JsonProperty("my_retweet")]
            public bool ShowMyRetweet { get; set; } = true;
        }

        public static void Load()
        {
            try
            {
                using (var file   = File.OpenRead(Program.ConfigPath))
                using (var reader = new StreamReader(file, Encoding.UTF8))
                {
                    new JsonSerializer().Populate(reader, new ConfigInstance());
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                using (var file   = File.OpenWrite(Program.ConfigPath))
                using (var writer = new StreamWriter(file, Encoding.UTF8))
                {
                    new JsonSerializer().Serialize(writer, new ConfigInstance());
                }
            }
            catch
            {
            }
        }

        private class ConfigInstance
        {
            [JsonProperty("accounts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public static IEnumerable<TwitterCredential> Accounts
            {
                get => TwitterClientFactory.Accounts;
                set => TwitterClientFactory.Accounts = value;
            }

            [JsonProperty("filter")]
            public static ConfigFilter Filter => Config.Filter;

            [JsonProperty("startup")]
            public static bool StartWithWindows
            {
                get => Config.StartWithWindows;
                set => Config.StartWithWindows = value;
            }
        }
    }
}
