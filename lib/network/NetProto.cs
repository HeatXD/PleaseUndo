using System.Collections.Generic;

namespace PleaseUndo
{
    public interface IPeerNetAdapter
    {
        void SendTo(NetMsg msg);
        List<NetMsg> ReceiveAllMessages();
    }

    public class NetProto : IPollSink
    {
        public class Event { }
        public class InputEvent : Event
        {
            GameInput<object> input;
        }
        public class SynchronizingEvent : Event
        {
            int total;
            int count;
        }
        public class NetworkInterruptedEvent : Event
        {
            int disconnect_timeout;
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
