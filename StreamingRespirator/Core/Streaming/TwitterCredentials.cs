using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming
{
    internal class TwitterCredentialList : List<TwitterCredential>
    {
        public ulong Revision { get; set; }
    }

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

        public HttpWebRequest CreateReqeust(string method, string uri)
            => this.CreateReqeust(method, new Uri(uri));

        private string m_xCsrfToken = null;
        public HttpWebRequest CreateReqeust(string method, Uri uri)
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

            var req = WebRequest.Create(uri) as HttpWebRequest;
            req.Method = method;
            req.UserAgent = "Streaming Respirator";

            if (method == "POST")
                req.ContentType = "application/x-www-form-urlencoded";

            req.Headers.Set("Cookie", this.Cookie);
            req.Headers.Set("X-Csrf-Token", this.m_xCsrfToken);
            req.Headers.Set("Authorization", "Bearer AAAAAAAAAAAAAAAAAAAAAF7aAAAAAAAASCiRjWvh7R5wxaKkFp7MM%2BhYBqM%3DbQ0JPmjU9F6ZoMhDfI4uTNAaQuTDm2uO9x3WFVr2xBZ2nhjdP0");
            req.Headers.Set("X-Twitter-Auth-Type", "OAuth2Session");
            req.Headers.Set("X-Twitter-Client-Version", "Twitter-TweetDeck-blackbird-chrome/4.0.190115122859 web/");

            return req;
        }


        public bool Reqeust(string method, string uriStr, byte[] data, out HttpStatusCode statusCode)
            => this.Reqeust(method, new Uri(uriStr), data, out statusCode);

        public bool Reqeust(string method, Uri uriStr, byte[] data, out HttpStatusCode statusCode)
        {
            var req = this.CreateReqeust(method, uriStr);

            if (method == "POST" && data != null)
                req.GetRequestStream().Write(data, 0, data.Length);

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    statusCode = res.StatusCode;

                    return true;
                }
            }
            catch (WebException webEx)
            {
                SentrySdk.CaptureException(webEx);

                webEx.Response?.Dispose();
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            statusCode = 0;
            return false;
        }

        public T Reqeust<T>(string method, string uriStr, byte[] data, out HttpStatusCode statusCode)
            => this.Reqeust<T>(method, new Uri(uriStr), data, out statusCode);

        public T Reqeust<T>(string method, Uri uri, byte[] data, out HttpStatusCode statusCode)
        {
            var req = this.CreateReqeust(method, uri);

            if (data != null)
                req.GetRequestStream().Write(data, 0, data.Length);

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    statusCode = res.StatusCode;

                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                    }
                }
            }
            catch (WebException webEx)
            {
                SentrySdk.CaptureException(webEx);

                webEx.Response?.Dispose();
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            statusCode = 0;
            return default;
        }

        /// <summary>
        /// 네트워크 에러 발생 시 WebException 을 Throw 함.
        /// </summary>
        /// <returns></returns>
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

                        this.Id = user.Id;
                        this.ScreenName = user.ScreenName;
                    }
                }
            }
            catch (WebException webEx)
            {
                SentrySdk.CaptureException(webEx);

                webEx.Response?.Dispose();

                if (webEx.Status != WebExceptionStatus.Success)
                    throw;

                return false;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return false;
            }

            return true;
        }
    }
}
