using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    [DebuggerDisplay("{Method} {RequestUriRaw} {Version}")]
    internal sealed class ProxyRequest : IDisposable
    {
        private readonly ProxyStream m_proxyStream;

        private ProxyRequest(Stream stream)
        {
            this.m_proxyStream = new ProxyStream(stream);
        }
        ~ProxyRequest()
        {
            this.Dispose(false);
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool m_disposed;
        private void Dispose(bool disposing)
        {
            if (this.m_disposed) return;
            this.m_disposed = true;

            if (disposing)
            {
                this.RequestBodyReader?.Dispose();
            }
        }

        public string Method        { get; private set; }
        public string RequestUriRaw { get; private set; } // CONNECT 전용
        public Uri    RequestUri    { get; set; }
        public string Version       { get; private set; }

        public string RemoteHost { get; private set; }
        public int    RemotePort { get; private set; }

        /// <summary>
        /// POST PUT PATCH 일 때만 생성됨
        /// </summary>
        public Stream RequestBodyReader { get; private set; }

        public WebHeaderCollection Headers { get; } = new WebHeaderCollection();

        public string ProxyAuthorization { get; private set; }
        public bool KeepAlive { get; private set; }

        public static bool TryParse(Stream proxyStream, bool isSsl, out ProxyRequest req)
        {
            req = new ProxyRequest(proxyStream);
            while (true)
            {
                var firstLine = req.m_proxyStream.ReadLine();
                if (firstLine == null)
                {
                    req.Dispose();
                    req = null;
                    return false;
                }

                try
                {
                    var sp = firstLine.Split(' ');
                    req.Method = sp[0];
                    req.RequestUriRaw = sp[1];
                    req.Version = sp[2];

                    if (req.Version.StartsWith("HTTP"))
                    {
                        break;
                    }
                }
                catch
                {
                    throw;
                }
            }

            string line;
            while ((line = req.m_proxyStream.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                var i = line.IndexOf(':');

                req.Headers.Add(line.Substring(0, i), line.Substring(i + 1).Trim());
            }

            req.RemoteHost = req.Method == "CONNECT" ? req.RequestUriRaw : req.Headers.Get("Host");

            var hostSep = req.RemoteHost.IndexOf(':');
            if (hostSep != -1)
            {
                req.RemotePort = int.Parse(req.RemoteHost.Substring(hostSep + 1));
                req.RemoteHost = req.RemoteHost.Substring(0, hostSep);
            }
            else
            {
                req.RemotePort = isSsl ? 443 : 80;
            }

            if (req.Method != "CONNECT")
            {
                var baseUri = new UriBuilder
                {
                    Scheme = isSsl ? "https" : "http",
                    Host = req.RemoteHost,
                    Port = req.RemotePort,
                }.Uri;

                req.RequestUri = new Uri(baseUri, req.RequestUriRaw);
            }

            if (req.Method == "POST" || req.Method == "PUT" || req.Method == "PATCH")
            {
                req.RequestBodyReader = new ProxyRequestBody(req);
            }

            req.ProxyAuthorization = req.Headers[HttpRequestHeader.ProxyAuthorization];
            req.Headers.Remove(HttpRequestHeader.ProxyAuthorization);

            req.KeepAlive = (req.Headers["Proxy-Connection"] ?? req.Headers[HttpRequestHeader.Connection])?.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ?? false;
            req.Headers.Remove("Proxy-Connection");

            return true;
        }

        /// <summary>ProxyRequest → HttpWebRequest</summary>
        /// <param name="create">베이스가 될 WebRequest 를 생성할 함수입니다. TwitterCredentials 를 위해 추가되었습니다.</param>
        /// <param name="copyAuthorization">true일 때 authorization 헤더 복사</param>
        /// <returns></returns>
        public WebRequest CreateRequest(WebRequest baseWebRequest, bool copyAuthorization)
        {
            var req = (baseWebRequest ?? WebRequest.Create(this.RequestUri)) as HttpWebRequest;
            req.Method = this.Method;

            foreach (var key in this.Headers.AllKeys)
            {
                var value = this.Headers.Get(key);

                switch (key.ToLower())
                {
                    case "accept"           : req.Accept           = value;             break;
                    case "connection"       :                                           break;
                    //case "content-length" : req.ContentLength    = long.Parse(value); break;
                    case "content-length"   :                                           break;
                    case "content-type"     : req.ContentType      = value;             break;
                    case "expect"           : req.Expect           = value;             break;
                    case "host"             : req.Host             = value;             break;
                    case "media-type"       : req.MediaType        = value;             break;
                    case "referer"          : req.Referer          = value;             break;
                    case "transfer-encoding": req.TransferEncoding = value;             break;
                    case "user-agent"       : req.UserAgent        = value;             break;

                    case "authorization":
                        if (copyAuthorization)
                            req.Headers.Set(key, value);
                        break;

                    default:
                        req.Headers.Set(key, value);
                        break;
                }
            }

            return req;
        }

        public void WriteRawRequest(Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.ASCII, 4096, true))
            {
                writer.NewLine = "\r\n";

                writer.WriteLine($"{this.Method} {this.RequestUriRaw} {this.Version}");

                foreach (var key in this.Headers.AllKeys)
                {
                    writer.WriteLine($"{key}: {this.Headers.Get(key)}");
                }

                writer.WriteLine();

                writer.Flush();
            }

            this.RequestBodyReader?.CopyTo(stream);
        }

        public IPEndPoint GetEndPoint()
        {
            if (!IPAddress.TryParse(this.RemoteHost, out IPAddress addr))
            {
                addr = Dns.GetHostAddresses(this.RemoteHost)[0];
            }

            return new IPEndPoint(addr, this.RemotePort);
        }

        private sealed class ProxyRequestBody : Stream
        {
            private readonly ProxyRequest m_request;

            private readonly MemoryStream m_chunkedBuffer = new MemoryStream(4096);

            public ProxyRequestBody(ProxyRequest req)
            {
                this.m_request = req;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.m_chunkedBuffer.Dispose();
                }

                base.Dispose(disposing);
            }

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override long Length => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanWrite => false;
            public override bool CanSeek => false;

            private int? m_remainContentLength;
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read;

                if (!this.m_remainContentLength.HasValue)
                {
                    if (this.m_request.Headers.Get("Transfer-Encoding") == "chunked")
                    {
                        this.m_remainContentLength = -1;
                    }
                    else
                    {
                        var v = this.m_request.Headers.Get("Content-Length");
                        if (v == null)
                        {
                            this.m_remainContentLength = 0;
                        }
                        else
                        {
                            this.m_remainContentLength = int.Parse(v);
                        }
                    }
                }

                if (this.m_remainContentLength.Value == 0)
                {
                    return 0;
                }
                else if (this.m_remainContentLength.Value == -1)
                {
                    read = this.m_chunkedBuffer.Read(buffer, offset, count);
                    if (read == 0)
                    {
                        this.m_chunkedBuffer.SetLength(0);

                        var remain = int.Parse(this.m_request.m_proxyStream.ReadLine(), NumberStyles.HexNumber);

                        while (remain > 0)
                        {
                            read = this.m_request.m_proxyStream.Read(buffer, offset, Math.Min(count, remain));
                            this.m_chunkedBuffer.Write(buffer, offset, read);

                            remain -= read;
                        }
                        this.m_request.m_proxyStream.ReadLine();

                        read = this.m_chunkedBuffer.Read(buffer, offset, count);
                    }

                    return read;
                }
                else
                {
                    read = this.m_request.m_proxyStream.Read(buffer, offset, Math.Min(count, this.m_remainContentLength.Value));

                    this.m_remainContentLength -= read;
                    if (this.m_remainContentLength < 0)
                        this.m_remainContentLength = 0;

                    return read;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
                => this.m_request.m_proxyStream.Seek(offset, origin);

            public override void Write(byte[] buffer, int offset, int count)
                => throw new NotSupportedException();

            public override void SetLength(long value)
                => throw new NotSupportedException();

            public override void Flush()
            {
            }
        }
    }
}
