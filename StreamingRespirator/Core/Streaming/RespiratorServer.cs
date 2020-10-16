using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming.Proxy;
using StreamingRespirator.Core.Streaming.Proxy.Handler;
using StreamingRespirator.Core.Streaming.Proxy.Streams;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Extensions;

namespace StreamingRespirator.Core.Streaming
{
    /// <summary>
    /// Constructor 실행 시 서버 바로 시작됨. Dispose 하면 서버 정지됨.
    /// </summary>
    internal class RespiratorServer : IDisposable
    {
        private const SslProtocols SslProtocol = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        private readonly CancellationTokenSource m_tunnelCancel = new CancellationTokenSource();

        private readonly Socket m_socketServer;

        private readonly Barrier m_connectionsBarrier = new Barrier(0);
        private readonly LinkedList<Socket> m_connections = new LinkedList<Socket>();

        public int Port { get; }

        public RespiratorServer(int port)
        {
            this.Port = port;

            this.m_socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            NativeMethods.SetHandleInformation(this.m_socketServer.Handle, NativeMethods.HANDLE_FLAGS.INHERIT, NativeMethods.HANDLE_FLAGS.NONE);

            this.m_socketServer.Bind(new IPEndPoint(IPAddress.Loopback, Config.Instance.Proxy.Port));
            this.m_socketServer.Listen(64);
            this.m_socketServer.BeginAccept(this.AcceptClient, null);

            NetworkChange.NetworkAvailabilityChanged += this.NetworkChange_NetworkAvailabilityChanged;
        }

