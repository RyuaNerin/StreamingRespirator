using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Sentry;

namespace StreamingRespirator.Core.Streaming
{
    internal static class Certificates
    {
        public static readonly X509Certificate2 CA     = new X509Certificate2(Properties.Resources.ca);
        public static readonly X509Certificate2 Client = new X509Certificate2(Properties.Resources.client);

        public static bool InstallCACertificates()
        {
            try
            {
                using (var certStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    certStore.Open(OpenFlags.ReadWrite | OpenFlags.IncludeArchived);

                    if (!certStore.Certificates.Cast<X509Certificate2>().Any(le => le.Equals(CA)))
                    {
                        certStore.Add(CA);
                    }

                    var oldCAList = certStore
                                    .Certificates
                                    .Cast<X509Certificate2>()
                                    .Where(le =>
                                    {
                                        if (le.Equals(CA))
                                            return false;

                                        var subjectLower = le.Subject.ToLower();
                                        return subjectLower.Contains("streaming") && subjectLower.Contains("respirator");
                                    });

                    foreach (var cert in oldCAList)
                    {
                        try
                        {
                            certStore.Remove(cert);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return false;
            }

            return true;
        }
    }
}
