using System;
using System.IO;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming
{
    internal class WaitableStream : Stream
    {
        private readonly Stream m_stream;
        private readonly ManualResetEventSlim m_event = new ManualResetEventSlim(false);

        public WaitableStream(Stream baseStream)
        {
            this.m_stream = baseStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.m_stream.Dispose();

                this.m_event.Set();
                this.m_event.Dispose();
            }
        }

        public override void Close()
        {
            try
            {
                this.m_stream.Flush();
                this.m_stream.Close();
            }
            catch
            {
            }
            finally
            {
                this.m_event.Set();
            }
        }

        public WaitHandle WaitHandle
            => this.m_event.WaitHandle;

        public override bool CanRead
            => this.m_stream.CanRead;

        public override bool CanSeek
            => this.m_stream.CanSeek;

        public override bool CanTimeout
            => this.m_stream.CanTimeout;

        public override bool CanWrite
            => this.m_stream.CanWrite;

        public override long Length
            => this.m_stream.Length;

        public override int ReadTimeout
        {
            get => this.m_stream.ReadTimeout;
            set => this.m_stream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => this.m_stream.WriteTimeout;
            set => this.m_stream.WriteTimeout = value;
        }

        public override long Position
        {
            get => this.m_stream.Position;
            set => this.m_stream.Position = value;
        }

        public override ObjRef CreateObjRef(Type requestedType)
            => this.m_stream.CreateObjRef(requestedType);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => this.m_stream.BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => this.m_stream.BeginWrite(buffer, offset, count, callback, state);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => this.m_stream.CopyToAsync(destination, bufferSize, cancellationToken);

        public override int EndRead(IAsyncResult asyncResult)
            => this.m_stream.EndRead(asyncResult);

        public override void EndWrite(IAsyncResult asyncResult)
            => this.m_stream.EndWrite(asyncResult);

        public override void Flush()
            => this.m_stream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => this.m_stream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
            => this.m_stream.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.m_stream.ReadAsync(buffer, offset, count, cancellationToken);

        public override int ReadByte()
            => this.m_stream.ReadByte();

        public override long Seek(long offset, SeekOrigin origin)
            => this.m_stream.Seek(offset, origin);

        public override void SetLength(long value)
            => this.m_stream.SetLength(0);

        public override void Write(byte[] buffer, int offset, int count)
            => this.m_stream.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.m_stream.WriteAsync(buffer, offset, count, cancellationToken);

        public override void WriteByte(byte value)
            => this.m_stream.WriteByte(value);
    }
}
