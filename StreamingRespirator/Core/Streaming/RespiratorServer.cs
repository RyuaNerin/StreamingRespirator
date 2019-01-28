using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        private static long ParseOwnerId(HeaderCollection headers)
        {
            if (!headers.Headers.TryGetValue("Authorization", out var authHeader))
                return 0;

            if (string.IsNullOrWhiteSpace(authHeader.Value))
                return 0;

            return ParseOwnerId(authHeader.Value);
        }
        private static long ParseOwnerId(NameValueCollection headers)
        {
            var header = headers.Get("Authorization");

            if (string.IsNullOrWhiteSpace(header))
                return 0;

            return ParseOwnerId(header);
        }

        private static long ParseOwnerId(string authorizationHeader)
        {
            var m = Regex.Match(authorizationHeader, "oauth_token=\"([0-9]+)\\-");
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
                
                var ownerId = ParseOwnerId(e.HttpClient.Request.Headers);
                if (ownerId != 0)
                {
                    res.StatusCode = (int)HttpStatusCode.Found;
                    res.StatusDescription = "Found";
                    res.Headers.AddHeader("Location", this.m_streamingUrl + ownerId);

                    Debug.WriteLine($"redirect to {this.m_streamingUrl + ownerId}");
                }

                e.Respond(res);
            }

            return Task.FromResult(true);
        }

        private Task Proxy_AfterResponse(object sender, SessionEventArgs e)
        {
            // POST https://api.twitter.com/1.1/statuses/destroy/:id.json
            // POST https://api.twitter.com/1.1/statuses/unretweet/:id.json

            if (e.HttpClient.Request.Method.ToUpper() == "POST")
            {
                var uri = e.HttpClient.Request.RequestUri;

                if (uri.Host == "api.twitter.com")
                {
                    var type = 0;

                         if (uri.AbsolutePath.StartsWith("/1.1/statuses/destroy/"  )) type = 1;
                    else if (uri.AbsolutePath.StartsWith("/1.1/statuses/unretweet/")) type = 2;

                    if (type != 0)
                    {
                        var idStr = uri.AbsolutePath;
                        idStr = idStr.Substring(idStr.LastIndexOf('/') + 1);
                        idStr = idStr.Substring(0, idStr.IndexOf('.'));

                        if (long.TryParse(idStr, out var id))
                        {
                            var ownerId = ParseOwnerId(e.HttpClient.Request.Headers);
                            if (ownerId != 0)
                            {
                                var twitClient = TwitterClientFactory.GetInsatnce(ownerId);
                                if (twitClient != null)
                                {
                                    if (type == 1) twitClient.CallStatusDestroy(id);
                                    if (type == 2) twitClient.CallStatusUnreweet(id);
                                }
                            }
                        }
                    }
                }
            }

            return Task.FromResult(true);
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

            long ownerId = ParseOwnerId(cnt.Request.Headers);

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

                    using (var sc = new StreamingConnection(new WaitableStream(cnt.Response.OutputStream), ownerId, desc))
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
            cnt.Response.Close();
        }
    }
}
