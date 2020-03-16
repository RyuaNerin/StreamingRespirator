using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal sealed class TunnelSslForward : Tunnel
    {
        public TunnelSslForward(ProxyRequest preq, Stream stream, CancellationToken token)
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
                    var taskToProxy  = this.CopyToAsync(this.ProxyStream, remoteStream    );
                    var taskToRemote = this.CopyToAsync(remoteStream    , this.ProxyStream);

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
