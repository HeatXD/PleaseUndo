public struct GGPONetworkStats
{
    public struct Network
    {
        public int send_queue_len;
        public int recv_queue_len;
        public int ping;
        public int kbps_sent;
    }
    public struct Timesync
    {
        public int local_frames_behind;
        public int remote_frames_behind;
    }

    Network Network;
    Timesync Timesync;
}
