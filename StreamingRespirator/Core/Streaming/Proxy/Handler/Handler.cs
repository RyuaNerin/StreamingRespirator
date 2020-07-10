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

            var factory = new TaskFactory();
            return new Task[]
            {
                CopyToAsync(factory, psSafe, rsSafe, this.CancelSource.Token).ContinueWith(Finalize),
                CopyToAsync(factory, rsSafe, psSafe, this.CancelSource.Token).ContinueWith(Finalize),
            };

            void Finalize(Task task)
            {
                try
                {
                    this.CancelSource.Cancel();
                }
                catch
                {
                }

                psSafe.Dispose();
                rsSafe.Dispose();

            }
        }

        private static async Task CopyToAsync(TaskFactory taskFactory, Stream from, Stream to, CancellationToken token)
        {
            var buff = new byte[CopyToBufferSize];
            int read;

            try
            {
                while ((read = await taskFactory.FromAsync(from.BeginRead, from.EndRead, buff, 0, CopyToBufferSize, token).ConfigureAwait(false)) > 0)
                {
                    await taskFactory.FromAsync(to.BeginWrite, to.EndWrite, buff, 0, read, token).ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }

        protected static readonly byte[] ConnectionEstablishedKA = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: keep-alive\r\nKeep-Alive: timeout=30\r\n\r\n");
        protected static readonly byte[] ConnectionEstablished   = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nConnection: close\r\n\r\n");
        protected static readonly byte[] ConnectionFailed        = Encoding.ASCII.GetBytes("HTTP/1.1 502 Connection Failed\r\nConnection: close\r\n\r\n");
    }
}
