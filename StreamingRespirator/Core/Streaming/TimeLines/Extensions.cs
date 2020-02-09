using System.Collections.Generic;
using StreamingRespirator.Core.Streaming.Twitter;

namespace StreamingRespirator.Core.Streaming.TimeLines
{
    internal static class Extensions
    {
        public static void AddUserToHashSet(this TwitterStatus status, ICollection<TwitterUser> lstUsers)
        {
            if (status.User != null)
                lstUsers.Add(status.User);

            status.RetweetedStatus?.AddUserToHashSet(lstUsers);
            status.QuotedStatus?.AddUserToHashSet(lstUsers);
        }
    }
}
