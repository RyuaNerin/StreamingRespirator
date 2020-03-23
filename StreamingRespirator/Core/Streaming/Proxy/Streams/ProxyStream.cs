using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming.Proxy.Streams
{
    /// <summary>
    /// 모든 Proxy Stream 이, 이 클래스를 기반으로 작동합니다.
    /// Dispose 시 BaseStream 스트림을 닫습니다.
    /// </summary>
    internal sealed class ProxyStream : Stream
    {
        private readonly MemoryStream m_buffStream = new MemoryStream(4096);
        private readonly byte[]       m_buff       = new byte[4096];

        private readonly Stream m_baseStream;

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public ProxyStream(Stream baseStream)
        {
            this.m_baseStream = baseStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.m_buffStream.Dispose();
                this.m_baseStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override bool CanRead  => this.m_baseStream.CanRead;
        public override bool CanSeek  => false;
        public override bool CanWrite => this.m_baseStream.CanWrite;
        public override long Length   => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int ReadTimeout
        {
            get => this.m_baseStream.ReadTimeout;
            set => this.m_baseStream.ReadTimeout = value;
        }
        public override int WriteTimeout
        {
            get => this.m_baseStream.WriteTimeout;
            set => this.m_baseStream.WriteTimeout = value;
        }

        public override bool CanTimeout
            => this.m_baseStream.CanTimeout;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = this.m_buffStream.Read(buffer, offset, count);
            if (read > 0)
                return read;

            this.m_buffStream.SetLength(0);

            read = this.m_baseStream.Read(this.m_buff, 0, this.m_buff.Length);
            if (read == 0)
                return 0;

            this.m_buffStream.Write(this.m_buff, 0, read);
            this.m_buffStream.Position = 0;

            return this.m_buffStream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            var buff = new byte[1];
            this.Read(buff, 0, 1);
            return buff[0];
        }

        public int Peek(byte[] buffer, int offset, int count)
        {
            var read = this.Read(buffer, offset, count);
            this.m_buffStream.Position -= read;
            return read;
        }

        public async Task<int> PeekAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await this.ReadAsync(buffer, offset, count, cancellationToken);
            this.m_buffStream.Position -= read;
            return read;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => new ReadTask(this.ReadAsync(buffer, offset, count), callback, state);
        public override int EndRead(IAsyncResult asyncResult)
            => ((ReadTask)asyncResult).Task.GetAwaiter().GetResult();

        private sealed class ReadTask : IAsyncResult
        {
            public Task<int> Task { get; }

            public bool IsCompleted
                => this.Task.IsCompleted;

            public WaitHandle AsyncWaitHandle
                => ((IAsyncResult)this.Task).AsyncWaitHandle;

            public bool CompletedSynchronously
                => ((IAsyncResult)this.Task).CompletedSynchronously;

            public object AsyncState { get; }

            public ReadTask(Task<int> task, AsyncCallback callback, object state)
            {
                task.ContinueWith(t => callback(this));

                this.Task = task;
                this.AsyncState = state;
            }
        }

        public string ReadLine()
        {
            using (var mem = new MemoryStream(256))
            {
                var buff = new byte[64];
                int buffLen = 0;
                int read;

                do
                {
                    read = this.Read(buff, buffLen, buff.Length - buffLen);
                    if (read == 0)
                        continue;

                    buffLen += read;

                    if (buffLen >= 2)
                    {
                        for (int i = 0; i < buffLen - 1; i++)
                        {
                            if (buff[i] == '\r' && buff[i + 1] == '\n')
                            {
                                if (i > 0)
                                {
                                    mem.Write(buff, 0, i);
                                }

                                this.m_buffStream.Position -= buffLen - i - 2;

                                return this.Encoding.GetString(mem.ToArray());
                            }
                        }

                        mem.Write(buff, 0, buffLen - 1);
                        buff[0] = buff[buffLen - 1];
                        buffLen = 1;
                    }
                } while (true);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = this.m_buffStream.Read(buffer, offset, count);
            if (read > 0)
                return read;

            this.m_buffStream.SetLength(0);

            read = await this.m_baseStream.ReadAsync(this.m_buff, 0, this.m_buff.Length);
            if (read == 0)
                return 0;

            this.m_buffStream.Write(this.m_buff, 0, read);
            this.m_buffStream.Position = 0;

            return this.m_buffStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
            => this.m_baseStream.Write(buffer, offset, count);

        public override void WriteByte(byte value)
            => this.m_baseStream.WriteByte(value);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.m_baseStream.WriteAsync(buffer, offset, count, cancellationToken);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => base.BeginWrite(buffer, offset, count, callback, state);
        public override void EndWrite(IAsyncResult asyncResult)
            => base.EndWrite(asyncResult);


        public override void Flush()
            => this.m_baseStream.Flush();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => this.m_baseStream.FlushAsync();
    }
}
