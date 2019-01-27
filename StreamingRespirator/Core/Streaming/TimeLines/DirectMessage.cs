using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StreamingRespirator.Core.Twitter;
using StreamingRespirator.Core.Twitter.Streaming;
using StreamingRespirator.Core.Twitter.Tweetdeck;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class DirectMessage : BaseTimeLine<StreamingDirectMessage>
    {
        public DirectMessage(TweetDeck tweetDeck)
            : base(tweetDeck)
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
        cursor                  | /////
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

        protected override IEnumerable<StreamingDirectMessage> ParseHtml(string html)
        {
            var dmJson = JsonConvert.DeserializeObject<Twitter.Tweetdeck.DirectMessage>(html);

            if (!(dmJson?.Item?.Entries?.Length > 0))
                return null;

            var curCursor = this.m_cursor;
            this.m_cursor = dmJson.Item.Cursor;

            if (curCursor == null)
                return null;

            return dmJson.Item
                         .Entries
                         .Where(e => e.Message != null)
                         .Select(e =>
                         {
                             var dm = new StreamingDirectMessage();

                             dm.Item.Id = e.Message.Data.Id;
                             dm.Item.IdStr = e.Message.Data.Id.ToString();
                             dm.Item.Text = e.Message.Data.Text;
                             dm.Item.CreatedAt = e.Message.Data.CreatedAt;

                             var sender = dmJson.Item.Users[e.Message.Data.Sender_Id];
                             dm.Item.Sender = sender;
                             dm.Item.SenderId = sender.Id;
                             dm.Item.SenderScreenName = sender.ScreenName;

                             var recipient = dmJson.Item.Users[e.Message.Data.Recipiend_Id];
                             dm.Item.Recipient = recipient;
                             dm.Item.RecipientId = recipient.Id;
                             dm.Item.RecipientScreenName = recipient.ScreenName;

                             return dm;
                         })
                         .ToArray();
        }

        protected override IEnumerable<TwitterUser> SelectUsers(IEnumerable<StreamingDirectMessage> items)
        {
            return items.Select(e => e.Item.Sender);
        }

        protected override IEnumerable<StreamingDirectMessage> FilterItemForConnection(StreamingConnection connection, IEnumerable<StreamingDirectMessage> items)
        {
            var lid = connection.LastDirectMessage;
            connection.LastDirectMessage = items.Max(e => e.Item.Id);

            if (lid == 0)
                return null;

            return items.Where(e => e.Item.Id > lid).OrderBy(e => e.Item.Id);
        }
    }
}
