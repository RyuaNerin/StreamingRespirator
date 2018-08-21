using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Switchboard.Server;

namespace StreamingRespirator
{
    public partial class Form1 : Form
    {
        private readonly ProxyHandler m_proxyHandler;
        private readonly SwitchboardServer m_proxy;

        private readonly ChromiumWebBrowser m_browser;

        public Form1()
        {
            InitializeComponent();

            this.m_proxyHandler = new ProxyHandler();
            this.m_proxy = new SwitchboardServer(new IPEndPoint(IPAddress.Loopback, 8080), this.m_proxyHandler);
            this.m_proxy.Start();

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
                }
            };

            this.m_browser.RequestHandler = new ChromeRequestHandler(this.m_proxyHandler);
            
            this.Controls.Add(this.m_browser);
        }

        private class ChromeRequestHandler : IRequestHandler
        {
            public ChromeRequestHandler(ProxyHandler handler)
            {
                this.m_handler = handler;
            }

            private readonly ProxyHandler m_handler;

            private readonly Dictionary<ulong, ResponseFilter> m_filters = new Dictionary<ulong, ResponseFilter>();

            public IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                if (request.Method == "GET" && request.Url.Contains("home_timeline"))
                {
                    var dataFilter = new ResponseFilter();
                    this.m_filters.Add(request.Identifier, dataFilter);
                    return dataFilter;
                }

                return null;
            }
            public void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
            {
                if (this.m_filters.TryGetValue(request.Identifier, out var filter))
                {
                    this.m_handler.Write(Encoding.UTF8.GetString(filter.Data));

                    filter.Dispose();

                    this.m_filters.Remove(request.Identifier);
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
            private readonly MemoryStream m_buffer = new MemoryStream(4096);

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

                dataInRead = dataIn.Length;
                dataOutWritten = Math.Min(dataInRead, dataOut.Length);

                var pos = dataIn.Position;
                CopyStream(dataIn, dataOut,       (int)dataOutWritten);

                dataIn.Position = pos;
                CopyStream(dataIn, this.m_buffer, (int)dataOutWritten);

                return FilterStatus.Done;
                // return dataOutWritten < dataInRead ? FilterStatus.NeedMoreData : FilterStatus.Done;
            }

            private static void CopyStream(Stream from, Stream to, int length)
            {
                byte[] buffer = new byte[4096];
                int read;
                while (length > 0 && (read = from.Read(buffer, 0, Math.Min(4096, length))) > 0)
                {
                    to.Write(buffer, 0, read);
                    length -= read;
                }
            }

            public byte[] Data
                => this.m_buffer.ToArray();

            public void Dispose()
            {
                this.m_buffer.Dispose();
            }
        }

        public class ProxyHandler : ISwitchboardRequestHandler
        {
            private readonly Stream m_dataStream = new SlidingStream();

            public ProxyHandler()
            {
            }

            public void Write(string data)
            {
                var sb = new StringBuilder(data.Length + 10);
                sb.Append(data.Length);
                sb.Append("\r\n");
                sb.Append(data);
                sb.Append("\r\n");

                var buff = Encoding.UTF8.GetBytes(sb.ToString());

                this.m_dataStream.Write(buff, 0, buff.Length);
            }

            public async Task<SwitchboardResponse> GetResponseAsync(SwitchboardContext context, SwitchboardRequest request)
            {
                var uri = new Uri(request.RequestUri);

                if (uri.Host == "userstream.twitter.com" &&
                    uri.AbsolutePath == "/1.1/user.json")
                {
                    var auth = request.Headers[HttpRequestHeader.Authorization];
                    if (!string.IsNullOrWhiteSpace(auth))
                    {
                        var res = new CustomSwitchboardResponse();
                        res.ResponseBody = this.m_dataStream;

                        return res;
                    }

                }
                
                var originalHost = uri.Host;
                IPAddress ip;

                if (uri.HostNameType == UriHostNameType.IPv4)
                {
                    ip = IPAddress.Parse(uri.Host);
                }
                else
                {
                    var ipAddresses = await Dns.GetHostAddressesAsync(uri.Host);
                    ip = ipAddresses[0];
                }

                var backendEp = new IPEndPoint(ip, uri.Port);
                
                if (uri.Scheme != "https")
                    await context.OpenOutboundConnectionAsync(backendEp);
                else
                    await context.OpenSecureOutboundConnectionAsync(backendEp, uri.Host);
                
                await context.OutboundConnection.WriteRequestAsync(request);

                var response = await context.OutboundConnection.ReadResponseAsync();

                return response;
            }
        }

        public class CustomSwitchboardResponse : SwitchboardResponse
        {
            public CustomSwitchboardResponse()
            {
                this.Headers["Transfer-Encoding"] = "chunked";
            }
        }

        public class SlidingStream : Stream
        {
            private class DataBox
            {
                public DataBox(byte[] data)
                {
                    this.Data = data;
                    this.Length = data.Length;
                    this.Position = 0;
                }
                public byte[] Data { get; }
                public int Length { get; }
                public int Position { get; set; }
            }
            
            private DataBox m_currentData = new DataBox(new byte[0]);
            private readonly Queue<DataBox> m_innerStack = new Queue<DataBox>();
            private readonly ManualResetEventSlim m_event = new ManualResetEventSlim();

            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override bool CanSeek => false;
            
            public override long Seek(long offset, SeekOrigin origin)
                => throw new NotImplementedException();

            public override void SetLength(long value)
                => throw new NotImplementedException();

            public override long Position
            {
                get => 0;
                set { }
            }

            public override void Flush()
            { }

            public override long Length => 0;

            private readonly object m_syncWrite = new object();
            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (this.m_syncWrite)
                {
                    var buff = new byte[count];
                    Array.Copy(buffer, offset, buff, 0, count);

                    lock (this.m_innerStack)
                        this.m_innerStack.Enqueue(new DataBox(buff));

                    this.m_event.Set();
                }
            }

            private readonly object m_syncRead = new object();
            public override int Read(byte[] buffer, int offset, int count)
            {
                lock (this.m_syncRead)
                {
                    this.m_event.Wait();
                    
                    while (this.m_currentData == null || this.m_currentData.Position >= this.m_currentData.Length)
                    {
                        lock (this.m_innerStack)
                        {
                            if (this.m_innerStack.Count > 0)
                            {
                                this.m_currentData = this.m_innerStack.Dequeue();
                                break;
                            }

                            this.m_event.Reset();
                        }

                        this.m_event.Wait();
                    }

                    var toRead = Math.Min(this.m_currentData.Length - this.m_currentData.Position, count);

                    Array.Copy(this.m_currentData.Data, this.m_currentData.Position, buffer, offset, toRead);

                    this.m_currentData.Position += toRead;

                    return toRead;
                }
            }
        }
    }
}