        ~RespiratorServer()
        {
            this.Dispose(false);
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool m_disposed;
        protected void Dispose(bool disposing)
        {
            if (this.m_disposed) return;
            this.m_disposed = true;

            if (disposing)
            {
                this.m_tunnelCancel.Cancel();

                this.m_socketServer.Close();

                this.CloseAllConnections();

                this.m_tunnelCancel.Dispose();
                this.m_socketServer.Dispose();

                this.m_connectionsBarrier.Dispose();
            }
        }

        private void CloseAllConnections()
        {
            Socket[] currentConnections;
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
                        client.Shutdown(SocketShutdown.Both);
                        client.Disconnect(false);
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
                this.m_connectionsBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            // 모든 커넥션을 닫는다.
            if (!e.IsAvailable)
            {
                this.CloseAllConnections();
            }
        }

        private void AcceptClient(IAsyncResult ar)
        {
            try
            {
                new Thread(this.SocketThread).Start(this.m_socketServer.EndAccept(ar));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
            finally
            {
                try
                {
                    this.m_socketServer.BeginAccept(this.AcceptClient, null);
                }
                catch
                {
                }
            }
        }

        private void SocketThread(object socketObject)
        {
            using (var socket = (Socket)socketObject)
            using (var stream = new NetworkStream(socket))
            {
                socket.ReceiveTimeout = 30 * 1000;
                socket.SendTimeout = 30 * 1000;

                stream.ReadTimeout = 30 * 1000;
                stream.WriteTimeout = 30 * 1000;

                var desc = $"{socket.LocalEndPoint} > {socket.RemoteEndPoint}";

                LinkedListNode<Socket> socketNode;

                this.m_connectionsBarrier.AddParticipant();
                lock (this.m_connections)
                {
                    socketNode = this.m_connections.AddLast(socket);
                    Console.WriteLine($"Connected {desc} ({this.m_connections.Count})");
                }

                try
                {
                    using (var proxyStream = new ProxyStream(stream))
                        this.SocketThreadSub(proxyStream);
                }
                catch (Exception)
                {
                }

                lock (this.m_connections)
                {
                    this.m_connections.Remove(socketNode);
                    Console.WriteLine($"Disconnected {desc} {this.m_connections.Count}");
                }
                try
                {
                    socket.Close();
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

        private void SocketThreadSub(ProxyStream proxyStream)
        {
            // https 연결인지, plain 인지 확인하는 과정
            // ContentType type
            // https://tools.ietf.org/html/rfc5246#page-41
            var buff = new byte[1];
            var read = proxyStream.Peek(buff, 0, buff.Length);

            if (read != buff.Length)
                throw new NotSupportedException();

            if (buff[0] == 22)
            {
                var ssl = new SslStream(proxyStream, false);
                ssl.AuthenticateAsServer(Certificates.Client, false, SslProtocol, false);

                proxyStream = new ProxyStream(ssl);
            }

            Handler handler = null;

            if (!ProxyRequest.TryParse(proxyStream, false, out var req))
                return;

            using (req)
            {
                // HTTPS
                if (req.Method == "CONNECT")
                {
                    // 호스트 확인하고 처리
                    switch (req.RemoteHost)
                    {
                        case "userstream.twitter.com":
                        case "api.twitter.com":
                        case "localhost":
                        case "127.0.0.1":
                            handler = new TunnelSslMitm(proxyStream, this.m_tunnelCancel.Token, Certificates.Client, SslProtocol, this.HandleContext);
                            break;

                        default:
                            handler = new TunnelSslForward(proxyStream, this.m_tunnelCancel.Token);
                            break;
                    }
                }

                // HTTP
                else
                {
                    // 호스트 확인하고 처리
                    switch (req.RemoteHost)
                    {
                        case "localhost":
                        case "127.0.0.1":
                            handler = new HandlerPlain(proxyStream, this.m_tunnelCancel.Token, this.HandleContext);
                            break;

                        default:
                            handler = new TunnelPlain(proxyStream, this.m_tunnelCancel.Token);
                            break;
                    }
                }

                using (handler)
                    handler.Handle(req);
            }
        }

        private void HandleContext(ProxyContext ctx)
        {
            if (!ctx.CheckAuthentication())
                return;

            switch (ctx.Request.RequestUri.Host)
            {
                case "userstream.twitter.com":
                    this.HostStreaming(ctx);
                    break;

                case "api.twitter.com":
                    this.HostAPI(ctx);
                    break;

                case "localhost":
                case "127.0.0.1":
                    this.HostLocalhost(ctx);
                    break;
            }
        }

        private void HostLocalhost(ProxyContext ctx)
        {
            if (TrimHost("userstream.twitter.com"))
            {
                this.HostStreaming(ctx);
                return;
            }

            if (TrimHost("api.twitter.com"))
            {
                this.HostAPI(ctx);
                return;
            }

            ctx.Response.StatusCode = HttpStatusCode.BadRequest;

            bool TrimHost(string host)
            {
                if (ctx.Request.RequestUri.AbsolutePath.StartsWith($"/{host}/"))
                {
                    ctx.Request.RequestUri = new UriBuilder(ctx.Request.RequestUri)
                    {
                        Host = host,
                        Path = ctx.Request.RequestUri.AbsolutePath.Substring(host.Length + 1),
                    }.Uri;

                    return true;
                }

                return false;
            }
        }

        private void HostStreaming(ProxyContext ctx)
        {
            var desc = $"{ctx.Request.RequestUri}";
            Debug.WriteLine($"streaming connected : {desc}");

            if (!ctx.Request.RequestUri.AbsolutePath.Equals("/1.1/user.json", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = HttpStatusCode.NotFound;
                return;
            }

            if (!TryGetOwnerId(ctx.Request.RequestUri, ctx.Request.Headers, null, out var ownerId))
            {
                ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
                return;
            }

            var twitterClient = TwitterClientFactory.GetClient(ownerId);
            if (twitterClient == null)
            {
                ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
                return;
            }

            ctx.Response.StatusCode = HttpStatusCode.OK;

            ctx.Response.Headers.Set("Content-type", "application/json; charset=utf-8");
            ctx.Response.Headers.Set("Connection", "close");

            using (var sc = new StreamingConnection(new WaitableStream(ctx.Response.ResponseStream), twitterClient))
            {
                twitterClient.AddConnection(sc);

                sc.Stream.WaitHandle.WaitOne();

                twitterClient.RemoveStream(sc);
            }

            Debug.WriteLine($"streaming disconnected : {desc}");
        }

        private void HostAPI(ProxyContext ctx)
        {
            switch (ctx.Request.RequestUri.AbsolutePath)
            {
                // api 호출 후 스트리밍에 destroy 날려주는 함수
                // POST https://api.twitter.com/1.1/statuses/destroy/:id.json
                // POST https://api.twitter.com/1.1/statuses/unretweet/:id.json
                case string path when path.StartsWith("/1.1/statuses/destroy/", StringComparison.OrdinalIgnoreCase) ||
                                      path.StartsWith("/1.1/statuses/unretweet/", StringComparison.OrdinalIgnoreCase):
                    if (HandleDestroyOrUnretweet(ctx))
                        return;
                    break;

                // api 호출 후 스트리밍에 리트윗 날려주는 함수
                // 404 : id = 삭제된 트윗일 수 있음
                // 200 : 성공시 스트리밍에 전송해서 한번 더 띄우도록
                // POST https://api.twitter.com/1.1/statuses/retweet/:id.json
                case string path when path.StartsWith("/1.1/statuses/retweet/", StringComparison.OrdinalIgnoreCase):
                    if (HandleRetweet(ctx))
                        return;
                    break;

                // d @ID 로 DM 보내는 기능 추가된 함수.
                // 401 : in_reply_to_status_id = 삭제된 트윗일 수 있음
                // POST https://api.twitter.com/1.1/statuses/update.json
                case string path when path.Equals("/1.1/statuses/update.json", StringComparison.OrdinalIgnoreCase):
                    if (HandleUpdate(ctx))
                        return;
                    break;
            }

            HandleTunnel(ctx);
        }

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
            //
            // 2020-02-17 추가
            // tweet_mode=extended 로 Extended Tweet Object 를 얻어오면 140자 이상의 트윗도 사용할 수 있다.
            // 관련 메모 : https://developer.twitter.com/en/docs/tweets/tweet-updates

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
                            status = Program.JsonSerializer.Deserialize<TwitterStatus>(jsonReader);
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
                var reqShow = twitClient.Credential.CreateReqeust("GET", $"https://api.twitter.com/1.1/statuses/show.json?id={status.Id}&include_entities=1&tweet_mode=extended");

                using (var resShow = (HttpWebResponse)reqShow.GetResponse())
                {
                    if (resShow.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = resShow.GetResponseStream())
                        using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var newStatus = Program.JsonSerializer.Deserialize<TwitterStatus>(jsonReader);
                            if (newStatus != null)
                            {
                                twitClient.SendStatus(newStatus);
                            }
                        }
                    }
                }
            }
            catch (WebException)
            {
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

        private static void HandleTunnel(ProxyContext ctx)
        {
            TryGetTwitterClient(ctx, null, out var twitClient);

            if (!TryCallAPIThenSetContext(ctx, null, twitClient, null, out _, out _))
            {
                ctx.Response.Headers.Clear();
                ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
            }
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
                            {
                                mem.Position = 0;
                                response = Program.JsonSerializer.Deserialize(reader, type);
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static HttpWebResponse CallAPI(ProxyContext ctx, Stream proxyReqBody, TwitterClient client)
        {
            var reqHttp = ctx.Request.CreateRequest(client?.Credential.CreateReqeust(ctx.Request.Method, ctx.Request.RequestUri), client == null);

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
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            return resHttp;
        }

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
            using (var reqStreamWriter = new StreamWriter(reqStream))
            {
                Program.JsonSerializer.Serialize(reqStreamWriter, dmData);
                reqStreamWriter.Flush();
            }

            if (!req.Do(out statusCode))
                return false;

            ctx.Response.StatusCode = statusCode;
            return true;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll")]
            public static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);

            [Flags]
            public enum HANDLE_FLAGS : uint
            {
                NONE = 0,
                INHERIT = 1,
                PROTECT_FROM_CLOSE = 2
            }
        }
    }
}
