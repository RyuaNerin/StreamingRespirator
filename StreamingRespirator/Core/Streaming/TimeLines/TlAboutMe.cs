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

        protected override string Method => "GET";
        protected override string GetUrl()
        {
            if (this.Cursor == null)
                return $"{BaseUrl}&count=1"; 
            else
                return $"{BaseUrl}&count=200&since_id={this.Cursor}";
        }

        protected override string ParseHtml(ActivityList data, List<TwitterStatus> lstItems, HashSet<TwitterUser> lstUsers, bool isNotFirstRefresh)
        {
            if (data.Count > 0)
            {
                if (isNotFirstRefresh)
                {
                    foreach (var activity in data)
                    {
                        foreach (var user in activity.Sources)
                            lstUsers.Add(user);

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

                    lstItems.Sort((a, b) => a.Id.CompareTo(b.Id));
                }

                return data.Max(e => e.MaxPosition).ToString();
            }

            return null;
        }

        protected override void UpdateStatus(TimeSpan waitTime)
        {
            this.m_twitterClient.TimelineUpdated(new StateUpdateData { WaitTimeAboutMe = waitTime });
        }
    }
}
