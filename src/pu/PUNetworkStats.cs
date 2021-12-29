namespace PleaseUndo
{
    public struct PUNetworkStats
    {
        public struct _Network
        {
            public int send_queue_len;
            public int recv_queue_len;
            public int ping;
            public int kbps_sent;
        }
        public struct _Timesync
        {
            public int local_frames_behind;
            public int remote_frames_behind;
        }

        public _Network Network;
        public _Timesync Timesync;
    }
}
