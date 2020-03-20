using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal sealed class TunnelSslMitm : Handler
    {
        private readonly HandleFunc m_handler;
        private readonly SslProtocols m_sslProtocols;
        private readonly X509Certificate2 m_certificate;

        public TunnelSslMitm(ProxyRequest preq, ProxyStream stream, CancellationToken token, X509Certificate2 certificate, SslProtocols sslProtocols, HandleFunc handler)
            : base(preq, stream, token)
        {
            this.m_sslProtocols = sslProtocols;
            this.m_certificate = certificate;

            this.m_handler = handler;
        }

        public override void Handle()
        {
            using (var proxyStreamSsl = new SslStream(this.ProxyStream))
            {
                this.ProxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                proxyStreamSsl.AuthenticateAsServer(this.m_certificate, false, this.m_sslProtocols, false);
                
                this.Request.Dispose();
                this.Request = null;

                while (ProxyRequest.TryParse(proxyStreamSsl, true, out var req))
                {
                    using (req)
                    using (var resp = new ProxyResponse(proxyStreamSsl))
                    {
                        try
                        {
                            this.m_handler(new ProxyContext(req, resp));
                        }
                        catch
                        {
                            if (!resp.HeaderSent)
                            {
                                resp.Headers.Clear();
                                resp.StatusCode = HttpStatusCode.InternalServerError;
                            }

                            throw;
                        }
                    }
                }
            }
        }
    }
}
