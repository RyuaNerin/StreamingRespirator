using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class TlHome : BaseTimeLine<TwitterStatus>
    {
        public TlHome(TwitterClient twitterClient)
            : base(twitterClient)
        {
        }

        /*
        include_my_retweet      | 1
        cards_platform          | Web-13
        include_entities        | 1
        include_user_entities   | 1
        include_cards           | 1
        send_error_codes        | 1
        tweet_mode              | extended
        include_ext_alt_text    | true
        include_reply_count	    | true
        ---------------------------------------
        since_id                | cursor
        count                   | 1 or 200
        */
        private const string BaseUrl = "https://api.twitter.com/1.1/statuses/home_timeline.json?&include_my_retweet=1&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true";

        private long m_cursor = 0;

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.m_cursor == 0)
                return BaseUrl + "&count=1";
            else
                return BaseUrl + "&count=200&since_id=" + this.m_cursor;
        }

        protected override IEnumerable<TwitterStatus> ParseHtml(string html)
        {
            var items = JsonConvert.DeserializeObject<TwitterStatusList>(html)
                                   .OrderBy(e => e.Id);

            if (items.Count() == 0)
                return null;

            var curCursor = this.m_cursor;
            this.m_cursor = items.Max(e => e.Id);

            if (curCursor == 0)
                return null;

            return items.ToArray();
        }

        protected override IEnumerable<TwitterUser> SelectUsers(IEnumerable<TwitterStatus> items)
        {
            return items.Select(e => e.User);
        }

        protected override IEnumerable<TwitterStatus> FilterItemForConnection(StreamingConnection connection, IEnumerable<TwitterStatus> items)
        {
            var lid = connection.LastStatus;
            connection.LastStatus = items.Max(e => e.Id);

            return items.Where(e => e.Id > lid).OrderBy(e => e.Id);
        }
    }
}
