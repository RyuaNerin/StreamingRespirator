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
                    var ctx = new ProxyContext(req, resp);
                    this.m_handler(ctx);
                }
            } while (ProxyRequest.TryParse(this.ProxyStream, false, out req));
        }
    }
}
