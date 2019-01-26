using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using StreamingRespirator.Core.Json.Streaming;
using StreamingRespirator.Core.Json.Tweetdeck;
using StreamingRespirator.Core.Windows;

using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using Timer = System.Threading.Timer;

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

        [JsonProperty("cookies")]
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

        public  static readonly Dictionary<long, Authorization> AuthArchive = new Dictionary<long, Authorization>();
        private static readonly Dictionary<long, TweetDeck    > Instances   = new Dictionary<long, TweetDeck    >();

        private readonly Authorization m_auth;
        private readonly HashSet<StreamingConnection> m_connections = new HashSet<StreamingConnection>();
        private readonly UserCache m_userCache = new UserCache();

        private readonly Timer m_timerHomeTimeLine;
        private readonly Timer m_timerActivity;
        private readonly Timer m_timerDirectMessage;

        static TweetDeck()
        {
            LoadCookie();
        }

        private TweetDeck(Authorization auth)
        {
            this.m_auth = auth;

            this.m_timerHomeTimeLine    = new Timer(this.RefreshTimeline);
            this.m_timerActivity        = new Timer(this.RefresAboutMe);
            this.m_timerDirectMessage   = new Timer(this.RefreshDirectMessage);
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
                this.m_timerHomeTimeLine .Dispose();
                this.m_timerActivity     .Dispose();
                this.m_timerDirectMessage.Dispose();

                this.m_userCache.Dispose();
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
            Task.Factory.StartNew(this.RefreshTimeline, null);
            Task.Factory.StartNew(this.RefresAboutMe, null);
            Task.Factory.StartNew(this.RefreshDirectMessage, null);
        }

        private void DisposeSelf()
        {
            lock (Instances)
                Instances.Remove(this.m_auth.Id);

            this.m_timerHomeTimeLine .Change(Timeout.Infinite, Timeout.Infinite);
            this.m_timerActivity     .Change(Timeout.Infinite, Timeout.Infinite);
            this.m_timerDirectMessage.Change(Timeout.Infinite, Timeout.Infinite);

            this.Dispose();
        }

        private string m_xCsrfToken = null;
        private HttpWebRequest CreateReqeust(string method, string uriStr)
        {
            if (this.m_xCsrfToken == null)
            {
                try
                {
                    this.m_xCsrfToken = this.m_auth.Cookies.GetCookies(CookieUri).Cast<Cookie>().First(e => e.Name == "ct0").Value;
                }
                catch
                {
                    this.m_xCsrfToken = null;
                }
            }

            var req = WebRequest.Create(uriStr) as HttpWebRequest;
            req.Method = method;
            req.CookieContainer = this.m_auth.Cookies;

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
            foreach (var cookie in this.m_auth.Cookies.GetCookies(CookieUri).Cast<Cookie>())
                cookie.Expires = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            this.m_xCsrfToken = null;
        }

        private bool CheckAuthorize()
        {
            var req = this.CreateReqeust("GET", "https://api.twitter.com/1.1/help/settings.json?settings_version=&feature_set_token=5e3cbb323c98cbaf69b160695062002707dd6f66");

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                    if (((int)res.StatusCode / 100) != 2)
                        return false;
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();

                return false;
            }

            req = this.CreateReqeust("GET", "https://api.twitter.com/1.1/account/verify_credentials.json");
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
                        this.m_auth.ScreenName = user.ScreenName;
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
                return long.Parse(Regex.Match(this.m_auth.Cookies.GetCookies(CookieUri).Cast<Cookie>().First(e => e.Name == "twid").Value, "\"u=(\\d+)\"").Groups[1].Value);
            }
            catch
            {
                return 0;
            }
        }

        private StreamingConnection[] GetConnections()
        {
            lock (this.m_connections)
                return this.m_connections.ToArray();
        }

        private static readonly DateTime ForTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting = Formatting.None,
            DateFormatString = "ddd MMM dd HH:mm:ss +ffff yyyy"
        };

        private void Request<Ttem>(
            Timer timer,
            string method,
            string url,
            Func<string, IEnumerable<Ttem>> parseHtml,
            Func<IEnumerable<Ttem>, IEnumerable<TwitterUser>> selectUsers,
            Func<StreamingConnection, IEnumerable<Ttem>, IEnumerable<Ttem>> filterItem)
        {
            var next = 0;

            var req = this.CreateReqeust("GET", url);
            IEnumerable<Ttem> items = null;
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                        items = parseHtml(reader.ReadToEnd());

                    /*
                    x-rate-limit-limit      : 225
                    x-rate-limit-remaining  : 9
                    x-rate-limit-reset      : 1548385894
                    */

                    if (int.TryParse(res.Headers.Get("x-rate-limit-remaining"), out int remaining) &&
                        int.TryParse(res.Headers.Get("x-rate-limit-reset"    ), out int reset    ))
                    {
                        next = Math.Min(1000, (int)((reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / remaining * 1000));
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }

            if (items != null && items.Count() > 0)
            {
                var task = Task.Factory.StartNew(() =>
                {
                    var users = selectUsers(items);
                    if (users != null)
                    {
                        foreach (var user in users)
                            if (this.m_userCache.IsUpdated(user))
                                this.UserUpdatedEvent(user);
                    }
                });

                Parallel.ForEach(this.GetConnections(),
                    connection =>
                    {
                        var filtered = filterItem(connection, items);
                        if (filtered == null)
                            return;

                        foreach (var item in filtered)
                            connection.SendToStream(JsonConvert.SerializeObject(item, Jss));
                    });

                task.Wait();
            }

            try
            {
                timer.Change(next > 0 ? next : 15 * 1000, Timeout.Infinite);
            }
            catch
            {
            }
        }
        
        private long m_cursor_timeline = 0;
        private void RefreshTimeline(object state)
        {
            /*
            since_id                | /////
            count                   | 200
            include_my_retweet      | 1
            cards_platform          | Web-13
            include_entities        | 1
            include_user_entities   | 1
            include_cards           | 1
            send_error_codes        | 1
            tweet_mode              | extended
            include_ext_alt_text    | true
            include_reply_count	    | true
            */
            var url = "https://api.twitter.com/1.1/statuses/home_timeline.json?count=200&include_my_retweet=1&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true";

            if (this.m_cursor_timeline > 0)
                url += "&since_id=" + this.m_cursor_timeline;

            this.Request(
                timer         : this.m_timerHomeTimeLine,
                method        : "GET",
                url           : url,
                parseHtml     : body =>
                {
                    var items = JsonConvert.DeserializeObject<Td_statuses>(body)
                                           .OrderBy(e => e.Id);

                    if (items.Count() == 0)
                        return null;

                    var newCursor = items.Max(e => e.Id);
                    var curCursor = this.m_cursor_timeline;
                    this.m_cursor_timeline = newCursor;

                    if (curCursor == 0)
                        return null;

                    return items.ToArray();
                },
                selectUsers   : items => items.Select(e => e.User),
                filterItem    : (conn, items) =>
                {
                    var lid = conn.LastStatus;
                    conn.LastStatus = items.Max(e => e.Id);

                    if (lid == 0)
                        return null;

                    return items.Where(e => e.Id > lid).OrderBy(e => e.Id);
                }
            );
        }

        private long m_cursor_activity_aboutMe = 0;
        private void RefresAboutMe(object state)
        {
            /*
            since_id                | /////
            model_version           | 7
            count                   | 200
            skip_aggregation        | true
            cards_platform          | Web-13
            include_entities        | 1
            include_user_entities   | 1
            include_cards           | 1
            send_error_codes        | 1
            tweet_mode              | extended
            include_ext_alt_text    | true
            include_reply_count     | true
            */
            var url = "https://api.twitter.com/1.1/activity/about_me.json?model_version=7&count=200&skip_aggregation=true&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true";

            if (this.m_cursor_activity_aboutMe > 0)
                url += "&since_id=" + this.m_cursor_activity_aboutMe;            

            this.Request(
                timer      : this.m_timerActivity,
                method     : "GET",
                url        : url,
                parseHtml  : body =>
                {
                    var items = JsonConvert.DeserializeObject<Td_activity>(body)
                                           .Where(e => e.Action == "retweet" || e.Action == "reply");

                    if (items.Count() == 0)
                        return null;

                    var newCursor = items.Max(e => e.MaxPosition);
                    var curCursor = this.m_cursor_activity_aboutMe;
                    this.m_cursor_activity_aboutMe = newCursor;

                    if (curCursor == 0)
                        return null;

                    return items.SelectMany(e => e.Targets)
                                .OrderBy(e => e.Id)
                                .ToArray();
                },
                selectUsers: items =>items.Select(e => e.User),
                filterItem : (conn, items) =>
                {
                    var lid = conn.LastActivity;
                    conn.LastActivity = items.Max(e => e.Id);

                    if (lid == 0)
                        return null;

                    return items.Where(e => e.Id > lid).OrderBy(e => e.Id);

                }
            );
        }

        private string m_cursor_DirectMessgae = null;
        private void RefreshDirectMessage(object state)
        {
            /*
            cursor                  | /////
            include_groups          | true
            ext                     | altText
            cards_platform          | Web-13
            include_entities        | 1
            include_user_entities   | 1
            include_cards           | 1
            send_error_codes        | 1
            tweet_mode              | extended
            include_ext_alt_text    | true
            include_reply_count	    | true
            */
            var url = "https://api.twitter.com/1.1/dm/user_updates.json?include_groups=true&ext=altText&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true ";

            if (this.m_cursor_DirectMessgae != null)
                url += "&cursor=" + this.m_cursor_DirectMessgae;

            this.Request(
                timer: this.m_timerDirectMessage,
                method: "GET",
                url: url,
                parseHtml: body =>
                {
                    var dmJson = JsonConvert.DeserializeObject<Td_dm>(body);

                    if (!(dmJson?.Item?.Entries?.Length > 0))
                        return null;

                    var newCursor = dmJson.Item.Cursor;
                    var curCursor = this.m_cursor_DirectMessgae;
                    this.m_cursor_DirectMessgae = newCursor;

                    if (curCursor == null)
                        return null;

                    return dmJson.Item
                                 .Entries
                                 .Where(e => e.Message != null)
                                 .Select(e =>
                                 {
                                     var dm = new St_dm();

                                     dm.Item.Id = e.Message.Data.Id;
                                     dm.Item.IdStr = e.Message.Data.Id.ToString();
                                     dm.Item.Text = e.Message.Data.Text;
                                     dm.Item.CreatedAt = e.Message.Data.CreatedAt;

                                     var sender = dmJson.Item.Users[e.Message.Data.Sender_Id];
                                     dm.Item.Sender = sender;
                                     dm.Item.SenderId = sender.Id;
                                     dm.Item.SenderScreenName = sender.ScreenName;

                                     var recipient = dmJson.Item.Users[e.Message.Data.Recipiend_Id];
                                     dm.Item.Recipient = recipient;
                                     dm.Item.RecipientId = recipient.Id;
                                     dm.Item.RecipientScreenName = recipient.ScreenName;

                                     return dm;
                                 })
                                 .ToArray();
                },
                selectUsers: items => items.Select(e => e.Item.Sender),
                filterItem: (conn, items) =>
                {
                    var lid = conn.LastDirectMessage;
                    conn.LastDirectMessage = items.Max(e => e.Item.Id);

                    if (lid == 0)
                        return null;

                    return items.Where(e => e.Item.Id > lid).OrderBy(e => e.Item.Id);

                }
            );
        }

        private void UserUpdatedEvent(TwitterUser user)
        {
            var data = JsonConvert.SerializeObject(
                new St_Event()
                {
                    Target = user,
                    Source = user,
                    Event  = "user_update"
                },
                Jss);
            
            Parallel.ForEach(this.GetConnections(), connection => connection.SendToStream(data));
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

                File.Encrypt(Program.CookiePath);
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
            }
            catch
            {
            }
        }
    }
}
