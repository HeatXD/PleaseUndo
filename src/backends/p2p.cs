using System;

namespace PleaseUndo
{
    public class Peer2PeerBackend<InputType> : GGPOSession<InputType>
    {
        const int UDP_MSG_MAX_PLAYERS = 4;
        const int RECOMMENDATION_INTERVAL = 240;
        const int DEFAULT_DISCONNECT_TIMEOUT = 5000;
        const int DEFAULT_DISCONNECT_NOTIFY_START = 750;
        const int MAX_SPECTATORS = 16;

        protected GGPOSessionCallbacks _callbacks;
        protected Poll _poll;
        protected Sync<InputType> _sync;
        //   protected  Udp                   _udp; //Still dont know what to do with this... :HEAT /TODO
        protected NetProto<InputType>[] _endpoints;
        protected NetProto<InputType>[] _spectators;
        protected int _num_spectators;
        protected int _input_size;

        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_recommended_sleep;

        protected int _next_spectator_frame;
        protected int _disconnect_timeout;
        protected int _disconnect_notify_start;

        NetMsg.ConnectStatus[] _local_connect_status;

        public Peer2PeerBackend(ref GGPOSessionCallbacks cb, int num_players)
        {
            _num_players = num_players;
            _sync = new Sync<InputType>(ref _local_connect_status);
            _disconnect_timeout = DEFAULT_DISCONNECT_TIMEOUT;
            _disconnect_notify_start = DEFAULT_DISCONNECT_NOTIFY_START;
            _num_spectators = 0;
            _next_spectator_frame = 0;
            _poll = new Poll();
            _callbacks = cb;
            _synchronizing = true;
            _next_recommended_sleep = 0;

            /*
             * Initialize the synchronziation layer
             */
            _sync.Init(new Sync<InputType>.Config
            {
                num_players = _num_players,
                callbacks = _callbacks,
                num_prediction_frames = Sync<InputType>.MAX_PREDICTION_FRAMES,
            });

            /*
             * Initialize the UDP port
             */
            //    _udp.Init(localport, &_poll, this);
            _endpoints = new NetProto<InputType>[_num_players];
            _spectators = new NetProto<InputType>[MAX_SPECTATORS];
            _local_connect_status = new NetMsg.ConnectStatus[UDP_MSG_MAX_PLAYERS];
            //    memset(_local_connect_status, 0, sizeof(_local_connect_status));
            for (int i = 0; i < _local_connect_status.Length; i++)
            {
                _local_connect_status[i].last_frame = -1;
            }

            /*
             * Preload the ROM
             */
            _callbacks.OnBeginGame();
        }

        public override GGPOErrorCode AddLocalPlayer(GGPOPlayer player, ref GGPOPlayerHandle handle)
        {
            // if (player.type == GGPOPlayerType.SPECTATOR) // Should be AddSpectatorPlayer
            // {
            //     return AddSpectator(player->u.remote.ip_address, player->u.remote.port);
            // }

            int queue = player.player_num - 1;
            if (player.player_num < 1 || player.player_num > _num_players)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_PLAYER_OUT_OF_RANGE;
            }
            handle = QueueToPlayerHandle(queue);

