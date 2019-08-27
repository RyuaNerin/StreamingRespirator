using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming
{
    internal class TwitterCredential
    {
        public static TwitterCredential GetCredential(string cookie)
        {
            var tempCredentials = new TwitterCredential()
            {
                Cookie = cookie,
            };

            if (!tempCredentials.VerifyCredentials())
                return null;

            return tempCredentials;
        }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }

        [JsonProperty("cookie")]
        public string Cookie { get; set; }

        private string m_xCsrfToken = null;
        public HttpWebRequest CreateReqeust(string method, string uriStr)
        {
            if (this.m_xCsrfToken == null)
            {
                try
                {
                    this.m_xCsrfToken = Regex.Match(this.Cookie, "ct0=([^;]+)").Groups[1].Value;
                }
                catch
                {
                    this.m_xCsrfToken = null;
                }
            }

            var req = CreateRequestBase(method, uriStr);
            req.Headers.Set("Cookie"                  , this.Cookie);
            req.Headers.Set("X-Csrf-Token"            , this.m_xCsrfToken);
            req.Headers.Set("Authorization"           , "Bearer AAAAAAAAAAAAAAAAAAAAAF7aAAAAAAAASCiRjWvh7R5wxaKkFp7MM%2BhYBqM%3DbQ0JPmjU9F6ZoMhDfI4uTNAaQuTDm2uO9x3WFVr2xBZ2nhjdP0");
            req.Headers.Set("X-Twitter-Auth-Type"     , "OAuth2Session");
            req.Headers.Set("X-Twitter-Client-Version", "Twitter-TweetDeck-blackbird-chrome/4.0.190115122859 web/");

            return req;
        }

        public bool Reqeust(string method, string uriStr, byte[] data = null)
        {
            var req = this.CreateReqeust(method, uriStr);

            if (method == "POST" && data != null)
                req.GetRequestStream().Write(data, 0, data.Length);

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    {
                        var buff = new byte[4096];
                        int read;
                        while ((read = stream.Read(buff, 0, 4096)) > 0);
                    }

                    return true;
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }
            catch
            {
            }

            return false;
        }
        public T Reqeust<T>(string method, string uriStr, byte[] data = null)
        {
            var req = this.CreateReqeust(method, uriStr);

            if (method == "POST" && data != null)
                req.GetRequestStream().Write(data, 0, data.Length);

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }
            catch
            {
            }

            return default(T);
        }

        public static HttpWebRequest CreateRequestBase(string method, string uriStr)
        {
            var req = WebRequest.Create(uriStr) as HttpWebRequest;
            req.Method = method;
            req.UserAgent = "Streaming Respirator";

            if (method == "POST")
                req.ContentType = "application/x-www-form-urlencoded";

            return req;
        }

        public bool VerifyCredentials()
        {
            var req = this.CreateReqeust("GET", "https://api.twitter.com/1.1/account/verify_credentials.json");

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    if (((int)res.StatusCode / 100) != 2)
                        return false;

                    using (var stream = res.GetResponseStream())
                    {
                        var reader = new StreamReader(stream, Encoding.UTF8);
                        var user = JsonConvert.DeserializeObject<TwitterUser>(reader.ReadToEnd());

                        this.Id         = user.Id;
                        this.ScreenName = user.ScreenName;
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();

                return false;
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
