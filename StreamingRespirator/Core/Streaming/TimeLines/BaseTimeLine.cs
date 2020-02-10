using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Core.Streaming.Twitter.Packet;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal interface ITimeLine : IDisposable
    {
        void Start();
        void Stop();
        void ForceRefresh();
    }

    internal abstract class BaseTimeLine<TApiResult, TItem> : ITimeLine, IDisposable
        where TItem : IPacket
    {
        protected readonly TwitterClient m_twitterClient;
        private readonly Timer m_timer;

        protected abstract string Method { get; }

        protected BaseTimeLine(TwitterClient twitterClient)
        {
            this.m_twitterClient = twitterClient;
            this.m_timer = new Timer(this.Refresh, null, Timeout.Infinite, Timeout.Infinite);
        }

        ~BaseTimeLine()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool m_disposed;
        private void Dispose(bool disposing)
        {
            if (this.m_disposed) return;
            this.m_disposed = true;

            if (disposing)
            {
                this.m_timer.Change(Timeout.Infinite, Timeout.Infinite);
                this.m_timer.Dispose();
            }
        }

        private volatile bool m_working;
        public void Start()
        {
            this.m_working = true;
            this.m_timer.Change(0, Timeout.Infinite);
        }
        public void Stop()
        {
            this.m_working = false;
            this.Clear();
            this.m_timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        protected abstract void Clear();

        protected abstract string GetUrl();
        protected abstract void ParseHtml(TApiResult data, List<TItem> lstItems, HashSet<TwitterUser> lstUsers);
        protected abstract void UpdateStatus(float nextTime);

        public void ForceRefresh()
        {
            this.m_timer.Change(0, Timeout.Infinite);
        }

        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private static readonly DateTime ForTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private void Refresh(object state)
        {
            var next = 15 * 1000;

            var req = this.m_twitterClient.Credential.CreateReqeust(this.Method, this.GetUrl());
            var setItems = new List<TItem>();
            var setUsers = new HashSet<TwitterUser>();
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                    using (var jsonReader = new JsonTextReader(streamReader))                        
                        this.ParseHtml(Serializer.Deserialize<TApiResult>(jsonReader), setItems, setUsers);

                    CalcNextRefresh(res.Headers, ref next);
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
                    using (var res = webEx.Response as HttpWebResponse)
                    {
                        CalcNextRefresh(res.Headers, ref next);
                    }
                }
                else
                {
                    SentrySdk.CaptureException(webEx);
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            var userCacheTask = Task.Factory.StartNew(() =>
            {
                foreach (var user in setUsers)
                    if (this.m_twitterClient.UserCache.IsUpdated(user))
                        this.UserUpdatedEvent(user);
            });

            if (setItems.Count > 0)
            {
                try
                {
                    Parallel.ForEach(this.m_twitterClient.GetConnections(),
                        connection =>
                        {
                            foreach (var item in setItems)
                                connection.SendToStream(item);
                        });
                }
                catch
                {
                }
            }

            userCacheTask.Wait();

            try
            {
                if (this.m_working)
                {
                    this.m_timer.Change(next > 0 ? next : 15 * 1000, Timeout.Infinite);

                    this.UpdateStatus(next / 1000f);
                }
            }
            catch
            {
            }
        }

        private static void CalcNextRefresh(WebHeaderCollection headers, ref int next)
        {
            /*
            x-rate-limit-limit      : 225
            x-rate-limit-remaining  : 9
            x-rate-limit-reset      : 1548385894
            */

            if (int.TryParse(headers.Get("x-rate-limit-remaining"), out int remaining) &&
                int.TryParse(headers.Get("x-rate-limit-reset"), out int reset))
            {
                if (Config.ReduceApiCall)
                    next = (int)((reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / (remaining / 2) * 1000);
                else
                    next = (int)((reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / remaining * 1000);

                next = Math.Max(1000, next);
            }
        }

        private void UserUpdatedEvent(TwitterUser user)
        {
            var data = new PacketUserUpdated()
            {
                Source = user,
                Target = user,
            };

            Parallel.ForEach(this.m_twitterClient.GetConnections(), connection => connection.SendToStream(data));
        }
    }
}
