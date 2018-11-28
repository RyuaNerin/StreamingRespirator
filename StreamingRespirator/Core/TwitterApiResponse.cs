namespace StreamingRespirator.Core
{
    internal enum ReqeustType
    {
        None,
        account__verify_credentials,
        statuses__home_timeline,
        activity__about_me,
        dm__user_updates,
        help__settings,
        users__contributees,
        tweetdeck__clients__blackbird__all,
    }

    internal class TwitterApiResponse
    {
        public TwitterApiResponse(long ownerId, ReqeustType requestType, string jsonData)
        {
            this.OwnerId      = ownerId;
            this.RequestType  = requestType;
            this.ResponseBody = jsonData;
        }

        public long        OwnerId      { get; }
        public ReqeustType RequestType  { get; }
        public string      ResponseBody { get; }
    }
}
