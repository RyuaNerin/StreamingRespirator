using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
            this.m_proxy.CertificateManager.RootCertificate = new X509Certificate2(Properties.Resources.pfx, string.Empty, X509KeyStorageFlags.Exportable);

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

        private static bool TryGetOwnerId(Uri uri, HeaderCollection headers, string reqeustBody, out long ownerId)
        {
            return
                (
                    headers.Headers.TryGetValue("Authorization", out var head) &&
                    TryParseOwnerId(head.Value, out ownerId)
                )
                || TryParseOwnerId(uri.Query,   out ownerId) 
                || TryParseOwnerId(reqeustBody, out ownerId);
        }
        private static bool TryGetOwnerId(Uri uri, NameValueCollection headers, string reqeustBody, out long ownerId)
        {
            return
                   TryParseOwnerId(headers.Get("Authorization"), out ownerId)
                || TryParseOwnerId(uri.Query,                    out ownerId)
                || TryParseOwnerId(reqeustBody,                  out ownerId);
        }

        private static bool TryParseOwnerId(string authorizationHeader, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(authorizationHeader))
                return false;

            var m = Regex.Match(authorizationHeader, "oauth_token=\"?([0-9]+)\\-");
            if (!m.Success)
                return false;

            return m.Success && long.TryParse(m.Groups[1].Value, out value);
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
                
                if (TryGetOwnerId(uri, e.HttpClient.Request.Headers, null, out var ownerId))
                {
                    res.StatusCode = (int)HttpStatusCode.Found;
                    res.StatusDescription = "Found";
                    res.Headers.AddHeader("Location", this.m_streamingUrl + ownerId);

                    Debug.WriteLine($"redirect to {this.m_streamingUrl + ownerId}");
                }

                e.Respond(res);
            }

            else if (uri.Host == "api.twitter.com")
            {
                // 삭제 패킷 전송
                // POST https://api.twitter.com/1.1/statuses/destroy/:id.json
                // POST https://api.twitter.com/1.1/statuses/unretweet/:id.json
                if (uri.AbsolutePath.StartsWith("/1.1/statuses/destroy/"))
                    ProxyDestroyOrUnretweet(e);
                else if (uri.AbsolutePath.StartsWith("/1.1/statuses/unretweet/"))
                    ProxyDestroyOrUnretweet(e);

                // 404 : id = 삭제된 트윗일 수 있음
                // 200 : 성공시 스트리밍에 전송해서 한번 더 띄우도록
                // POST https://api.twitter.com/1.1/statuses/retweet/:id.json
                else if (uri.AbsolutePath.StartsWith("/1.1/statuses/retweet/"))
                    ProxyRetweet(e);

                // 401 : in_reply_to_status_id = 삭제된 트윗일 수 있음
                // POST https://api.twitter.com/1.1/statuses/update.json
                else if (uri.AbsolutePath.StartsWith("/1.1/statuses/update.json"))
                    ProxyUpdate(e);
            }


            return Task.FromResult(true);
        }
        private static bool GetInstance(SessionEventArgs e, string requestBodyStr, out long ownerId, out TwitterClient twitClient)
        {
            twitClient = null;
            if (!TryGetOwnerId(e.HttpClient.Request.RequestUri, e.HttpClient.Request.Headers, requestBodyStr, out ownerId))
                return false;

            twitClient = null;
            if (ownerId == 0)
                return false;

            twitClient = TwitterClientFactory.GetInsatnce(ownerId);
            if (twitClient == null)
                return false;

            return true;
        }
        private static void ProxyRetweet(SessionEventArgs e)
        {
            var reqeustBody = GetResponseBody(e, out var reqeustBodyStr);

            if (!SendResponse(e, reqeustBody, out var statusCode, out var body))
                return;

            if (GetInstance(e, reqeustBodyStr, out var ownerId, out var twitClient))
            {
                if (statusCode == 404)
                    twitClient.StatusMaybeDestroyed(ParseJsonId(e.HttpClient.Request.RequestUri));
                else if (Config.Filter.ShowMyRetweet)
                {
                    var status = JsonConvert.DeserializeObject<TwitterStatus>(body);

                    if (status != null)
                        twitClient.SendStatus(status);
                }
            }
        }
        private static void ProxyDestroyOrUnretweet(SessionEventArgs e)
        {
            var reqeustBody = GetResponseBody(e, out var reqeustBodyStr);

            if (!SendResponse(e, reqeustBody, out var statusCode, out var body))
                return;

            if (GetInstance(e, reqeustBodyStr, out var ownerId, out var twitClient))
            {
                if (statusCode == 200)
                {
                    var status = JsonConvert.DeserializeObject<TwitterStatus>(body);

                    if (status != null)
                        twitClient.StatusDestroyed(status.Id);
                }
            }
        }
        private static void ProxyUpdate(SessionEventArgs e)
        {
            var reqeustBody = GetResponseBody(e, out var reqeustBodyStr);
            NameValueCollection postData = null;

            // d @ScreenName data
            if (e.HttpClient.Request.ContentType.Contains("application/x-www-form-urlencoded"))
            {
                postData = HttpUtility.ParseQueryString(reqeustBodyStr, Encoding.UTF8);
            }

            int statusCode;
            if (SendDMInsteadOfPublic(e, reqeustBodyStr, postData, out statusCode))
                return;
            
            if (!SendResponse(e, reqeustBody, out statusCode, out var body))
                return;

            if (GetInstance(e, reqeustBodyStr, out var ownerId, out var twitClient))
            {
                if (statusCode == 401)
                {
                    var idStr = postData["in_reply_to_status_id"];
                    if (idStr != null && long.TryParse(idStr, out var id))
                        twitClient.StatusMaybeDestroyed(id);
                }
            }
        }
        private static void Send500Response(SessionEventArgs e)
        {
            var resProxy = new Response
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                StatusDescription = "Internal Server Error",
            };

            e.Respond(resProxy, true);
        }
        private static byte[] GetResponseBody(SessionEventArgs e, out string requestBodyStr)
        {
            if (e.HttpClient.Request.HasBody)
            {
                var task = e.GetRequestBody();
                task.Wait();

                var buff = task.Result;
                requestBodyStr = Encoding.UTF8.GetString(buff);

                return buff;
            }

            requestBodyStr = null;
            return null;
        }
        private static bool SendResponse(SessionEventArgs e, byte[] requestBody, out int statusCode, out string body)
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

            if (e.HttpClient.Request.HasBody)
            {
                reqHttp.GetRequestStream().Write(requestBody, 0, requestBody.Length);
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
            {
                Send500Response(e);
                return false;
            }
            
            using (resHttp)
            {
                statusCode = (int)resHttp.StatusCode;

                using (var mem = new MemoryStream(Math.Min((int)resHttp.ContentLength, 4096)))
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
                            case "content-length":
                                break;

                            default:
                                resProxy.Headers.AddHeader(headerName, resHttp.Headers[headerName]);
                                break;
                        }
                    }

                    e.Respond(resProxy);

                    return true;
                }
            }
        }

        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting = Formatting.None,
        };
        private static bool SendDMInsteadOfPublic(SessionEventArgs e, string reqeustBodyStr, NameValueCollection postData, out int statusCode)
        {
            statusCode = (int)HttpStatusCode.NotFound;

            if (postData == null)
                return false;

            var status = postData["status"];
            if (status == null)
                return false;
            
            var m = Regex.Match(status, "^d @?([A-Za-z0-9_]{1,15}) (.+)$");
            if (!m.Success)
                return false;

            var screenName = m.Groups[1].Value;
            var text = m.Groups[2].Value;
            
            if (!GetInstance(e, reqeustBodyStr, out var ownerId, out var twitClient))
                return false;

            var userId = twitClient.UserCache.GetUserIdByScreenName(screenName);
            if (userId == 0)
            {
                var user = twitClient.Credential.Reqeust<TwitterUser>("GET", "https://api.twitter.com/1.1/users/show.json?screen_name=" + Uri.EscapeUriString(screenName));
                if (user == null)
                {
                    statusCode = (int)HttpStatusCode.NotFound;
                    return true;
                }

                twitClient.UserCache.IsUpdated(user);
            }

            var dmData = new DirectMessageNew();
            dmData.Data.Type = "message_create";
            dmData.Data.MessageCreate.Target.RecipientId = userId.ToString();
            dmData.Data.MessageCreate.MessageData.Text = text;

            var dmDataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dmData, Jss));

            var succ = twitClient.Credential.Reqeust("POST", "https://api.twitter.com/1.1/direct_messages/events/new.json", dmDataBytes);

            var resProxy = new Response()
            {
                HttpVersion       = e.HttpClient.Request.HttpVersion,
                StatusCode        = succ ? (int)HttpStatusCode.OK  : (int)HttpStatusCode.NotFound ,
                StatusDescription = succ ?                    "OK" :                    "NotFound",
            };
            e.Respond(resProxy);

            return true;
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

            long ownerId;

            if (!TryGetOwnerId(cnt.Request.Url, cnt.Request.Headers, null, out ownerId))
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
