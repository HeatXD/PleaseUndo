using MessagePack;

namespace PleaseUndo
{
    [Union(0, typeof(NetInputMsg))]
    [Union(1, typeof(NetInputAckMsg))]
    [Union(2, typeof(NetQualityReplyMsg))]
    [Union(3, typeof(NetQualityReportMsg))]
    [Union(4, typeof(NetSyncReplyMsg))]
    [Union(5, typeof(NetSyncRequestMsg))]
    [Union(6, typeof(NetKeepAliveMsg))]
    [MessagePackObject]
    public abstract class NetMsg
    {
        public enum MsgType
        {
            Invalid = 0,
            SyncRequest = 1,
            SyncReply = 2,
            Input = 3,
            QualityReport = 4,
            QualityReply = 5,
            KeepAlive = 6,
            InputAck = 7,
        };

        [MessagePackObject]
        public struct ConnectStatus
        {
            [Key(0)]
            public int last_frame;
            [Key(1)]
            public uint disconnected;
        }

        [Key(0)]
        public MsgType type;
        [Key(1)]
        public ushort sequence_number;

        public static byte[] Serialize<T>(T message) where T : NetMsg { return MessagePackSerializer.Serialize<T>(message); }
        public static T Deserialize<T>(byte[] data) where T : NetMsg { return MessagePackSerializer.Deserialize<T>(data); }

        public int PacketSize()
        {
            //return Marshal.SizeOf(this);
            return Serialize(this).Length;
        }
    }

    [MessagePackObject]
    public class NetInputMsg : NetMsg
    {
        [Key(2)]
        public ConnectStatus[] peer_connect_status; // fixed size of UDP_MSG_MAX_PLAYERS

        [Key(3)]
        public uint start_frame;

        [Key(4)]
        public int disconnect_requested;
        [Key(5)]
        public int ack_frame;

        [Key(6)]
        public ushort num_bits;
        [Key(7)]
        public byte input_size; // XXX: shouldn't be in every single packet!
        [Key(8)]
        public byte[] bits; /* must be last */ // fixed size of MAX_COMPRESSED_BITS
    }

    [MessagePackObject]
    public class NetInputAckMsg : NetMsg
    {
        [Key(2)]
        public int ack_frame;
    }

    [MessagePackObject]
    public class NetQualityReplyMsg : NetMsg
    {
        [Key(2)]
        public uint pong;
    }

    [MessagePackObject]
    public class NetQualityReportMsg : NetMsg
    {
        [Key(2)]
        public byte frame_advantage; /* what's the other guy's frame advantage? */
        [Key(3)]
        public uint ping;
    }

    [MessagePackObject]
    public class NetSyncReplyMsg : NetMsg
    {
        [Key(2)]
        public uint random_reply;
    }

    [MessagePackObject]
    public class NetSyncRequestMsg : NetMsg
    {
        [Key(2)]
        public uint random_request;  /* please reply back with this random data */
        [Key(3)]
        public ushort remote_magic;
        [Key(4)]
        public byte remote_endpoint;
    }

    [MessagePackObject]
    public class NetKeepAliveMsg : NetMsg { }
}
