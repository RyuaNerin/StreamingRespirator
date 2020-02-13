using System;
using System.IO;
using System.Threading;

namespace StreamingRespirator.Core.Streaming.Proxy.Streams
{
    /// <summary>
    /// Tunnel 에서 CopyToaSync 할 때 한 쪽 소켓이 닫히면 작업을 취소시키며 반대쪽 소켓도 닫아버리는데, 이때 EndRead, EndWrite 에서 발생하는 ObjectDisposedException 오류를 무시하는 스트림
    /// </summary>
    internal class SafeAsyncStream : Stream
    {
        private readonly Stream m_baseStream;

        public SafeAsyncStream(Stream baseStream)
        {
            this.m_baseStream = baseStream;
        }

        public override bool CanRead => this.m_baseStream.CanRead;
        public override bool CanSeek => this.m_baseStream.CanSeek;
        public override bool CanWrite => this.m_baseStream.CanWrite;
        public override long Length => this.m_baseStream.Length;

        public override long Position
        {
            get => this.m_baseStream.Position;
            set => this.m_baseStream.Position = value;
        }

        public override void Flush()
            => this.m_baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => this.m_baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => this.m_baseStream.Seek(offset, origin);

        public override void SetLength(long value)
            => this.m_baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
            => this.m_baseStream.Write(buffer, offset, count);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => this.m_baseStream.BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => this.m_baseStream.BeginWrite(buffer, offset, count, callback, state);

        public override int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return this.m_baseStream.EndRead(asyncResult);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
            catch (IOException)
            {
            }
            catch
            {
                throw;
            }
        }
        public override void EndWrite(IAsyncResult asyncResult)
        {
            try
            {
                 this.m_baseStream.EndWrite(asyncResult);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch
            {
                throw;
            }
        }

        public class AsyncFailedResult : IAsyncResult, IDisposable
        {
            public AsyncFailedResult(object state)
            {
                this.AsyncState = state;
            }
            ~AsyncFailedResult()
            {
                this.Dispose(false);
            }
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }
            private bool m_disposed = false;
            protected void Dispose(bool disposing)
            {
                if (this.m_disposed) return;
                this.m_disposed = true;

                if (disposing)
                {
                    this.AsyncWaitHandle.Dispose();
                }
            }

            public object AsyncState { get; }

            public WaitHandle AsyncWaitHandle => new SimpleWaitHandle();

            public bool IsCompleted => true;
            public bool CompletedSynchronously => true;

            public class SimpleWaitHandle : WaitHandle
            {
                public override bool WaitOne(int millisecondsTimeout, bool exitContext)
                    => true;

                public override bool WaitOne(TimeSpan timeout, bool exitContext)
                    => true;

                public override bool WaitOne()
                    => true;

                public override bool WaitOne(int millisecondsTimeout)
                    => true;

                public override bool WaitOne(TimeSpan timeout)
                    => true;
            }
        }
    }
}
