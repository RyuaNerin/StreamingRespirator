using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamingRespirator.Core.Json;
using StreamingRespirator.Core.Json.Streaming;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal interface ITimeLine : IDisposable
    {
        void Start();
        void Stop();
    }

    internal abstract class BaseTimeLine<T> : ITimeLine, IDisposable
    {
        private readonly TweetDeck m_tweetDeck;
        private readonly Timer m_timer;

        protected abstract string Method { get; }

        protected BaseTimeLine(TweetDeck tweetDeck)
        {
            this.m_tweetDeck = tweetDeck;
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
            this.m_timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        protected abstract string GetUrl();
        protected abstract IEnumerable<T> ParseHtml(string html);
        protected abstract IEnumerable<TwitterUser> SelectUsers(IEnumerable<T> items);
        protected abstract IEnumerable<T> FilterItemForConnection(StreamingConnection connection, IEnumerable<T> items);

        private static readonly DateTime ForTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private void Refresh(object state)
        {
            var next = 0;

            var req = this.m_tweetDeck.CreateReqeust(this.Method, this.GetUrl());
            IEnumerable<T> items = null;
            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                        items = this.ParseHtml(reader.ReadToEnd());

                    /*
                    x-rate-limit-limit      : 225
                    x-rate-limit-remaining  : 9
                    x-rate-limit-reset      : 1548385894
                    */

                    if (int.TryParse(res.Headers.Get("x-rate-limit-remaining"), out int remaining) &&
                        int.TryParse(res.Headers.Get("x-rate-limit-reset"), out int reset))
                    {
                        next = Math.Min(1000, (int)((reset - (DateTime.UtcNow - ForTimeStamp).TotalSeconds) / remaining * 1000));
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }

            if (items != null && items.Count() > 0)
            {
                var task = Task.Factory.StartNew(() =>
                {
                    var users = this.SelectUsers(items);
                    if (users != null)
                    {
                        foreach (var user in users)
                            if (this.m_tweetDeck.UserCache.IsUpdated(user))
                                this.UserUpdatedEvent(user);
                    }
                });

                Parallel.ForEach(this.m_tweetDeck.GetConnections(),
                    connection =>
                    {
                        var filtered = this.FilterItemForConnection(connection, items);
                        if (filtered == null)
                            return;

                        foreach (var item in filtered)
                            connection.SendToStream(item);
                    });

                task.Wait();
            }

            try
            {
                if (this.m_working)
                    this.m_timer.Change(next > 0 ? next : 15 * 1000, Timeout.Infinite);
            }
            catch
            {
            }
        }

        private void UserUpdatedEvent(TwitterUser user)
        {
            var data = new St_Event()
            {
                Event  = "user_update",
                Source = user,
                Target = user,
            };

            Parallel.ForEach(this.m_tweetDeck.GetConnections(), connection => connection.SendToStream(data));
        }
    }
}
