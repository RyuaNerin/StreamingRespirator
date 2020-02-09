using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal sealed class TunnelPlain : Tunnel
    {
        public TunnelPlain(ProxyRequest preq, Stream stream)
            : base(preq, stream)
        {
        }

        public override void Handle()
        {
            using (var remoteClient = new TcpClient())
            {
                remoteClient.ReceiveTimeout = 30 * 1000;
                remoteClient.Connect(this.GetEndPoint());

                using (var remoteStream = remoteClient.GetStream())
                {
                    var taskRemoteToProxy = remoteStream.CopyToAsync(this.m_proxyStream);

                    this.m_request.Headers.Set("Connection", "close");
                    this.m_request.WriteRawRequest(remoteStream);

                    try
                    {
                        Task.WaitAll(taskRemoteToProxy);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
