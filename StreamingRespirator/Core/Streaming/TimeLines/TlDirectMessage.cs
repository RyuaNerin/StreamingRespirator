using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Core.Streaming.Twitter.Packet;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class TlDirectMessage : BaseTimeLine<PacketDirectMessage>
    {
        public TlDirectMessage(TwitterClient twitterClient)
            : base(twitterClient)
        {
        }

        /*
        include_groups          | true
        ext                     | altText
        cards_platform          | Web-13
        include_entities        | 1
        include_user_entities   | 1
        include_cards           | 1
        send_error_codes        | 1
        tweet_mode              | extended
        include_ext_alt_text    | true
        include_reply_count	    | true
        ----------------------------------------
        since_id                |     | cursor
        */
        private const string BaseUrl = "https://api.twitter.com/1.1/dm/user_updates.json?include_groups=true&ext=altText&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true";

        private string m_cursor = null;

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.m_cursor == null)
                return BaseUrl;
            else
                return BaseUrl + "&cursor=" + this.m_cursor;
        }

        protected override (IEnumerable<PacketDirectMessage> items, IEnumerable<TwitterUser> users) ParseHtml(string html)
        {
            var dm = JsonConvert.DeserializeObject<DirectMessage>(html);
            if (!(dm?.Item?.Entries?.Length > 0))
                return (null, null);

            var items = dm.Item
                          .Entries
                          .Where(e => e.Message != null)
                          .Select(e => ToPacket(dm, e))
                          .OrderBy(e => e.Item.Id)
                          .ToArray();

            var users = items.Select(e => e.Item.Sender);

            var curCursor = this.m_cursor;
            this.m_cursor = dm.Item.Cursor;

            if (curCursor == null)
                items = null;

            return (items, users);
        }

        private static PacketDirectMessage ToPacket(DirectMessage dm, DirectMessage.Entry e)
        {
            var packet = new PacketDirectMessage();
            packet.Item.Id        = e.Message.Data.Id;
            packet.Item.Text      = e.Message.Data.Text;
            packet.Item.CreatedAt = e.Message.Data.CreatedAt;
            packet.Item.Sender    = dm.Item.Users[e.Message.Data.Sender_Id];
            packet.Item.Recipient = dm.Item.Users[e.Message.Data.Recipiend_Id];

            return packet;
        }

        protected override void UpdateStatus(float waitTime)
        {
            this.m_twitterClient.TimelineUpdated(new StateUpdateData { WaitTimeDm = waitTime });
        }
    }
}
