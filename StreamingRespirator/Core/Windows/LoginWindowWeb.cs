using System;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using mshtml;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core.Windows
{
    [System.ComponentModel.DesignerCategory("CODE")]
    internal class LoginWindowWeb : Form
    {
        static readonly Uri TwitterUri = new Uri("https://twitter.com/");

        private readonly WebBrowser m_webBrowser;
        private SHDocVw.WebBrowser m_webBrowserSh;

        public TwitterCredential TwitterCredential { get; private set; }

        static LoginWindowWeb()
        {
            NativeMethods.SetCookieSupressBehavior();
        }

        public LoginWindowWeb()
        {
            this.SuspendLayout();

            this.m_webBrowser = new WebBrowser();
            this.m_webBrowser.AllowWebBrowserDrop            = false;
            this.m_webBrowser.CausesValidation               = false;
            this.m_webBrowser.Dock                           = DockStyle.Fill;
            this.m_webBrowser.IsWebBrowserContextMenuEnabled = false;
            this.m_webBrowser.ScrollBarsEnabled              = false;
            this.m_webBrowser.Visible                        = false;

            this.m_webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(this.ctlWeb_DocumentCompleted);
            this.m_webBrowser.Navigating        += new WebBrowserNavigatingEventHandler(this.ctlWeb_Navigating);

            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode       = AutoScaleMode.Dpi;
            this.BackColor           = Color.White;
            this.ClientSize          = new Size(380, 240);
            this.FormBorderStyle     = FormBorderStyle.FixedSingle;
            this.MaximizeBox         = false;
            this.MinimizeBox         = false;
            this.StartPosition       = FormStartPosition.CenterScreen;

            this.Controls.Add(this.m_webBrowser);

            this.Text = "스트리밍 호흡기 로그인";

            this.ResumeLayout(false);
        }

        private bool m_disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.m_disposed)
            {
                this.m_disposed = true;

                try
                {
                    this.m_webBrowserSh?.Quit();
                }
                catch
                {
                }

                try
                {
                    Marshal.ReleaseComObject(this.m_webBrowserSh);
                }
                catch
                {
                }

                this.m_webBrowser.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.m_webBrowserSh = (SHDocVw.WebBrowser)this.m_webBrowser.ActiveXInstance;

            this.m_webBrowserSh.Resizable = false;
            this.m_webBrowserSh.Silent = true;
            this.m_webBrowserSh.StatusBar = false;
            this.m_webBrowserSh.TheaterMode = false;
            this.m_webBrowserSh.Offline = false;
            this.m_webBrowserSh.MenuBar = false;
            this.m_webBrowserSh.RegisterAsBrowser = false;
            this.m_webBrowserSh.RegisterAsDropTarget = false;
            this.m_webBrowserSh.AddressBar = false;

            this.m_webBrowser.Navigate("https://twitter.com/login");
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            this.m_webBrowser.Focus();
        }

        private void ctlWeb_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            this.m_webBrowser.Visible = false;
        }

        private bool m_logined = false;
        private void ctlWeb_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (this.m_webBrowser.Url.Host == "twitter.com" && this.m_webBrowser.Url.AbsolutePath == "/")
            {
                if (this.m_logined)
                    return;

                this.m_logined = true;

                this.m_webBrowser.Stop();

                Task.Factory.StartNew(() =>
                {
                    var cookie = NativeMethods.GetCookies(TwitterUri).GetCookieHeader(TwitterUri);
                    var twitCred = TwitterCredential.GetCredential(cookie);

                    if (twitCred != null)
                    {
                        this.Invoke(new Action(() => MessageBox.Show(this, twitCred.ScreenName + "가 추가되었습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Information)));

                        this.TwitterCredential = twitCred;
                    }
                    else
                    {
                        this.Invoke(new Action(() => MessageBox.Show(this, "인증에 실패하였습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));
                    }

                    this.Invoke(new Action(this.Close));
                });
                return;
            }

            try
            {
                var doc = (HTMLDocument)this.m_webBrowser.Document.DomDocument;

                try
                {
                    this.m_webBrowser.Document.InvokeScript("eval", new object[] { "$(document).contextmenu(function(){return false;});" });
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

                this.m_webBrowser.Visible = true;
            }
            catch
            {
            }
        }

        private static class NativeMethods
        {
            [DllImport("wininet.dll")]
            private static extern bool InternetSetOption(
                int hInternet,
                int dwOption,
                IntPtr lpBuffer,
                int dwBufferLength);

            [DllImport("wininet.dll")]
            private static extern bool InternetGetCookieEx(
                string url,
                string cookieName,
                StringBuilder cookieData,
                ref int size,
                int dwFlags,
                IntPtr lpReserved);

            private const int INTERNET_COOKIE_HTTPONLY = 0x00002000;
            private const int INTERNET_OPTION_SUPPRESS_BEHAVIOR = 81;

            public static void SetCookieSupressBehavior()
            {
                var optionPtr = IntPtr.Zero;
                try
                {
                    optionPtr = Marshal.AllocHGlobal(4);
                    Marshal.WriteInt32(optionPtr, 3);

                    InternetSetOption(0, INTERNET_OPTION_SUPPRESS_BEHAVIOR, optionPtr, 4);
                }
                finally
                {
                    if (optionPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(optionPtr);
                }
            }

            public static CookieContainer GetCookies(Uri uri)
            {
                var cc = new CookieContainer();

                GetCookies(uri, cc, 0);
                GetCookies(uri, cc, INTERNET_COOKIE_HTTPONLY);

                return cc;
            }
            private static void GetCookies(Uri uri, CookieContainer cc, int option)
            {
                int datasize = 8192 * 16;
                StringBuilder cookieData = new StringBuilder(datasize);
                if (!InternetGetCookieEx(uri.ToString(), null, cookieData, ref datasize, option, IntPtr.Zero))
                {
                    if (datasize < 0)
                        return;

                    cookieData = new StringBuilder(datasize);
                    if (!InternetGetCookieEx(uri.ToString(), null, cookieData, ref datasize, option, IntPtr.Zero))
                        return;
                }

                if (cookieData.Length > 0)
                    cc.SetCookies(uri, cookieData.ToString().Replace(';', ','));
            }
        }
    }

    internal static class HtmlElementExtension
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
