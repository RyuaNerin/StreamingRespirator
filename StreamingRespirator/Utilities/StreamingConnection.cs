namespace StreamingRespirator.Utilities
{
    internal class StreamingConnection
    {
        public StreamingConnection(WaitableStream item, long ownerId, string description)
        {
            this.Stream      = item;
            this.OwnerId     = ownerId;
            this.Description = description;
        }

        public WaitableStream Stream      { get; }
        public string         Description { get; }
        public long           OwnerId     { get; }
    }
}
