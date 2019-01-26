using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StreamingRespirator.Core.Json;
using StreamingRespirator.Core.Json.Tweetdeck;

namespace StreamingRespirator.Core.Streaming
{
    internal class UserCache : IDisposable
    {
        private class UserInfo
        {
            public long     Id;
            public DateTime LastAccess;
            public string   Name;
            public string   ScreenName;
            public string   ProfileImage;
        }

        private readonly TimeSpan CacheExpires = TimeSpan.FromHours(6);
        private readonly Dictionary<long, UserInfo> Cache = new Dictionary<long, UserInfo>();

        private readonly Timer timerCleaner;

        public UserCache()
        {
            this.timerCleaner = new Timer(this.CleanCache, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void Dispose()
        {
            this.timerCleaner.Change(Timeout.Infinite, Timeout.Infinite);
            this.timerCleaner.Dispose();
            GC.SuppressFinalize(this);
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
                    if (info.Name           != user.Name            ) { modified = true; info.Name         = user.Name;             }
                    if (info.ScreenName     != user.ScreenName      ) { modified = true; info.ScreenName   = user.ScreenName;       }
                    if (info.ProfileImage   != user.ProfileFimageUrl) { modified = true; info.ProfileImage = user.ProfileFimageUrl; }

                    info.LastAccess = DateTime.Now;

                    return modified;
                }
                else
                {
                    this.Cache.Add(user.Id, new UserInfo {
                        Id           = user.Id,
                        Name         = user.Name,
                        ScreenName   = user.ScreenName,
                        ProfileImage = user.ProfileFimageUrl,
                        LastAccess   = DateTime.Now,
                    });

                    return false;
                }
            }
        }
    }
}
