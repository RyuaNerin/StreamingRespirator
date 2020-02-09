using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using StreamingRespirator.Utilities;

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

        public long OwnerId
            => this.Client.Credential.Id;

        public StreamingConnection(WaitableStream item, TwitterClient client)
        {
            this.Stream = item;
            this.Client = client;

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
            }
        }

        private static readonly byte[] KeepAlivePacket = Encoding.UTF8.GetBytes("\r\n");
        private void SendKeepAlive(object sender)
        {
            this.SendToStream(KeepAlivePacket);
        }

        private void SendFriends(object sender)
        {
            this.Client.SendFriendsPacket(this);
        }

        private static readonly JsonSerializerSettings Jss = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            Formatting = Formatting.None,
            DateFormatString = "ddd MMM dd HH:mm:ss +ffff yyyy"
        };
        public void SendToStream(IPacket data)
        {
            Debug.WriteLine(data);

            this.SendToStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data, Jss) + "\r\n\r\n"));

            this.m_keepAlive.Change(KeepAlivePeriod, KeepAlivePeriod);
        }

        private void SendToStream(byte[] data)
        {
            try
            {
                this.Stream.Write(data, 0, data.Length);
            }
            catch
            {
                this.Stream.Close();
            }
        }
    }
}
