using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StreamingRespirator.Core.Windows;

namespace StreamingRespirator.Core.Streaming
{
    internal struct StateUpdateData
    {
        public bool IsRemoved;
        public string ScreenName;
        public bool? Connected;
        public TimeSpan? WaitTimeHome;
        public TimeSpan? WaitTimeAboutMe;
        public TimeSpan? WaitTimeDm;
    }

    internal static class TwitterClientFactory
    {
        private static readonly Dictionary<long, TwitterClient> Instances = new Dictionary<long, TwitterClient>();
        private static ulong CurrentRevision = 0;

        public static int AccountCount
        {
            get
            {
                lock (Instances)
                    return Instances.Count;
            }
        }

        public static void CopyInstances(TwitterCredentialList lst)
        {
            lock (Instances)
            {
                if (lst.Revision != CurrentRevision)
                {
                    lst.Revision = CurrentRevision;

                    lst.Clear();

                    foreach (var v in Instances.Values)
                        lst.Add(v.Credential);
                }
            }
        }

        public static void SetInstances(TwitterCredentialList lst)
        {
            lock (Instances)
            {
                foreach (var cred in lst)
                {
                    if (string.IsNullOrWhiteSpace(cred.Cookie) ||
                        string.IsNullOrWhiteSpace(cred.ScreenName))
                        continue;

                    AddClient(cred);
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

                var verified = false;
                for (int i = 0; i < 3 && !verified; i++)
                {
                    verified = inst.Credential.VerifyCredentials();
                    if (!verified)
                        Program.NetworkAvailable.WaitOne(TimeSpan.FromSeconds(3));
                }

                if (!verified)
                {
                    //RemoveClient(id);
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
                CurrentRevision++;

                Instances[id].Dispose();
                Instances.Remove(id);

                ClientStatusUpdatedEvent(id, new StateUpdateData { IsRemoved = true });

                Config.Save();
            }
        }

        public static void AddClient(TwitterCredential twitCred)
        {
            lock (Instances)
            {
                CurrentRevision++;

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
                    twitClient.Credential.Cookie = twitCred.Cookie;
                }

                ClientStatusUpdatedEvent(twitCred.Id, new StateUpdateData { ScreenName = twitCred.ScreenName });

                Config.Save();
            }
        }

        public static void AddClient()
        {
            TwitterCredential cred;

            using (var frm = new LoginWindow())
            {
                frm.ShowDialog();
                cred = frm.TwitterCredential;
            }

            if (cred != null)
                AddClient(cred);
        }
    }
}
