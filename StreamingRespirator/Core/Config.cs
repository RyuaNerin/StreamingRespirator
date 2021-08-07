using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal class Config
    {
        public static Config Instance { get; private set; }

        public static void Load()
        {
            try
            {
                using (var file = File.OpenRead(Program.ConfigPath))
                using (var reader = new StreamReader(file, Encoding.UTF8))
                {
                    var inst = new Config();
                    Program.JsonSerializer.Populate(reader, inst);
                    Instance = inst;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            Instance = Instance ?? new Config();

            TwitterClientFactory.SetInstances(Instance.Accounts);

            Loaded = true;
        }

        private static volatile bool Loaded;
        public static void Save()
        {
            if (!Loaded)
                return;

            TwitterClientFactory.CopyInstances(Instance.Accounts);

            try
            {
                using (var file = File.OpenWrite(Program.ConfigPath))
                using (var writer = new StreamWriter(file, Encoding.UTF8))
                {
                    Program.JsonSerializer.Serialize(writer, Instance);
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        private Config()
        {
        }

        [JsonIgnore]
        public object Lock { get; } = new object();

        [JsonProperty("accounts")]
        public TwitterCredentialList Accounts { get; } = new TwitterCredentialList();

        [JsonProperty("startup")]
        public bool StartWithWindows { get; set; } = false;

        [JsonProperty("reduce_api_call")]
        public bool ReduceApiCall { get; set; } = false;

        [JsonProperty("filter")]
        public ConfigFilter Filter { get; } = new ConfigFilter();
        public class ConfigFilter
        {
            [JsonProperty("retweeted")]
            public bool ShowRetweetedMyStatus { get; set; } = true;

            [JsonProperty("retweet_with_comment")]
            public bool ShowRetweetWithComment { get; set; } = true;

            [JsonProperty("my_retweet")]
            public bool ShowMyRetweet { get; set; } = true;
        }

        [JsonProperty("proxy")]
        public ConfigProxy Proxy { get; } = new ConfigProxy();
        public class ConfigProxy
        {
            [JsonProperty("port")]
            public int Port { get; set; } = 8811;

            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("pw")]
            public string Pw { get; set; }
        }
    }
}
