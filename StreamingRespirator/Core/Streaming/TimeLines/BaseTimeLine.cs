using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
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

    internal abstract class BaseTimeLine
    {
        static BaseTimeLine()
        {
            SystemEvents.PowerModeChanged += (sender, e) =>
            {
                if (e.Mode == PowerModes.Resume)
                    PowerResumed?.Invoke();
            };
        }

        
        protected static event Action PowerResumed;
    }

    internal abstract class BaseTimeLine<TApiResult, TItem> : BaseTimeLine, ITimeLine, IDisposable
        where TItem : IPacket
    {
        private readonly static TimeSpan WaitMin     = TimeSpan.FromSeconds(1);
        private readonly static TimeSpan WaitOnError = TimeSpan.FromSeconds(10);

        protected readonly TwitterClient m_twitterClient;
        private readonly Timer m_timer;

        protected abstract string Method { get; }

        protected BaseTimeLine(TwitterClient twitterClient)
        {
            this.m_twitterClient = twitterClient;
            this.m_timer = new Timer(this.Refresh, null, Timeout.Infinite, Timeout.Infinite);

            PowerResumed += this.Clear;
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

            PowerResumed -= this.Clear;
        }

        private long m_working;
        protected Timer Timer => Interlocked.Read(ref this.m_working) == 0 ? null : this.m_timer;

        public void Start()
        {
            Interlocked.Exchange(ref this.m_working, 1);

            lock (this.m_timer)
                this.m_timer.Change(0, Timeout.Infinite);
        }
        public void Stop()
        {
            Interlocked.Exchange(ref this.m_working, 1);
            this.Clear();

            lock (this.m_timer)
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
            var next = WaitOnError;

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

                    next = CalcNextRefresh(res.Headers);
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
                    using (var res = webEx.Response as HttpWebResponse)
                    {
                        next = CalcNextRefresh(res.Headers);
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
                this.Timer?.Change((int)next.TotalSeconds, Timeout.Infinite);
                this.UpdateStatus((float)next.TotalSeconds);
            }
            catch
            {
            }
        }

        private static TimeSpan CalcNextRefresh(WebHeaderCollection headers)
        {
            /*
            x-rate-limit-limit      : 225
            x-rate-limit-remaining  : 9
            x-rate-limit-reset      : 1548385894
            */

            if (int.TryParse(headers.Get("x-rate-limit-remaining"), out int remaining) &&
                int.TryParse(headers.Get("x-rate-limit-reset"), out int reset))
            {
                double sec;

                if (Config.Instance.ReduceApiCall)
                    sec = (reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / (remaining / 2) * 1000;
                else
                    sec = (reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / (remaining    ) * 1000;

                var ts = TimeSpan.FromSeconds(sec);
                return ts > WaitMin ? ts : WaitMin;
            }

            return WaitOnError;
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
