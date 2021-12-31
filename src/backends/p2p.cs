using System;

namespace PleaseUndo
{
    public class Peer2PeerBackend : PUSession
    {
        const int UDP_MSG_MAX_PLAYERS = 4;
        const int RECOMMENDATION_INTERVAL = 240;
        const int DEFAULT_DISCONNECT_TIMEOUT = 5000;
        const int DEFAULT_DISCONNECT_NOTIFY_START = 750;
        const int MAX_SPECTATORS = 16;

        protected PUSessionCallbacks _callbacks;
        protected Poll _poll;
        protected Sync _sync;
        //   protected  Udp                   _udp; //Still dont know what to do with this... :HEAT /TODO
        protected NetProto[] _endpoints;
        protected NetProto[] _spectators;
        protected int _num_spectators;
        protected int _input_size;

        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_recommended_sleep;

        protected int _next_spectator_frame;
        protected int _disconnect_timeout;
        protected int _disconnect_notify_start;

        NetMsg.ConnectStatus[] _local_connect_status;

        public Peer2PeerBackend(ref PUSessionCallbacks cb, int num_players, int input_size)
        {
            _num_players = num_players;
            _disconnect_timeout = DEFAULT_DISCONNECT_TIMEOUT;
            _disconnect_notify_start = DEFAULT_DISCONNECT_NOTIFY_START;
            _num_spectators = 0;
            _next_spectator_frame = 0;
            _poll = new Poll();
            _callbacks = cb;
            _synchronizing = true;
            _next_recommended_sleep = 0;

            /*
             * Initialize endpoints
             */
            _endpoints = new NetProto[_num_players];
            _spectators = new NetProto[MAX_SPECTATORS];
            _local_connect_status = new NetMsg.ConnectStatus[UDP_MSG_MAX_PLAYERS];

            for (int i = 0; i < _local_connect_status.Length; i++)
            {
                _local_connect_status[i] = new NetMsg.ConnectStatus { last_frame = -1 };
            }

            /*
            * Initialize the synchronziation layer
            */
            _sync = new Sync(ref _local_connect_status);
            _sync.Init(new Sync.Config
            {
                callbacks = _callbacks,
                input_size = input_size,
                num_players = _num_players,
                num_prediction_frames = Sync.MAX_PREDICTION_FRAMES,
            });

            /*
             * Preload the ROM
             */
            _callbacks.OnBeginGame();
        }