            // if (player.type == GGPOPlayerType.REMOTE) // Is now AddRemotePlayer
            // {
            //     AddRemotePlayer(player->u.remote.ip_address, player->u.remote.port, queue);
            // }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode AddRemotePlayer(GGPOPlayer player, ref GGPOPlayerHandle handle, IPeerNetAdapter peerNetAdapter)
        {
            _synchronizing = true;

            var queue = player.player_num - 1;
            handle = QueueToPlayerHandle(queue);
            _endpoints[queue] = new NetProto<InputType>(queue, ref peerNetAdapter, ref _poll);
            _endpoints[queue].SetDisconnectTimeout(_disconnect_timeout);
            _endpoints[queue].SetDisconnectNotifyStart(_disconnect_notify_start);
            _endpoints[queue].Synchronize();

            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode DoPoll(int timeout)
        {
            if (!_sync.InRollback())
            {
                _poll.Pump(0);

                PollUdpProtocolEvents();

                if (!_synchronizing)
                {
                    _sync.CheckSimulation(timeout);

                    // notify all of our endpoints of their local frame number for their
                    // next connection quality report
                    int current_frame = _sync.GetFrameCount();
                    for (int i = 0; i < _num_players; i++)
                    {
                        _endpoints[i].SetLocalFrameNumber(current_frame);
                    }

                    int total_min_confirmed;
                    if (_num_players <= 2)
                    {
                        total_min_confirmed = Poll2Players(current_frame);
                    }
                    else
                    {
                        total_min_confirmed = PollNPlayers(current_frame);
                    }

                    Logger.Log("last confirmed frame in p2p backend is {0}.", total_min_confirmed);
                    if (total_min_confirmed >= 0)
                    {
                        Logger.Assert(total_min_confirmed != int.MaxValue);
                        if (_num_spectators > 0)
                        {
                            while (_next_spectator_frame <= total_min_confirmed)
                            {
                                Logger.Log("pushing frame %d to spectators.", _next_spectator_frame);

                                var input = new GameInput<InputType>();
                                input.frame = _next_spectator_frame;
                                // input.size = _input_size * _num_players; // Not needed in C#
                                _sync.GetConfirmedInputs(input.inputs, _input_size * _num_players, _next_spectator_frame);
                                for (int i = 0; i < _num_spectators; i++)
                                {
                                    _spectators[i].SendInput(ref input);
                                }
                                _next_spectator_frame++;
                            }
                        }
                        Logger.Log("setting confirmed frame in sync to {0}.", total_min_confirmed);
                        _sync.SetLastConfirmedFrame(total_min_confirmed);
                    }

                    // send timesync notifications if now is the proper time
                    if (current_frame > _next_recommended_sleep)
                    {
                        int interval = 0;
                        for (int i = 0; i < _num_players; i++)
                        {
                            interval = System.Math.Max(interval, _endpoints[i].RecommendFrameDelay());
                        }

                        if (interval > 0)
                        {
                            GGPOEvent info = new GGPOTimesyncEvent { code = GGPOEventCode.GGPO_EVENTCODE_TIMESYNC, frames_ahead = interval };
                            _callbacks.OnEvent(info);
                            _next_recommended_sleep = current_frame + RECOMMENDATION_INTERVAL;
                        }
                    }
                    // XXX: this is obviously a farce...
                    // TODO: In C# what's that stuff?
                    // if (timeout)
                    // {
                    //     Sleep(1);
                    // }
                }
            }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SyncInput(ref InputType[] values, int size, ref int disconnect_flags)
        {
            int flags;

            // Wait until we've started to return inputs.
            if (_synchronizing)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_NOT_SYNCHRONIZED;
            }
            flags = _sync.SynchronizeInputs(ref values, size);
            if (disconnect_flags != 0)
            {
                disconnect_flags = flags;
            }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode AddLocalInput(GGPOPlayerHandle player, InputType[] values, int size)
        {
            int queue = 0;
            GGPOErrorCode result;
            GameInput<InputType> input = new GameInput<InputType>();

            if (_sync.InRollback())
            {
                return GGPOErrorCode.GGPO_ERRORCODE_IN_ROLLBACK;
            }
            if (_synchronizing)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_NOT_SYNCHRONIZED;
            }

            result = PlayerHandleToQueue(player, ref queue);
            if (!GGPO_SUCCEEDED(result))
            {
                return result;
            }

            input.Init(-1, values, size);

            // Feed the input for the current frame into the synchronzation layer.
            if (!_sync.AddLocalInput(queue, ref input))
            {
                return GGPOErrorCode.GGPO_ERRORCODE_PREDICTION_THRESHOLD;
            }

            if (input.frame != (int)GameInput<InputType>.Constants.NullFrame)
            { // xxx: <- comment why this is the case
              // Update the local connect status state to indicate that we've got a
              // confirmed local frame for this player.  this must come first so it
              // gets incorporated into the next packet we send.

                Logger.Log("setting local connect status for local queue {0} to {1}", queue, input.frame);
                _local_connect_status[queue].last_frame = input.frame;

                // Send the input to all the remote players.
                for (int i = 0; i < _num_players; i++)
                {
                    if (_endpoints[i].IsInitialized())
                    {
                        _endpoints[i].SendInput(ref input);
                    }
                }
            }

            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SetFrameDelay(GGPOPlayerHandle player, int delay)
        {
            int queue = 0;
            GGPOErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!GGPO_SUCCEEDED(result))
            {
                return result;
            }
            _sync.SetFrameDelay(queue, delay);
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SetDisconnectTimeout(int timeout)
        {
            _disconnect_timeout = timeout;
            for (int i = 0; i < _num_players; i++)
            {
                if (_endpoints[i].IsInitialized())
                {
                    _endpoints[i].SetDisconnectTimeout(_disconnect_timeout);
                }
            }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SetDisconnectNotifyStart(int timeout)
        {
            _disconnect_notify_start = timeout;
            for (int i = 0; i < _num_players; i++)
            {
                if (_endpoints[i].IsInitialized())
                {
                    _endpoints[i].SetDisconnectNotifyStart(_disconnect_notify_start);
                }
            }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode Chat(string text)
        {
            throw new NotImplementedException();
        }

        public override GGPOErrorCode IncrementFrame()
        {
            Logger.Log("End of frame ({0})...\n", _sync.GetFrameCount());
            _sync.IncrementFrame();
            DoPoll(0);
            PollSyncEvents();

            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode GetNetworkStats(ref GGPONetworkStats stats, GGPOPlayerHandle player)
        {
            int queue = 0;
            GGPOErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!GGPO_SUCCEEDED(result))
            {
                return result;
            }

            // memset(stats, 0, sizeof *stats); // not needed in C#
            _endpoints[queue].GetNetworkStats(ref stats);

            return GGPOErrorCode.GGPO_OK;
        }

        /*
         * Called only as the result of a local decision to disconnect.  The remote
         * decisions to disconnect are a result of us parsing the peer_connect_settings
         * blob in every endpoint periodically.
         */
        public override GGPOErrorCode DisconnectPlayer(GGPOPlayerHandle player)
        {
            int queue = 0;
            GGPOErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!GGPO_SUCCEEDED(result))
            {
                return result;
            }

            if (_local_connect_status[queue].disconnected != 0)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_PLAYER_DISCONNECTED;
            }

            if (!_endpoints[queue].IsInitialized())
            {
                int current_frame = _sync.GetFrameCount();
                // xxx: we should be tracking who the local player is, but for now assume
                // that if the endpoint is not initalized, this must be the local player.
                Logger.Log("Disconnecting local player {0} at frame {1} by user request.\n", queue, _local_connect_status[queue].last_frame);
                for (int i = 0; i < _num_players; i++)
                {
                    if (_endpoints[i].IsInitialized())
                    {
                        DisconnectPlayerQueue(i, current_frame);
                    }
                }
            }
            else
            {
                Logger.Log("Disconnecting queue {0} at frame {1} by user request.\n", queue, _local_connect_status[queue].last_frame);
                DisconnectPlayerQueue(queue, _local_connect_status[queue].last_frame);
            }
            return GGPOErrorCode.GGPO_OK;
        }

        private int Poll2Players(int current_frame)
        {
            int i;

            // discard confirmed frames as appropriate
            int total_min_confirmed = int.MaxValue;
            for (i = 0; i < _num_players; i++)
            {
                bool queue_connected = true;
                if (_endpoints[i].IsRunning())
                {
                    int ignore = 0;
                    queue_connected = _endpoints[i].GetPeerConnectStatus(i, ref ignore);
                }
                if (!(_local_connect_status[i].disconnected != 0))
                {
                    total_min_confirmed = System.Math.Min(_local_connect_status[i].last_frame, total_min_confirmed);
                }
                Logger.Log("  local endp: connected = {0}, last_received = {1}, total_min_confirmed = {2}.\n", !(_local_connect_status[i].disconnected != 0), _local_connect_status[i].last_frame, total_min_confirmed);
                if (!queue_connected && !(_local_connect_status[i].disconnected != 0))
                {
                    Logger.Log("disconnecting i {0} by remote request.\n", i);
                    DisconnectPlayerQueue(i, total_min_confirmed);
                }
                Logger.Log("  total_min_confirmed = {0}.\n", total_min_confirmed);
            }
            return total_min_confirmed;
        }

        private int PollNPlayers(int current_frame)
        {
            throw new System.NotImplementedException();
        }

        private void PollUdpProtocolEvents()
        {
            NetProto<InputType>.Event evt = new NetProto<InputType>.Event();
            for (int i = 0; i < _num_players; i++)
            {
                while (_endpoints[i] != null && _endpoints[i].GetEvent(ref evt))
                {
                    OnUdpProtocolPeerEvent(ref evt, i);
                }
            }
            for (int i = 0; i < _num_spectators; i++)
            {
                while (_spectators[i].GetEvent(ref evt))
                {
                    OnUdpProtocolSpectatorEvent(ref evt, i);
                }
            }
        }

        private void OnUdpProtocolSpectatorEvent(ref NetProto<InputType>.Event evt, int queue)
        {
            GGPOPlayerHandle handle = QueueToSpectatorHandle(queue);
            OnUdpProtocolEvent(ref evt, handle);

            switch (evt.type)
            {
                case NetProto<InputType>.Event.Type.Disconnected:
                    _spectators[queue].Disconnect();
                    _callbacks.OnEvent(new GGPODisconnectedFromPeerEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_DISCONNECTED_FROM_PEER,
                        player = handle
                    });
                    break;
            }
        }

        private void OnUdpProtocolPeerEvent(ref NetProto<InputType>.Event evt, int queue)
        {
            OnUdpProtocolEvent(ref evt, QueueToPlayerHandle(queue));
            switch (evt.type)
            {
                case NetProto<InputType>.Event.Type.Input:
                    if (!(_local_connect_status[queue].disconnected != 0))
                    {
                        var inputEvent = (evt as NetProto<InputType>.InputEvent);

                        int current_remote_frame = _local_connect_status[queue].last_frame;
                        int new_remote_frame = inputEvent.input.frame;
                        Logger.Assert(current_remote_frame == -1 || new_remote_frame == (current_remote_frame + 1));

                        _sync.AddRemoteInput(queue, ref inputEvent.input);
                        // Notify the other endpoints which frame we received from a peer
                        Logger.Log("setting remote connect status for queue %d to %d\n", queue, inputEvent.input.frame);
                        _local_connect_status[queue].last_frame = inputEvent.input.frame;
                    }
                    break;
                case NetProto<InputType>.Event.Type.Disconnected:
                    DisconnectPlayer(QueueToPlayerHandle(queue));
                    break;
            }
        }

        private void OnUdpProtocolEvent(ref NetProto<InputType>.Event evt, GGPOPlayerHandle handle)
        {
            switch (evt.type)
            {
                case NetProto<InputType>.Event.Type.Connected:
                    _callbacks.OnEvent(new GGPOConnectedToPeerEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_CONNECTED_TO_PEER,
                        player = handle
                    });
                    break;
                case NetProto<InputType>.Event.Type.Synchronizing:
                    var connectedEvent = evt as NetProto<InputType>.SynchronizingEvent;
                    _callbacks.OnEvent(new GGPOSynchronizingWithPeerEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_SYNCHRONIZING_WITH_PEER,
                        player = handle,
                        count = connectedEvent.count,
                        total = connectedEvent.total,
                    });
                    break;
                case NetProto<InputType>.Event.Type.Synchronzied:
                    _callbacks.OnEvent(new GGPOSynchronizedWithPeerEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_SYNCHRONIZED_WITH_PEER,
                        player = handle,
                    });
                    CheckInitialSync();
                    break;

