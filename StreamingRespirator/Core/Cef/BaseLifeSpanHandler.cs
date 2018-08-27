using CefSharp;

namespace StreamingRespirator.Core.Cef
{
    internal class BaseLifeSpanHandler : ILifeSpanHandler
    {
        protected virtual bool DoClose(IWebBrowser browserControl, IBrowser browser)
            => true;

        protected virtual void OnAfterCreated(IWebBrowser browserControl, IBrowser browser)
        {
        }

        protected virtual void OnBeforeClose(IWebBrowser browserControl, IBrowser browser)
        {
        }

        protected virtual bool OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            return true;
        }

        bool ILifeSpanHandler.DoClose(IWebBrowser browserControl, IBrowser browser)
            => this.DoClose(browserControl, browser);

        void ILifeSpanHandler.OnAfterCreated(IWebBrowser browserControl, IBrowser browser)
            => this.DoClose(browserControl, browser);

        void ILifeSpanHandler.OnBeforeClose(IWebBrowser browserControl, IBrowser browser)
            => this.DoClose(browserControl, browser);

        bool ILifeSpanHandler.OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            => this.OnBeforePopup(browserControl, browser, frame, targetUrl, targetFrameName, targetDisposition, userGesture, popupFeatures, windowInfo, browserSettings, ref noJavascriptAccess, out newBrowser);
    }
}
