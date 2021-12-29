using System;
using System.Collections.Generic;

namespace PleaseUndo
{
    public abstract class IPeerNetAdapter
    {
        public abstract void Send(NetMsg msg);
        public abstract List<NetMsg> ReceiveAllMessages();
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
        public class ConnectedEvent : Event { }
        public class DisconnectedEvent : Event { }
        public class InputEvent : Event
        {
            public GameInput input;
        }
        public class SynchronizedEvent : Event { }
        public class SynchronizingEvent : Event
        {
            public int total;
            public int count;
        }
        public class NetworkInterruptedEvent : Event
        {
            public int disconnect_timeout;
        }
        public class NetworkResumedEvent : Event { }

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

        const int UDP_HEADER_SIZE = 28;
        const int UDP_SHUTDOWN_TIMER = 5000;
        const int NUM_SYNC_PACKETS = 5;
        const int SYNC_FIRST_RETRY_INTERVAL = 500;
        const int SYNC_RETRY_INTERVAL = 2000;
        const int RUNNING_RETRY_INTERVAL = 200;
        const int QUALITY_REPORT_INTERVAL = 1000;
        const int NETWORK_STATS_INTERVAL = 1000;
        const int KEEP_ALIVE_INTERVAL = 200;
        const int UDP_MSG_MAX_PLAYERS = 4;
        const int MAX_SEQ_DISTANCE = (1 << 15);

        /*
         * Network transmission information
         */
        protected IPeerNetAdapter net_adapter;
        protected int queue;
        protected bool connected;
        protected int send_latency;
        protected int oop_percent;
        protected RingBuffer<QueueEntry> send_queue;
        protected OO_Packet oo_packet;
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
        protected RingBuffer<GameInput> pending_output;
        protected GameInput last_received_input;
        protected GameInput last_sent_input;
        protected GameInput last_acked_input;
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
        protected TimeSync timesync;

        /*
         * Event queue
         */
        protected RingBuffer<Event> event_queue;

        public NetProto(int queue, ref IPeerNetAdapter peerNetAdapter, ref Poll poll)
        {
            this.queue = queue;
            this.net_adapter = peerNetAdapter;
            poll.RegisterLoop(this);
            // init buffers and arrays
            this.peer_connect_status = new NetMsg.ConnectStatus[UDP_MSG_MAX_PLAYERS];
            this.send_queue = new RingBuffer<QueueEntry>(64);
            this.pending_output = new RingBuffer<GameInput>(64);
            this.event_queue = new RingBuffer<Event>(64);
        }

        public void SendInput(ref GameInput input)
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

        public void SendPendingOutput()
        {
            var msg = new NetInputMsg { type = NetMsg.MsgType.Input };
            int i, j, offset = 0;
            byte[] bits;
            GameInput last;

            if (pending_output.Size() > 0)
            {
                last = last_acked_input;
                bits = msg.bits;
                msg.start_frame = (uint)pending_output.Front().frame;
                msg.input_size = (byte)pending_output.Front().size;

                Logger.Assert(last.frame == -1 || last.frame + 1 == msg.start_frame);
                for (j = 0; j < pending_output.Size(); j++)
                {
                    var current = pending_output.Item(j);
                    // if (memcmp(current.bits, last.bits, current.size) != 0)
                    // {
                    Logger.Assert((GameInput.GAMEINPUT_MAX_BYTES * GameInput.GAMEINPUT_MAX_PLAYERS * 8) < (1 << BitVector.NibbleSize));
                    for (i = 0; i < current.size * 8; i++)
                    {
                        Logger.Assert(i < (1 << BitVector.NibbleSize));
                        if (current.Value(i) != last.Value(i))
                        {
                            BitVector.SetBit(msg.bits, ref offset);
                            if (current.Value(i)) { BitVector.SetBit(bits, ref offset); } else { BitVector.ClearBit(bits, ref offset); }
                            BitVector.WriteNibblet(bits, i, ref offset);
                        }
                    }
                    // }
                    BitVector.ClearBit(msg.bits, ref offset);
                    last = last_sent_input = current;
                }
            }
            else
            {
                msg.start_frame = 0;
                msg.input_size = 0;
            }
            msg.ack_frame = last_received_input.frame;
            msg.num_bits = (System.UInt16)offset;

            msg.disconnect_requested = current_state == State.Disconnected ? 1 : 0;
            // TODO: find what's going on here
            if (true /* local_connect_status != null */)
            {
                // memcpy(msg->u.input.peer_connect_status, _local_connect_status, sizeof(UdpMsg::connect_status) * UDP_MSG_MAX_PLAYERS);
            }
            else
            {
                // memset(msg->u.input.peer_connect_status, 0, sizeof(UdpMsg::connect_status) * UDP_MSG_MAX_PLAYERS);
            }

            // Logger.Assert(offset < MAX_COMPRESSED_BITS);

            SendMsg(msg);
        }

