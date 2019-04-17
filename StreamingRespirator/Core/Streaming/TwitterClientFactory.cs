using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using StreamingRespirator.Core.Windows;

namespace StreamingRespirator.Core.Streaming
{
    internal struct StateUpdateData
    {
        public bool         IsRemoved;
        public string       ScreenName;
        public bool?        Connected;
        public float?       WaitTimeHome;
        public float?       WaitTimeAboutMe;
        public float?       WaitTimeDm;
    }

    static class TwitterClientFactory
    {
        public static readonly Uri CookieUri = new Uri("https://twitter.com/");

        private static readonly Dictionary<long, TwitterClient> Instances = new Dictionary<long, TwitterClient>();

        public static int AccountCount
        {
            get
            {
                lock (Instances)
                    return Instances.Count;
            }
        }

        public static IEnumerable<TwitterCredential> Accounts
        {
            get
            {
                lock (Instances)
                    return Instances.Select(e => e.Value.Credential).ToArray();
            }
            set
            {
                lock (Instances)
                {
                    foreach (var cred in value)
                    {
                        if (string.IsNullOrWhiteSpace(cred.Cookie) ||
                            string.IsNullOrWhiteSpace(cred.ScreenName))
                            continue;

                        AddClient(cred);
                    }
                }
            }
        }

        public static event Action<long, StateUpdateData> ClientUpdated;

        private static void ClientStatusUpdatedEvent(long id, StateUpdateData statusData)
            => ClientUpdated?.Invoke(id, statusData);

        public static TwitterClient GetClient(long id)
        {
            lock (Instances)
            {
                if (!Instances.ContainsKey(id))
                    return null;

                var inst = Instances[id];

                var befScreenName = inst.Credential.ScreenName;
                if (!inst.Credential.VerifyCredentials())
                {
                    RemoveClient(id);
                    return null;
                }

                if (inst.Credential.ScreenName != befScreenName)
                    ClientStatusUpdatedEvent(id, new StateUpdateData { ScreenName = inst.Credential.ScreenName });

                return inst;
            }
        }

        public static TwitterClient GetInsatnce(long id)
        {
            lock (Instances)
            {
                if (!Instances.ContainsKey(id))
                    return null;

                return Instances[id];
            }
        }

        public static void RemoveClient(long id)
        {
            lock (Instances)
            {
                Instances[id].Dispose();
                Instances.Remove(id);

                ClientStatusUpdatedEvent(id, new StateUpdateData { IsRemoved = true });

                Config.Save();
            }
        }

        private static void AddClient(TwitterCredential twitCred)
        {
            lock (Instances)
            {
                TwitterClient twitClient;
                if (!Instances.ContainsKey(twitCred.Id))
                {
                    twitClient = new TwitterClient(twitCred);
                    twitClient.ClientUpdated += ClientStatusUpdatedEvent;

                    Instances.Add(twitCred.Id, twitClient);
                }
                else
                {
                    twitClient = Instances[twitCred.Id];
                    twitClient.Credential.ScreenName = twitCred.ScreenName;
                    twitClient.Credential.Cookie     = twitCred.Cookie;
                }

                ClientStatusUpdatedEvent(twitCred.Id, new StateUpdateData { ScreenName = twitCred.ScreenName });

                Config.Save();
            }
        }

        public static void AddClient(Control invoker)
        {
            string cookie = null;

            var dialogResult = (DialogResult)invoker.Invoke(new Func<DialogResult>(
                () =>
                {
                    using (var frm = new LoginWindowWeb())
                    {
                        frm.ShowDialog();

                        cookie = frm.Cookie;

                        return frm.DialogResult;
                    }
                }));

            if (dialogResult == DialogResult.OK)
            {
                var twitCred = GetCredential(cookie);

                if (twitCred != null)
                {
                    invoker.Invoke(new Action(() => MessageBox.Show(twitCred.ScreenName + "가 추가되었습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Information)));

                    AddClient(twitCred);
                }
                else
                {
                    invoker.Invoke(new Action(() => MessageBox.Show("인증에 실패하였습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));
                }
            }
            else if (dialogResult == DialogResult.Abort)
            {
                invoker.Invoke(new Action(() => MessageBox.Show("알 수 없는 오류입니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Asterisk)));
            }
        }

        private static TwitterCredential GetCredential(string cookie)
        {
            var tempCredentials = new TwitterCredential()
            {
                Cookie = cookie,
            };

            if (!tempCredentials.VerifyCredentials())
                return null;
            
            return tempCredentials;
        }
    }
}
