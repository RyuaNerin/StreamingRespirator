using System;
using System.IO;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;

namespace StreamingRespirator.Core
{
    internal static class GithubLatestRelease
    {
        public static bool CheckNewVersion()
        {
            try
            {
                LatestRealease last;

                var req = WebRequest.CreateHttp("https://api.github.com/repos/RyuaNerin/StreamingRespirator/releases/latest");
                req.Timeout = 5000;
                req.UserAgent = "StreamingRespirator";
                using (var res = req.GetResponse())
                {
                    using (var rStream = res.GetResponseStream())
                    {
                        var sReader = new StreamReader(rStream);
                        
                        last = JsonConvert.DeserializeObject<LatestRealease>(sReader.ReadToEnd());
                    }
                }

                return new Version(last.TagName) > Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch
            {
                return false;
            }
        }

        private class LatestRealease
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }
        }
    }
}
