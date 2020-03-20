using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal abstract class Handler : IDisposable
    {
        protected const int CopyToBufferSize = 32 * 1024;

        protected ProxyRequest Request { get; set; }
        protected ProxyStream ProxyStream { get; }
        protected CancellationTokenSource CancelSource { get; }

        protected Handler(ProxyRequest preq, ProxyStream stream, CancellationToken token)
        {
            this.Request = preq;
            this.ProxyStream = stream;

            this.CancelSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            this.CancelSource.Token.Register(stream.Close);
        }
        ~Handler()
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
                this.Request?.Dispose();

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

        protected Task[] CopyToAsyncBoth(Stream remoteStream)
        {
            var psSafe = new SafeAsyncStream(this.ProxyStream);
            var rsSafe = new SafeAsyncStream(remoteStream);

            return new Task[]
            {
                /*
#if DEBUG
                CopyToAsync("proxy -> remote", psSafe, rsSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
                CopyToAsync("remote -> proxy", rsSafe, psSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
#else
*/
                psSafe.CopyToAsync(rsSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
                rsSafe.CopyToAsync(psSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
//#endif
            };

            void Finalize(Task task)
            {
                if (!task.IsFaulted && !task.IsCanceled)
                    this.CancelSource.Cancel();
            }
        }

        private static async Task CopyToAsync(string tag, Stream source, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            var buff = new byte[bufferSize];

            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await source.ReadAsync(buff, 0, buff.Length, cancellationToken);
                Console.WriteLine($"{tag} Read : {read}");
                if (read == 0)
                    return;

                await destination.WriteAsync(buff, 0, read, cancellationToken);
                Console.WriteLine($"{tag} Write");
            }
        }

        protected IPEndPoint GetEndPoint()
        {
            if (!IPAddress.TryParse(this.Request.RemoteHost, out IPAddress addr))
            {
                addr = Dns.GetHostAddresses(this.Request.RemoteHost)[0];
            }

            return new IPEndPoint(addr, this.Request.RemotePort);
        }

        protected static readonly byte[] ConnectionEstablished = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: close\r\n\r\n");
        protected static readonly byte[] ConnectionFailed      = Encoding.ASCII.GetBytes("HTTP/1.1 502 Connection Failed\r\nConnection: close\r\n\r\n");
    }
}
