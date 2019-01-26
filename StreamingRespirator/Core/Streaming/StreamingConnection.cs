using System.Text;
using System.Timers;
using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core.Streaming
{
    internal class StreamingConnection
    {
        private readonly Timer m_keepAlive = new Timer();

        public StreamingConnection(WaitableStream item, long ownerId, string description)
        {
            this.Stream      = item;
            this.OwnerId     = ownerId;
            this.Description = description;

            this.m_keepAlive = new Timer();
        }

        public WaitableStream Stream      { get; }
        public string         Description { get; }
        public long           OwnerId     { get; }

        public long LastStatus        { get; set; }
        public long LastActivity      { get; set; }
        public long LastDirectMessage { get; set; }
        
        private static readonly byte[] KeepAlivePacket = Encoding.UTF8.GetBytes("\r\n");
        private void KeepAlive_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.SendToStream(KeepAlivePacket);
        }

        public void SendToStream(string data)
        {
            this.SendToStream(Encoding.UTF8.GetBytes(data + "\r\n"));
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
