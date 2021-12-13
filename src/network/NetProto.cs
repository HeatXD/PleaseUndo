using System;
using System.Collections.Generic;

namespace PleaseUndo
{
    public interface IPeerNetAdapter<InputType>
    {
        void Send(NetMsg msg);
        List<NetMsg> ReceiveAllMessages();
    }

    public class NetProto<InputType> : IPollSink
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
        const int SYNC_FIRST_RETRY_INTERVAL = 500;
        const int SYNC_RETRY_INTERVAL = 2000;
        const int RUNNING_RETRY_INTERVAL = 200;
        const int QUALITY_REPORT_INTERVAL = 1000;
        const int NETWORK_STATS_INTERVAL = 1000;
        const int KEEP_ALIVE_INTERVAL = 200;
        const int UDP_MSG_MAX_PLAYERS = 4;

        /*
         * Network transmission information
         */
        protected IPeerNetAdapter<InputType> net_adapter;
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
        protected bool disconnect_event_sent;
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
            poll.RegisterLoop(this);
            // init buffers and arrays
            this.peer_connect_status = new NetMsg.ConnectStatus[UDP_MSG_MAX_PLAYERS];
            this.send_queue = new RingBuffer<QueueEntry>(64);
            this.pending_output = new RingBuffer<GameInput<InputType>>(64);
            this.event_queue = new RingBuffer<Event>(64);
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
            LogMsg("send", msg);

            packets_sent++;
            last_send_time = (uint)Platform.GetCurrentTimeMS();
            bytes_sent += msg.PacketSize();

            msg.sequence_number = (ushort)next_send_seq++;

            var entry = new QueueEntry();
            entry.queue_time = Platform.GetCurrentTimeMS();
            entry.msg = msg;

            send_queue.Push(entry);
            PumpSendQueue();
        }

        protected void LogMsg(string prefix, NetMsg msg)
        {
            if (msg is NetSyncRequestMsg)
            {
                NetSyncRequestMsg tmp_msg = (NetSyncRequestMsg)msg;
                Logger.Log("{0} sync-request ({1}).\n", prefix, tmp_msg.random_request);
            }
            else if (msg is NetSyncReplyMsg)
            {
                NetSyncReplyMsg tmp_msg = (NetSyncReplyMsg)msg;
                Logger.Log("{0} sync-reply ({1}).\n", prefix, tmp_msg.random_reply);
            }
            else if (msg is NetQualityReportMsg)
            {
                Logger.Log("{0} quality report.\n", prefix);
            }
            else if (msg is NetQualityReply)
            {
                Logger.Log("{0} quality reply.\n", prefix);
            }
            else if (msg is NetKeepAlive)
            {
                Logger.Log("{0} keep alive.\n", prefix);
            }
            else if (msg is NetInputMsg)
            {
                NetInputMsg tmp_msg = (NetInputMsg)msg;
                Logger.Log("{0} game input {1} ({2} bits).\n", prefix, tmp_msg.start_frame, tmp_msg.num_bits);
            }
            else if (msg is NetInputAckMsg)
            {
                Logger.Log("{0} input ack.\n", prefix);
            }
            else
            {
                Logger.Assert(false, "Unknown NetMsg type.");
            }
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

        protected void SendPendingOutput()
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

        public bool GetPeerConnectStatus(int id, ref int frame)
        {
            frame = peer_connect_status[id].last_frame;
            return peer_connect_status[id].disconnected == 0;
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

        bool IPollSink.OnLoopPoll()
        {
            if (!IsInitialized())
            {
                return true;
            }

            uint now = (uint)Platform.GetCurrentTimeMS();
            uint next_interval;

            PumpSendQueue();

            switch (current_state)
            {
                case State.Syncing:
                    next_interval = (inner_state.Sync.roundtrips_remaining == NUM_SYNC_PACKETS) ? (uint)SYNC_FIRST_RETRY_INTERVAL : (uint)SYNC_RETRY_INTERVAL;
                    if (last_send_time != 0 && last_send_time + next_interval < now)
                    {
                        Logger.Log("No luck syncing after {0} ms... Re-queueing sync packet.\n", next_interval);
                        SendSyncRequest();
                    }
                    break;
                case State.Running:
                    // xxx: rig all this up with a timer wrapper
                    if (inner_state.Running.last_input_packet_recv_time == 0 || inner_state.Running.last_input_packet_recv_time + RUNNING_RETRY_INTERVAL < now)
                    {
                        Logger.Log("Haven't exchanged packets in a while (last received:{0}  last sent:{1}).  Resending.\n", last_received_input.frame, last_sent_input.frame);
                        SendPendingOutput();
                        inner_state.Running.last_input_packet_recv_time = now;
                    }

                    if (inner_state.Running.last_quality_report_time == 0 || inner_state.Running.last_quality_report_time + QUALITY_REPORT_INTERVAL < now)
                    {
                        NetQualityReportMsg msg = new NetQualityReportMsg();
                        msg.ping = (uint)Platform.GetCurrentTimeMS();
                        msg.frame_advantage = (byte)local_frame_advantage;
                        SendMsg(msg);
                        inner_state.Running.last_quality_report_time = now;
                    }

                    if (inner_state.Running.last_network_stats_interval == 0 || inner_state.Running.last_network_stats_interval + NETWORK_STATS_INTERVAL < now)
                    {
                        UpdateNetworkStats();
                        inner_state.Running.last_network_stats_interval = now;
                    }

                    if (last_send_time != 0 && last_send_time + KEEP_ALIVE_INTERVAL < now)
                    {
                        Logger.Log("Sending keep alive packet\n");
                        SendMsg(new NetKeepAlive());
                    }

                    if (disconnect_timeout != 0 && disconnect_notify_start != 0 &&
                        !disconnect_notify_sent && (last_recv_time + disconnect_notify_start < now))
                    {
                        Logger.Log("Endpoint has stopped receiving packets for {0} ms.  Sending notification.\n", disconnect_notify_start);
                        NetworkInterruptedEvent evt = new NetworkInterruptedEvent();
                        evt.disconnect_timeout = (int)(disconnect_timeout - disconnect_notify_start);
                        QueueEvent(evt);
                        disconnect_notify_sent = true;
                    }

                    if (disconnect_timeout != 0 && (last_recv_time + disconnect_timeout < now))
                    {
                        if (!disconnect_event_sent)
                        {
                            Logger.Log("Endpoint has stopped receiving packets for {0} ms.  Disconnecting.\n", disconnect_timeout);
                            Event evt = new Event();
                            evt.type = Event.Type.Disconnected;
                            QueueEvent(evt);
                            disconnect_event_sent = true;
                        }
                    }
                    break;
                case State.Disconnected:
                    if (shutdown_timeout < now)
                    {
                        Logger.Log("Shutting down NetProto connection.\n");
                        net_adapter = null;
                        shutdown_timeout = 0;
                    }
                    break;
            }
            return true;
        }

        protected void QueueEvent(Event e)
        {
            LogEvent("Queuing event", e);
            event_queue.Push(e);
        }

        protected void LogEvent(string prefix, Event e)
        {
            if (e.type == Event.Type.Synchronzied)
            {
                Logger.Log("{0} (event: Synchronzied).\n", prefix);
            }
        }

        protected void UpdateNetworkStats()
        {
            throw new NotImplementedException();
        }

        protected void PumpSendQueue()
        {
            while (!send_queue.Empty())
            {
                QueueEntry qe = send_queue.Front();
                // TODO
            }
        }
    }
}
