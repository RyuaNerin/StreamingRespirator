using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using StreamingRespirator.Core.Windows;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace StreamingRespirator.Core.Streaming
{
    internal struct StateUpdateData
    {
        public bool         IsRemoved;
        public string       ScreenName;
        public bool?        Connected;
        public float?       WaitTimeHome;
        public float?       WaitTimeAboutMe;
        public float?       WaitTimeDm;
    }

    static class TwitterClientFactory
    {
        public static readonly Uri CookieUri = new Uri("https://twitter.com/");

        private static readonly Dictionary<long, TwitterClient> Instances = new Dictionary<long, TwitterClient>();

        public static int AccountCount
        {
            get
            {
                lock (Instances)
                    return Instances.Count;
            }
        }

        public static IEnumerable<TwitterCredential> Accounts
        {
            get
            {
                lock (Instances)
                    return Instances.Select(e => e.Value.Credential).ToArray();
            }
            set
            {
                lock (Instances)
                {
                    foreach (var cred in value)
                    {
                        if (string.IsNullOrWhiteSpace(cred.Cookie) ||
                            string.IsNullOrWhiteSpace(cred.ScreenName))
                            continue;

                        AddClient(cred);
                    }
                }
            }
        }

        public static event Action<long, StateUpdateData> ClientUpdated;

        private static void ClientStatusUpdatedEvent(long id, StateUpdateData statusData)
            => ClientUpdated?.Invoke(id, statusData);

        public static TwitterClient GetClient(long id)
        {
            lock (Instances)
            {
                if (!Instances.ContainsKey(id))
                    return null;

                var inst = Instances[id];

                var befScreenName = inst.Credential.ScreenName;
                if (!inst.Credential.VerifyCredentials())
                {
                    RemoveClient(id);
                    return null;
                }

                if (inst.Credential.ScreenName != befScreenName)
                    ClientStatusUpdatedEvent(id, new StateUpdateData { ScreenName = inst.Credential.ScreenName });

                return inst;
            }
        }

        public static TwitterClient GetInsatnce(long id)
        {
            lock (Instances)
            {
                if (!Instances.ContainsKey(id))
                    return null;

                return Instances[id];
            }
        }

        public static void RemoveClient(long id)
        {
            lock (Instances)
            {
                Instances[id].Dispose();
                Instances.Remove(id);

                ClientStatusUpdatedEvent(id, new StateUpdateData { IsRemoved = true });

                Config.Save();
            }
        }

        private static void AddClient(TwitterCredential twitCred)
        {
            lock (Instances)
            {
                TwitterClient twitClient;
                if (!Instances.ContainsKey(twitCred.Id))
                {
                    twitClient = new TwitterClient(twitCred);
                    twitClient.ClientUpdated += ClientStatusUpdatedEvent;

                    Instances.Add(twitCred.Id, twitClient);
                }
                else
                {
                    twitClient = Instances[twitCred.Id];
                    twitClient.Credential.ScreenName = twitCred.ScreenName;
                    twitClient.Credential.Cookie     = twitCred.Cookie;
                }

                ClientStatusUpdatedEvent(twitCred.Id, new StateUpdateData { ScreenName = twitCred.ScreenName });

                Config.Save();
            }
        }

        public static void AddClient(Control invoker)
        {
            string id = null;
            string pw = null;
            
            if ((bool)invoker.Invoke(new Func<bool>(
                () =>
                {
                    using (var frm = new LoginWindow(null))
                    {
                        if (frm.ShowDialog() != DialogResult.OK)
                            return true;

                        id = frm.Username;
                        pw = frm.Password;

                        return false;
                    }
                })))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pw))
                return;

            var twitCred = Login(id, pw, out string errorMessage);
            if (twitCred != null)
            {
                invoker.Invoke(new Action(() => MessageBox.Show(twitCred.ScreenName + "가 추가되었습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));

                AddClient(twitCred);
            }
            else if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                invoker.Invoke(new Action(() => MessageBox.Show(errorMessage, "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));
            }
            else
            {
            }
        }

        private static TwitterCredential Login(string id, string pw, out string body)
        {
            body = null;

            var cookieContainer = new CookieContainer();

            var authenticity_token = LoginStep1(cookieContainer);
            if (string.IsNullOrWhiteSpace(authenticity_token))
                return null;
            
            if (!LoginStep2(id, pw, cookieContainer, authenticity_token, out body))
                return null;

            var tempCredentials = new TwitterCredential()
            {
                Cookie = cookieContainer.GetCookieHeader(CookieUri),
            };

            if (!tempCredentials.VerifyCredentials())
                return null;
            
            return tempCredentials;
        }

        private static string LoginStep1(CookieContainer cookieContainer)
        {
            var req = TwitterCredential.CreateRequestBase("GET", "https://twitter.com/");
            req.CookieContainer = cookieContainer;
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    if (((int)res.StatusCode / 100) != 2)
                        return null;

                    var html = new HtmlDocument();

                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                        html.LoadHtml(reader.ReadToEnd());

                    return html.DocumentNode.SelectSingleNode("//input[@name='authenticity_token']")?.GetAttributeValue("value", null);
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }
            catch
            {
            }

            return null;
        }

        private static bool LoginStep2(string id, string pw, CookieContainer cookieContainer, string authenticity_token, out string errorMessage)
        {
            errorMessage = null;

            var postData = Encoding.UTF8.GetBytes(
                new Dictionary<string, string>
                {
                    ["session[username_or_email]"] = id,
                    ["session[password]"         ] = pw,
                    ["scribe_log"                ] = "",
                    ["redirect_after_login"      ] = "https://tweetdeck.twitter.com/?via_twitter_login=true",
                    ["remember_me"               ] = "1",
                    ["authenticity_token"        ] = authenticity_token
                }.Select(e => $"{Uri.EscapeDataString(e.Key)}={Uri.EscapeDataString(e.Value)}")
                 .Aggregate(new StringBuilder(), (cur, next) => { if (cur.Length > 0) cur.Append('&'); return cur.Append(next); })
                 .ToString()
            );

            var req = TwitterCredential.CreateRequestBase("POST", "https://twitter.com/sessions");
            req.CookieContainer = cookieContainer;

            try
            {
                req.GetRequestStream().Write(postData, 0, postData.Length);

                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    return ((int)res.StatusCode / 100) == 2 && res.ResponseUri.Host == "tweetdeck.twitter.com";
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
                    using (var res = webEx.Response)
                    {
                        var html = new HtmlDocument();

                        using (var stream = res.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                            html.LoadHtml(reader.ReadToEnd());

                        errorMessage = html.DocumentNode.SelectSingleNode("//span[@class='message-text']")?.InnerText;
                    }
                }
            }

            return false;
        }
    }
}
