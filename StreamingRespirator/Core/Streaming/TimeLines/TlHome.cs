using System;
using System.Collections.Generic;
using System.Linq;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class TlHome : BaseTimeLine<TwitterStatusList, TwitterStatus>
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

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.Cursor == null)
                return $"{BaseUrl}&count=1";
            else
                return $"{BaseUrl}&count=200&since_id={this.Cursor}";
        }

        protected override string ParseHtml(TwitterStatusList data, List<TwitterStatus> lstItems, HashSet<TwitterUser> lstUsers, bool isNotFirstRefresh)
        {
            if (data.Count > 0)
            {
                if (isNotFirstRefresh && data.Count > 0)
                {
                    foreach (var item in data)
                    {
                        item.AddUserToHashSet(lstUsers);
                        lstItems.Add(item);
                    }

                    lstItems.Sort((a, b) => a.Id.CompareTo(b.Id));
                }

                return data.Max(e => e.Id).ToString();
            }


            return null;
        }

        protected override void UpdateStatus(TimeSpan waitTime)
        {
            this.m_twitterClient.TimelineUpdated(new StateUpdateData { WaitTimeHome = waitTime });
        }
    }
}
