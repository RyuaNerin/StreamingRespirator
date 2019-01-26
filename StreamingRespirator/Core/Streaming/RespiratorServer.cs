using System;
using System.Collections.Generic;
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
        private const int KeepAlivePeriod = 5 * 1000;
        
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
            if (e.HttpClient.Request.RequestUri.Host == "userstream.twitter.com")
            {
                e.DecryptSsl = true;
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private static long ParseOwnerId(string authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader)) return 0;

            var m = Regex.Match(authorizationHeader, "oauth_token=\"([0-9]+)\\-");
            return m.Success && long.TryParse(m.Groups[1].Value, out var ownerId) ? ownerId : 0;
        }

        private Task Proxy_BeforeRequest(object sender, SessionEventArgs e)
        {
            var uri = e.HttpClient.Request.RequestUri;

            Debug.WriteLine($"reqeust : {uri.AbsoluteUri}");

            if (uri.Host == "userstream.twitter.com" &&
                uri.AbsolutePath == "/1.1/user.json")
            {
                var res = new Response(new byte[0])
                {
                    HttpVersion = e.HttpClient.Request.HttpVersion,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    StatusDescription = "Unauthorized",
                };

                if (e.HttpClient.Request.Headers.Headers.TryGetValue("Authorization", out var authHeader))
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

            new Thread(this.ConnectionThread).Start(cnt);
        }

        private void ConnectionThread(object listenerContextObject)
        {
            var cnt = (HttpListenerContext)listenerContextObject;

            var desc = $"{cnt.Request.Url.AbsolutePath} / {cnt.Request.LocalEndPoint} <> {cnt.Request.RemoteEndPoint}";

            Debug.WriteLine($"streaming connected : {desc}");

            long ownerId = ParseOwnerId(cnt.Request.Headers["Authorization"]);

            if (ownerId == 0)
                if (cnt.Request.Url.AbsolutePath.Length > 1)
                    long.TryParse(cnt.Request.Url.AbsolutePath.Substring(1), out ownerId);

            if (ownerId != 0)
            {
                using (var sc = new StreamingConnection(new WaitableStream(cnt.Response.OutputStream), ownerId, desc))
                {
                    lock (this.m_connections)
                        this.m_connections.Add(sc);

                    cnt.Response.AppendHeader("Content-type", "application/json; charset=utf-8");
                    cnt.Response.AppendHeader("Connection", "close");
                    cnt.Response.SendChunked = true;

                    //////////////////////////////////////////////////

                    var td = TweetDeck.GetTweetDeck(ownerId, MainContext.Invoker);

                    if (td == null)
                    {
                        cnt.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        sc.Stream.Close();
                    }
                    else
                    {
                        td.AddConnection(sc);

                        sc.Stream.WaitHandle.WaitOne();

                        td.RemoveStream(sc);
                    }

                    lock (this.m_connections)
                        this.m_connections.Remove(sc);
                }

                //////////////////////////////////////////////////
            }

            Debug.WriteLine($"streaming disconnected : {desc}");
            cnt.Response.Close();
        }
    }
}
