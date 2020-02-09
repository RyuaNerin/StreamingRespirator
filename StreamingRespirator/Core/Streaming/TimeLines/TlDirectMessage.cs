using System.Collections.Generic;
using System.Linq;
using StreamingRespirator.Core.Streaming.Twitter;
using StreamingRespirator.Core.Streaming.Twitter.Packet;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class TlDirectMessage : BaseTimeLine<DirectMessage, PacketDirectMessage>
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
        protected override void Clear()
        {
            this.m_cursor = null;
        }

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.m_cursor == null)
                return BaseUrl;
            else
                return BaseUrl + "&cursor=" + this.m_cursor;
        }

        protected override void ParseHtml(DirectMessage data, List<PacketDirectMessage> lstItems, HashSet<TwitterUser> lstUsers)
        {
            if ((data?.Items?.Entries?.Length ?? 0) == 0)
                return;

            foreach (var item in data.Items.Entries.Where(e => e.Message != null))
            {
                lstItems.Add(ToPacket(data, item));
            }
            foreach (var user in data.Items.Users.Values)
            {
                lstUsers.Add(user);
            }

            lstItems.Sort((a, b) => a.Item.Id.CompareTo(b.Item.Id));

            var curCursor = this.m_cursor;
            this.m_cursor = data.Items.Cursor;

            if (curCursor == null)
                lstItems.Clear();
        }

        private static PacketDirectMessage ToPacket(DirectMessage dm, DirectMessage.Entry e)
        {
            return new PacketDirectMessage
            {
                Item = new PacketDirectMessage.DirectMessageItem
                {
                    Id        = e.Message.Data.Id,
                    Text      = e.Message.Data.Text,
                    CreatedAt = e.Message.Data.CreatedAt,
                    Sender    = dm.Items.Users[e.Message.Data.Sender_Id],
                    Recipient = dm.Items.Users[e.Message.Data.Recipiend_Id],
                },
            };
        }

        protected override void UpdateStatus(float waitTime)
        {
            this.m_twitterClient.TimelineUpdated(new StateUpdateData { WaitTimeDm = waitTime });
        }
    }
}
