using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal sealed class ProxyResponse : IDisposable
    {
        private readonly Stream m_stream;
        private readonly ProxyResponseWriter m_responseWriter;

        public Stream ResponseStream => this.m_responseWriter;

        public ProxyResponse(Stream stream)
        {
            this.m_stream = stream;

            this.m_responseWriter = new ProxyResponseWriter(this);
        }
        ~ProxyResponse()
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
                this.WriteHeader(true);

                this.m_responseWriter.Dispose();
            }
        }

        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public WebHeaderCollection Headers { get; } = new WebHeaderCollection();

        private readonly object m_writerHeaderLock = new object();
        private bool m_headerSent;
        public bool HeaderSent
        {
            get
            {
                lock (this.m_writerHeaderLock)
                    return this.m_headerSent;
            }
        }
        public void SetNoResponse()
        {
            lock (this.m_writerHeaderLock)
            {
                this.m_headerSent = true;
            }
        }
        private void WriteHeader(bool disposing)
        {
            lock (this.m_writerHeaderLock)
            {
                if (this.m_headerSent)
                    return;
                this.m_headerSent = true;


                using (var writer = new StreamWriter(this.m_stream, Encoding.ASCII, 4096, true))
                {
                    writer.NewLine = "\r\n";

                    writer.WriteLine("HTTP/1.1 {0:000} {1}", (int)this.StatusCode, HttpWorkerRequest.GetStatusDescription((int)this.StatusCode));

                    if (disposing && !this.m_responseWriter.BodyWritten)
                    {
                        this.Headers.Set(HttpResponseHeader.ContentLength, "0");
                    }

                    foreach (var key in this.Headers.AllKeys)
                    {
                        writer.WriteLine($"{key}: {this.Headers.Get(key)}");
                    }

                    writer.WriteLine();

                    writer.Flush();
                }
            }
        }

        public void FromHttpWebResponse(HttpWebResponse resHttp, Stream stream)
        {
            this.StatusCode = resHttp.StatusCode;

            foreach (var headerName in resHttp.Headers.AllKeys)
            {
                switch (headerName.ToLower())
                {
                    case "connection":  break;
                    case "keep-alive":  break;

                    default:
                        this.Headers.Set(headerName, resHttp.Headers.Get(headerName));
                        break;
                }
            }

            stream.CopyTo(this.ResponseStream);
        }

        private sealed class ProxyResponseWriter : Stream
        {
            private readonly ProxyResponse m_resp;

            public ProxyResponseWriter(ProxyResponse resp)
            {
                this.m_resp = resp;
            }

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override long Length => throw new NotSupportedException();
            public override bool CanWrite => true;
            public override bool CanRead => false;
            public override bool CanSeek => false;

            private bool m_doCheckHeaders = true;
            private bool m_chunked;

            public bool BodyWritten { get; private set; }

            private static readonly byte[] CrLf = new byte[] { (byte)'\r', (byte)'\n' };

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count == 0)
                    return;

                this.BodyWritten = true;

                if (this.m_doCheckHeaders)
                {
                    this.m_doCheckHeaders = false;

                    if (this.m_resp.Headers.Get("Content-Length") == null)
                    {
                        this.m_resp.Headers.Set("Transfer-Encoding", "chunked");
                        this.m_chunked = true;
                    }

                    this.m_resp.WriteHeader(false);
                }

                if (this.m_chunked)
                {
                    var buff = Encoding.ASCII.GetBytes(count.ToString("x"));

                    this.m_resp.m_stream.Write(buff, 0, buff.Length);
                    this.m_resp.m_stream.Write(CrLf, 0, CrLf.Length);
                    this.m_resp.m_stream.Write(buffer, offset, count);
                    this.m_resp.m_stream.Write(CrLf, 0, CrLf.Length);
                    this.m_resp.m_stream.Flush();
                }
                else
                {
                    this.m_resp.m_stream.Write(buffer, offset, count);
                }

                this.m_resp.m_stream.Flush();
            }

            public override void Flush()
                => this.m_resp.m_stream.Flush();

            public override void SetLength(long value)
                => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin)
                => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
                => throw new NotSupportedException();
        }
    }
}
