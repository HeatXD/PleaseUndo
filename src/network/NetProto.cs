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

        const int UDP_SHUTDOWN_TIMER = 5000;
        const int NUM_SYNC_PACKETS = 5;

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
         * Rift synchronization.
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
            if (IsInitialized())
            {
                current_state = State.Syncing;
                inner_state.Sync.roundtrips_remaining = NUM_SYNC_PACKETS;
                SendSyncRequest();
            }
        }

        protected void SendSyncRequest()
        {
            inner_state.Sync.random = Platform.RandUint();
            NetSyncRequestMsg msg = new NetSyncRequestMsg();
            msg.random_request = inner_state.Sync.random;
            SendMsg(msg);
        }

        protected void SendMsg(NetMsg msg)
        {
            throw new NotImplementedException();
        }

        public bool IsInitialized() => net_adapter != null;
        public bool IsRunning() => current_state == State.Running;
        public bool IsSynchronized() => current_state == State.Running;

        public void SendInput(ref GameInput<InputType> input)
        {
            //    if (_udp) {
            if (current_state == State.Running)
            {
                /*
                 * Check to see if this is a good time to adjust for the rift...
                 */
                timesync.advance_frame(input, local_frame_advantage, remote_frame_advantage);

                /*
                 * Save this input packet
                 *
                 * XXX: This queue may fill up for spectators who do not ack input packets in a timely
                 * manner.  When this happens, we can either resize the queue (ug) or disconnect them
                 * (better, but still ug).  For the meantime, make this queue really big to decrease
                 * the odds of this happening...
                 */
                pending_output.Push(input);
                //   }
                SendPendingOutput();
            }
        }

        private void SendPendingOutput()
        {
            throw new NotImplementedException();
        }

        public void SetDisconnectTimeout(int timeout)
        {
            disconnect_timeout = (uint)timeout;
        }

        public void SetDisconnectNotifyStart(int timeout)
        {
            disconnect_notify_start = (uint)timeout;
        }

        public void SetLocalFrameNumber(int localFrame)
        {
            /*
             * Estimate which frame the other guy is one by looking at the
             * last frame they gave us plus some delta for the one-way packet
             * trip time.
             */
            int remoteFrame = last_received_input.frame + (round_trip_time * 60 / 1000);

            /*
             * Our frame advantage is how many frames *behind* the other guy
             * we are.  Counter-intuative, I know.  It's an advantage because
             * it means they'll have to predict more often and our moves will
             * pop more frequenetly.
             */
            local_frame_advantage = remoteFrame - localFrame;
        }

        public int RecommendFrameDelay()
        {
            // XXX: require idle input should be a configuration parameter
            return timesync.recommend_frame_wait_duration(false);
        }

        public bool GetPeerConnectStatus(int i, ref int v)
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            current_state = State.Disconnected;
            shutdown_timeout = (uint)(Platform.GetCurrentTimeMS() + UDP_SHUTDOWN_TIMER);
        }

        public bool GetEvent(ref NetProto<InputType>.Event e)
        {
            if (event_queue.Size() == 0)
            {
                return false;
            }
            e = event_queue.Front();
            event_queue.Pop();
            return true;
        }

        public void GetNetworkStats(ref GGPONetworkStats stats)
        {
            stats.Network.ping = round_trip_time;
            stats.Network.send_queue_len = pending_output.Size();
            stats.Network.kbps_sent = kbps_sent;
            stats.Timesync.remote_frames_behind = remote_frame_advantage;
            stats.Timesync.local_frames_behind = local_frame_advantage;
        }
    }
}
