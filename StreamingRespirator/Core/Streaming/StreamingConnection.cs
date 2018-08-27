using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core.Streaming
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

        public long LastStatus        { get; set; }
        public long LastActivity      { get; set; }
        public long LastDirectMessage { get; set; }
    }
}