        public void SendInputAck()
        {
            SendMsg(new NetInputAckMsg { type = NetMsg.MsgType.InputAck, ack_frame = last_received_input.frame });
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

        public bool OnLoopPoll()
        {
            if (!IsInitialized())
            {
                return true;
            }

            var messages = net_adapter.ReceiveAllMessages();
            foreach (var message in messages)
            {
                OnMsg(message);
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
                        var msg = new NetQualityReportMsg { type = NetMsg.MsgType.QualityReport };
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
                        SendMsg(new NetKeepAliveMsg { type = NetMsg.MsgType.KeepAlive });
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

        public void Disconnect()
        {
            current_state = State.Disconnected;
            shutdown_timeout = (uint)(Platform.GetCurrentTimeMS() + UDP_SHUTDOWN_TIMER);
        }

        public void SendSyncRequest()
        {
            inner_state.Sync.random = Platform.RandUint();
            NetSyncRequestMsg msg = new NetSyncRequestMsg { type = NetMsg.MsgType.SyncRequest };
            msg.random_request = inner_state.Sync.random;
            SendMsg(msg);
        }

        public void SendMsg(NetMsg msg)
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

        public void HandlesMsg()
        {
            // Unused
        }

        public void OnMsg(NetMsg msg)
        {
            var handled = false;
            var seq = msg.sequence_number;

            if (msg.type != NetMsg.MsgType.SyncRequest &&
                   msg.type != NetMsg.MsgType.SyncReply)
            {
                // if (msg.magic != _remote_magic_number)
                // {
                //     LogMsg("recv rejecting", msg);
                //     return;
                // }

                // filter out out-of-order packets
                var skipped = (System.UInt16)((int)seq - (int)next_recv_seq);
                // Log("checking sequence number -> next - seq : %d - %d = %d\n", seq, _next_recv_seq, skipped);
                if (skipped > MAX_SEQ_DISTANCE)
                {
                    Logger.Log("dropping out of order packet (seq: {0}, last seq:{1})\n", seq, next_recv_seq);
                    return;
                }
            }

            next_recv_seq = seq;
            LogMsg("recv", msg);

            switch (msg.type)
            {
                case NetMsg.MsgType.Invalid:
                    handled = OnInvalid(msg);
                    break;
                case NetMsg.MsgType.SyncRequest:
                    handled = OnSyncRequest(msg);
                    break;
                case NetMsg.MsgType.SyncReply:
                    handled = OnSyncReply(msg);
                    break;
                case NetMsg.MsgType.Input:
                    handled = OnInput(msg);
                    break;
                case NetMsg.MsgType.QualityReport:
                    handled = OnQualityReport(msg);
                    break;
                case NetMsg.MsgType.QualityReply:
                    handled = OnQualityReply(msg);
                    break;
                case NetMsg.MsgType.KeepAlive:
                    handled = OnKeepAlive(msg);
                    break;
                case NetMsg.MsgType.InputAck:
                    handled = OnInputAck(msg);
                    break;
                default:
                    handled = OnInvalid(msg);
                    break;
            }
            if (handled)
            {
                last_recv_time = (uint)Platform.GetCurrentTimeMS();
                if (disconnect_notify_sent && current_state == State.Running)
                {
                    QueueEvent(new NetworkResumedEvent { type = Event.Type.NetworkResumed });
                    disconnect_notify_sent = false;
                }
            }
        }

        public void UpdateNetworkStats()
        {
            int now = Platform.GetCurrentTimeMS();

            if (stats_start_time == 0)
            {
                stats_start_time = now;
            }

            int total_bytes_send = bytes_sent + (UDP_HEADER_SIZE * packets_sent);
            float seconds = (now - stats_start_time) / 1000;
            float Bps = total_bytes_send / seconds;
            float udp_overhead = (float)(100.0 * (UDP_HEADER_SIZE * packets_sent) / bytes_sent);

            kbps_sent = (int)(Bps / 1024);

            Logger.Log("(Just copied probably not correct) Network Stats --\n Bandwidth: {0} KBps Packets Sent: {1} ({2} pps)\n KB Sent: {3} UDP Overhead: {4} %%.\n",
                kbps_sent,
                packets_sent,
                (float)packets_sent * 1000 / (now - stats_start_time),
                total_bytes_send / 1024.0,
                udp_overhead);
        }

        public void QueueEvent(Event e)
        {
            LogEvent("Queuing event", e);
            event_queue.Push(e);
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

        public bool GetPeerConnectStatus(int id, ref int frame)
        {
            frame = peer_connect_status[id].last_frame;
            return peer_connect_status[id].disconnected == 0;
        }

        public void LogMsg(string prefix, NetMsg message)
        {
            switch (message.type)
            {
                case NetMsg.MsgType.Invalid:
                    Logger.Assert(false, prefix + " - Invalid NetMsg type.");
                    break;
                case NetMsg.MsgType.SyncRequest:
                    var net_sync_msg_req = (NetSyncRequestMsg)message;
                    Logger.Log("{0} sync-request ({1}).\n", prefix, net_sync_msg_req.random_request);
                    break;
                case NetMsg.MsgType.SyncReply:
                    var net_sync_msg_rep = (NetSyncReplyMsg)message;
                    Logger.Log("{0} sync-reply ({1}).\n", prefix, net_sync_msg_rep.random_reply);
                    break;
                case NetMsg.MsgType.Input:
                    var input_msg = (NetInputMsg)message;
                    Logger.Log("{0} game input {1} ({2} bits).\n", prefix, input_msg.start_frame, input_msg.num_bits);
                    break;
                case NetMsg.MsgType.InputAck:
                    Logger.Log("{0} input ack.\n", prefix);
                    break;
                case NetMsg.MsgType.QualityReport:
                    Logger.Log("{0} quality report.\n", prefix);
                    break;
                case NetMsg.MsgType.QualityReply:
                    Logger.Log("{0} quality reply.\n", prefix);
                    break;
                case NetMsg.MsgType.KeepAlive:
                    Logger.Log("{0} keep alive.\n", prefix);
                    break;
                default:
                    Logger.Assert(false, "Unknown NetMsg type.");
                    break;
            }
        }

        public bool OnInvalid(NetMsg msg)
        {
            Logger.Assert(false, string.Format("Invalid msg in NetProto: {0}", new { msg, type = msg.type }));
            return false;
        }

        public bool OnSyncRequest(NetMsg msg)
        {
            // if (_remote_magic_number != 0 && msg->hdr.magic != _remote_magic_number)
            // {
            //     Log("Ignoring sync request from unknown endpoint (%d != %d).\n",
            //          msg->hdr.magic, _remote_magic_number);
            //     return false;
            // }
            var reply = new NetSyncReplyMsg { type = NetMsg.MsgType.SyncReply };
            // reply.random_reply = msg.sync_request.random_request;
            SendMsg(reply);
            return true;
        }

        public bool OnSyncReply(NetMsg msg)
        {
            if (current_state != State.Syncing)
            {
                Logger.Log("Ignoring SyncReply while not synching.\n");
                //   return msg->hdr.magic == _remote_magic_number;
                return false;
            }

            // if (msg->u.sync_reply.random_reply != _state.sync.random)
            // {
            //     Log("sync reply %d != %d.  Keep looking...\n",
            //         msg->u.sync_reply.random_reply, _state.sync.random);
            //     return false;
            // }

            if (!connected)
            {
                QueueEvent(new ConnectedEvent { type = Event.Type.Connected });
                connected = true;
            }

            Logger.Log("Checking sync state ({0} round trips remaining).\n", inner_state.Sync.roundtrips_remaining);
            if (--inner_state.Sync.roundtrips_remaining == 0)
            {
                Logger.Log("Synchronized!\n");
                QueueEvent(new SynchronizedEvent { type = Event.Type.Synchronzied });
                current_state = State.Running;
                last_received_input.frame = -1;
                //     _remote_magic_number = msg->hdr.magic;
            }
            else
            {
                var evt = new SynchronizingEvent { total = NUM_SYNC_PACKETS, count = (int)(NUM_SYNC_PACKETS - inner_state.Sync.roundtrips_remaining) };
                QueueEvent(evt);
                SendSyncRequest();
            }
            return true;
        }

        public bool OnInput(NetMsg msg)
        {
            var inputMsg = msg as NetInputMsg;
            if (inputMsg.disconnect_requested != 0)
            {
                if (current_state != State.Disconnected && !disconnect_event_sent)
                {
                    QueueEvent(new DisconnectedEvent { type = Event.Type.Disconnected });
                    disconnect_event_sent = true;
                }
            }
            else
            {
                /*
                * Update the peer connection status if this peer is still considered to be part
                * of the network.
                */
                var remote_status = inputMsg.peer_connect_status;
                for (var i = 0; i < peer_connect_status.Length; i++)
                {
                    if (remote_status != null) // CHECK ADDED, NOT IN GGPO
                    {
                        Logger.Assert(remote_status[i].last_frame >= peer_connect_status[i].last_frame);
                        peer_connect_status[i].disconnected = (peer_connect_status[i].disconnected != 0 || remote_status[i].disconnected != 0) ? (uint)1 : (uint)0;
                        peer_connect_status[i].last_frame = System.Math.Max(peer_connect_status[i].last_frame, remote_status[i].last_frame);
                    }
                }
            }

            /*
            * Decompress the input.
            */
            var last_received_frame_number = last_received_input.frame;
            // if (inputMsg.num_bits != 0)
            {
                var offset = 0;
                var bits = inputMsg.bits;
                var numBits = inputMsg.num_bits;
                var currentFrame = inputMsg.start_frame;

                // _last_received_input.size = msg->u.input.input_size;
                if (last_received_input.frame < 0)
                {
                    last_received_input.frame = (int)(inputMsg.start_frame - 1);
                }
                while (offset < numBits)
                {
                    /*
                    * Keep walking through the frames (parsing bits) until we reach
                    * the inputs for the frame right after the one we're on.
                    */
                    Logger.Assert(currentFrame <= (last_received_input.frame + 1));
                    bool useInputs = currentFrame == last_received_input.frame + 1;

                    // while (BitVector_ReadBit(bits, &offset))
                    // {
                    //     int on = BitVector_ReadBit(bits, &offset);
                    //     int button = BitVector_ReadNibblet(bits, &offset);
                    //     if (useInputs)
                    //     {
                    //         if (on)
                    //         {
                    //             _last_received_input.set(button);
                    //         }
                    //         else
                    //         {
                    //             _last_received_input.clear(button);
                    //         }
                    //     }
                    // }
                    // ASSERT(offset <= numBits);

                    /*
                    * Now if we want to use these inputs, go ahead and send them to
                    * the emulator.
                    */
                    if (useInputs)
                    {
                        /*
                        * Move forward 1 frame in the stream.
                        */
                        char[] desc = new char[1024];
                        Logger.Assert(currentFrame == last_received_input.frame + 1);
                        last_received_input.frame = (int)currentFrame;

                        /*
                        * Send the event to the emualtor
                        */
                        var evt = new InputEvent { type = Event.Type.Input, input = last_received_input };

                        inner_state.Running.last_input_packet_recv_time = (uint)Platform.GetCurrentTimeMS();

                        QueueEvent(evt);
                    }
                    else
                    {
                        Logger.Log("Skipping past frame:({0}) current is {1}.\n", currentFrame, last_received_input.frame);
                    }

                    /*
                    * Move forward 1 frame in the input stream.
                    */
                    currentFrame++;
                }
            }
            Logger.Assert(last_received_input.frame >= last_received_frame_number);

            /*
            * Get rid of our buffered input
            */
            while (pending_output.Size() > 0 && pending_output.Front().frame < inputMsg.ack_frame)
            {
                Logger.Log("Throwing away pending output frame {0}\n", pending_output.Front().frame);
                last_acked_input = pending_output.Front();
                pending_output.Pop();
            }
            return true;
        }

        public bool OnInputAck(NetMsg msg)
        {
            /*
             * Get rid of our buffered input
             */
            while (pending_output.Size() > 0 && pending_output.Front().frame < (msg as NetInputMsg).ack_frame)
            {
                Logger.Log("Throwing away pending output frame {0}\n", pending_output.Front().frame);
                last_acked_input = pending_output.Front();
                pending_output.Pop();
            }
            return true;
        }

        public bool OnQualityReport(NetMsg msg)
        {
            var report = (msg as NetQualityReportMsg);
            var reply = new NetQualityReplyMsg { type = NetMsg.MsgType.QualityReply, pong = report.ping };
            SendMsg(reply);

            remote_frame_advantage = report.frame_advantage;
            return true;
        }

        public bool OnQualityReply(NetMsg msg)
        {
            round_trip_time = (int)(Platform.GetCurrentTimeMS() - (msg as NetQualityReplyMsg).pong);
            return true;
        }

        public bool OnKeepAlive(NetMsg msg)
        {
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

        public void SetDisconnectTimeout(int timeout)
        {
            disconnect_timeout = (uint)timeout;
        }

        public void SetDisconnectNotifyStart(int timeout)
        {
            disconnect_notify_start = (uint)timeout;
        }

        public void LogEvent(string prefix, Event e)
        {
            if (e.type == Event.Type.Synchronzied)
            {
                Logger.Log("{0} (event: Synchronzied).\n", prefix);
            }
        }

        public void PumpSendQueue()
        {
            var rand = new Random();

            while (!send_queue.Empty())
            {
                QueueEntry entry = send_queue.Front();

                if (send_latency != 0)
                {
                    int jitter = (send_latency * 2 / 3) + ((rand.Next() % send_latency) / 3);
                    if (Platform.GetCurrentTimeMS() < send_queue.Front().queue_time + jitter)
                    {
                        break;
                    }
                }

                if (oop_percent != 0 && oo_packet.msg != null && ((rand.Next() % 100) < oop_percent))
                {
                    int delay = rand.Next() % (send_latency * 10 + 1000);
                    Logger.Log("creating rogue oop (seq: {0}  delay: {1})\n", entry.msg.sequence_number, delay);
                    oo_packet.send_time = Platform.GetCurrentTimeMS() + delay;
                    oo_packet.msg = entry.msg;
                }
                else
                {
                    net_adapter.Send(entry.msg);
                    entry.msg = null;
                }

                send_queue.Pop();
            }

            if (oo_packet.msg != null && oo_packet.send_time < Platform.GetCurrentTimeMS())
            {
                Logger.Log("sending rogue oop!");
                net_adapter.Send(oo_packet.msg);

                oo_packet.msg = null;
            }

        }

        public void ClearSendQueue()
        {
            // Unused
        }

        public bool IsInitialized() => net_adapter != null;
        public bool IsRunning() => current_state == State.Running;
        public bool IsSynchronized() => current_state == State.Running;
    }
}
