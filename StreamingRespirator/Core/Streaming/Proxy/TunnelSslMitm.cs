using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Sentry;

namespace StreamingRespirator.Core.Streaming.Proxy
{
    internal sealed class TunnelSslMitm : Tunnel
    {
        private const SslProtocols SslProtocol = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        private readonly MitmHandler m_handler;
        private readonly X509Certificate2 m_certificate;

        public TunnelSslMitm(ProxyRequest preq, Stream stream, X509Certificate2 certificate, MitmHandler handler)
            : base(preq, stream)
        {
            this.m_certificate = certificate;
            this.m_handler = handler;
        }

        public override void Handle()
        {
            using (var proxyStreamSsl = new SslStream(this.ProxyStream))
            {
                this.ProxyStream.Write(ConnectionEstablished, 0, ConnectionEstablished.Length);

                proxyStreamSsl.AuthenticateAsServer(this.m_certificate, false, SslProtocol, false);

                using (var reqSSL = ProxyRequest.Parse(proxyStreamSsl, true))
                {
                    using (var resp = new ProxyResponse(proxyStreamSsl))
                    {
                        try
                        {
                            if (this.m_handler(new ProxyContext(reqSSL, resp)))
                                return;
                        }
                        catch
                        {
                            if (!resp.HeaderSent)
                            {
                                using (var respErr = new ProxyResponse(proxyStreamSsl))
                                    respErr.StatusCode = HttpStatusCode.InternalServerError;
                            }

                            throw;
                        }

                        if (resp.HeaderSent)
                            return;

                        resp.SetNoResponse();
                    }

                    using (var remoteClient = new TcpClient())
                    {
                        this.CancelSource.Token.Register(remoteClient.Close);

                        try
                        {
                            remoteClient.Connect(this.GetEndPoint());
                        }
                        catch
                        {
                            using (var respErr = new ProxyResponse(proxyStreamSsl))
                                respErr.StatusCode = HttpStatusCode.InternalServerError;

                            throw;
                        }

                        using (var remoteStream = remoteClient.GetStream())
                        using (var remoteStreamSsl = new SslStream(remoteStream))
                        {
                            remoteStreamSsl.AuthenticateAsClient(reqSSL.RemoteHost);

                            var taskToProxy = this.CopyToAsync(proxyStreamSsl, remoteStreamSsl);

                            reqSSL.WriteRawRequest(remoteStreamSsl);
                            var taskToRemote = this.CopyToAsync(remoteStreamSsl, proxyStreamSsl);

                            try
                            {
                                Task.WaitAll(taskToProxy, taskToRemote);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }
    }
}
