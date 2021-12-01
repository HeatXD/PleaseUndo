namespace PleaseUndo
{
    public class NetMsg
    {
        public struct ConnectStatus
        {
            public int last_frame;
            public uint disconnected;
        }

        public ushort sequence_number;
    }

    public class NetSyncRequestMsg : NetMsg
    {
        public uint random_request;  /* please reply back with this random data */
        public ushort remote_magic;
        public byte remote_endpoint;
    }

    public class NetSyncReplyMsg : NetMsg
    {
        public uint random_reply;
    }

    public class NetQualityReportMsg : NetMsg
    {
        public byte frame_advantage; /* what's the other guy's frame advantage? */
        public uint ping;
    }

    public class NetQualityReply : NetMsg
    {
        public uint pong;
    }

    public class NetInputMsg : NetMsg
    {
        public ConnectStatus[] peer_connect_status; // fixed size of UDP_MSG_MAX_PLAYERS

        public uint start_frame;

        public int disconnect_requested;
        public int ack_frame;

        public ushort num_bits;
        public byte input_size; // XXX: shouldn't be in every single packet!
        public byte[] bits; /* must be last */ // fixed size of MAX_COMPRESSED_BITS
    }

    public class NetInputAckMsg : NetMsg
    {
        public int ack_frame;
    }
}
