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

        public WaitableStream Stream      { get; }
        public string         Description { get; }
        public long           OwnerId     { get; }

        public long LastStatus        { get; set; }
        public long LastActivity      { get; set; }
        public long LastDirectMessage { get; set; }

        public StreamingConnection(WaitableStream item, long ownerId, string description)
        {
            this.Stream      = item;
            this.OwnerId     = ownerId;
            this.Description = description;

            this.m_keepAlive = new Timer(this.SendKeepAlive, null, KeepAlivePeriod, KeepAlivePeriod);
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
                this.m_keepAlive.Dispose();
                this.Stream     .Dispose();
            }
        }
        
        private static readonly byte[] KeepAlivePacket = Encoding.UTF8.GetBytes("\r\n");
        private void SendKeepAlive(object sender)
        {
            this.SendToStream(KeepAlivePacket);
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
                this.Stream.Flush();
            }
            catch
            {
                this.Stream.Close();
            }
        }
    }
}
