using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal class Config
    {
        public static Config Instance { get; }

        static Config()
        {
            try
            {
                using (var file = File.OpenRead(Program.ConfigPath))
                using (var reader = new StreamReader(file, Encoding.UTF8))
                {
                    var inst = new Config();
                    new JsonSerializer().Populate(reader, inst);
                    Instance = inst;
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);

                Instance = new Config();
            }

            TwitterClientFactory.SetInstances(Instance.Accounts);

            Interlocked.Exchange(ref Load, 1);
        }

        private static long Load = 0;
        public static void Save()
        {
            if (Interlocked.Read(ref Load) == 0)
                return;

            TwitterClientFactory.CopyInstances(Instance.Accounts);

            try
            {
                using (var file = File.OpenWrite(Program.ConfigPath))
                using (var writer = new StreamWriter(file, Encoding.UTF8))
                {
                    new JsonSerializer().Serialize(writer, Instance);
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }


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
        }
    }
}
