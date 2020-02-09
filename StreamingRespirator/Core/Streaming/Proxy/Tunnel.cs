using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal abstract class Tunnel
    {
        protected readonly ProxyRequest m_request;
        protected readonly Stream m_proxyStream;

        protected Tunnel(ProxyRequest preq, Stream stream)
        {
            this.m_request = preq;
            this.m_proxyStream = stream;
        }

        private static CancellationTokenSource CancelSource = new CancellationTokenSource();
        public static void Exit()
        {
            CancelSource.Cancel();
        }

        /// <summary>
        /// 내부 Exception 모두 throw 함
        /// </summary>
        public abstract void Handle();

        protected IPEndPoint GetEndPoint()
        {
            if (!IPAddress.TryParse(this.m_request.RemoteHost, out IPAddress addr))
            {
                addr = Dns.GetHostAddresses(this.m_request.RemoteHost)[0];
            }

            return new IPEndPoint(addr, this.m_request.RemotePort);
        }

        protected static readonly byte[] ConnectionEstablished = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: close\r\n\r\n");
        protected static readonly byte[] ConnectionFailed      = Encoding.ASCII.GetBytes("HTTP/1.1 502 Connection Failed\r\nConnection: close\r\n\r\n");
    }
}
