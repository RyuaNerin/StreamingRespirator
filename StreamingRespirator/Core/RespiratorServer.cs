using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamingRespirator.Core.Json.Streaming;
using StreamingRespirator.Core.Json.Tweetdeck;
using StreamingRespirator.Utilities;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace StreamingRespirator.Core
{
    internal class RespiratorServer
    {
        private const int KeepAlivePeriod = 5 * 1000;

        private const int ProxyPortMin     = 1000;
        private const int ProxyPortDefault = 8080;
        private const int ProxyPortMax     = 9999;

        private const int StreamingPortMin     =  1000;
        private const int StreamingPortDefault = 51443;
        private const int StreamingPortMax     = 65000;

        public RespiratorServer()
        {
            this.m_proxy = new ProxyServer();
            this.m_proxy.BeforeRequest += this.Proxy_BeforeRequest;
            this.SetProxyPort(ProxyPortDefault);

            this.m_httpStreamingListener = new HttpListener();
            this.SetStreamingPort(StreamingPortDefault);

            this.m_keepAliveTimer = new Timer(this.KeepAliveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void SetProxyPort(int port)
        {
            if (this.m_proxyEndPoint != null)
                this.m_proxy.RemoveEndPoint(this.m_proxyEndPoint);


            this.m_proxyEndPoint = new ExplicitProxyEndPoint(IPAddress.Loopback, port, true);
            this.m_proxyEndPoint.BeforeTunnelConnectRequest += this.EntPoint_BeforeTunnelConnectRequest;

            this.m_proxy.AddEndPoint(this.m_proxyEndPoint);
        }

        private void SetStreamingPort(int port)
        {
            this.WebHttpPort = port;
            this.m_streamingUrl = $"http://127.0.0.1:{port}/";

            this.m_httpStreamingListener.Prefixes.Clear();
            this.m_httpStreamingListener.Prefixes.Add(this.m_streamingUrl);
        }

        private ExplicitProxyEndPoint m_proxyEndPoint;

        private readonly ProxyServer m_proxy;
        private readonly HttpListener m_httpStreamingListener;
        private readonly Timer m_keepAliveTimer;

        private readonly List<StreamingConnection> m_connections = new List<StreamingConnection>();

        private string m_streamingUrl;
        
        public int ProxyPort => this.m_proxyEndPoint.Port;
        public int WebHttpPort { get; private set; }

        private static readonly Random rnd = new Random(DateTime.Now.Millisecond);
        public void Start()
        {
            do
            {
                try
                {
                    this.m_proxy.Start();
                    break;
                }
                catch
                {
                    this.SetProxyPort(rnd.Next(ProxyPortMin, ProxyPortMax));
                }
            } while (true);

            do
            {
                try
                {
                    this.m_httpStreamingListener.Start();
                    break;
                }
                catch (Exception)
                {
                    this.SetProxyPort(rnd.Next(StreamingPortMin, StreamingPortMax));
                }
            } while (true);

            this.m_httpStreamingListener.BeginGetContext(this.Listener_GetHttpContext, null);

            Task.Factory.StartNew(this.QueueWorker, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            Parallel.ForEach(this.m_connections, e => { e.Stream.Close(); e.Stream.WaitHandle.WaitOne(); });

            this.m_proxy.Stop();
            this.m_httpStreamingListener.Stop();
        }

        private static readonly byte[] KeepAlivePacket = Encoding.UTF8.GetBytes("\r\n");
        private void KeepAliveTimerCallback(object state)
        {
            this.SendToStream(0, KeepAlivePacket);
        }

        private Task EntPoint_BeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            e.DecryptSsl = e.WebSession.Request.RequestUri.Host == "userstream.twitter.com";

            return Task.FromResult(true);
        }

        private static long ParseOwnerId(string authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader)) return 0;

            var m = Regex.Match(authorizationHeader, "oauth_token=\"([0-9]+)\\-");
            return m.Success && long.TryParse(m.Groups[1].Value, out var ownerId) ? ownerId : 0;
        }

        private Task Proxy_BeforeRequest(object sender, SessionEventArgs e)
        {
            var uri = e.WebSession.Request.RequestUri;

            Debug.WriteLine($"reqeust : {uri.AbsoluteUri}");

            if (uri.Host == "userstream.twitter.com" &&
                uri.AbsolutePath == "/1.1/user.json")
            {
                var res = new Response(new byte[0])
                {
                    HttpVersion = e.WebSession.Request.HttpVersion,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    StatusDescription = "Unauthorized",
                };

                if (e.WebSession.Request.Headers.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var ownerId = ParseOwnerId(authHeader.Value);
                    if (ownerId != 0)
                    {
                        res.StatusCode = (int)HttpStatusCode.Found;
                        res.StatusDescription = "Found";
                        res.Headers.AddHeader("Location", this.m_streamingUrl + ownerId);

                        Debug.WriteLine($"redirect to {this.m_streamingUrl + ownerId}");
                    }
                }

                e.Respond(res);
            }

            return Task.FromResult(true);
        }

        private void Listener_GetHttpContext(IAsyncResult ar)
        {
            var cnt = this.m_httpStreamingListener.EndGetContext(ar);
            this.m_httpStreamingListener.BeginGetContext(this.Listener_GetHttpContext, null);

            Task.Factory.StartNew(
                e =>
                {
                    var eCnt = (HttpListenerContext)e;

                    var desc = $"{eCnt.Request.Url.AbsolutePath} / {cnt.Request.LocalEndPoint} <> {cnt.Request.RemoteEndPoint}";

                    Debug.WriteLine($"streaming connected : {desc}");

                    long ownerId = ParseOwnerId(eCnt.Request.Headers["Authorization"]);

                    if (ownerId == 0)
                        if (eCnt.Request.Url.AbsolutePath.Length > 1)
                            long.TryParse(eCnt.Request.Url.AbsolutePath.Substring(1), out ownerId);

                    if (ownerId != 0)
                    {
                        var ws = new WaitableStream(eCnt.Response.OutputStream);
                        var sc = new StreamingConnection(ws, ownerId, desc);

                        lock (this.m_connections)
                        {
                            this.m_connections.Add(sc);

                            if (this.m_connections.Count == 1)
                                this.m_keepAliveTimer.Change(KeepAlivePeriod, KeepAlivePeriod);
                        }

                        eCnt.Response.AppendHeader("Content-type", "application/json; charset=utf-8");
                        eCnt.Response.AppendHeader("Connection", "close");
                        eCnt.Response.SendChunked = true;

                        ws.Flush();

                        SendToStream(ownerId, NoticeJson);

                        ws.WaitHandle.WaitOne();

                        lock (this.m_connections)
                        {
                            this.m_connections.Remove(sc);

                            if (this.m_connections.Count == 0)
                                this.m_keepAliveTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        }
                    }

                    Debug.WriteLine($"streaming disconnected : {desc}");
                    eCnt.Response.Close();
                }, cnt);
        }

        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting = Formatting.None,
            DateFormatString = "ddd MMM dd HH:mm:ss +ffff yyyy"
        };

        private readonly ConcurrentQueue<TwitterApiResponse> m_streamingQueue = new ConcurrentQueue<TwitterApiResponse>();
        private readonly ManualResetEventSlim m_streamingQueueEvnet = new ManualResetEventSlim(false);

        public void AddApiResponse(TwitterApiResponse response)
        {
            lock (this.m_connections)
                if (this.m_connections.Count == 0)
                    return;

            this.m_streamingQueue.Enqueue(response);
            this.m_streamingQueueEvnet.Set();
        }

        private void QueueWorker()
        {
            try
            {
                do
                {
                    if (this.m_streamingQueue.TryDequeue(out var queue))
                    {
                        JToken jt = null;

                        try
                        {
                            jt = JToken.Parse(queue.ResponseBody);
                        }
                        catch
                        {
                            return;
                        }

                        switch (queue.RequestType)
                        {
                            case ReqeustType.Statuses:
                                this.QueueWorker_Statuses(queue, jt.ToObject<Td_statuses>());
                                break;

                            case ReqeustType.Activity:
                                this.QueueWorker_Activity(queue, jt.ToObject<Td_activity>());
                                break;

                            case ReqeustType.DirectMessage:
                                this.QueueWorker_DirectMessage(queue, jt.ToObject<Td_dm>());
                                break;
                        }
                    }
                    else
                    {
                        this.m_streamingQueueEvnet.Reset();
                        this.m_streamingQueueEvnet.Wait();
                    }
                } while (true);
            }
            catch
            {
                throw;
            }
        }
        
        private long m_last_status = 0;
        private void QueueWorker_Statuses(TwitterApiResponse response, Td_statuses json)
        {
            if (json == null) return;

            var max_id = this.m_last_status;

            var items = json.Where(e => this.m_last_status < e.Id)
                            .OrderBy(e => e.Id);

            if (items.Count() == 0)
                return;

            if (this.m_last_status == 0)
                max_id = items.Last().Id;

            else
            {
                foreach (var item in items)
                {
                    Debug.WriteLine($"status updated: {response.OwnerId} {item.Text}");

                    SendToStream(response.OwnerId, JsonConvert.SerializeObject(item, Jss));

                    max_id = Math.Max(max_id, item.Id);
                }
            }


            this.m_last_status = max_id;
        }


        private long m_last_aboutMe = 0;
        private void QueueWorker_Activity(TwitterApiResponse response, Td_activity json)
        {
            if (json == null) return;

            var max_id = this.m_last_aboutMe;

            var items = json.Where(e => e.Action == "retweet")
                            .SelectMany(e => e.Targets)
                            .Where(e => this.m_last_aboutMe < e.Id)
                            .OrderBy(e => e.Id);

            if (items.Count() == 0)
                return;

            if (this.m_last_aboutMe == 0)
                max_id = items.Last().Id;

            else
            {
                foreach (var item in items)
                {
                    Debug.WriteLine($"about me : retweeted: {item.Text}");

                    SendToStream(response.OwnerId, JsonConvert.SerializeObject(item, Jss));

                    max_id = Math.Max(max_id, item.Id);
                }
            }

            this.m_last_aboutMe = max_id;
        }

        private long m_last_dm = 0;
        private void QueueWorker_DirectMessage(TwitterApiResponse response, Td_dm json)
        {
            if (json == null || json?.Item?.Conversations == null) return;

            var max_id = this.m_last_aboutMe;

            var entries = json.Item.Entries.Where(e => this.m_last_dm < e.Message.Data.Id)
                                   .OrderBy(e => e.Message.Data.Id);
            
            foreach (var entry in entries)
            {
                var dm = new St_dm();

                dm.Item.Id        = entry.Message.Data.Id;
                dm.Item.IdStr     = entry.Message.Data.Id.ToString();
                dm.Item.Text      = entry.Message.Data.Text;
                dm.Item.CreatedAt = entry.Message.Data.CreatedAt;

                var sender = json.Item.Users[entry.Message.Data.Sender_Id];
                dm.Item.Sender           = sender;
                dm.Item.SenderId         = sender.Id;
                dm.Item.SenderScreenName = sender.ScreenName;

                var recipient = json.Item.Users[entry.Message.Data.Recipiend_Id];
                dm.Item.Recipient           = recipient;
                dm.Item.RecipientId         = recipient.Id;
                dm.Item.RecipientScreenName = recipient.ScreenName;

                Debug.WriteLine($"direct message : {dm.Item.Text}");
                SendToStream(response.OwnerId, JsonConvert.SerializeObject(dm, Jss));

                max_id = Math.Max(max_id, entry.Message.Data.Id);
            }

            this.m_last_aboutMe = max_id;
        }

        private void SendToStream(long ownerId, string data)
        {
            data += "\r\n";

            SendToStream(ownerId, Encoding.UTF8.GetBytes(data));
        }

        private void SendToStream(long ownerId, byte[] data)
        {
            StreamingConnection[] boxArray;

            lock (this.m_connections)
                boxArray = this.m_connections.ToArray();

            Parallel.ForEach(boxArray,
                box =>
                {
                    if (ownerId != -1 && box.OwnerId != ownerId)
                        return;

                    try
                    {
                        box.Stream.Write(data, 0, data.Length);
                        box.Stream.Flush();

                        Debug.WriteLine($"Streaming. Size: {data.Length} - {box.Description}");
                    }
                    catch (HttpListenerException ex)
                    {
                        Debug.WriteLine($"Streaming. Exception - {box.Description}");
                        Debug.Indent();
                        Debug.WriteLine(ex.ToString());
                        Debug.Unindent();

                        box.Stream.Close();
                    }
                });
        }

        private static readonly byte[] NoticeJson = Encoding.UTF8.GetBytes("{\"created_at\":\"Sat Aug 25 12:00:41 +0000 2018\",\"id\":1033323274954895361,\"id_str\":\"1033323274954895361\",\"full_text\":\"\\uc131\\uacf5\\uc801\\uc73c\\ub85c \\ud638\\ud761\\uae30\\uc5d0 \\uc5f0\\uacb0\\ub418\\uc5c8\\uc2b5\\ub2c8\\ub2e4.\\n\\n\\uc774 \\ud504\\ub85c\\uadf8\\ub7a8\\uc740 GNU Public Licesnse v3 \\ub85c \\ub77c\\uc774\\uc120\\uc2a4\\ub85c \\ubc30\\ud3ec\\ub429\\ub2c8\\ub2e4.\\n\\uc774 \\ud504\\ub85c\\uadf8\\ub7a8\\uc73c\\ub85c \\uc778\\ud55c \\ubaa8\\ub4e0 \\ucc45\\uc784\\uc740 \\uc0ac\\uc6a9\\uc790\\uc5d0\\uac8c \\uc788\\uc2b5\\ub2c8\\ub2e4.\\n\\n\\ubc84\\uadf8 \\uc2e0\\uace0 : https://t.co/h5LNn9mH4K\\n\\n\\ubb38\\uc758 :@_RyuaRin\",\"truncated\":false,\"display_text_range\":[0,146],\"entities\":{\"hashtags\":[],\"symbols\":[],\"user_mentions\":[{\"screen_name\":\"_RyuaRin\",\"name\":\"RyuaNerin\",\"id\":433843459,\"id_str\":\"433843459\",\"indices\":[137,146]}],\"urls\":[{\"url\":\"https://t.co/h5LNn9mH4K\",\"expanded_url\":\"https://github.com/RyuaNerin/StreamingRespirator/issues\",\"display_url\":\"github.com/RyuaNerin/Stre…\",\"indices\":[107,130]}]},\"source\":\"<a href=\\\"https://about.twitter.com/products/tweetdeck\\\" rel=\\\"nofollow\\\">TweetDeck</a>\",\"in_reply_to_status_id\":null,\"in_reply_to_status_id_str\":null,\"in_reply_to_user_id\":null,\"in_reply_to_user_id_str\":null,\"in_reply_to_screen_name\":null,\"user\":{\"id\":433843459,\"id_str\":\"433843459\",\"name\":\"RyuaNerin\",\"screen_name\":\"_RyuaRin\",\"location\":\"\",\"description\":\"RyuaNerin\",\"url\":\"https://t.co/C4nd9KflYh\",\"entities\":{\"url\":{\"urls\":[{\"url\":\"https://t.co/C4nd9KflYh\",\"expanded_url\":\"https://ryuanerin.kr\",\"display_url\":\"ryuanerin.kr\",\"indices\":[0,23]}]},\"description\":{\"urls\":[]}},\"protected\":false,\"followers_count\":0,\"fast_followers_count\":0,\"normal_followers_count\":0,\"friends_count\":0,\"listed_count\":0,\"created_at\":\"Sun Dec 11 03:03:09 +0000 2011\",\"favourites_count\":0,\"utc_offset\":null,\"time_zone\":null,\"geo_enabled\":true,\"verified\":false,\"statuses_count\":0,\"media_count\":0,\"lang\":\"ko\",\"contributors_enabled\":false,\"is_translator\":false,\"is_translation_enabled\":true,\"profile_background_color\":\"C0DEED\",\"profile_background_image_url\":\"http://abs.twimg.com/images/themes/theme1/bg.png\",\"profile_background_image_url_https\":\"https://abs.twimg.com/images/themes/theme1/bg.png\",\"profile_background_tile\":true,\"profile_image_url\":\"http://pbs.twimg.com/profile_images/887933626159030274/8GbIq_qG_normal.jpg\",\"profile_image_url_https\":\"https://pbs.twimg.com/profile_images/887933626159030274/8GbIq_qG_normal.jpg\",\"profile_banner_url\":null,\"profile_image_extensions_alt_text\":null,\"profile_banner_extensions_alt_text\":null,\"profile_link_color\":\"1B95E0\",\"profile_sidebar_border_color\":\"FFFFFF\",\"profile_sidebar_fill_color\":\"DDEEF6\",\"profile_text_color\":\"333333\",\"profile_use_background_image\":false,\"has_extended_profile\":false,\"default_profile\":false,\"default_profile_image\":false,\"has_custom_timelines\":true,\"following\":true,\"follow_request_sent\":false,\"notifications\":false,\"business_profile_state\":\"none\",\"translator_type\":\"regular\",\"require_some_consent\":false},\"geo\":null,\"coordinates\":null,\"place\":null,\"contributors\":null,\"is_quote_status\":false,\"retweet_count\":0,\"favorite_count\":0,\"reply_count\":0,\"conversation_id\":1033323274954895361,\"conversation_id_str\":\"1033323274954895361\",\"favorited\":false,\"retweeted\":false,\"possibly_sensitive\":true,\"possibly_sensitive_appealable\":false,\"lang\":\"ko\",\"supplemental_language\":null}");
    }
}
