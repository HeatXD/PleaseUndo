using MessagePack;

namespace PleaseUndo
{
    [Union(0, typeof(NetInputMsg))]
    [Union(1, typeof(NetInputAckMsg))]
    [Union(2, typeof(NetQualityReply))]
    [Union(3, typeof(NetQualityReportMsg))]
    [Union(4, typeof(NetSyncReplyMsg))]
    [Union(5, typeof(NetSyncRequestMsg))]
    [Union(6, typeof(NetKeepAlive))]
    [MessagePackObject]
    public abstract class NetMsg
    {
        [MessagePackObject]
        public struct ConnectStatus
        {
            [Key(0)]
            public int last_frame;
            [Key(1)]
            public uint disconnected;
        }

        [Key(0)]
        public ushort sequence_number;

        public static byte[] Serialize<T>(T message) where T : NetMsg { return MessagePackSerializer.Serialize<T>(message); }
        public static T Deserialize<T>(byte[] data) where T : NetMsg { return MessagePackSerializer.Deserialize<T>(data); }
    }

    [MessagePackObject]
    public class NetInputMsg : NetMsg
    {
        [Key(1)]
        public ConnectStatus[] peer_connect_status; // fixed size of UDP_MSG_MAX_PLAYERS

        [Key(2)]
        public uint start_frame;

        [Key(3)]
        public int disconnect_requested;
        [Key(4)]
        public int ack_frame;

        [Key(5)]
        public ushort num_bits;
        [Key(6)]
        public byte input_size; // XXX: shouldn't be in every single packet!
        [Key(7)]
        public byte[] bits; /* must be last */ // fixed size of MAX_COMPRESSED_BITS
    }

    [MessagePackObject]
    public class NetInputAckMsg : NetMsg
    {
        [Key(1)]
        public int ack_frame;
    }

    [MessagePackObject]
    public class NetQualityReply : NetMsg
    {
        [Key(1)]
        public uint pong;
    }

    [MessagePackObject]
    public class NetQualityReportMsg : NetMsg
    {
        [Key(1)]
        public byte frame_advantage; /* what's the other guy's frame advantage? */
        [Key(2)]
        public uint ping;
    }

    [MessagePackObject]
    public class NetSyncReplyMsg : NetMsg
    {
        [Key(1)]
        public uint random_reply;
    }

    [MessagePackObject]
    public class NetSyncRequestMsg : NetMsg
    {
        [Key(1)]
        public uint random_request;  /* please reply back with this random data */
        [Key(2)]
        public ushort remote_magic;
        [Key(3)]
        public byte remote_endpoint;
    }

    [MessagePackObject]
    public class NetKeepAlive : NetMsg
    {

    }
}
