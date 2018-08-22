using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Remoting;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Http.Responses;
using Titanium.Web.Proxy.Models;

namespace StreamingRespirator
{
    public partial class Form1 : Form
    {
        public static Form1 Instance { get; private set; }

        public Form1()
        {
            Instance = this;

            InitializeComponent();
            
            CefSharpSettings.Proxy = null;

            this.m_browser = new ChromiumWebBrowser("https://tweetdeck.twitter.com/")
            {
                Dock = DockStyle.Fill,
                BrowserSettings =
                {
                    DefaultEncoding           = "UTF-8",
                    WebGl                     = CefState.Disabled,
                    Plugins                   = CefState.Disabled,
                    JavascriptAccessClipboard = CefState.Disabled,
                    ImageLoading              = CefState.Disabled,
                    JavascriptCloseWindows    = CefState.Disabled,
                    ApplicationCache          = CefState.Enabled,
                },
                RequestHandler = new ChromeRequestHandler()
            };

            this.m_proxy = new ProxyServer();
            this.m_proxy.BeforeRequest += this.Sr_BeforeRequest;
            this.m_proxy.ServerCertificateValidationCallback += (s, e) => { e.IsValid = true; return Task.FromResult(0); };
            this.m_proxy.ClientCertificateSelectionCallback += (s, e) => Task.FromResult(0);
            this.m_proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, 8800, true));
            this.m_proxy.Start();

            this.m_httpStreamingListener = new HttpListener();
            this.m_httpStreamingListener.Prefixes.Add("http://127.0.0.1:8801/");
            this.m_httpStreamingListener.Start();
            this.m_httpStreamingListener.BeginGetContext(this.GetHttpContext, null);


