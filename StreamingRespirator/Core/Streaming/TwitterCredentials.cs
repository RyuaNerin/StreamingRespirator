using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Extensions;

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

        public bool VerifyCredentials()
        {
            var req = this.CreateReqeust("GET", "https://api.twitter.com/1.1/account/verify_credentials.json");

            if (!req.Do<TwitterUser>(out var statusCode, out var user))
                return false;

            if (statusCode != HttpStatusCode.OK)
                return false;

            this.Id = user.Id;
            this.ScreenName = user.ScreenName;

            return true;
        }
    }
}
