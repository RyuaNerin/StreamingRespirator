using System;
using System.Net;
using System.Text;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal delegate void HandleFunc(ProxyContext ctx);

    internal sealed class ProxyContext
    {
        public ProxyContext(ProxyRequest req, ProxyResponse resp)
        {
            this.Request = req;
            this.Response = resp;
        }

        public ProxyRequest Request { get; }
        public ProxyResponse Response { get; }

        public bool CheckAuthentication()
        {
            string id, pw;
            lock (Config.Instance.Lock)
            {
                id = Config.Instance.Proxy.Id;
                pw = Config.Instance.Proxy.Pw;
            }

            if (!string.IsNullOrWhiteSpace(Config.Instance.Proxy.Id))
            {
                var pa = this.Request.ProxyAuthorization;

                if (string.IsNullOrWhiteSpace(pa))
                {
                    this.Response.StatusCode = HttpStatusCode.ProxyAuthenticationRequired;
                    this.Response.Headers.Set(HttpResponseHeader.ProxyAuthenticate, "Basic realm=\"Access to Streamning-Respirator\"");

                    if (this.Request.KeepAlive)
                    {
                        this.Response.Headers.Set(HttpResponseHeader.Connection, "Keep-Alive");
                        this.Response.Headers.Set(HttpResponseHeader.KeepAlive, "timeout=30");
                    }
                    return false;
                }

                var sp = pa.IndexOf(' ');

                if (pa.Substring(0, sp) != "Basic")
                {
                    this.Response.StatusCode = HttpStatusCode.ProxyAuthenticationRequired;
                    this.Response.Headers.Set(HttpResponseHeader.ProxyAuthenticate, "Basic realm=\"Access to Streamning-Respirator\"");
                    if (this.Request.KeepAlive)
                    {
                        this.Response.Headers.Set(HttpResponseHeader.Connection, "Keep-Alive");
                        this.Response.Headers.Set(HttpResponseHeader.KeepAlive, "timeout=30");
                    }
                    return false;
                }

                if (pa.Substring(sp + 1).Trim() != Convert.ToBase64String(Encoding.ASCII.GetBytes($"{id}:{pw}")))
                {
                    this.Response.StatusCode = HttpStatusCode.Unauthorized;
                    return false;
                }
            }

            return true;
        }
    }
}
