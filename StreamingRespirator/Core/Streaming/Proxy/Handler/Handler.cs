using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal abstract class Handler : IDisposable
    {
        protected const int CopyToBufferSize = 32 * 1024;

        protected ProxyStream ProxyStream { get; }
        protected CancellationTokenSource CancelSource { get; }

        protected Handler(ProxyStream stream, CancellationToken token)
        {
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
        public abstract void Handle(ProxyRequest req);

        protected Task[] CopyToAsyncBoth(Stream remoteStream)
        {
            var psSafe = new SafeAsyncStream(this.ProxyStream);
            var rsSafe = new SafeAsyncStream(remoteStream);

            return new Task[]
            {
                psSafe.CopyToAsync(rsSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
                rsSafe.CopyToAsync(psSafe, CopyToBufferSize, this.CancelSource.Token).ContinueWith(Finalize),
            };

            void Finalize(Task task)
            {
                psSafe.Dispose();
                rsSafe.Dispose();

                if (!task.IsFaulted && !task.IsCanceled)
                    this.CancelSource.Cancel();
            }
        }

        protected static readonly byte[] ConnectionEstablished = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: Keep-Alive\r\bKeep-Alive: timeout=30\r\n\r\n");
        protected static readonly byte[] ConnectionFailed      = Encoding.ASCII.GetBytes("HTTP/1.1 502 Connection Failed\r\nConnection: close\r\n\r\n");
    }
}
