using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming
{
    internal class UserCache : IDisposable
    {
        private class UserInfo
        {
            public long Id;
            public DateTime LastAccess;
            public string Name;
            public string ScreenName;
            public string ProfileImage;
        }

        private readonly TimeSpan CacheExpires = TimeSpan.FromHours(1);
        private readonly SortedDictionary<long, UserInfo> Cache = new SortedDictionary<long, UserInfo>();

        private readonly Timer timerCleaner;

        public UserCache()
        {
            this.timerCleaner = new Timer(this.CleanCache, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        ~UserCache()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool m_disposing;
        protected void Dispose(bool disposing)
        {
            if (this.m_disposing) return;
            this.m_disposing = true;

            if (disposing)
            {
                this.timerCleaner.Change(Timeout.Infinite, Timeout.Infinite);
                this.timerCleaner.Dispose();
            }
        }

        private void CleanCache(object state)
        {
            lock (this.Cache)
            {
                var checkAccess = DateTime.Now - this.CacheExpires;

                var expired = this.Cache.Where(e => e.Value.LastAccess < checkAccess).Select(e => e.Key).ToArray();

                foreach (var id in expired)
                    this.Cache.Remove(id);
            }
        }

        public void Clear()
        {
            lock (this.Cache)
            {
                this.Cache.Clear();
            }
        }

        public bool IsUpdated(TwitterUser user)
        {
            if (user.Id == 0)
                return false;

            lock (this.Cache)
            {
                if (this.Cache.ContainsKey(user.Id))
                {
                    var info = this.Cache[user.Id];

                    var modified = false;
                    if (info.Name != user.Name) { modified = true; info.Name = user.Name; }
                    if (info.ScreenName != user.ScreenName) { modified = true; info.ScreenName = user.ScreenName; }
                    if (info.ProfileImage != user.ProfileFimageUrl) { modified = true; info.ProfileImage = user.ProfileFimageUrl; }

                    info.LastAccess = DateTime.Now;

                    return modified;
                }
                else
                {
                    this.Cache.Add(user.Id, new UserInfo
                    {
                        Id = user.Id,
                        Name = user.Name,
                        ScreenName = user.ScreenName,
                        ProfileImage = user.ProfileFimageUrl,
                        LastAccess = DateTime.Now,
                    });

                    return false;
                }
            }
        }

        public long GetUserIdByScreenName(string screenName)
        {
            lock (this.Cache)
            {
                var info = this.Cache.Values.FirstOrDefault(e => e.ScreenName.Equals(screenName, StringComparison.OrdinalIgnoreCase));

                return info != null ? info.Id : 0;
            }
        }
    }
}
