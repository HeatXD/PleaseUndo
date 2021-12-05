using MessagePack;

namespace PleaseUndo
{
    [Union(0, typeof(NetInputMsg))]
    [Union(1, typeof(NetInputAckMsg))]
    [Union(2, typeof(NetQualityReply))]
    [Union(3, typeof(NetQualityReportMsg))]
    [Union(4, typeof(NetSyncReplyMsg))]
    [Union(5, typeof(NetSyncRequestMsg))]
    [MessagePackObject]
    public abstract class NetMsg
    {
        public struct ConnectStatus
        {
            public int last_frame;
            public uint disconnected;
        }

        [Key(0)]
        public ushort sequence_number;
    }

    [MessagePackObject]
    public class NetInputMsg : NetMsg
    {
        [Key(0)]
        public ConnectStatus[] peer_connect_status; // fixed size of UDP_MSG_MAX_PLAYERS

        [Key(1)]
        public uint start_frame;

        [Key(2)]
        public int disconnect_requested;
        [Key(3)]
        public int ack_frame;

        [Key(4)]
        public ushort num_bits;
        [Key(5)]
        public byte input_size; // XXX: shouldn't be in every single packet!
        [Key(6)]
        public byte[] bits; /* must be last */ // fixed size of MAX_COMPRESSED_BITS
    }

    [MessagePackObject]
    public class NetInputAckMsg : NetMsg
    {
        [Key(0)]
        public int ack_frame;
    }

    [MessagePackObject]
    public class NetQualityReply : NetMsg
    {
        [Key(0)]
        public uint pong;
    }

    [MessagePackObject]
    public class NetQualityReportMsg : NetMsg
    {
        [Key(0)]
        public byte frame_advantage; /* what's the other guy's frame advantage? */
        [Key(1)]
        public uint ping;
    }

    [MessagePackObject]
    public class NetSyncReplyMsg : NetMsg
    {
        [Key(0)]
        public uint random_reply;
    }

    [MessagePackObject]
    public class NetSyncRequestMsg : NetMsg
    {
        [Key(0)]
        public uint random_request;  /* please reply back with this random data */
        [Key(1)]
        public ushort remote_magic;
        [Key(2)]
        public byte remote_endpoint;
    }
}
