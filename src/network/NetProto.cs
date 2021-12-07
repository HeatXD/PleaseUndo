using System.Collections.Generic;

namespace PleaseUndo
{
    public interface IPeerNetAdapter<InputType>
    {
        void Send(NetMsg msg);
        List<NetMsg> ReceiveAllMessages();
    }

    public class NetProto<InputType>
    {
        public class Event { }
        public class InputEvent : Event
        {
            public GameInput<InputType> input;
        }
        public class SynchronizingEvent : Event
        {
            public int total;
            public int count;
        }
        public class NetworkInterruptedEvent : Event
        {
            public int disconnect_timeout;
        }

        public enum State
        {
            Syncing,
            Synchronzied,
            Running,
            Disconnected
        };

        public struct Stats
        {
            public int ping;
            public int remote_frame_advantage;
            public int local_frame_advantage;
            public int send_queue_len;
            // Udp::Stats          udp;
        };

        public struct OO_Packet
        {
            public int send_time;
            //   sockaddr_in dest_addr;
            public NetMsg msg;
        }

        public struct InnerState
        {
            public struct SyncState
            {
                public uint roundtrips_remaining;
                public uint random;
            };
            public struct RunningState
            {
                public uint last_quality_report_time;
                public uint last_network_stats_interval;
                public uint last_input_packet_recv_time;
            }

            public SyncState Sync;
            public RunningState Running;
        }

        public struct QueueEntry
        {
            public int queue_time;
            // sockaddr_in dest_addr;
            public NetMsg msg;
        };
    }
}
