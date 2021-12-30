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
            return BitConverter.ToUInt32(rndBytes, 0);
        }

        public static int memcmp(byte[] a, byte[] b, uint count)
        {
            if (a.Length < b.Length) { return -1; }
            if (a.Length > b.Length) { return 1; }
            for (var i = 0; i < System.Math.Min(count, a.Length); i++)
            {
                if (a[i] < b[i]) { return -1; }
                if (a[i] > b[i]) { return 1; }
            }
            return 0;
        }
    }
}
