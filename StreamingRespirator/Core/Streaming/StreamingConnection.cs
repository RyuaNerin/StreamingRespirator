using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace StreamingRespirator.Core.Streaming
{
    internal interface IPacket
    {
    }

    internal class StreamingConnection : IDisposable
    {
        private const int KeepAlivePeriod = 5 * 1000;

        private readonly Timer m_keepAlive;
        private readonly Timer m_friends;

        public WaitableStream Stream { get; }
        public TwitterClient Client { get; }

        private readonly StreamWriter m_streamWriter;

        public long OwnerId
            => this.Client.Credential.Id;

        public StreamingConnection(WaitableStream item, TwitterClient client)
        {
            this.Stream = item;
            this.Client = client;

            this.m_streamWriter = new StreamWriter(item, Encoding.UTF8, 4096, true);

            this.m_keepAlive = new Timer(this.SendKeepAlive, null, KeepAlivePeriod, KeepAlivePeriod);
            this.m_friends = new Timer(this.SendFriends, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        ~StreamingConnection()
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
                this.m_friends.Dispose();
                this.m_keepAlive.Dispose();
                this.m_streamWriter.Dispose();
            }
        }

        private readonly object m_sendLock = new object();

        private void SendKeepAlive(object sender)
        {
            lock (this.m_sendLock)
            {
                try
                {
                    this.m_streamWriter.WriteLine();
                    this.m_streamWriter.Flush();
                }
                catch
                {
                    this.Stream.Close();
                }

                try
                {
                    this.m_keepAlive.Change(KeepAlivePeriod, KeepAlivePeriod);
                }
                catch
                {
                }
            }
        }

        private void SendFriends(object sender)
        {
            this.Client.SendFriendsPacket(this);
        }

        public void SendToStream(IPacket data)
        {
            lock (this.m_sendLock)
            {
                Debug.WriteLine(data);

                try
                {
                    Program.JsonSerializer.Serialize(this.m_streamWriter, data);
                    this.m_streamWriter.WriteLine();
                    this.m_streamWriter.Flush();
                }
                catch
                {
                    this.Stream.Close();
                }

                try
                {
                    this.m_keepAlive.Change(KeepAlivePeriod, KeepAlivePeriod);
                }
                catch
                {
                }
            }
        }
    }
}
