using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal abstract class Tunnel : IDisposable
    {
        protected const int CopyToBufferSize = 32 * 1024;

        private static readonly CancellationTokenSource GlobalCancelSource = new CancellationTokenSource();
        public static void CancelAllTunnel()
        {
            GlobalCancelSource.Cancel();
        }

        protected ProxyRequest Reqeust { get; }
        protected Stream ProxyStream { get; }
        protected CancellationTokenSource CancelSource { get; }

        protected Tunnel(ProxyRequest preq, Stream stream)
        {
            this.Reqeust = preq;
            this.ProxyStream = stream;

            this.CancelSource = CancellationTokenSource.CreateLinkedTokenSource(GlobalCancelSource.Token);
        }
        ~Tunnel()
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
                try
                {
                    this.CancelSource.Cancel();
                }
                catch
                {
                }
                this.CancelSource.Dispose();
            }
        }

        /// <summary>
        /// 내부 Exception 모두 throw 함
        /// </summary>
        public abstract void Handle();

        protected Task CopyToAsync(Stream dst, Stream src)
            => src.CopyToAsync(dst, CopyToBufferSize, this.CancelSource.Token).ContinueWith(e => this.CancelSource.Cancel());

        protected IPEndPoint GetEndPoint()
        {
            if (!IPAddress.TryParse(this.Reqeust.RemoteHost, out IPAddress addr))
            {
                addr = Dns.GetHostAddresses(this.Reqeust.RemoteHost)[0];
            }

            return new IPEndPoint(addr, this.Reqeust.RemotePort);
        }

        protected static readonly byte[] ConnectionEstablished = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: close\r\n\r\n");
        protected static readonly byte[] ConnectionFailed      = Encoding.ASCII.GetBytes("HTTP/1.1 502 Connection Failed\r\nConnection: close\r\n\r\n");
    }
}
