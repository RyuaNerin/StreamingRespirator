namespace StreamingRespirator.Core.Streaming.Proxy
{
    /// <returns>false 면 forward</returns>
    internal delegate bool MitmHandler(ProxyContext ctx);

    internal sealed class ProxyContext
    {
        public ProxyContext(ProxyRequest req, ProxyResponse resp)
        {
            this.Request = req;
            this.Response = resp;
        }

        public ProxyRequest Request { get; }
        public ProxyResponse Response { get; }
    }
}
