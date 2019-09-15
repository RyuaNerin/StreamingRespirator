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

                bool verified;

                try
                {
                    verified = inst.Credential.VerifyCredentials();
                }
                catch
                {
                    return null;
                }

                if (!verified)
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
            var twitCred = (TwitterCredential)invoker.Invoke(new Func<TwitterCredential>(
                () =>
                {
                    using (var frm = new LoginWindowWeb())
                    {
                        frm.ShowDialog();

                        return frm.TwitterCredential;
                    }
                }));

            if (twitCred != null)
                AddClient(twitCred);
        }
    }
}
