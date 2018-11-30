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

namespace StreamingRespirator.Core.Streaming
{
    internal class RespiratorServer
    {
        private const int KeepAlivePeriod = 5 * 1000;

        private const int ProxyPortMin     =  1000;
        private const int ProxyPortDefault = 34811;
        private const int ProxyPortMax     = 65000;

        private const int StreamingPortMin     =  1000;
        private const int StreamingPortDefault = 51443;
        private const int StreamingPortMax     = 65000;

        public RespiratorServer()
        {
            this.m_proxy = new ProxyServer();
            this.m_proxy.BeforeRequest += this.Proxy_BeforeRequest;
            this.m_proxy.ExceptionFunc = new ExceptionHandler(this.HandleException);

            this.m_httpStreamingListener = new HttpListener();

            this.m_keepAliveTimer = new Timer(this.KeepAliveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void HandleException(Exception ex)
        {

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
            this.m_streamingUrl = $"http://127.0.0.1:{port}/";

            this.m_httpStreamingListener.Prefixes.Clear();
            this.m_httpStreamingListener.Prefixes.Add(this.m_streamingUrl);
        }

        private readonly ProxyServer m_proxy;
        private ExplicitProxyEndPoint m_proxyEndPoint;

        private readonly HttpListener m_httpStreamingListener;
        private string m_streamingUrl;

        private readonly Timer m_keepAliveTimer;

        private readonly List<StreamingConnection> m_connections = new List<StreamingConnection>();
                
        public int ProxyPort => this.m_proxyEndPoint.Port;

        private static readonly Random rnd = new Random(DateTime.Now.Millisecond);

        private bool m_started = false;
        public void Start()
        {
            if (this.m_started)
                return;
            this.m_started = true;

            int port;
            int tried;

            port = ProxyPortDefault;
            tried = 0;
            while (tried++ < 3)
            {
                try
                {
                    this.SetProxyPort(port);
                    this.m_proxy.Start();
                    break;
                }
                catch (Exception ex)
                {
                    port = rnd.Next(ProxyPortMin, ProxyPortMax);

                    if (tried == 3)
                        throw ex;
                }
            }


            port = StreamingPortDefault;
            tried = 0;
            while (tried++ < 3)
            {
                try
                {
                    this.SetStreamingPort(port);
                    this.m_httpStreamingListener.Start();
                    break;
                }
                catch (Exception ex)
                {
                    port = rnd.Next(StreamingPortMin, StreamingPortMax);

                    if (tried == 3)
                        throw ex;
                }
            }

            this.m_httpStreamingListener.BeginGetContext(this.Listener_GetHttpContext, null);

            Task.Factory.StartNew(this.QueueWorker, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            if (!this.m_started)
                return;

            this.m_proxy.Stop();
            this.m_httpStreamingListener.Stop();

            Parallel.ForEach(this.GetConnections(0), e => { e.Stream.Close(); e.Stream.WaitHandle.WaitOne(); });
        }

        private StreamingConnection[] GetConnections(long ownerId = 0)
        {
            lock (this.m_connections)
                return ownerId == 0 ? this.m_connections.ToArray() : this.m_connections.Where(e => e.OwnerId == ownerId).ToArray();
        }

        private static readonly byte[] KeepAlivePacket = Encoding.UTF8.GetBytes("\r\n");
        private void KeepAliveTimerCallback(object state)
        {
            foreach (var conn in this.GetConnections(0))
                this.SendToStream(conn, KeepAlivePacket);
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
            HttpListenerContext cnt;
            try
            {
                cnt = this.m_httpStreamingListener.EndGetContext(ar);
            }
            catch
            {
                return;
            }

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
                        var sc = new StreamingConnection(new WaitableStream(eCnt.Response.OutputStream), ownerId, desc);

                        eCnt.Response.AppendHeader("Content-type", "application/json; charset=utf-8");
                        eCnt.Response.AppendHeader("Connection", "close");
                        eCnt.Response.SendChunked = true;
                        
                        SendToStream(sc, KeepAlivePacket);

                        lock (this.m_connections)
                        {
                            this.m_connections.Add(sc);

                            if (this.m_connections.Count == 1)
                                this.m_keepAliveTimer.Change(KeepAlivePeriod, KeepAlivePeriod);
                        }

                        sc.Stream.WaitHandle.WaitOne();

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
            do
            {
                this.m_streamingQueueEvnet.Wait();

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

                    var connArray = this.GetConnections(queue.OwnerId);

                    try
                    {
                        switch (queue.RequestType)
                        {
                            case ReqeustType.statuses__home_timeline:
                                {
                                    var json = jt.ToObject<Td_statuses>();
                                    foreach (var conn in connArray)
                                        this.QueueWorker_Statuses(conn, json);
                                }
                                break;

                            case ReqeustType.activity__about_me:
                                {
                                    var json = jt.ToObject<Td_activity>();
                                    foreach (var conn in connArray)
                                        this.QueueWorker_Activity(conn, json);
                                }
                                break;

                            case ReqeustType.dm__user_updates:
                                {
                                    var json = jt.ToObject<Td_dm>();
                                    foreach (var conn in connArray)
                                        this.QueueWorker_DirectMessage(conn, json);
                                }
                                break;
                        }
                    }
                    catch
                    {

                    }
                }
                else
                {
                    this.m_streamingQueueEvnet.Reset();
                }
            } while (true);
        }
        
        private void QueueWorker_Statuses(StreamingConnection conn, Td_statuses json)
        {
            if (json == null) return;

            var max_id = conn.LastStatus;

            var items = json.Where(e => conn.LastStatus < e.Id)
                            .OrderBy(e => e.Id);

            if (items.Count() == 0)
                return;

            if (conn.LastStatus == 0)
                max_id = items.Last().Id;

            else
            {
                foreach (var item in items)
                {
                    Debug.WriteLine($"status updated: {conn.OwnerId} {item.Text}");

                    SendToStream(conn, JsonConvert.SerializeObject(item, Jss));

                    max_id = Math.Max(max_id, item.Id);
                }
            }

            conn.LastStatus = max_id;
        }
        
        private void QueueWorker_Activity(StreamingConnection conn, Td_activity json)
        {
            if (json == null) return;

            var max_id = conn.LastActivity;

            var items = json.Where(e => e.Action == "retweet" || e.Action == "reply")
                            .SelectMany(e => e.Targets)
                            .Where(e => max_id < e.Id)
                            .OrderBy(e => e.Id);

            if (items.Count() == 0)
                return;

            if (conn.LastActivity == 0)
                max_id = items.Last().Id;

            else
            {
                foreach (var item in items)
                {
                    Debug.WriteLine($"about me : retweeted: {item.Text}");

                    SendToStream(conn, JsonConvert.SerializeObject(item, Jss));

                    max_id = Math.Max(max_id, item.Id);
                }
            }

            conn.LastActivity = max_id;
        }
        
        private void QueueWorker_DirectMessage(StreamingConnection conn, Td_dm json)
        {
            if (json == null || json?.Item?.Conversations == null) return;

            var max_id = conn.LastDirectMessage;

            var entries = json.Item.Entries.Where(e => conn.LastDirectMessage < e.Message.Data.Id)
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
                SendToStream(conn, JsonConvert.SerializeObject(dm, Jss));

                max_id = Math.Max(max_id, entry.Message.Data.Id);
            }

            conn.LastDirectMessage = max_id;
        }

        private void SendToStream(StreamingConnection conn, string data)
        {
            data += "\r\n";

            SendToStream(conn, Encoding.UTF8.GetBytes(data));
        }

        private void SendToStream(StreamingConnection conn, byte[] data)
        {
            try
            {
                Debug.WriteLine($"Streaming. Sending. Size: {data.Length} - {conn.Description}");

                conn.Stream.Write(data, 0, data.Length);
                conn.Stream.Flush();

                Debug.WriteLine($"Streaming. Sent,    Size: {data.Length} - {conn.Description}");
            }
            catch (HttpListenerException ex)
            {
                Debug.WriteLine($"Streaming. Exception - {conn.Description}");
                Debug.Indent();
                Debug.WriteLine(ex.ToString());
                Debug.Unindent();

                conn.Stream.Close();
            }
        }
    }
}
