using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core.Windows
{
    internal partial class LoginWindow : Form
    {
        private static readonly Uri TwitterUri = new Uri("https://twitter.com/");

        public LoginWindow()
        {
            this.InitializeComponent();
        }

        public TwitterCredential TwitterCredential { get; private set; }

        private void ctlHelp_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://streaming.ryuar.in/")?.Dispose();
            }
            catch
            {
            }
        }

        private void ctlCookie_TextChanged(object sender, EventArgs e)
        {
            this.ctlCookie.Text = this.ctlCookie.Text.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
        }

        private void ctlOkFile_Click(object sender, EventArgs e)
        {
            this.ctlOkFile.Enabled = false;

            if (this.ofg.ShowDialog() == DialogResult.OK)
            {
                using (var fs = File.OpenRead(this.ofg.FileName))
                {
                    this.Parse(fs, null);
                }
            }

            this.ctlOkFile.Enabled = true;
        }

        private void ctlOkText_Click(object sender, EventArgs e)
        {
            this.ctlOkText.Enabled = false;

            this.Parse(null, this.ctlCookie.Text);

            this.ctlOkText.Enabled = true;
        }

        private async void Parse(Stream stream, string text)
        {
            var cookieStr = await ParseInner(stream, text);
            var twitCred = await Task.Factory.StartNew(() => TwitterCredential.GetCredential(cookieStr));

            if (twitCred != null)
            {
                MessageBox.Show(string.Format(Lang.LoginWindowWeb__AddSuccess, twitCred.ScreenName), Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.TwitterCredential = twitCred;
            }
            else
            {
                MessageBox.Show(Lang.LoginWindowWeb__AddError, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            this.Close();
        }

        private static async Task<string> ParseInner(Stream stream, string text)
        {
            bool ok;
            string cookieStr;

            (ok, cookieStr) = await Do(ParseJson);
            if (ok) return cookieStr;

            (ok, cookieStr) = await Do(ParseNetscape);
            if (ok) return cookieStr;

            (ok, cookieStr) = await Do(ParseNameValuePair);
            if (ok) return cookieStr;

            return null;

            async Task<(bool, string)> Do(Func<TextReader, Task<(bool, string)>> func)
            {
                TextReader tr;
                if (stream != null)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    tr = new StreamReader(stream);
                }
                else
                {
                    tr = new StringReader(text);
                }

                using (tr)
                {
                    return await func(tr);
                }
            }
        }

        private static async Task<(bool, string)> ParseNetscape(TextReader tr)
        {
            var cc = new CookieContainer();
            string line;

            while ((line = await tr.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                try
                {
                    var s = line.Split('\t');

                    // Uri          flag    Path    Secure  expire      name        value
                    // 0            1       2       3       4           5           6
                    // .twitter.com TRUE    /       FALSE   1634351167  des_opt_in  N
                    var cookie = new Cookie(s[5], s[6], s[2], s[0])
                    {
                        //Discard = s[1] == "TRUE",
                        Secure = s[3] == "TRUE",
                        Expires = DateTime.MaxValue,
                    };

                    cc.Add(cookie);
                }
                catch
                {
                }
            }

            var cookieStr = cc.GetCookieHeader(TwitterUri);
            return (!string.IsNullOrWhiteSpace(cookieStr), cookieStr);
        }

        private static async Task<(bool, string)> ParseJson(TextReader tr)
        {
            var cc = new CookieContainer();

            try
            {
                using (var jtr = new JsonTextReader(tr))
                {
                    foreach (var jc in await Task.Factory.StartNew(() => JsonSerializer.Create().Deserialize<CookieJson[]>(jtr)))
                    {
                        var cookie = new Cookie(jc.Name, jc.Value, jc.Path, jc.Domain)
                        {
                            HttpOnly = jc.HttpOnly,
                            Secure = jc.Secure,
                            Expires = DateTime.MaxValue,
                        };

                        cc.Add(cookie);
                    }
                }
            }
            catch
            {
            }

            var cookieStr = cc.GetCookieHeader(TwitterUri);
            return (!string.IsNullOrWhiteSpace(cookieStr), cookieStr);
        }
        private class CookieJson
        {
            /*
            {
                "domain": ".twitter.com",
                "expirationDate": 1767759727.130219,
                "hostOnly": false,
                "httpOnly": false,
                "name": "remember_checked_on",
                "path": "/",
                "sameSite": "unspecified",
                "secure": true,
                "session": false,
                "storeId": "0",
                "value": "1",
                "id": 15
            },
            */
            [JsonProperty("domain"        )] public string Domain         { get; set; }
            [JsonProperty("expirationDate")] public long   ExpirationDate { get; set; }
            [JsonProperty("httpOnly"      )] public bool   HttpOnly       { get; set; }
            [JsonProperty("name"          )] public string Name           { get; set; }
            [JsonProperty("path"          )] public string Path           { get; set; }
            [JsonProperty("secure"        )] public bool   Secure         { get; set; }
            [JsonProperty("value"         )] public string Value          { get; set; }
        }

        private static async Task<(bool, string)> ParseNameValuePair(TextReader tr)
        {
            string line;
            string cookieStr = null;

            while ((line = await tr.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                if (Regex.IsMatch(line, @"((\w+)=([^\s]+);)+"))
                {
                    cookieStr = line;
                }
            }

            return (!string.IsNullOrWhiteSpace(cookieStr), cookieStr);
        }
    }
}