            this.Controls.Add(this.m_browser);
        }

        private readonly ChromiumWebBrowser m_browser;
        private readonly ProxyServer m_proxy;
        private readonly HttpListener m_httpStreamingListener;

        private readonly List<WaitingStream> m_stream = new List<WaitingStream>();

        private void GetHttpContext(IAsyncResult ar)
        {
            var cnt = this.m_httpStreamingListener.EndGetContext(ar);
            this.m_httpStreamingListener.BeginGetContext(this.GetHttpContext, null);
            
            Debug.WriteLine($"streaming connected : {cnt.Request.LocalEndPoint} - {cnt.Request.RemoteEndPoint}");

            ThreadPool.QueueUserWorkItem(
                e =>
                {
                    var eCnt = (HttpListenerContext)e;

                    eCnt.Response.AppendHeader("content-type", "text/json; charset=utf-8");
                    eCnt.Response.SendChunked = true;

                    var ws = new WaitingStream(eCnt.Response.OutputStream, $"{cnt.Request.LocalEndPoint} - {cnt.Request.RemoteEndPoint}");

                    lock (this.m_stream)
                        this.m_stream.Add(ws);

                    ws.WaitHandle.WaitOne();
                    
                    Debug.WriteLine($"streaming disconnected : {eCnt.Request.LocalEndPoint} - {eCnt.Request.RemoteEndPoint}");

                    eCnt.Response.Close();
                }, cnt);
        }

        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting           = Formatting.None,
            DateFormatString     = "ddd MMM dd HH:mm:ss +ffff yyyy"
        };
        private long m_lastTweet = 0;
        public void NewTweet(string data, string url)
        {
            JToken ja = null;

            try
            {
                ja = JToken.Parse(data);
            }
            catch
            {
                return;
            }

            foreach (JObject jo in ja)
            {
                var id = jo["id"].Value<long>();

                if (this.m_lastTweet < id)
                {
                    Write(JsonConvert.SerializeObject(jo, Jss));

                    this.m_lastTweet = id;
                }
            }
        }

        private void Write(string data)
        {
            var buff = Encoding.UTF8.GetBytes(data);

            WaitingStream[] streamArray;
            
            lock (this.m_stream)
                streamArray = this.m_stream.ToArray();

            foreach (var stream in streamArray)
            {
                ThreadPool.QueueUserWorkItem(
                    e =>
                    {
                        (var eStream, var eData) = (ValueTuple<WaitingStream, byte[]>)e;

                        try
                        {                            
                            eStream.Write(eData, 0, eData.Length);
                            eStream.Flush();

                            Debug.WriteLine($"Streaming. Size: {eData.Length} - {eStream.Description}");
                        }
                        catch (HttpListenerException ex)
                        {
                            Debug.WriteLine($"exception socket {eStream.Description}");
                            Debug.WriteLine(ex.ToString());

                            lock (this.m_stream)
                            {
                                eStream.Close();
                                eStream.Dispose();

                                this.m_stream.Remove(eStream);
                            }
                        }
                    }, (stream, buff));
            }
        }

        private async Task Sr_BeforeRequest(object sender, SessionEventArgs e)
        {
            var uri = e.WebSession.Request.RequestUri;

            Debug.WriteLine($"proxy request : {uri.AbsoluteUri}");

            if (uri.Host == "userstream.twitter.com" &&
                uri.AbsolutePath == "/1.1/user.json")
            {
                Debug.WriteLine($"redirected : {uri.AbsoluteUri}");

                var res = new Response(new byte[0])
                {
                    HttpVersion = e.WebSession.Request.HttpVersion,
                    StatusCode = (int)HttpStatusCode.Found,
                    StatusDescription = "Found"
                };

                res.Headers.AddHeader("Location", "http://127.0.0.1:8801/");

                e.Respond(res);
            }
        }

        private class ChromeRequestHandler : IRequestHandler
        {
            private readonly Dictionary<ulong, ResponseFilter> m_filters = new Dictionary<ulong, ResponseFilter>();

            public IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                if (request.Method == "GET" && request.Url.Contains("home_timeline"))
                {
                    var dataFilter = new ResponseFilter();

                    lock (this.m_filters)
                        this.m_filters.Add(request.Identifier, dataFilter);

                    return dataFilter;
                }

                return null;
            }
            public void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
            {
                if (request.Method == "GET" && request.Url.Contains("home_timeline"))
                {
                    lock (this.m_filters)
                    {
                        if (this.m_filters.TryGetValue(request.Identifier, out var filter))
                        {
                            this.m_filters.Remove(request.Identifier);

                            var uri = request.Url;

                            ThreadPool.QueueUserWorkItem(e =>
                            {
                                var ft = (ResponseFilter)e;

                                Form1.Instance.NewTweet(Encoding.UTF8.GetString(ft.Data), uri);

                                ft.Dispose();
                            }, filter);
                        }
                    }
                }
            }

            public bool CanGetCookies(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request)
            => true;
            public bool CanSetCookie(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, CefSharp.Cookie cookie)
            => true;

            public bool GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
            => false;
            public bool OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect)
            => false;
            public CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
            {
                callback.Dispose();
                return CefReturnValue.Continue;
            }
            public bool OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
            {
                callback.Dispose();
                return false;
            }
            public bool OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
            => false;
            public void OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath)
            {
            }
            public bool OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url)
            => false;
            public bool OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
            {
                callback.Dispose();
                return false;
            }
            public void OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status)
            {
            }
            public void OnRenderViewReady(IWebBrowser browserControl, IBrowser browser)
            {
            }
            public void OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
            {
            }
            public bool OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
                => false;
            public bool OnSelectClientCertificate(IWebBrowser browserControl, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
            {
                callback.Dispose();
                return false;
            }
        }

        public class ResponseFilter : IResponseFilter
        {
            private readonly MemoryStream m_buffer = new MemoryStream(32768);

            public byte[] Data
                => this.m_buffer.ToArray();

            bool IResponseFilter.InitFilter()
            {
                return true;
            }

            FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
            {
                if (dataIn == null)
                {
                    dataInRead = 0;
                    dataOutWritten = 0;

                    return FilterStatus.Done;
                }

                var len = Math.Min(dataIn.Length, dataOut.Length);
                var buffer = new byte[len];

                var read = dataIn.Read(buffer, 0, (int)len);

                this.m_buffer.Write(buffer, 0, read);
                      dataOut.Write(buffer, 0, read);

                dataInRead     = read;
                dataOutWritten = read;

                return FilterStatus.Done;
            }

            private static void CopyStream(Stream from, Stream to, int length)
            {
                byte[] buffer = new byte[32768];
                int read;
                while (length > 0 && (read = from.Read(buffer, 0, Math.Min(32768, length))) > 0)
                {
                    to.Write(buffer, 0, read);
                    length -= read;
                }
            }

            public void Dispose()
            {
                this.m_buffer.Dispose();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.m_browser.ShowDevTools();
        }

        private class WaitingStream : Stream
        {
            private readonly Stream m_stream;
            private readonly ManualResetEventSlim m_event = new ManualResetEventSlim(false);

            public WaitingStream(Stream baseStream, string description)
            {
                this.m_stream = baseStream;
                this.Description = description;
            }

            public string Description { get; }

            protected override void Dispose(bool disposing)
            {
                this.m_event.Set();

                this.m_event.Dispose();

                base.Dispose(disposing);
            }

            public override void Close()
            {
                    base.Close();
                this.m_event.Set();
            }

            public WaitHandle WaitHandle
                => this.m_event.WaitHandle;

            public override bool CanRead
                => this.m_stream.CanRead;

            public override bool CanSeek
                => this.m_stream.CanSeek;

            public override bool CanTimeout
                => this.m_stream.CanTimeout;

            public override bool CanWrite
                => this.m_stream.CanWrite;

            public override long Length
                => this.m_stream.Length;

            public override int ReadTimeout
            {
                get => this.m_stream.ReadTimeout;
                set => this.m_stream.ReadTimeout = value;
            }

            public override int WriteTimeout
            {
                get => this.m_stream.WriteTimeout;
                set => this.m_stream.WriteTimeout = value;
            }

            public override long Position
            {
                get => this.m_stream.Position;
                set => this.m_stream.Position = value;
            }

            public override ObjRef CreateObjRef(Type requestedType)
                => this.m_stream.CreateObjRef(requestedType);

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
                => this.m_stream.BeginRead(buffer, offset, count, callback, state);

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
                => this.m_stream.BeginWrite(buffer, offset, count, callback, state);

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
                => this.m_stream.CopyToAsync(destination, bufferSize, cancellationToken);

            public override int EndRead(IAsyncResult asyncResult)
                => this.m_stream.EndRead(asyncResult);

            public override void EndWrite(IAsyncResult asyncResult)
                => this.m_stream.EndWrite(asyncResult);

            public override void Flush()
                => this.m_stream.Flush();

            public override Task FlushAsync(CancellationToken cancellationToken)
                => this.m_stream.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count)
                => this.m_stream.Read(buffer, offset, count);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => this.m_stream.ReadAsync(buffer, offset, count, cancellationToken);

            public override int ReadByte()
                => this.m_stream.ReadByte();

            public override long Seek(long offset, SeekOrigin origin)
                => this.m_stream.Seek(offset, origin);

            public override void SetLength(long value)
                => this.m_stream.SetLength(0);

            public override void Write(byte[] buffer, int offset, int count)
                => this.m_stream.Write(buffer, offset, count);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => this.m_stream.WriteAsync(buffer, offset, count, cancellationToken);

            public override void WriteByte(byte value)
                => this.m_stream.WriteByte(value);
        }
    }
}
