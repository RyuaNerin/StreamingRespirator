using System;
using System.IO;
using System.Text;

namespace StreamingRespirator.Core.Streaming.Proxy.Streams
{
    /// <summary>
    /// StreamReader 쓰면 버퍼를 이용할 방법이 없어서... golang 의 bufio 같은 느낌으로 만들어봄.
    /// </summary>
    internal sealed class RequestStreamReader : Stream
    {
        private readonly MemoryStream m_buffStream = new MemoryStream(4096);
        private readonly byte[]       m_buff       = new byte[4096];

        private readonly Stream m_baseStream;

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public RequestStreamReader(Stream baseStream)
        {
            this.m_baseStream = baseStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.m_buffStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override bool CanRead  => true;
        public override bool CanSeek  => false;
        public override bool CanWrite => false;
        public override long Length   => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = this.m_buffStream.Read(buffer, offset, count);
            if (read > 0)
                return read;

            this.m_buffStream.SetLength(0);

            read = this.m_baseStream.Read(this.m_buff, 0, this.m_buff.Length);
            Console.WriteLine("Read : " + BitConverter.ToString(this.m_buff, 0, read).Replace('-', ' '));
            if (read == 0)
                return 0;

            this.m_buffStream.Write(this.m_buff, 0, read);
            this.m_buffStream.Position = 0;

            return this.m_buffStream.Read(buffer, offset, count);
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
                        break;

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

                                    this.m_buffStream.Position -= buffLen - i - 2;
                                }

                                return this.Encoding.GetString(mem.ToArray());
                            }
                        }

                        mem.Write(buff, 0, buffLen);
                        buffLen = 0;
                    }
                } while (true);

                return null;
            }
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
