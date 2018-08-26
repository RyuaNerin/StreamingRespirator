namespace StreamingRespirator.Core
{
    internal enum ReqeustType
    {
        None,
        Account,
        Statuses,
        Activity,
        DirectMessage
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
