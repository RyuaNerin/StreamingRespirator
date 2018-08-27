using System.Security.Cryptography.X509Certificates;
using CefSharp;

namespace StreamingRespirator.Core.Cef
{
    internal class BaseRequestHandler : IRequestHandler
    {
        protected virtual IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            => null;
        
        protected virtual void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        { }
        
        protected virtual bool CanGetCookies(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request)
            => true;
        
        protected virtual bool CanSetCookie(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, Cookie cookie)
            => true;
        
        protected virtual bool GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        => false;
        
        protected virtual bool OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect)
        => false;
        
        protected virtual CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            if (!callback.IsDisposed)
                callback.Dispose();
            return CefReturnValue.Continue;
        }
        
        protected virtual bool OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
        {
            if (!callback.IsDisposed)
                callback.Dispose();
            return false;
        }
        
        protected virtual bool OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
        => false;
        
        protected virtual void OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath)
        { }
        
        protected virtual bool OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url)
        => false;

        protected virtual bool OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
        {
            if (!callback.IsDisposed)
                callback.Dispose();
            return false;
        }

        protected virtual void OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status)
        {
        }

        protected virtual void OnRenderViewReady(IWebBrowser browserControl, IBrowser browser)
        {
        }

        protected virtual void OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
        {
        }

        protected virtual bool OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            => false;

        protected virtual bool OnSelectClientCertificate(IWebBrowser browserControl, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
        {
            if (!callback.IsDisposed)
                callback.Dispose();
            return false;
        }


        IResponseFilter IRequestHandler.GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        => this.GetResourceResponseFilter(browserControl, browser, frame, request, response);

        void IRequestHandler.OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        => this.OnResourceLoadComplete(browserControl, browser, frame, request, response, status, receivedContentLength);

        bool IRequestHandler.CanGetCookies(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request)
        => this.CanGetCookies(browserControl, browser, frame, request);

        bool IRequestHandler.CanSetCookie(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, Cookie cookie)
        => this.CanSetCookie(browserControl, browser, frame, request, cookie);

        bool IRequestHandler.GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        => this.GetAuthCredentials(browserControl, browser, frame, isProxy, host, port, realm, scheme, callback);

        bool IRequestHandler.OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect)
        => this.OnBeforeBrowse(browserControl, browser, frame, request, isRedirect);

        CefReturnValue IRequestHandler.OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        => this.OnBeforeResourceLoad(browserControl, browser, frame, request, callback);

        bool IRequestHandler.OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
        => this.OnCertificateError(browserControl, browser, errorCode, requestUrl, sslInfo, callback);

        bool IRequestHandler.OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
        => this.OnOpenUrlFromTab(browserControl, browser, frame, targetUrl, targetDisposition, userGesture);

        void IRequestHandler.OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath)
        => this.OnPluginCrashed(browserControl, browser, pluginPath);

        bool IRequestHandler.OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url)
        => this.OnProtocolExecution(browserControl, browser, url);

        bool IRequestHandler.OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
        => this.OnQuotaRequest(browserControl, browser, originUrl, newSize, callback);

        void IRequestHandler.OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status)
        => this.OnRenderProcessTerminated(browserControl, browser, status);

        void IRequestHandler.OnRenderViewReady(IWebBrowser browserControl, IBrowser browser)
        => this.OnRenderViewReady(browserControl, browser);

        void IRequestHandler.OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
        => this.OnResourceRedirect(browserControl, browser, frame, request, response, ref newUrl);

        bool IRequestHandler.OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        => this.OnResourceResponse(browserControl, browser, frame, request, response);

        bool IRequestHandler.OnSelectClientCertificate(IWebBrowser browserControl, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
        => this.OnSelectClientCertificate(browserControl, browser, isProxy, host, port, certificates, callback);
    }
}
