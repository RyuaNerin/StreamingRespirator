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
        private readonly StreamWriter m_streamWriter;

        public Stream ResponseStream { get; }

        public ProxyResponse(Stream stream)
        {
            this.m_stream       = stream;
            this.m_streamWriter = new StreamWriter(this.m_stream, Encoding.ASCII, 4096, true)
            {
                NewLine = "\r\n",
            };

            this.ResponseStream = new ProxyResponseWriter(this);
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
                this.WriteHeader();

                this.ResponseStream.Dispose();
                this.m_streamWriter.Dispose();
            }
        }

        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public WebHeaderCollection Headers { get; } = new WebHeaderCollection();

        private bool m_chunked;
        public void SetChunked()
        {
            this.m_chunked = true;
        }

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
        private void WriteHeader()
        {
            lock (this.m_writerHeaderLock)
            {
                if (this.m_headerSent)
                    return;
                this.m_headerSent = true;

                this.m_streamWriter.WriteLine("HTTP/1.1 {0:000} {1}", (int)this.StatusCode, HttpWorkerRequest.GetStatusDescription((int)this.StatusCode));

                foreach (var key in this.Headers.AllKeys)
                {
                    this.m_streamWriter.WriteLine($"{key}: {this.Headers.Get(key)}");
                }

                this.m_streamWriter.WriteLine();

                this.m_streamWriter.Flush();
            }
        }

        public void FromHttpWebResponse(HttpWebResponse resHttp, Stream stream)
        {
            this.StatusCode = resHttp.StatusCode;

            foreach (var headerName in resHttp.Headers.AllKeys)
            {
                this.Headers.Set(headerName, resHttp.Headers.Get(headerName));
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
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (this.m_doCheckHeaders && count > 0)
                {
                    this.m_doCheckHeaders = false;

                    if (this.m_resp.m_chunked || this.m_resp.Headers.Get("Content-Length") == null)
                    {
                        this.m_resp.Headers.Set("Transfer-Encoding", "chunked");
                    }
                }

                this.m_resp.WriteHeader();

                if (this.m_resp.m_chunked)
                {
                    this.m_resp.m_streamWriter.WriteLine(count.ToString("x"));
                    this.m_resp.m_streamWriter.Flush();
                    this.m_resp.m_stream.Write(buffer, offset, count);
                    this.m_resp.m_stream.Flush();
                    this.m_resp.m_streamWriter.WriteLine();
                    this.m_resp.m_streamWriter.Flush();
                }
                else
                {
                    this.m_resp.m_stream.Write(buffer, offset, count);
                }
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
