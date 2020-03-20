using System.Threading;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal class HandlerPlain : Handler
    {
        private readonly HandleFunc m_handler;

        public HandlerPlain(ProxyRequest preq, ProxyStream stream, CancellationToken token, HandleFunc handler)
            : base(preq, stream, token)
        {
            this.m_handler = handler;
        }

        public override void Handle()
        {
            using (var resp = new ProxyResponse(this.ProxyStream))
            {
                var ctx = new ProxyContext(this.Request, resp);
                this.m_handler(ctx);
            }

            this.Request.Dispose();

            ProxyRequest.TryParse(this.ProxyStream, false, out var req);
            this.Request = req;
        }
    }
}
