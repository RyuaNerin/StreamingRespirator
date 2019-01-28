using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.TimeLines;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Core.Streaming.Twitter.Packet;

namespace StreamingRespirator.Core.Streaming
{
    internal class TwitterClient : IDisposable
    {
        private readonly HashSet<StreamingConnection> m_connections = new HashSet<StreamingConnection>();

        public event Action<long> StreamingStarted;
        public event Action<long> StreamingStoped;

        private readonly ITimeLine m_tlHome;
        private readonly ITimeLine m_tlAboutMe;
        private readonly ITimeLine m_tlDm;
        
        public TwitterCredential Credential { get; }
        
        public UserCache UserCache { get; } = new UserCache();

        public TwitterClient(TwitterCredential credential)
        {
            this.Credential = credential;

            this.m_tlHome    = new TlHome         (this);
            this.m_tlAboutMe = new TlAboutMe      (this);
            this.m_tlDm      = new TlDirectMessage(this);
        }

        ~TwitterClient()
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
                this.m_tlHome   .Dispose();
                this.m_tlAboutMe.Dispose();
                this.m_tlDm     .Dispose();

                this.UserCache.Dispose();
            }

            lock (this.m_connections)
            {
                Parallel.ForEach(this.m_connections, e => e.Stream.Close());

                this.m_connections.Clear();
            }

        }

        public void AddConnection(StreamingConnection connection)
        {
            this.SendFriendsPacket(connection);

            lock (this.m_connections)
            {
                var befCount = this.m_connections.Count;

                this.m_connections.Add(connection);

                if (befCount == 0)
                    this.StartTimeLine();
            }
        }
        public void RemoveStream(StreamingConnection connection)
        {
            lock (this.m_connections)
            {
                var befCount = this.m_connections.Count;

                this.m_connections.Remove(connection);
                
                if (befCount == 1)
                    this.StopTimeLine();
            }
        }

        private void StartTimeLine()
        {
            this.m_tlHome   .Start();
            this.m_tlAboutMe.Start();
            this.m_tlDm     .Start();

            this.StreamingStarted?.Invoke(this.Credential.Id);
        }

        private void StopTimeLine()
        {
            this.m_tlHome   .Stop();
            this.m_tlAboutMe.Stop();
            this.m_tlDm     .Stop();

            this.StreamingStoped?.Invoke(this.Credential.Id);
        }

        private class FriendsCursor
        {
            [JsonProperty("ids")]
            public long[] Ids { get; set; }
        }
        public void SendFriendsPacket(StreamingConnection connection)
        {
            var GetFriendsPacket = new PacketFriends
            {
                Friends = this.GetFriendsPacket(),
            };

            Parallel.ForEach(this.GetConnections(), e => e.SendToStream(GetFriendsPacket));
        }
        private long[] GetFriendsPacket()
        {
            var req = this.Credential.CreateReqeust("GET", $"https://api.twitter.com/1.1/friends/ids.json?count=5000user_id={this.Credential.Id}");

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        return JsonConvert.DeserializeObject<FriendsCursor>(reader.ReadToEnd()).Ids;
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }
            catch
            {
            }

            return null;
        }

        public StreamingConnection[] GetConnections()
        {
            lock (this.m_connections)
                return this.m_connections.ToArray();
        }

        public void StatusDestroyed(long id)
        {
            Task.Factory.StartNew(() => this.SendDeletePacket(this.Credential.Id, id));
        }
        private void SendDeletePacket(long userId, long id)
        {
            var packetDelete = new PacketDelete();
            packetDelete.Delete.Status.UserId = userId;
            packetDelete.Delete.Status.Id = id;

            Parallel.ForEach(this.GetConnections(), e => e.SendToStream(packetDelete));
        }

        public void StatusMaybeDestroyed(long id)
        {
            Task.Factory.StartNew(() => this.CheckStatus(id));
        }
        private void CheckStatus(long id)
        {
            if (this.ShowStatus(id) != null)
                return;

            var packetDelete = new PacketDelete();
            packetDelete.Delete.Status.UserId = 0;
            packetDelete.Delete.Status.Id = id;

            Parallel.ForEach(this.GetConnections(), e => e.SendToStream(packetDelete));
        }
        private TwitterStatus ShowStatus(long id)
        {
            /*
            id                   | ///
            */
            var req = this.Credential.CreateReqeust("GET", "https://api.twitter.com/1.1/statuses/show.json?id=" + id);

            try
            {
                using (var res = req.GetResponse() as HttpWebResponse)
                {
                    using (var stream = res.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return JsonConvert.DeserializeObject<TwitterStatus>(reader.ReadToEnd());
                    }
                }
            }
            catch (WebException webEx)
            {
                webEx.Response?.Dispose();
            }
            catch
            {
            }

            return null;
        }

        public void SendStatus(TwitterStatus stauts)
        {
            Task.Factory.StartNew(() => Parallel.ForEach(this.GetConnections(), e => e.SendToStream(stauts)));
        }
    }
}
