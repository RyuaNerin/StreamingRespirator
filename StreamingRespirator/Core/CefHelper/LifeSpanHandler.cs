using CefSharp;

namespace StreamingRespirator.Core.CefHelper
{
    internal class LifeSpanHandler : BaseLifeSpanHandler
    {
        protected override bool OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            return false;
        }
    }
}
