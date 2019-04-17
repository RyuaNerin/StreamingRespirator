using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using mshtml;

namespace StreamingRespirator.Core.Windows
{
    public partial class LoginWindowWeb : Form
    {
        static readonly Uri TwitterUri = new Uri("https://twitter.com/");

        private SHDocVw.WebBrowser m_iWebBrowser;

        public string Cookie { get; private set; }

        public LoginWindowWeb()
        {
            this.InitializeComponent();
        }

        private void LoginWindow2_Load(object sender, EventArgs e)
        {
            this.m_iWebBrowser = (SHDocVw.WebBrowser)this.ctlWeb.ActiveXInstance;

            this.m_iWebBrowser.Resizable = false;
            this.m_iWebBrowser.Silent = true;
            this.m_iWebBrowser.StatusBar = false;
            this.m_iWebBrowser.TheaterMode = false;
            this.m_iWebBrowser.Offline = false;
            this.m_iWebBrowser.MenuBar = false;
            this.m_iWebBrowser.RegisterAsBrowser = false;
            this.m_iWebBrowser.RegisterAsDropTarget = false;
            this.m_iWebBrowser.AddressBar = false;

            if (ClearTwitterCookies())
            {
                this.ctlWeb.Navigate("https://twitter.com/login");
            }
            else
            {
                this.DialogResult = DialogResult.Abort;
                this.Close();
            }
        }

        private void LoginWindow2_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                this.m_iWebBrowser?.Quit();
            }
            catch
            {
            }

            try
            {
                Marshal.ReleaseComObject(this.m_iWebBrowser);
            }
            catch
            {
            }
        }

        private void ctlWeb_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            this.ctlWeb.Visible = false;
        }

        private void ctlWeb_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (this.ctlWeb.Url.Host == "twitter.com" && this.ctlWeb.Url.AbsolutePath == "/")
            {
                this.ctlWeb.Stop();

                this.Cookie = GetTwitterCookies().GetCookieHeader(TwitterUri);
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            try
            {
                var doc = (HTMLDocument)this.ctlWeb.Document.DomDocument;

                try
                {
                    this.ctlWeb.Document.InvokeScript("eval", new object[] { "$(document).contextmenu(function(){return false;});" });
                }
                catch
                {
                }

                doc.RemoveElementByClass("topbar js-topbar");
                doc.RemoveElementByClass("clearfix mobile has-sms");
                doc.RemoveElementByClass("subchck");

                doc.body.style.backgroundColor = "#FFF";

                IHTMLElement elem;

                elem = doc.GetElementByClassName("message-text");
                if (elem != null)
                {
                    var msg = elem.innerText;
                    if (!string.IsNullOrWhiteSpace(msg))
                        MessageBox.Show(this, msg, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    elem.RemoveElement();
                }

                elem = doc.getElementById("page-container");
                if (elem != null)
                {
                    elem.style.padding = "3px";
                    elem.style.border = "";
                }

                this.ctlWeb.Visible = true;
            }
            catch
            {
            }
        }


        private static bool ClearTwitterCookies()
        {
            var optionPtr = IntPtr.Zero;
            try
            {
                optionPtr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(optionPtr, 3);

                return NativeMethods.InternetSetOption(0, 81/*INTERNET_OPTION_SUPPRESS_BEHAVIOR*/, optionPtr, sizeof(int));
            }
            finally
            {
                if (optionPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(optionPtr);
            }
        }

        private static CookieContainer GetTwitterCookies()
        {
            var cc = new CookieContainer();

            GetTwitterCookies(cc, 0);
            GetTwitterCookies(cc, NativeMethods.INTERNET_COOKIE_HTTPONLY);

            return cc;
        }

        private static void GetTwitterCookies(CookieContainer cc, int option)
        {
            int datasize = 8192 * 16;
            StringBuilder cookieData = new StringBuilder(datasize);
            if (!NativeMethods.InternetGetCookieEx(TwitterUri.ToString(), null, cookieData, ref datasize, option, IntPtr.Zero))
            {
                if (datasize < 0)
                    return;

                cookieData = new StringBuilder(datasize);
                if (!NativeMethods.InternetGetCookieEx(TwitterUri.ToString(), null, cookieData, ref datasize, option, IntPtr.Zero))
                    return;
            }

            if (cookieData.Length > 0)
                cc.SetCookies(TwitterUri, cookieData.ToString().Replace(';', ','));
        }

        private static class NativeMethods
        {
            [DllImport("wininet.dll", SetLastError = true)]
            public static extern bool InternetGetCookieEx(
                string url,
                string cookieName,
                StringBuilder cookieData,
                ref int size,
                int dwFlags,
                IntPtr lpReserved);

            [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern InternetCookieState InternetSetCookieEx(
                string lpszURL,
                string lpszCookieName,
                string lpszCookieData,
                int dwFlags,
                int dwReserved);

            [DllImport("wininet.dll")]
            public static extern bool InternetSetOption(
                int hInternet,
                int dwOption,
                IntPtr lpBuffer,
                int dwBufferLength);

            public const int INTERNET_COOKIE_HTTPONLY = 0x00002000;

            public enum InternetCookieState : int
            {
                COOKIE_STATE_UNKNOWN = 0x0,
                COOKIE_STATE_ACCEPT = 0x1,
                COOKIE_STATE_PROMPT = 0x2,
                COOKIE_STATE_LEASH = 0x3,
                COOKIE_STATE_DOWNGRADE = 0x4,
                COOKIE_STATE_REJECT = 0x5,
                COOKIE_STATE_MAX = COOKIE_STATE_REJECT
            }
        }
    }

    internal static class HtmlExtension
    {
        public static IHTMLElement GetElementByClassName(this HTMLDocument element, string className)
            => ((IHTMLElementCollection)((dynamic)element)?.getElementsByClassName(className)).At(0);

        public static IHTMLElement At(this IHTMLElementCollection collection, int index)
            => collection != null && index < collection.length ? (IHTMLElement)collection.item(index: index) : null;

        public static void RemoveElementById(this HTMLDocument doc, string value)
        {
            try
            {
                doc.getElementById(value).RemoveElement();
            }
            catch
            {
            }
        }
        public static void RemoveElementByClass(this HTMLDocument doc, string value)
        {
            try
            {
                doc.GetElementByClassName(value).RemoveElement();
            }
            catch
            {
            }
        }

        public static void RemoveElement(this IHTMLElement element)
        {
            if (element == null)
                return;

            try
            {
                ((dynamic)element).parentNode.removeChild(element);
            }
            catch
            {
            }
        }
    }
}
