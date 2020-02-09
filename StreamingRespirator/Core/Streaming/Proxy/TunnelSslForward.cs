using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal sealed class TunnelSslForward : Tunnel
    {
        public TunnelSslForward(ProxyRequest preq, Stream stream)
            : base(preq, stream)
        {
        }

        public override void Handle()
        {
            using (var remoteClient = new TcpClient())
            {
                remoteClient.ReceiveTimeout = 10 * 1000;

                try
                {
                    remoteClient.Connect(this.GetEndPoint());
                }
                catch
                {
                    this.ProxyStream.Write(ConnectionFailed, 0, ConnectionFailed.Length);
                    throw;
                }

                using (var remoteStream = remoteClient.GetStream())
                {
                    var taskToProxy  = this.CopyToAsync(this.ProxyStream, remoteStream    );
                    var taskToRemote = this.CopyToAsync(remoteStream    , this.ProxyStream);

                    this.ProxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                    try
                    {
                        Task.WaitAll(taskToProxy, taskToRemote);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
