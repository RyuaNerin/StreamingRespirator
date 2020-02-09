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
                    this.m_proxyStream.Write(ConnectionFailed, 0, ConnectionFailed.Length);
                    throw;
                }

                using (var remoteStream = remoteClient.GetStream())
                {
                    var taskRemoteToProxy =       remoteStream.CopyToAsync(this.m_proxyStream, 4096);
                    var taskProxyToRemote = this.m_proxyStream.CopyToAsync(remoteStream, 4096);

                    this.m_proxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                    try
                    {
                        Task.WaitAll(taskRemoteToProxy, taskProxyToRemote);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
