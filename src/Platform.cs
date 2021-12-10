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

        public static uint RandUint()
        {
            Random rnd = new Random();
            byte[] rndBytes = new byte[4];
            rnd.NextBytes(rndBytes);
            return BitConverter.ToUInt32(rndBytes);
        }
    }
}