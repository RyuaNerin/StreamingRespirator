using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal sealed class TunnelSslForward : Handler
    {
        public TunnelSslForward(ProxyRequest preq, ProxyStream stream, CancellationToken token)
            : base(preq, stream, token)
        {
        }

        public override void Handle()
        {
            using (var remoteClient = new TcpClient())
            {
                this.CancelSource.Token.Register(remoteClient.Close);

                try
                {
                    remoteClient.Connect(this.GetEndPoint());
                }
                catch
                {
                    this.ProxyStream.Write(ConnectionFailed, 0, ConnectionFailed.Length);
                    throw;
                }

                this.ProxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                using (var remoteStream = remoteClient.GetStream())
                {
                    var tasks = this.CopyToAsyncBoth(remoteStream);

                    try
                    {
                        Task.WaitAll(tasks);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
