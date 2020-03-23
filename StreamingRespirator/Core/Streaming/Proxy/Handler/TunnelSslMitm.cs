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

        public TunnelSslMitm(ProxyStream stream, CancellationToken token, X509Certificate2 certificate, SslProtocols sslProtocols, HandleFunc handler)
            : base(stream, token)
        {
            this.m_sslProtocols = sslProtocols;
            this.m_certificate = certificate;

            this.m_handler = handler;
        }

        public override void Handle(ProxyRequest req)
        {
            using (var proxyStreamSsl = new SslStream(this.ProxyStream, true))
            {

                if (req.KeepAlive)
                    this.ProxyStream.Write(ConnectionEstablishedKA, 0, ConnectionEstablishedKA.Length);
                else
                    this.ProxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                proxyStreamSsl.AuthenticateAsServer(this.m_certificate, false, this.m_sslProtocols, false);

                req.Dispose();

                while (ProxyRequest.TryParse(proxyStreamSsl, true, out req))
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

                    if (!req.KeepAlive)
                        break;
                }
            }
        }
    }
}
