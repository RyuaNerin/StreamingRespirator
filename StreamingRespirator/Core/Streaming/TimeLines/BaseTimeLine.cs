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
        private const double WaitMin     = 1;
        private const double WaitOnError = 10;

        protected readonly TwitterClient m_twitterClient;

        protected abstract string Method { get; }

        protected BaseTimeLine(TwitterClient twitterClient)
        {
            this.m_twitterClient = twitterClient;

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
                this.m_threadCancel?.Cancel();
                this.m_threadIsNotWorking.Wait();
            }

            PowerResumed -= this.Clear;
        }

        private readonly ManualResetEventSlim m_threadIsNotWorking = new ManualResetEventSlim(true);
        private Thread m_threadWorking;
        private CancellationTokenSource m_threadCancel;
        private CancellationTokenSource m_threadRefreshForce;

        public void Start()
        {
            if (!this.m_threadIsNotWorking.IsSet)
                return;

            this.m_threadWorking = new Thread(this.RefreshThread)
            {
                IsBackground = true,
                Name = $"{this.m_twitterClient.Credential.ScreenName} - {this.GetType().Name}",
            };
            this.m_threadCancel?.Dispose();
            this.m_threadCancel = new CancellationTokenSource();

            this.m_threadIsNotWorking.Reset();
            this.m_threadWorking.Start(this.m_threadCancel.Token);
        }
        public void Stop()
        {
            this.m_threadCancel?.Cancel();
            this.m_threadIsNotWorking.Wait();
            this.Clear();
        }

        private readonly object m_cursorLock = new object();
        protected string Cursor { get; private set; }
        private bool m_firstRefresh = true;

        protected void Clear()
        {
            lock (this.m_cursorLock)
                this.Cursor = null;

            this.m_firstRefresh = true;
        }

        protected abstract string GetUrl();

        /// <returns>New Curosr</returns>
        protected abstract string ParseHtml(TApiResult data, List<TItem> lstItems, HashSet<TwitterUser> lstUsers, bool isFirstRefresh);
        protected abstract void UpdateStatus(TimeSpan nextTime);

        public void ForceRefresh()
        {
            try
            {
                this.m_threadRefreshForce.Cancel();
            }
            catch
            {
            }
        }

        private void RefreshThread(object arg)
        {
            var token = (CancellationToken)arg;

            while (!token.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Max(this.Refresh(token).TotalSeconds, WaitMin));

                this.UpdateStatus(delay);

                using (this.m_threadRefreshForce = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    this.m_threadRefreshForce.CancelAfter(delay);

                    this.m_threadRefreshForce.Token.WaitHandle.WaitOne();
                }
            }

            this.m_threadIsNotWorking.Set();
        }

        /// <returns>대기 시간.</returns>
        private TimeSpan Refresh(CancellationToken token)
        {
            var req = this.m_twitterClient.Credential.CreateReqeust(this.Method, this.GetUrl());

            HttpWebResponse res = null;

            try
            {
                res = req.GetResponse() as HttpWebResponse;
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                {
                    res = webEx.Response as HttpWebResponse;
                }
                else
                {
                    return TimeSpan.FromSeconds(WaitOnError);
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return TimeSpan.FromSeconds(WaitOnError);
            }

            using (res)
            {
                try
                {
                    var setItems = new List<TItem>();
                    var setUsers = new HashSet<TwitterUser>();

                    if (res.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = res.GetResponseStream())
                        {
#if DEBUG
                            using (var buff = new MemoryStream(4096))
                            {
                                stream.CopyTo(buff);
                                buff.Position = 0;

                                lock (this.m_cursorLock)
                                {
                                    try
                                    {
                                        using (var streamReader = new StreamReader(buff, Encoding.UTF8))
                                        using (var jsonReader = new JsonTextReader(streamReader))
                                        {
                                            var cursor = this.ParseHtml(Program.JsonSerializer.Deserialize<TApiResult>(jsonReader), setItems, setUsers, !this.m_firstRefresh);
                                            if (cursor != null)
                                                this.Cursor = cursor;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        buff.Position = 0;
                                        using (var streamReader = new StreamReader(buff, Encoding.UTF8))
                                            ex.Data.Add("data.json", streamReader.ReadToEnd());

                                        throw ex;
                                    }
                                }
                            }
#else
                            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                            using (var jsonReader = new JsonTextReader(streamReader))
                            {
                                lock (this.m_cursorLock)
                                {
                                    var cursor = this.ParseHtml(Program.JsonSerializer.Deserialize<TApiResult>(jsonReader), setItems, setUsers, !this.m_firstRefresh);
                                    if (cursor != null)
                                        this.Cursor = cursor;
                                }
                            }
#endif
                        }

                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                Parallel.ForEach(
                                    setUsers,
                                    new ParallelOptions
                                    {
                                        CancellationToken = token,
                                    },
                                    user =>
                                    {
                                        if (this.m_twitterClient.UserCache.IsUpdated(user))
                                            this.UserUpdatedEvent(user);
                                    });
                            }
                            catch
                            {
                            }
                        });

                        if (!this.m_firstRefresh && setItems.Count > 0)
                        {
                            Task.Factory.StartNew(() =>
                            {
                                try
                                {
                                    Parallel.ForEach(
                                        this.m_twitterClient.GetConnections(),
                                        new ParallelOptions
                                        {
                                            CancellationToken = token,
                                        },
                                        connection =>
                                        {
                                            foreach (var item in setItems)
                                                connection.SendToStream(item);
                                        });
                                }
                                catch
                                {
                                }
                            });
                        }
                        this.m_firstRefresh = false;
                    }

                    return CalcNextRefresh(res.Headers);
                }
                catch (JsonSerializationException)
                {
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
            }

            return TimeSpan.FromSeconds(WaitOnError);
        }

        private static readonly DateTime ForTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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
                var resetTime = ForTimeStamp.AddSeconds(reset);
                var now = DateTime.UtcNow;

                if (resetTime < now)
                {
                    return TimeSpan.FromSeconds(WaitMin);
                }
                else if (remaining == 0)
                {
                    return resetTime - now;
                }
                else
                {
                    var sec = (resetTime - now).TotalSeconds;

                    if (Config.Instance.ReduceApiCall)
                        sec /= (remaining / 2);
                    else
                        sec /= (remaining);

                    return TimeSpan.FromSeconds(Math.Max(sec, WaitMin));
                }
            }

            return TimeSpan.FromSeconds(WaitOnError);
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