                case NetProto<InputType>.Event.Type.NetworkInterrupted:
                    var netInterruptedEvent = evt as NetProto<InputType>.NetworkInterruptedEvent;
                    _callbacks.OnEvent(new GGPOConnectionInterruptedEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_CONNECTION_INTERRUPTED,
                        player = handle,
                        disconnect_timeout = netInterruptedEvent.disconnect_timeout
                    });
                    break;
                case NetProto<InputType>.Event.Type.NetworkResumed:
                    _callbacks.OnEvent(new GGPOConnectionResumedEvent
                    {
                        code = GGPOEventCode.GGPO_EVENTCODE_CONNECTION_INTERRUPTED,
                        player = handle,
                    });
                    break;
            }
        }

        private void DisconnectPlayerQueue(int queue, int syncto)
        {

            int framecount = _sync.GetFrameCount();

            _endpoints[queue].Disconnect();

            Logger.Log("Changing queue %d local connect status for last frame from {0} to {1} on disconnect request (current: {2}).\n",
                queue, _local_connect_status[queue].last_frame, syncto, framecount);

            _local_connect_status[queue].disconnected = 1;
            _local_connect_status[queue].last_frame = syncto;

            if (syncto < framecount)
            {
                Logger.Log("adjusting simulation to account for the fact that {0} disconnected @ {1}.\n", queue, syncto);
                _sync.AdjustSimulation(syncto);
                Logger.Log("finished adjusting simulation.\n");
            }

            GGPOEvent info = new GGPODisconnectedFromPeerEvent { code = GGPOEventCode.GGPO_EVENTCODE_DISCONNECTED_FROM_PEER, player = QueueToPlayerHandle(queue) };
            _callbacks.OnEvent(info);

            CheckInitialSync();
        }

        private void CheckInitialSync()
        {
            int i;

            if (_synchronizing)
            {
                // Check to see if everyone is now synchronized.  If so,
                // go ahead and tell the client that we're ok to accept input.
                for (i = 0; i < _num_players; i++)
                {
                    // xxx: IsInitialized() must go... we're actually using it as a proxy for "represents the local player"
                    if (_endpoints[i].IsInitialized() && !_endpoints[i].IsSynchronized() && !(_local_connect_status[i].disconnected != 0))
                    {
                        return;
                    }
                }
                for (i = 0; i < _num_spectators; i++)
                {
                    if (_spectators[i].IsInitialized() && !_spectators[i].IsSynchronized())
                    {
                        return;
                    }
                }

                GGPOEvent info = new GGPORunningEvent { code = GGPOEventCode.GGPO_EVENTCODE_RUNNING };
                _callbacks.OnEvent(info);
                _synchronizing = false;
            }
        }

        private GGPOPlayerHandle QueueToPlayerHandle(int queue)
        {
            return new GGPOPlayerHandle { handle = queue + 1 };
        }

        private GGPOPlayerHandle QueueToSpectatorHandle(int queue)
        {
            /* out of range of the player array, basically */
            return new GGPOPlayerHandle { handle = queue + 1000 };
        }

        private GGPOErrorCode PlayerHandleToQueue(GGPOPlayerHandle player, ref int queue)
        {
            int offset = ((int)player.handle - 1);
            if (offset < 0 || offset >= _num_players)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_INVALID_PLAYER_HANDLE;
            }
            queue = offset;
            return GGPOErrorCode.GGPO_OK;
        }

        private bool GGPO_SUCCEEDED(GGPOErrorCode result)
        {
            return result == GGPOErrorCode.GGPO_ERRORCODE_SUCCESS;
        }

        private void PollSyncEvents()
        {
            Sync<InputType>.Event e = new Sync<InputType>.Event();
            while (_sync.GetEvent(ref e))
            {
                // OnSyncEvent(e); // OnSyncEvent is never implemented
            }
        }
    }
}
