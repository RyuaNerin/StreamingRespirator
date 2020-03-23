using System.IO;
using System.Net;
using System.Threading;
using StreamingRespirator.Core.Streaming.Proxy.Streams;

namespace StreamingRespirator.Core.Streaming.Proxy.Handler
{
    internal sealed class TunnelPlain : Handler
    {
        public TunnelPlain(ProxyStream stream, CancellationToken token)
            : base(stream, token)
        {
        }

        public override void Handle(ProxyRequest req)
        {
            HttpWebRequest hreq = null;

            this.CancelSource.Token.Register(() =>
            {
                try
                {
                    hreq.Abort();
                }
                catch
                {
                }
            });

            do
            {
                using (req)
                using (var resp = new ProxyResponse(this.ProxyStream))
                {
                    hreq = req.CreateRequest(null, true) as HttpWebRequest;
                    if (req.RequestBodyReader != null)
                    {
                        var hreqStream = hreq.GetRequestStream();
                        req.RequestBodyReader.CopyTo(hreqStream);
                    }

                    HttpWebResponse hresp = null;

                    try
                    {
                        hresp = hreq.GetResponse() as HttpWebResponse;
                    }
                    catch (WebException ex)
                    {
                        hresp = ex.Response as HttpWebResponse;
                    }
                    catch
                    {
                    }

                    if (hresp == null)
                    {
                        resp.StatusCode = HttpStatusCode.InternalServerError;

                        req.RequestBodyReader?.CopyTo(Stream.Null);
                    }
                    else
                    {
                        using (hresp)
                        {
                            if (req.KeepAlive)
                            {
                                resp.Headers.Set(HttpResponseHeader.Connection, "Keep-Alive");
                                resp.Headers.Set(HttpResponseHeader.KeepAlive, "timeout=30");
                            }
                            
                            using (var hrespBody = hresp.GetResponseStream())
                                resp.FromHttpWebResponse(hresp, hrespBody);
                        }
                    }

                    if (!req.KeepAlive)
                        break;
                }
            } while (ProxyRequest.TryParse(this.ProxyStream, false, out req));
        }
    }
}
