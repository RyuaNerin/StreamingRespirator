using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Utilities;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace StreamingRespirator.Core.Streaming
{
    internal class RespiratorServer
    {
        public const int ProxyPort = 8811;

        private const int StreamingPortMin     =  1000;
        private const int StreamingPortDefault = 51443;
        private const int StreamingPortMax     = 65000;

        private readonly ProxyServer m_proxy;
        private readonly ExplicitProxyEndPoint m_proxyEndPoint;

        private readonly HttpListener m_httpStreamingListener;
        private string m_streamingUrl;

        private readonly HashSet<StreamingConnection> m_connections = new HashSet<StreamingConnection>();

        public bool IsRunning { get; private set; }

        public RespiratorServer()
        {
            this.m_proxyEndPoint = new ExplicitProxyEndPoint(IPAddress.Loopback, ProxyPort);
            this.m_proxyEndPoint.BeforeTunnelConnectRequest += this.EntPoint_BeforeTunnelConnectRequest;

            this.m_proxy = new ProxyServer();
            this.m_proxy.CertificateManager.RootCertificateIssuerName = "Streaming-Respirator";
            this.m_proxy.CertificateManager.RootCertificateName = "Streaming-Respirator Root Certificate Authority";

            this.m_proxy.AddEndPoint(this.m_proxyEndPoint);
            this.m_proxy.BeforeRequest += this.Proxy_BeforeRequest;
            this.m_proxy.AfterResponse += this.Proxy_AfterResponse;

            this.m_httpStreamingListener = new HttpListener();
        }

        public void Start()
        {
            if (this.IsRunning)
                return;
            
            this.m_proxy.Start();

            var rnd = new Random(DateTime.Now.Millisecond);

            var port = StreamingPortDefault;
            var tried = 0;
            while (tried++ < 3)
            {
                try
                {
                    this.m_streamingUrl = $"http://127.0.0.1:{port}/";

                    this.m_httpStreamingListener.Prefixes.Clear();
                    this.m_httpStreamingListener.Prefixes.Add(this.m_streamingUrl);

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

            this.IsRunning = true;
        }

        public void Stop()
        {
            if (!this.IsRunning)
                return;

            this.m_proxy.Stop();
            this.m_httpStreamingListener.Stop();

            Parallel.ForEach(
                this.m_connections.ToArray(),
                e =>
                {
                    try
                    {
                        e.Stream.Close();
                    }
                    catch
                    {
                    }
                    e.Stream.WaitHandle.WaitOne();
                });
        }

        private Task EntPoint_BeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            if (e.HttpClient.Request.RequestUri.Host == "userstream.twitter.com" ||
                e.HttpClient.Request.RequestUri.Host == "api.twitter.com")
            {
                e.DecryptSsl = true;
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private static long ParseOwnerId(Uri uri, HeaderCollection headers)
        {
            string authHeader = null;

            if (headers.Headers.TryGetValue("Authorization", out var head))
            {
                if (!string.IsNullOrWhiteSpace(head.Value))
                    authHeader = head.Value;
            }

            if (authHeader == null)
                authHeader = uri.Query;


            return ParseOwnerId(authHeader);
        }
        private static long ParseOwnerId(Uri uri, NameValueCollection headers)
        {
            var header = headers.Get("Authorization");

            if (string.IsNullOrWhiteSpace(header))
                header = uri.Query;

            return ParseOwnerId(header);
        }

        private static long ParseOwnerId(string authorizationHeader)
        {
            var m = Regex.Match(authorizationHeader, "oauth_token=\"?([0-9]+)\\-");
            if (!m.Success)
                return 0;

            return m.Success && long.TryParse(m.Groups[1].Value, out var ownerId) ? ownerId : 0;
        }

        private Task Proxy_BeforeRequest(object sender, SessionEventArgs e)
        {
            var uri = e.HttpClient.Request.RequestUri;
            
            if (uri.Host == "userstream.twitter.com" &&
                uri.AbsolutePath == "/1.1/user.json")
            {
                var res = new Response(new byte[0])
                {
                    HttpVersion = e.HttpClient.Request.HttpVersion,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    StatusDescription = "Unauthorized",
                };
                
                var ownerId = ParseOwnerId(uri, e.HttpClient.Request.Headers);
                if (ownerId != 0)
                {
                    res.StatusCode = (int)HttpStatusCode.Found;
                    res.StatusDescription = "Found";
                    res.Headers.AddHeader("Location", this.m_streamingUrl + ownerId);

                    Debug.WriteLine($"redirect to {this.m_streamingUrl + ownerId}");
                }

                e.Respond(res);
            }

            // 404 : id = 삭제된 트윗일 수 있음
            // 200 : 성공시 스트리밍에 전송해서 한번 더 띄우도록
            // POST https://api.twitter.com/1.1/statuses/retweet/:id.json
            else if (uri.Host == "api.twitter.com" &&
                     uri.AbsolutePath.StartsWith("/1.1/statuses/retweet/"))
            {
                CustomRequest_Retweet(e);
            }

            return Task.FromResult(true);
        }
        private static bool CustomRequest_Retweet(SessionEventArgs e)
        {
            var ownerId = ParseOwnerId(e.HttpClient.Request.RequestUri, e.HttpClient.Request.Headers);
            if (ownerId == 0)
                return false;

            var twitClient = TwitterClientFactory.GetInsatnce(ownerId);
            if (twitClient == null)
                return false;
            
            string body;
            int statusCode;
            var response = GetResponse(e, false, out statusCode, out body);
            if (response == null)
                return false;

            e.Respond(response);

            if (statusCode == 404)
                twitClient.StatusMaybeDestroyed(ParseJsonId(e.HttpClient.Request.RequestUri));
            else
            {
                var status = JsonConvert.DeserializeObject<TwitterStatus>(body);

                if (status != null)
                    twitClient.SendStatus(status);
            }

            return true;
        }
        private static Response GetResponse(SessionEventArgs e, bool sendBody, out int statusCode, out string body)
        {
            statusCode = 0;
            body = null;

            var reqProxy = e.HttpClient.Request;

            var reqHttp = WebRequest.Create(reqProxy.RequestUri) as HttpWebRequest;
            reqHttp.Method = reqProxy.Method;
            
            foreach (var head in reqProxy.Headers)
            {
                switch (head.Name.ToLower())
                {
                    case "accept"               : reqHttp.Accept            = head.Value; break;
                    case "connection"           : reqHttp.Connection        = head.Value; break;
                    case "content-length"       :                                         break;
                    case "content-type"         : reqHttp.ContentType       = head.Value; break;
                    case "expect"               : reqHttp.Expect            = head.Value; break;
                    case "host"                 : reqHttp.Host              = head.Value; break;
                    case "media-type"           : reqHttp.MediaType         = head.Value; break;
                    case "referer"              : reqHttp.Referer           = head.Value; break;
                    case "transfer-encoding"    : reqHttp.TransferEncoding  = head.Value; break;
                    case "user-agent"           : reqHttp.UserAgent         = head.Value; break;

                    default:
                        reqHttp.Headers.Set(head.Name, head.Value);
                        break;
                }
            }

            if (sendBody && e.HttpClient.Request.HasBody)
            {
                var task = e.GetRequestBody();
                task.Wait();

                var buff = task.Result;
                reqHttp.GetRequestStream().Write(buff, 0, buff.Length);
            }

            HttpWebResponse resHttp = null;
            try
            {
                resHttp = reqHttp.GetResponse() as HttpWebResponse;
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                    resHttp = webEx.Response as HttpWebResponse;
            }
            catch
            {
            }

            if (resHttp == null)
                return null;
            
            using (resHttp)
            {
                statusCode = (int)resHttp.StatusCode;

                using (var mem = new MemoryStream(4096))
                {
                    using (var stream = resHttp.GetResponseStream())
                    {
                        var buff = new byte[4096];
                        var count = 0;

                        while ((count = stream.Read(buff, 0, 4096)) > 0)
                            mem.Write(buff, 0, count);
                    }

                    mem.Position = 0;

                    using (var reader = new StreamReader(mem))
                        body = reader.ReadToEnd();

                    var resProxy = new Response(mem.ToArray())
                    {
                        HttpVersion = e.HttpClient.Request.HttpVersion,
                        StatusCode = (int)resHttp.StatusCode,
                        StatusDescription = resHttp.StatusDescription,
                    };

                    foreach (var headerName in resHttp.Headers.AllKeys)
                    {
                        switch (headerName.ToLower())
                        {
                            case "content-type": resProxy.ContentType = resHttp.Headers[headerName]; break;

                            default:
                                resProxy.Headers.AddHeader(headerName, resHttp.Headers[headerName]);
                                break;
                        }
                    }

                    return resProxy;
                }
            }
        }

        private Task Proxy_AfterResponse(object sender, SessionEventArgs e)
        {
            this.Proxy_AfterResponse(e);
            return Task.FromResult(true);
        }
        private async void Proxy_AfterResponse(SessionEventArgs e)
        {
            var uri = e.HttpClient.Request.RequestUri;
            if (uri.Host != "api.twitter.com")
                return;

            var ownerId = ParseOwnerId(uri, e.HttpClient.Request.Headers);
            if (ownerId == 0)
                return;

            var twitClient = TwitterClientFactory.GetInsatnce(ownerId);
            if (twitClient == null)
                return;

            // 삭제 패킷 전송
            // POST https://api.twitter.com/1.1/statuses/destroy/:id.json
            // POST https://api.twitter.com/1.1/statuses/unretweet/:id.json
            if (uri.AbsolutePath.StartsWith("/1.1/statuses/destroy/"))
            {
                twitClient.StatusDestroyed(ParseJsonId(uri));
                return;
            }
            if (uri.AbsolutePath.StartsWith("/1.1/statuses/unretweet/"))
            {
                twitClient.StatusDestroyed(ParseJsonId(uri));
                return;
            }

            // 401 : in_reply_to_status_id = 삭제된 트윗일 수 있음
            // POST https://api.twitter.com/1.1/statuses/update.json
            if (uri.AbsolutePath.StartsWith("/1.1/statuses/update.json"))
            {
                if (e.HttpClient.Response.StatusCode == 401)
                {
                    string queryString;

                    if (e.HttpClient.Request.HasBody)
                        queryString = await e.GetRequestBodyAsString();
                    else
                        queryString = uri.Query;

                    var m = Regex.Match(queryString, "in_reply_to_status_id=(\\d)+");

                    if (m.Success && long.TryParse(m.Groups[1].Value, out var id))
                    {
                        twitClient.StatusMaybeDestroyed(id);
                    }
                }
            }
        }

        private static long ParseJsonId(Uri uri)
        {
            var idStr = uri.AbsolutePath;
            idStr = idStr.Substring(idStr.LastIndexOf('/') + 1);
            idStr = idStr.Substring(0, idStr.IndexOf('.'));

            return long.TryParse(idStr, out var id) ? id : 0;
        }

        private void Listener_GetHttpContext(IAsyncResult ar)
        {
            try
            {
                new Thread(this.ConnectionThread).Start(this.m_httpStreamingListener.EndGetContext(ar));
            }
            catch
            {
                return;
            }

            this.m_httpStreamingListener.BeginGetContext(this.Listener_GetHttpContext, null);
        }

        private void ConnectionThread(object listenerContextObject)
        {
            var cnt = (HttpListenerContext)listenerContextObject;

            var desc = $"{cnt.Request.Url.AbsolutePath} / {cnt.Request.LocalEndPoint} <> {cnt.Request.RemoteEndPoint}";

            Debug.WriteLine($"streaming connected : {desc}");

            long ownerId = ParseOwnerId(cnt.Request.Url, cnt.Request.Headers);

            if (ownerId == 0)
                if (cnt.Request.Url.AbsolutePath.Length > 1)
                    long.TryParse(cnt.Request.Url.AbsolutePath.Substring(1), out ownerId);

            if (ownerId != 0)
            {
                var twitterClient = TwitterClientFactory.GetClient(ownerId);

                if (twitterClient == null)
                {
                    cnt.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                }
                else
                {
                    cnt.Response.AppendHeader("Content-type", "application/json; charset=utf-8");
                    cnt.Response.AppendHeader("Connection", "close");
                    cnt.Response.SendChunked = true;

                    using (var sc = new StreamingConnection(new WaitableStream(cnt.Response.OutputStream), twitterClient))
                    {
                        lock (this.m_connections)
                            this.m_connections.Add(sc);

                        twitterClient.AddConnection(sc);

                        sc.Stream.WaitHandle.WaitOne();

                        lock (this.m_connections)
                            this.m_connections.Remove(sc);

                        twitterClient.RemoveStream(sc);
                    }
                }

                //////////////////////////////////////////////////
            }

            Debug.WriteLine($"streaming disconnected : {desc}");
            cnt.Response.OutputStream.Dispose();
            cnt.Response.Close();
        }
    }
}
