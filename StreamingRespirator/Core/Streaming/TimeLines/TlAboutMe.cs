using System;
using System.Collections.Generic;
using System.Linq;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal class TlAboutMe : BaseTimeLine<ActivityList, TwitterStatus>
    {
        public TlAboutMe(TwitterClient twitterClient)
            : base(twitterClient)
        {
        }

        /*
        model_version           | 7
        skip_aggregation        | true
        cards_platform          | Web-13
        include_entities        | 1
        include_user_entities   | 1
        include_cards           | 1
        send_error_codes        | 1
        tweet_mode              | extended
        include_ext_alt_text    | true
        include_reply_count     | true
        ----------------------------------------
        since_id                |     | cursor
        count                   |  1  | 200
        */
        private const string BaseUrl = "https://api.twitter.com/1.1/activity/about_me.json?model_version=7&skip_aggregation=true&cards_platform=Web-13&include_entities=1&include_user_entities=1&include_cards=1&send_error_codes=1&tweet_mode=extended&include_ext_alt_text=true&include_reply_count=true";

        private long m_cursor = 0;
        protected override void Clear()
        {
            this.m_cursor = 0;
        }

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.m_cursor == 0)
                return BaseUrl + "&count=1";
            else
                return BaseUrl + "&count=200&since_id=" + this.m_cursor;
        }

        protected override void ParseHtml(ActivityList data, List<TwitterStatus> lstItems, HashSet<TwitterUser> lstUsers)
        {
            if (data.Count == 0)
                return;

            var itemsList = new List<TwitterStatus>();
            var usersList = new HashSet<TwitterUser>();

            foreach (var activity in data)
            {
                foreach (var user in activity.Sources)
                    usersList.Add(user);

                foreach (var tweet in activity.Targets)
                    tweet.AddUserToHashSet(lstUsers);

                if ((Config.Instance.Filter.ShowRetweetedMyStatus && activity.Action == "retweet")
                    || (Config.Instance.Filter.ShowRetweetWithComment && activity.Action == "quote")
                    || activity.Action == "reply")
                {
                    var isRetweeted = activity.Action == "retweet";

                    foreach (var tweet in activity.Targets)
                    {
                        // Retweet 일 때 full_text 가 잘려서 도착하는 문제가 있다
                        // retweeted_status 를 기반으로 후처리한다
                        if (isRetweeted && tweet.RetweetedStatus != null)
                        {
                            var retweet = tweet.RetweetedStatus;

                            if (tweet.DisplayTextRange[1] < retweet.DisplayTextRange[0])
                            {
                                tweet.AdditionalData["entities"] = retweet.AdditionalData["entities"];
                                tweet.DisplayTextRange = retweet.DisplayTextRange;

                                tweet.AdditionalData["full_text"] = $"RT @{tweet.User.ScreenName}: {retweet.AdditionalData["full_text"]}";
                            }
                        }

                        lstItems.Add(tweet);
                    }
                }
            }

            itemsList.Sort((a, b) => a.Id.CompareTo(b.Id));

            var curCursor = this.m_cursor;
            this.m_cursor = data.Max(e => e.MaxPosition);
            if (curCursor == 0)
                itemsList.Clear();
        }

        protected override void UpdateStatus(TimeSpan waitTime)
        {
            this.m_twitterClient.TimelineUpdated(new StateUpdateData { WaitTimeAboutMe = waitTime });
        }
    }
}
