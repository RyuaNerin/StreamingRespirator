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
                this.CancelSource.Token.Register(remoteClient.Close);

                remoteClient.Connect(this.GetEndPoint());

                using (var remoteStream = remoteClient.GetStream())
                {
                    var taskToProxy = this.CopyToAsync(this.ProxyStream, remoteStream);

                    this.Reqeust.Headers.Set("Connection", "close");
                    this.Reqeust.WriteRawRequest(remoteStream);

                    try
                    {
                        Task.WaitAll(taskToProxy);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