        public override PUErrorCode AddLocalPlayer(PUPlayer player, ref PUPlayerHandle handle)
        {
            // if (player.type == PUPlayerType.SPECTATOR) // Should be AddSpectatorPlayer
            // {
            //     return AddSpectator(player->u.remote.ip_address, player->u.remote.port);
            // }

            int queue = player.player_num - 1;
            if (player.player_num < 1 || player.player_num > _num_players)
            {
                return PUErrorCode.PU_ERRORCODE_PLAYER_OUT_OF_RANGE;
            }
            handle = QueueToPlayerHandle(queue);

            // if (player.type == PUPlayerType.REMOTE) // Is now AddRemotePlayer
            // {
            //     AddRemotePlayer(player->u.remote.ip_address, player->u.remote.port, queue);
            // }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode AddRemotePlayer(PUPlayer player, ref PUPlayerHandle handle, IPeerNetAdapter peerNetAdapter)
        {
            _synchronizing = true;

            var queue = player.player_num - 1;
            handle = QueueToPlayerHandle(queue);
            _endpoints[queue] = new NetProto(queue, peerNetAdapter, _local_connect_status, ref _poll);
            _endpoints[queue].SetDisconnectTimeout(_disconnect_timeout);
            _endpoints[queue].SetDisconnectNotifyStart(_disconnect_notify_start);
            _endpoints[queue].Synchronize();

            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode DoPoll(int timeout)
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
                        if (_endpoints[i] != null) // CHECK ADDED, NOT IN GGPO
                        {
                            _endpoints[i].SetLocalFrameNumber(current_frame);
                        }
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

                    Logger.Log("last confirmed frame in p2p backend is {0}. \n", total_min_confirmed);
                    if (total_min_confirmed >= 0)
                    {
                        Logger.Assert(total_min_confirmed != int.MaxValue);
                        if (_num_spectators > 0)
                        {
                            while (_next_spectator_frame <= total_min_confirmed)
                            {
                                Logger.Log("pushing frame {0} to spectators.", _next_spectator_frame);

                                var input = new GameInput
                                {
                                    frame = _next_spectator_frame,
                                    size = (uint)(_input_size * _num_players),
                                };
                                _sync.GetConfirmedInputs(input.bits, _input_size * _num_players, _next_spectator_frame);

                                for (int i = 0; i < _num_spectators; i++)
                                {
                                    _spectators[i].SendInput(ref input);
                                }
                                _next_spectator_frame++;
                            }
                        }
                        Logger.Log("setting confirmed frame in sync to {0}.\n", total_min_confirmed);
                        _sync.SetLastConfirmedFrame(total_min_confirmed);
                    }

                    // send timesync notifications if now is the proper time
                    if (current_frame > _next_recommended_sleep)
                    {
                        int interval = 0;
                        for (int i = 0; i < _num_players; i++)
                        {
                            if (_endpoints[i] != null) // CHECK ADDED, NOT IN GGPO
                            {
                                interval = Math.Max(interval, _endpoints[i].RecommendFrameDelay());
                            }
                        }

                        if (interval > 0)
                        {
                            _callbacks.OnEvent(new PUTimesyncEvent
                            {
                                code = PUEventCode.PU_EVENTCODE_TIMESYNC,
                                frames_ahead = interval
                            });
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

            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SyncInput(ref byte[] values, int size, ref int disconnect_flags)
        {
            int flags;

            // Wait until we've started to return inputs.
            if (_synchronizing)
            {
                return PUErrorCode.PU_ERRORCODE_NOT_SYNCHRONIZED;
            }
            flags = _sync.SynchronizeInputs(ref values, size);
            if (disconnect_flags != 0)
            {
                disconnect_flags = flags;
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode AddLocalInput(PUPlayerHandle player, byte[] values, int size)
        {
            int queue = 0;
            PUErrorCode result;

            if (_sync.InRollback())
            {
                return PUErrorCode.PU_ERRORCODE_IN_ROLLBACK;
            }
            if (_synchronizing)
            {
                return PUErrorCode.PU_ERRORCODE_NOT_SYNCHRONIZED;
            }

            result = PlayerHandleToQueue(player, ref queue);
            if (!PU_SUCCEEDED(result))
            {
                return result;
            }

            var input = new GameInput(-1, values, (uint)values.Length);

            // Feed the input for the current frame into the synchronzation layer.
            if (!_sync.AddLocalInput(queue, ref input))
            {
                return PUErrorCode.PU_ERRORCODE_PREDICTION_THRESHOLD;
            }

            if (input.frame != (int)GameInput.Constants.NullFrame)
            { // xxx: <- comment why this is the case
              // Update the local connect status state to indicate that we've got a
              // confirmed local frame for this player.  this must come first so it
              // gets incorporated into the next packet we send.

                Logger.Log("setting local connect status for local queue {0} to {1}\n", queue, input.frame);
                _local_connect_status[queue].last_frame = input.frame;

                // Send the input to all the remote players.
                for (int i = 0; i < _num_players; i++)
                {
                    if (_endpoints[i] != null && _endpoints[i].IsInitialized())
                    {
                        _endpoints[i].SendInput(ref input);
                    }
                }
            }

            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SetFrameDelay(PUPlayerHandle player, int delay)
        {
            int queue = 0;
            PUErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!PU_SUCCEEDED(result))
            {
                return result;
            }
            _sync.SetFrameDelay(queue, delay);
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SetDisconnectTimeout(int timeout)
        {
            _disconnect_timeout = timeout;
            for (int i = 0; i < _num_players; i++)
            {
                if (_endpoints[i].IsInitialized())
                {
                    _endpoints[i].SetDisconnectTimeout(_disconnect_timeout);
                }
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SetDisconnectNotifyStart(int timeout)
        {
            _disconnect_notify_start = timeout;
            for (int i = 0; i < _num_players; i++)
            {
                if (_endpoints[i].IsInitialized())
                {
                    _endpoints[i].SetDisconnectNotifyStart(_disconnect_notify_start);
                }
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode Chat(string text)
        {
            throw new NotImplementedException();
        }

        public override PUErrorCode IncrementFrame()
        {
            Logger.Log("End of frame ({0})...\n", _sync.GetFrameCount());
            _sync.IncrementFrame();
            DoPoll(0);
            PollSyncEvents();

            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode GetNetworkStats(ref PUNetworkStats stats, PUPlayerHandle player)
        {
            int queue = 0;
            PUErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!PU_SUCCEEDED(result))
            {
                return result;
            }

            // memset(stats, 0, sizeof *stats); // not needed in C#
            _endpoints[queue].GetNetworkStats(ref stats);

            return PUErrorCode.PU_OK;
        }

        /*
         * Called only as the result of a local decision to disconnect.  The remote
         * decisions to disconnect are a result of us parsing the peer_connect_settings
         * blob in every endpoint periodically.
         */
        public override PUErrorCode DisconnectPlayer(PUPlayerHandle player)
        {
            int queue = 0;
            PUErrorCode result;

            result = PlayerHandleToQueue(player, ref queue);
            if (!PU_SUCCEEDED(result))
            {
                return result;
            }

            if (_local_connect_status[queue].disconnected != 0)
            {
                return PUErrorCode.PU_ERRORCODE_PLAYER_DISCONNECTED;
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
            return PUErrorCode.PU_OK;
        }

        private int Poll2Players(int current_frame)
        {
            int i;

            // discard confirmed frames as appropriate
            int total_min_confirmed = int.MaxValue;
            for (i = 0; i < _num_players; i++)
            {
                bool queue_connected = true;
                if (_endpoints[i] != null && _endpoints[i].IsRunning()) // CHECK ADDED != null, NOT IN GGPO
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
            int i, queue;
            int last_received = 0;

            // discard confirmed frames as appropriate
            int total_min_confirmed = int.MaxValue;
            for (queue = 0; queue < _num_players; queue++)
            {
                bool queue_connected = true;
                int queue_min_confirmed = int.MaxValue;
                Logger.Log("considering queue {0}.\n", queue);
                for (i = 0; i < _num_players; i++)
                {
                    // we're going to do a lot of logic here in consideration of endpoint i.
                    // keep accumulating the minimum confirmed point for all n*n packets and
                    // throw away the rest.
                    if (_endpoints[i] != null && _endpoints[i].IsRunning()) // CHECK ADDED != null, NOT IN GGPO
                    {
                        bool connected = _endpoints[i].GetPeerConnectStatus(queue, ref last_received);

                        queue_connected = queue_connected && connected;
                        queue_min_confirmed = System.Math.Min(last_received, queue_min_confirmed);
                        Logger.Log("  endpoint {0}: connected = {1}, last_received = {2}, queue_min_confirmed = {3}.\n", i, connected, last_received, queue_min_confirmed);
                    }
                    else
                    {
                        Logger.Log("  endpoint {0}: ignoring... not running.\n", i);
                    }
                }
                // merge in our local status only if we're still connected!
                if (_local_connect_status[queue].disconnected != 0)
                {
                    queue_min_confirmed = Math.Min(_local_connect_status[queue].last_frame, queue_min_confirmed);
                }
                Logger.Log("  local endp: connected = {0}, last_received = {1}, queue_min_confirmed = {2}.\n", _local_connect_status[queue].disconnected == 0, _local_connect_status[queue].last_frame, queue_min_confirmed);

                if (queue_connected)
                {
                    total_min_confirmed = Math.Min(queue_min_confirmed, total_min_confirmed);
                }
                else
                {
                    // check to see if this disconnect notification is further back than we've been before.  If
                    // so, we need to re-adjust.  This can happen when we detect our own disconnect at frame n
                    // and later receive a disconnect notification for frame n-1.
                    if (_local_connect_status[queue].disconnected == 0 || _local_connect_status[queue].last_frame > queue_min_confirmed)
                    {
                        Logger.Log("disconnecting queue {0} by remote request.\n", queue);
                        DisconnectPlayerQueue(queue, queue_min_confirmed);
                    }
                }
                Logger.Log("  total_min_confirmed = {0}.\n", total_min_confirmed);
            }
            return total_min_confirmed;
        }

        private void PollUdpProtocolEvents()
        {
            NetProto.Event evt = new NetProto.Event();
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

        private void OnUdpProtocolSpectatorEvent(ref NetProto.Event evt, int queue)
        {
            PUPlayerHandle handle = QueueToSpectatorHandle(queue);
            OnUdpProtocolEvent(ref evt, handle);

            switch (evt.type)
            {
                case NetProto.Event.Type.Disconnected:
                    _spectators[queue].Disconnect();
                    _callbacks.OnEvent(new PUDisconnectedFromPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_DISCONNECTED_FROM_PEER,
                        player = handle
                    });
                    break;
            }
        }

        private void OnUdpProtocolPeerEvent(ref NetProto.Event evt, int queue)
        {
            OnUdpProtocolEvent(ref evt, QueueToPlayerHandle(queue));
            switch (evt.type)
            {
                case NetProto.Event.Type.Input:
                    if (_local_connect_status[queue].disconnected == 0)
                    {
                        var inputEvent = (evt as NetProto.InputEvent);

                        int current_remote_frame = _local_connect_status[queue].last_frame;
                        int new_remote_frame = inputEvent.input.frame;
                        Logger.Assert(current_remote_frame == -1 || new_remote_frame == (current_remote_frame + 1));

                        var input = inputEvent.input;
                        _sync.AddRemoteInput(queue, ref input);

                        // Notify the other endpoints which frame we received from a peer
                        Logger.Log("setting remote connect status for queue {0} to {1}\n", queue, inputEvent.input.frame);
                        _local_connect_status[queue].last_frame = inputEvent.input.frame;
                    }
                    break;
                case NetProto.Event.Type.Disconnected:
                    DisconnectPlayer(QueueToPlayerHandle(queue));
                    break;
            }
        }

        private void OnUdpProtocolEvent(ref NetProto.Event evt, PUPlayerHandle handle)
        {
            switch (evt.type)
            {
                case NetProto.Event.Type.Connected:
                    _callbacks.OnEvent(new PUConnectedToPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTED_TO_PEER,
                        player = handle
                    });
                    break;
                case NetProto.Event.Type.Synchronizing:
                    var connectedEvent = evt as NetProto.SynchronizingEvent;
                    _callbacks.OnEvent(new PUSynchronizingWithPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_SYNCHRONIZING_WITH_PEER,
                        player = handle,
                        count = connectedEvent.count,
                        total = connectedEvent.total,
                    });
                    break;
                case NetProto.Event.Type.Synchronzied:
                    _callbacks.OnEvent(new PUSynchronizedWithPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_SYNCHRONIZED_WITH_PEER,
                        player = handle,
                    });
                    CheckInitialSync();
                    break;

                case NetProto.Event.Type.NetworkInterrupted:
                    var netInterruptedEvent = evt as NetProto.NetworkInterruptedEvent;
                    _callbacks.OnEvent(new PUConnectionInterruptedEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTION_INTERRUPTED,
                        player = handle,
                        disconnect_timeout = netInterruptedEvent.disconnect_timeout
                    });
                    break;
                case NetProto.Event.Type.NetworkResumed:
                    _callbacks.OnEvent(new PUConnectionResumedEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTION_INTERRUPTED,
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

            _callbacks.OnEvent(new PUDisconnectedFromPeerEvent
            {
                code = PUEventCode.PU_EVENTCODE_DISCONNECTED_FROM_PEER,
                player = QueueToPlayerHandle(queue)
            });

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
                    if (_endpoints[i] != null && _endpoints[i].IsInitialized() && !_endpoints[i].IsSynchronized() && _local_connect_status[i].disconnected == 0) // CHECK ADDED != null, NOT IN GGPO
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

                _callbacks.OnEvent(new PURunningEvent { code = PUEventCode.PU_EVENTCODE_RUNNING });
                _synchronizing = false;
            }
        }

        private PUPlayerHandle QueueToPlayerHandle(int queue)
        {
            return new PUPlayerHandle { handle = queue + 1 };
        }

        private PUPlayerHandle QueueToSpectatorHandle(int queue)
        {
            /* out of range of the player array, basically */
            return new PUPlayerHandle { handle = queue + 1000 };
        }

        private PUErrorCode PlayerHandleToQueue(PUPlayerHandle player, ref int queue)
        {
            int offset = ((int)player.handle - 1);
            if (offset < 0 || offset >= _num_players)
            {
                return PUErrorCode.PU_ERRORCODE_INVALID_PLAYER_HANDLE;
            }
            queue = offset;
            return PUErrorCode.PU_OK;
        }

        private bool PU_SUCCEEDED(PUErrorCode result)
        {
            return result == PUErrorCode.PU_ERRORCODE_SUCCESS;
        }

        private void PollSyncEvents()
        {
            Sync.Event e = new Sync.Event();
            while (_sync.GetEvent(ref e))
            {
                // OnSyncEvent(e); // OnSyncEvent is never implemented
            }
        }
    }
}
