using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal sealed class TunnelSslForward : Handler
    {
        public TunnelSslForward(ProxyStream stream, CancellationToken token)
            : base(stream, token)
        {
        }

        public override void Handle(ProxyRequest req)
        {
            using (var remoteClient = new TcpClient())
            {
                this.CancelSource.Token.Register(() =>
                {
                    try
                    {
                        remoteClient.Close();
                    }
                    catch
                    {
                    }
                });

                try
                {
                    remoteClient.Connect(req.GetEndPoint());
                }
                catch
                {
                    this.ProxyStream.Write(ConnectionFailed, 0, ConnectionFailed.Length);
                    throw;
                }

                if (req.KeepAlive)
                    this.ProxyStream.Write(ConnectionEstablishedKA, 0, ConnectionEstablishedKA.Length);
                else
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
