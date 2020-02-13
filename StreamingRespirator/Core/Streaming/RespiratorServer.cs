using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming.Proxy;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Extensions;

namespace StreamingRespirator.Core.Streaming
{
    /// <summary>
    /// Constructor 실행 시 서버 바로 시작됨. Dispose 하면 서버 정지됨.
    /// </summary>
    internal class RespiratorServer : IDisposable
    {
        private readonly TcpListener m_tcpListener;

        private readonly Barrier m_connectionsBarrier = new Barrier(0);
        private readonly LinkedList<TcpClient> m_connections = new LinkedList<TcpClient>();

        public bool IsRunning { get; private set; }

        public RespiratorServer()
        {
            this.m_tcpListener = new TcpListener(new IPEndPoint(IPAddress.Loopback, Config.Instance.Proxy.Port));

            if (this.IsRunning)
                return;
            this.IsRunning = true;

            this.m_tcpListener.Start(64);
            this.m_tcpListener.BeginAcceptTcpClient(this.AcceptClient, null);
        }
        ~RespiratorServer()
        {
            this.Dispose(false);
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        private bool m_disposed;
        protected void Dispose(bool disposing)
        {
            if (this.m_disposed) return;
            this.m_disposed = true;

            if (disposing)
            {
                Tunnel.CancelAllTunnel();

                this.m_tcpListener.Stop();

                TcpClient[] currentConnections;
                lock (this.m_connections)
                {
                    currentConnections = this.m_connections.ToArray();
                }

                Parallel.ForEach(
                    currentConnections,
                    client =>
                    {
                        try
                        {
                            client.Client.Shutdown(SocketShutdown.Both);
                            client.Client.Disconnect(false);
                        }
                        catch
                        {
                        }

                        try
                        {
                            client.Close();
                        }
                        catch
                        {
                        }
                    });

                try
                {
                    this.m_connectionsBarrier.SignalAndWait();
                }
                catch
                {
                }

                this.m_connectionsBarrier.Dispose();
            }
        }

        private void AcceptClient(IAsyncResult ar)
        {
            try
            {
                var client = this.m_tcpListener.EndAcceptTcpClient(ar);

                new Thread(this.SocketThread).Start(client);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
            finally
            {
                try
                {
                    this.m_tcpListener.BeginAcceptTcpClient(this.AcceptClient, null);
                }
                catch
                {
                }
            }
        }

        private void SocketThread(object socketObject)
        {
            using (var client = (TcpClient)socketObject)
            using (var clientStream = client.GetStream())
            {
                var desc = $"{client.Client.LocalEndPoint} > {client.Client.RemoteEndPoint}";

                LinkedListNode<TcpClient> clientNode;

                this.m_connectionsBarrier.AddParticipant();
                lock (this.m_connections)
                {
                    clientNode = this.m_connections.AddLast(client);
                    Debug.WriteLine($"Connected {desc} ({this.m_connections.Count})");
                }

                try
                {
                    this.SocketThreadSub(clientStream);
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }

                lock (this.m_connections)
                {
                    this.m_connections.Remove(clientNode);
                    Debug.WriteLine($"Disconnected {desc} {this.m_connections.Count}");
                }
                try
                {
                    client.Close();
                }
                catch
                {
                }

                try
                {
                    this.m_connectionsBarrier.RemoveParticipant();
                }
                catch
                {
                }
            }
        }

        private void SocketThreadSub(Stream clientStream)
        {
            using (var req = ProxyRequest.Parse(clientStream, false))
            {
                Tunnel t = null;

                // HTTPS
                if (req.Method == "CONNECT")
                {
                    // 호스트 확인하고 처리
                    var host = req.RemoteHost;

                    switch (host)
                    {
                        case "userstream.twitter.com":
                            t = new TunnelSslMitm(req, clientStream, Certificates.Client, this.HostStreaming);
                            break;

                        case "api.twitter.com":
                            t = new TunnelSslMitm(req, clientStream, Certificates.Client, this.HostAPI);
                            break;

                        default:
                            t = new TunnelSslForward(req, clientStream);
                            break;
                    }
                }

                // HTTP
                else
                {
                    t = new TunnelPlain(req, clientStream);
                }

                using (t)
                    t.Handle();
            }
        }

        private bool HostStreaming(ProxyContext ctx)
        {
            var desc = $"{ctx.Request.RequestUri}";
            Debug.WriteLine($"streaming connected : {desc}");

            if (!ctx.Request.RequestUri.AbsolutePath.Equals("/1.1/user.json", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = HttpStatusCode.NotFound;
                return true;
            }

            if (!TryGetOwnerId(ctx.Request.RequestUri, ctx.Request.Headers, null, out var ownerId))
            {
                ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
                return true;
            }

            var twitterClient = TwitterClientFactory.GetClient(ownerId);
            if (twitterClient == null)
            {
                ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
                return true;
            }

            ctx.Response.StatusCode = HttpStatusCode.OK;

            ctx.Response.Headers.Set("Content-type", "application/json; charset=utf-8");
            ctx.Response.Headers.Set("Connection", "close");
            ctx.Response.SetChunked();

            using (var sc = new StreamingConnection(new WaitableStream(ctx.Response.ResponseStream), twitterClient))
            {
                twitterClient.AddConnection(sc);

                sc.Stream.WaitHandle.WaitOne();

                twitterClient.RemoveStream(sc);
            }

            Debug.WriteLine($"streaming disconnected : {desc}");

            return true;
        }

        private bool HostAPI(ProxyContext ctx)
        {
            switch (ctx.Request.RequestUri.AbsolutePath)
            {
                // api 호출 후 스트리밍에 destroy 날려주는 함수
                // POST https://api.twitter.com/1.1/statuses/destroy/:id.json
                // POST https://api.twitter.com/1.1/statuses/unretweet/:id.json
                case string path when path.StartsWith("/1.1/statuses/destroy/", StringComparison.OrdinalIgnoreCase) ||
                                      path.StartsWith("/1.1/statuses/unretweet/", StringComparison.OrdinalIgnoreCase):
                    return HandleDestroyOrUnretweet(ctx);

                // api 호출 후 스트리밍에 리트윗 날려주는 함수
                // 404 : id = 삭제된 트윗일 수 있음
                // 200 : 성공시 스트리밍에 전송해서 한번 더 띄우도록
                // POST https://api.twitter.com/1.1/statuses/retweet/:id.json
                case string path when path.StartsWith("/1.1/statuses/retweet/", StringComparison.OrdinalIgnoreCase):
                    return HandleRetweet(ctx);

                // d @ID 로 DM 보내는 기능 추가된 함수.
                // 401 : in_reply_to_status_id = 삭제된 트윗일 수 있음
                // POST https://api.twitter.com/1.1/statuses/update.json
                case string path when path.Equals("/1.1/statuses/update.json", StringComparison.OrdinalIgnoreCase):
                    return HandleUpdate(ctx);

                default:
                    return HandleTunnel(ctx);
            }
        }

        private static readonly JsonSerializer JsonSerializer = new JsonSerializer();

        private static bool HandleDestroyOrUnretweet(ProxyContext ctx)
        {
            if (!TryGetTwitterClient(ctx, null, out var twitClient))
                return false;

            if (!TryCallAPIThenSetContext<TwitterStatus>(ctx, null, twitClient, out var statusCode, out var status))
            {
                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                return true;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                twitClient.StatusDestroyed(status.Id);
            }

            return true;
        }

        private static bool HandleRetweet(ProxyContext ctx)
        {
            if (!TryGetTwitterClient(ctx, null, out var twitClient))
                return false;

            // 내 리트윗 다시 표시 기능을 끄면 별도 처리를 해줄 필요가 없음.
            if (!Config.Instance.Filter.ShowMyRetweet)
            {
                if (TryCallAPIThenSetContext(ctx, null, twitClient, out _))
                    return true;

                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                return true;
            }

            // Azurea 기준으로 Retweet 후에 full_text 값이 날아오지 않는다.
            // 1. full_text 값을 얻기 위해
            // 2. 리트윗 API 호출 한 다음
            // 3. (2) 의 호출을 그대로 전송하고
            // 4. (2) 가 성공하면 리트윗 한 트윗을 statuses/show.json 한 후 그 결과값을 리턴한다.

            var res = CallAPI(ctx, null, twitClient);
            if (res == null)
            {
                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                return true;
            }

            TwitterStatus status = null;

            using (res)
            {
                using (var stream = res.GetResponseStream())
                {
                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        // 트윗이 삭제된 경우 404 반환됨.
                        if (res.StatusCode == HttpStatusCode.NotFound)
                        {
                            twitClient.StatusMaybeDestroyed(ParseJsonId(ctx.Request.RequestUri));
                        }

                        ctx.Response.FromHttpWebResponse(res, stream);
                        return true;
                    }

                    using (var mem = new MemoryStream(4096))
                    {
                        stream.CopyTo(mem);

                        using (var streamReader = new StreamReader(mem, Encoding.UTF8))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            mem.Position = 0;
                            ctx.Response.FromHttpWebResponse(res, mem);

                            mem.Position = 0;
                            status = JsonSerializer.Deserialize<TwitterStatus>(jsonReader);
                        }
                    }

                }
            }

            if (status == null || status.AdditionalData.ContainsKey("full_text"))
            {
                twitClient.SendStatus(status);
                return true;
            }

            try
            {
                var reqShow = twitClient.Credential.CreateReqeust("GET", $"https://api.twitter.com/1.1/statuses/show.json?id={status.Id}&include_entities=1");

                using (var resShow = (HttpWebResponse)reqShow.GetResponse())
                {
                    if (resShow.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = resShow.GetResponseStream())
                        using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var newStatus = JsonSerializer.Deserialize<TwitterStatus>(jsonReader);
                            if (newStatus != null)
                            {
                                twitClient.SendStatus(newStatus);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);

                twitClient.SendStatus(status);
            }

            return true;

            long ParseJsonId(Uri uri)
            {
                var idStr = uri.AbsolutePath;
                idStr = idStr.Substring(idStr.LastIndexOf('/') + 1);
                idStr = idStr.Substring(0, idStr.IndexOf('.'));

                return long.TryParse(idStr, out var id) ? id : 0;
            }
        }

        private static bool HandleUpdate(ProxyContext ctx)
        {
            // Azurea 에서 HTTP 호출 시 헤더 사용이 불가능하므로,
            // Azurea Custom Via 에서 OAuth Header 를 POST 에 넣어서 전송하기 때문에 이렇게 처리함.
            // https://github.com/RyuaNerin/CustomViaForAzurea
            using (var mem = new MemoryStream(4096))
            using (var memReader = new StreamReader(mem, Encoding.UTF8))
            {
                ctx.Request.RequestBodyReader?.CopyTo(mem);

                mem.Position = 0;
                var bodyStr = memReader.ReadToEnd();

                if (!TryGetTwitterClient(ctx, bodyStr, out var twitClient))
                    return false;

                NameValueCollection postData = null;

                if (ctx.Request.Headers.Get("Content-Type")?.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    postData = HttpUtility.ParseQueryString(bodyStr, Encoding.UTF8);

                    var status = postData["status"];
                    if (status != null)
                    {
                        var m = Regex.Match(status, "^d @?([A-Za-z0-9_]{3,15}) (.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            if (!TrySendDirectMessageThenSetContext(ctx, twitClient, m.Groups[1].Value, m.Groups[2].Value, out _))
                            {
                                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                                return true;
                            }

                            return true;
                        }
                    }
                }

                // client 를 넘겨주지 않아서 Client 의 App Name 을 표시.
                // client 를 넘겨주면 via Tweetdeck 으로 고정된다.
                mem.Position = 0;
                if (!TryCallAPIThenSetContext(ctx, mem, null, out var statusCode))
                {
                    ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                    return true;
                }

                // 트윗이 삭제된 경우 401 메시지가 발생한다.
                if (statusCode == HttpStatusCode.Unauthorized)
                {
                    if (postData != null)
                    {
                        if (long.TryParse(postData["in_reply_to_status_id"], out var id))
                            twitClient.StatusMaybeDestroyed(id);
                    }
                }
            }

            return true;
        }

        private static bool HandleTunnel(ProxyContext ctx)
        {
            if (!TryGetTwitterClient(ctx, null, out var twitClient))
                return false;

            if (!TryCallAPIThenSetContext(ctx, null, twitClient, null, out _, out _))
            {
                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                return true;
            }

            return true;
        }

        private static bool TryGetOwnerId(Uri uri, WebHeaderCollection authorizationValue, string body, out long ownerId)
        {
            return TryParseOwnerId(authorizationValue.Get("Authorization"), out ownerId)
                || TryParseOwnerId(uri.Query, out ownerId)
                || TryParseOwnerId(body, out ownerId);

            bool TryParseOwnerId(string authorizationHeader, out long value)
            {
                value = 0;
                if (string.IsNullOrWhiteSpace(authorizationHeader))
                    return false;

                var m = Regex.Match(authorizationHeader, "oauth_token=\"?([0-9]+)\\-");
                if (!m.Success)
                    return false;

                return m.Success && long.TryParse(m.Groups[1].Value, out value);
            }
        }
        private static bool TryGetTwitterClient(ProxyContext ctx, string requestBody, out TwitterClient twitClient)
        {
            twitClient = null;
            if (!TryGetOwnerId(ctx.Request.RequestUri, ctx.Request.Headers, requestBody, out var ownerId))
                return false;

            twitClient = TwitterClientFactory.GetInsatnce(ownerId);
            if (twitClient == null)
                return false;

            return true;
        }

        /// <summary>
        /// Response 전송 하므로 사용에 주의
        /// 오류 발생 시 false 를 반환함. return 해줘야 함.
        /// </summary>
        private static bool TryCallAPIThenSetContext(ProxyContext ctx, Stream proxyReqBody, TwitterClient client, out HttpStatusCode responseStatusCode)
            => TryCallAPIThenSetContext(ctx, proxyReqBody, client, null, out responseStatusCode, out _);

        /// <summary>
        /// Response 전송 하므로 사용에 주의
        /// 오류 발생 시 false 를 반환함. return 해줘야 함.
        /// </summary>
        private static bool TryCallAPIThenSetContext<T>(ProxyContext ctx, Stream proxyReqBody, TwitterClient client, out HttpStatusCode responseStatusCode, out T response)
            where T: class
        {
            var res = TryCallAPIThenSetContext(ctx, proxyReqBody, client, typeof(T), out responseStatusCode, out var obj);
            response = obj as T;
            return res;
        }

        private static bool TryCallAPIThenSetContext(ProxyContext ctx, Stream proxyReqBody, TwitterClient client, Type type, out HttpStatusCode responseStatusCode, out object response)
        {
            responseStatusCode = 0;
            response = default;

            var resHttp = CallAPI(ctx, proxyReqBody, client);

            if (resHttp == null)
            {
                return false;
            }

            using (resHttp)
            {
                responseStatusCode = resHttp.StatusCode;

                using (var stream = resHttp.GetResponseStream())
                {
                    if (type == null)
                    {
                        ctx.Response.FromHttpWebResponse(resHttp, stream);
                    }
                    else
                    {
                        using (var mem = new MemoryStream(Math.Min((int)resHttp.ContentLength, 4096)))
                        {
                            stream.CopyTo(mem);

                            mem.Position = 0;
                            ctx.Response.FromHttpWebResponse(resHttp, mem);

                            using (var reader = new StreamReader(mem, Encoding.UTF8))
                            using (var jsonReader = new JsonTextReader(reader))
                            {
                                mem.Position = 0;
                                response = JsonSerializer.Deserialize(jsonReader, type);
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static HttpWebResponse CallAPI(ProxyContext ctx, Stream proxyReqBody, TwitterClient client)
        {
            var reqHttp = ctx.Request.CreateRequest((method, uri) => client?.Credential.CreateReqeust(method, uri), client == null);

            if (proxyReqBody == null)
            {
                proxyReqBody = ctx.Request.RequestBodyReader;
                var v = ctx.Request.Headers.Get("Content-Length");
                if (v != null)
                {
                    ctx.Response.Headers.Set("Content-Length", v);
                }
            }
            else if (proxyReqBody is MemoryStream mem)
            {
                ctx.Response.Headers.Set("Content-Length", mem.Length.ToString());
            }

            proxyReqBody?.CopyTo(reqHttp.GetRequestStream());

            HttpWebResponse resHttp = null;
            try
            {
                resHttp = reqHttp.GetResponse() as HttpWebResponse;
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                    resHttp = webEx.Response as HttpWebResponse;
                else
                    SentrySdk.CaptureException(webEx);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            return resHttp;
        }

        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting = Formatting.None,
        };

        /// <summary>
        /// Response 전송 하므로 사용에 주의
        /// 오류 발생 시 false 를 반환함. return 해줘야 함.
        /// </summary>
        private static bool TrySendDirectMessageThenSetContext(ProxyContext ctx, TwitterClient twitClient, string screenName, string text, out HttpStatusCode statusCode)
        {
            statusCode = 0;

            var userId = twitClient.UserCache.GetUserIdByScreenName(screenName);
            if (userId == 0)
            {
                // 유저 정보를 얻지 못하면 User.ID 를 얻지 못하므로 실패처리했다고 보낸다.
                // DM 으로 보낼 것 퍼블릭으로 작성하면 안됨.
                var reqUser = twitClient.Credential.CreateReqeust("GET", $"https://api.twitter.com/1.1/users/show.json?screen_name={Uri.EscapeUriString(screenName)}");
                if (!reqUser.Do<TwitterUser>(out var reqUserStatusCode, out var user) || reqUserStatusCode != HttpStatusCode.OK)
                {
                    ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
                    return true;
                }

                twitClient.UserCache.IsUpdated(user);
                userId = user.Id;
            }

            var dmData = new DirectMessageNew();
            dmData.Data.Type = "message_create";
            dmData.Data.MessageCreate.Target.RecipientId = userId.ToString();
            dmData.Data.MessageCreate.MessageData.Text = text;

            var req = twitClient.Credential.CreateReqeust("POST", "https://api.twitter.com/1.1/direct_messages/events/new.json");
            req.ContentType = "application/json; charset=utf-8";

            using (var reqStream = req.GetRequestStream())
            using (var reqStreamWriter = new StreamWriter(reqStream, Encoding.UTF8))
            {
                JsonSerializer.Serialize(reqStreamWriter, dmData);
                reqStreamWriter.Flush();
            }

            if (!req.Do(out statusCode))
                return false;

            ctx.Response.StatusCode = statusCode;
            return true;
        }
    }
}
