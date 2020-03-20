using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal sealed class TunnelPlain : Handler
    {
        public TunnelPlain(ProxyRequest preq, ProxyStream stream, CancellationToken token)
            : base(preq, stream, token)
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
                    this.Request.WriteRawRequest(remoteStream);

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
