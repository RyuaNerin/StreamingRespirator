using System.Net;
using System.Threading;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal class HandlerPlain : Handler
    {
        private readonly HandleFunc m_handler;

        public HandlerPlain(ProxyStream stream, CancellationToken token, HandleFunc handler)
            : base(stream, token)
        {
            this.m_handler = handler;
        }

        public override void Handle(ProxyRequest req)
        {
            do
            {
                using (req)
                using (var resp = new ProxyResponse(this.ProxyStream))
                {
                    if (req.KeepAlive)
                    {
                        resp.Headers.Set(HttpResponseHeader.Connection, "Keep-Alive");
                        resp.Headers.Set(HttpResponseHeader.KeepAlive, "timeout=30");
                    }

                    this.m_handler(new ProxyContext(req, resp));

                    if (!req.KeepAlive)
                        break;
                }
            } while (ProxyRequest.TryParse(this.ProxyStream, false, out req));
        }
    }
}
