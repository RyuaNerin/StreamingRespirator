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
        since_id                |     | cursor
        count                   |  1  | 200
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

        protected override (IEnumerable<TwitterStatus> items, IEnumerable<TwitterUser> users) ParseHtml(string html)
        {
            var list = JsonConvert.DeserializeObject<TwitterStatusList>(html);
            if (list.Count == 0)
                return (null, null);

            var items = list.OrderBy(e => e.Id)
                            .ToArray();
            var users = items.Select(e => e.User);

            var curCursor = this.m_cursor;
            this.m_cursor = list.Max(e => e.Id);

            if (curCursor == 0)
                items = null;

            return (items, users);
        }
    }
}
