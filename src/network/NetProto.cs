using System;
using System.Collections.Generic;

namespace PleaseUndo
{
    public interface IPeerNetAdapter<InputType> : IPollSink
    {
        void Send(NetMsg msg);
        List<NetMsg> ReceiveAllMessages();
    }

    public class NetProto<InputType>
    {
        public class Event
        {
            public enum Type
            {
                Unknown = -1,
                Connected,
                Synchronizing,
                Synchronzied,
                Input,
                Disconnected,
                NetworkInterrupted,
                NetworkResumed,
            };

            public Type type;
        }

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
        /*
         * Network transmission information
         */
        protected IPeerNetAdapter<InputType> net_adapter;
        protected string peer_addr; //? idk if needed
        protected int queue;
        protected bool connected;
        protected int send_latency;
        protected int oop_percent;
        protected RingBuffer<QueueEntry> send_queue;
        /*
         * Stats
         */
        protected int round_trip_time;
        protected int packets_sent;
        protected int bytes_sent;
        protected int kbps_sent;
        protected int stats_start_time;
        /*
        * The state machine
        */
        protected NetMsg.ConnectStatus local_connect_status;
        protected NetMsg.ConnectStatus[] peer_connect_status;
        protected State current_state;
        protected InnerState inner_state; // _state
        /*
         * Fairness.
         */
        protected int local_frame_advantage;
        protected int remote_frame_advantage;
        /*
         * Packet loss...
         */
        protected RingBuffer<GameInput<InputType>> pending_output;
        protected GameInput<InputType> last_received_input;
        protected GameInput<InputType> last_sent_input;
        protected GameInput<InputType> last_acked_input;
        protected uint last_send_time;
        protected uint last_recv_time;
        protected uint shutdown_timeout;
        protected uint disconnect_event_sent;
        protected uint disconnect_timeout;
        protected uint disconnect_notify_start;
        protected bool disconnect_notify_sent;
        protected uint next_send_seq;
        protected uint next_recv_seq;
        /*
 *       Rift synchronization.
        */
        protected TimeSync<InputType> timesync;

        /*
         * Event queue
         */
        protected RingBuffer<Event> event_queue;

        public NetProto(int queue, ref IPeerNetAdapter<InputType> peerNetAdapter, ref Poll poll)
        {
            this.queue = queue;
            this.net_adapter = peerNetAdapter;
            poll.RegisterLoop((IPollSink)this); // had to remove ref
        }

        public void Synchronize()
        {

        }

        public bool IsInitialized() => net_adapter != null;
        public bool IsRunning() => current_state == State.Running;
        public bool IsSynchronized() => current_state == State.Running;

        public void SendInput(GameInput<InputType> input)
        {
            throw new NotImplementedException();
        }

        public void SetDisconnectTimeout(int disconnect_timeout)
        {
            throw new NotImplementedException();
        }

        public void SetDisconnectNotifyStart(int disconnect_notify_start)
        {
            throw new NotImplementedException();
        }

        public void SetLocalFrameNumber(int current_frame)
        {
            throw new NotImplementedException();
        }

        public byte RecommendFrameDelay()
        {
            throw new NotImplementedException();
        }

        public bool GetPeerConnectStatus(int i, ref int v)
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public bool GetEvent(ref NetProto<InputType>.Event evt)
        {
            throw new NotImplementedException();
        }

        public void GetNetworkStats(ref GGPONetworkStats stats)
        {
            throw new NotImplementedException();
        }
    }
}
