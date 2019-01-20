using System;

namespace Agile.API.Client.Helpers
{
    public static class ServerTime
    {
        private static int lastUnixTimeStamp;

        public static long UnixTimeStampUtc()
        {
            var currentTime = DateTime.Now;
            var dt = currentTime.ToUniversalTime();
            var unixEpoch = new DateTime(1970, 1, 1);
            var unixTimeStamp = (Int32) (dt.Subtract(unixEpoch)).TotalSeconds;
            // it's possible the nonce gets created >1 per second, but it MUST be greater each time, just add one
            if (unixTimeStamp <= lastUnixTimeStamp)
            {
                unixTimeStamp = lastUnixTimeStamp + 1;
            }
            lastUnixTimeStamp = unixTimeStamp;
            return unixTimeStamp;
        }

        public static double GetTimeStamp(DateTime dt)
        {
            var unixEpoch = new DateTime(1970, 1, 1);
            return dt.Subtract(unixEpoch).TotalSeconds;
        }

    }
}
