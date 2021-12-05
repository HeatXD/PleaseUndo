using System;

namespace PleaseUndo
{
    public class Platform
    {
        public static int GetCurrentTimeMS()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return (int)now.ToUnixTimeMilliseconds();
        }
    }
}