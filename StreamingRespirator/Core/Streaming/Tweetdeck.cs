using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.TimeLines;
using StreamingRespirator.Core.Twitter;
using StreamingRespirator.Core.Windows;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace StreamingRespirator.Core.Streaming
{
    internal class Authorization
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }

        [JsonIgnore]
        public CookieContainer Cookies { get; } = new CookieContainer();

        [JsonProperty("cookies", ObjectCreationHandling = ObjectCreationHandling.Replace, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [System.ComponentModel.Browsable(false)]
        public IDictionary<string, string> JsonCookies
        {
            get => this.Cookies.GetCookies(TweetDeck.CookieUri).Cast<Cookie>().ToDictionary(e => e.Name, e => e.Value);
            set
            {
                foreach (var kv in value)
                    this.Cookies.Add(TweetDeck.CookieUri, new Cookie(kv.Key, kv.Value));
            }
        }
    }

    internal class TweetDeck : IDisposable
    {
        public static readonly Uri CookieUri = new Uri("https://twitter.com/");

        private static readonly Dictionary<long, Authorization> AuthArchive = new Dictionary<long, Authorization>();
        private static readonly Dictionary<long, TweetDeck    > Instances   = new Dictionary<long, TweetDeck    >();

        private readonly HashSet<StreamingConnection> m_connections = new HashSet<StreamingConnection>();

        private readonly ITimeLine m_tlHome;
        private readonly ITimeLine m_tlAboutMe;
        private readonly ITimeLine m_tlDm;

        public Authorization Auth      { get; }
        public UserCache     UserCache { get; } = new UserCache();


        static TweetDeck()
        {
            LoadCookie();
        }

        private TweetDeck(Authorization auth)
        {
            this.Auth = auth;

            this.m_tlHome    = new HomeTimeLine   (this);
            this.m_tlAboutMe = new ActivityAboutMe(this);
            this.m_tlDm      = new DirectMessage  (this);
        }

        ~TweetDeck()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool m_disposed;
        private void Dispose(bool disposing)
        {
            if (this.m_disposed) return;
            this.m_disposed = true;

            if (disposing)
            {
                this.m_tlHome   .Dispose();
                this.m_tlAboutMe.Dispose();
                this.m_tlDm     .Dispose();

                this.UserCache.Dispose();
            }
        }

        public static void ClearAuth()
        {
            lock (AuthArchive)
            {
                lock (Instances)
                {
                    foreach (var inst in Instances)
                    {
                        inst.Value.Dispose();
                    }
                }
            }
        }


        public static TweetDeck GetTweetDeck(long userId, Control invoker)
        {
            lock (Instances)
            {
                TweetDeck td;
                if (Instances.ContainsKey(userId))
                    td = Instances[userId];
                else
                {
                    Authorization auth;
                    lock (AuthArchive)
                    {
                        if (!AuthArchive.ContainsKey(userId))
                            AuthArchive.Add(userId, new Authorization() { Id = userId });

                        auth = AuthArchive[userId];
                    }

                    td = new TweetDeck(auth);
                    Instances.Add(userId, td);
                }

                if (!td.CheckAuthorize())
                {
                    string message_text = null;
                    string defaultUsername = null;

                    string id   = null;
                    string pw   = null;
                    string body = null;

                    var tried = 0;
                    while (tried++ < 3)
                    {
                        if (!string.IsNullOrWhiteSpace(message_text))
                            invoker.Invoke(new Action(() => MessageBox.Show(message_text, "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));

                        if ((bool)invoker.Invoke(new Func<bool>(
                            () =>
                            {
                                using (var frm = new LoginWindow(defaultUsername))
                                {
                                    if (frm.ShowDialog() != DialogResult.OK)
                                        return true;

                                    id = frm.Username;
                                    pw = frm.Password;

                                    return false;
                                }
                            })))
                        {
                            Instances.Remove(userId);
                            AuthArchive.Remove(userId);

                            return null;
                        }

                        var loginedId = td.Login(id, pw, out body);
                        if (loginedId == userId)
                        {
                            break;
                        }
                        else if (loginedId != 0)
                        {
                            invoker.Invoke(new Action(() => MessageBox.Show(message_text, "스트리밍 연결 ID 와 다른 ID 입니다!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));
                        }
                        else if (body != null)
                        {
                            var html = new HtmlDocument();
                            html.LoadHtml(body);

                            message_text    = html.DocumentNode.SelectSingleNode("//span.message-text"      )?.InnerText;
                            defaultUsername = html.DocumentNode.SelectSingleNode("//input.js-username-field")?.GetAttributeValue("value", null);

                            if (tried >= 3)
                            {
                                return null;
                            }
                        }
                    }
                }

                SaveCookie();

                return td;
            }
        }

        public void AddConnection(StreamingConnection connection)
        {
            lock (this.m_connections)
            {
                this.m_connections.Add(connection);

                if (this.m_connections.Count == 1)
                    this.StartRefresh();
            }
        }
        public void RemoveStream(StreamingConnection connection)
        {
            lock (this.m_connections)
            {
                this.m_connections.Remove(connection);
                
                if (this.m_connections.Count == 0)
                    this.DisposeSelf();
            }
        }

        private void StartRefresh()
        {
            this.m_tlHome   .Start();
            this.m_tlAboutMe.Start();
            this.m_tlDm     .Start();
        }

        private void DisposeSelf()
        {
            lock (Instances)
                Instances.Remove(this.Auth.Id);

            this.m_tlHome   .Stop();
            this.m_tlAboutMe.Stop();
            this.m_tlDm     .Stop();

            this.Dispose();
        }

        private string m_xCsrfToken = null;
        public HttpWebRequest CreateReqeust(string method, string uriStr)
        {
            if (this.m_xCsrfToken == null)
            {
                try
                {
                    this.m_xCsrfToken = this.Auth.Cookies.GetCookies(CookieUri).Cast<Cookie>().First(e => e.Name == "ct0").Value;
                }
                catch
                {
                    this.m_xCsrfToken = null;
                }
            }

            var req = WebRequest.Create(uriStr) as HttpWebRequest;
            req.Method = method;
            req.CookieContainer = this.Auth.Cookies;

            req.UserAgent = "Streaming Respirator";

            if (method == "POST")
                req.ContentType = "application/x-www-form-urlencoded";

            req.Headers.Set("X-Csrf-Token"            , this.m_xCsrfToken);
            req.Headers.Set("Authorization"           , "Bearer AAAAAAAAAAAAAAAAAAAAAF7aAAAAAAAASCiRjWvh7R5wxaKkFp7MM%2BhYBqM%3DbQ0JPmjU9F6ZoMhDfI4uTNAaQuTDm2uO9x3WFVr2xBZ2nhjdP0");
            req.Headers.Set("X-Twitter-Auth-Type"     , "OAuth2Session");
            req.Headers.Set("X-Twitter-Client-Version", "Twitter-TweetDeck-blackbird-chrome/4.0.190115122859 web/");

            return req;
        }

        private void ClearCookie()
        {
            foreach (var cookie in this.Auth.Cookies.GetCookies(CookieUri).Cast<Cookie>())
                cookie.Expires = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            this.m_xCsrfToken = null;
        }

        private bool CheckAuthorize()
        {
            var req = this.CreateReqeust("GET", "https://api.twitter.com/1.1/help/settings.json?settings_version=&feature_set_token=5e3cbb323c98cbaf69b160695062002707dd6f66");

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
                        this.Auth.ScreenName = user.ScreenName;
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

        private long Login(string id, string pw, out string body)
        {
            body = null;

            this.ClearCookie();

            var req = this.CreateReqeust("GET", "https://twitter.com/");
            string authenticity_token = null;
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    if (((int)res.StatusCode / 100) != 2)
                        return 0;

                    var html = new HtmlDocument();

                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                        html.LoadHtml(reader.ReadToEnd());

                    authenticity_token = html.DocumentNode.SelectSingleNode("//input[@name='authenticity_token']")?.GetAttributeValue("value", null);
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }

            if (string.IsNullOrWhiteSpace(authenticity_token))
                return 0;

            var postData = ToPostData(new Dictionary<string, string>
            {
                ["session[username_or_email]"] = id,
                ["session[password]"         ] = pw,
                ["scribe_log"                ] = "",
                ["redirect_after_login"      ] = "https://tweetdeck.twitter.com/?via_twitter_login=true",
                ["remember_me"               ] = "1",
                ["authenticity_token"        ] = authenticity_token
            });

            req = this.CreateReqeust("POST", "https://twitter.com/sessions");

            req.GetRequestStream().Write(postData, 0, postData.Length);
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                        body = reader.ReadToEnd();

                    if (((int)res.StatusCode / 100) != 2 || res.ResponseUri.Host != "tweetdeck.twitter.com")
                        return 0;
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
                    using (var res = webEx.Response)
                    {
                        using (var stream = res.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                            body = reader.ReadToEnd();
                    }
                }
            }

            if (!this.CheckAuthorize())
                return 0;

            try
            {
                return long.Parse(Regex.Match(this.Auth.Cookies.GetCookies(CookieUri).Cast<Cookie>().First(e => e.Name == "twid").Value, "\"u=(\\d+)\"").Groups[1].Value);
            }
            catch
            {
                return 0;
            }
        }

        private class FriendsCursor
        {
            [JsonProperty("ids")]
            public long[] Ids { get; set; }
        }
        public long[] GetFriends()
        {
            /*
            count   | 5000
            user_id | ///
            */
            var req = this.CreateReqeust("GET", $"https://api.twitter.com/1.1/friends/ids.json?count=5000user_id={this.Auth.Id}");

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        return JsonConvert.DeserializeObject<FriendsCursor>(reader.ReadToEnd()).Ids;
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }

            return null;
        }

        public StreamingConnection[] GetConnections()
        {
            lock (this.m_connections)
                return this.m_connections.ToArray();
        }
        
        public static byte[] ToPostData(Dictionary<string, string> dic)
        {
            var sb = new StringBuilder();
            foreach (var st in dic)
                sb.Append($"{Uri.EscapeDataString(st.Key)}={Uri.EscapeDataString(st.Value)}&");

            sb.Remove(sb.Length - 1, 1);

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void LoadCookie()
        {
            try
            {
                lock (AuthArchive)
                {
                    using (var file = File.OpenRead(Program.CookiePath))
                    using (var reader = new StreamReader(file, Encoding.ASCII))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Populate(reader, AuthArchive);
                    }
                }
            }
            catch
            {
            }
        }

        private static void SaveCookie()
        {
            try
            {
                lock (AuthArchive)
                {
                    using (var file = File.OpenWrite(Program.CookiePath))
                    using (var writer = new StreamWriter(file, Encoding.ASCII) { AutoFlush = true })
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(writer, AuthArchive);
                    }
                }

                File.Encrypt(Program.CookiePath);
            }
            catch
            {
            }
        }
    }
}
