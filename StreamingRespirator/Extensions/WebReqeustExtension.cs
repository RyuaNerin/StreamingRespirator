using System;
using System.IO;
using System.Net;
using System.Text;
using Sentry;
using StreamingRespirator.Core;

namespace StreamingRespirator.Extensions
{
    internal static class WebReqeustExtension
    {
        public static bool Do(this WebRequest req)
            => req.Do(null, out _, out _);

        public static bool Do(this WebRequest req, out HttpStatusCode statusCode)
            => req.Do(null, out statusCode, out _);

        public static bool Do<T>(this WebRequest req, out T response)
            where T : class
            => req.Do<T>(out _, out response);

        public static bool Do<T>(this WebRequest req, out HttpStatusCode statusCode, out T response)
            where T : class
        {
            response = null;

            if (!req.Do(typeof(T), out statusCode, out var value))
                return false;

            response = value as T;
            return true;
        }

        private static bool Do(this WebRequest req, Type type, out HttpStatusCode statusCode, out object response)
        {
            response = null;
            statusCode = 0;

            HttpWebResponse res = null;

            try
            {
                res = req.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                    res = ex.Response as HttpWebResponse;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            if (res != null)
            {
                using (res)
                {
                    try
                    {
                        statusCode = res.StatusCode;

                        if (type != null)
                        {
                            using (var stream = res.GetResponseStream())
                            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                            {
                                response = Program.JsonSerializer.Deserialize(streamReader, type);
                            }
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }
            }

            return false;
        }
    }
}
