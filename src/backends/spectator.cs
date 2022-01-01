
using System;

namespace PleaseUndo
{
    public class SpectatorBackend : PUSession
    {
        const int SPECTATOR_FRAME_BUFFER_SIZE = 64;

        protected PUSessionCallbacks _callbacks;
        protected Poll _poll;
        protected NetProto _host;
        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_input_to_send;
        protected GameInput[] _inputs;

        public SpectatorBackend(ref PUSessionCallbacks cb, int num_players, ref IPeerNetAdapter net_adapter)
        {
            _inputs = new GameInput[SPECTATOR_FRAME_BUFFER_SIZE];
            _callbacks = cb;
            _num_players = num_players;
            _synchronizing = true;
            _next_input_to_send = 0;

            for (int i = 0; i < _inputs.Length; i++)
            {
                _inputs[i].frame = -1;
            }

            /*
             * Init the host endpoint
             */
            _host = new NetProto(0, net_adapter, null, _poll);
            _host.Synchronize();

            /*
             * Preload the ROM
             */
            _callbacks.OnBeginGame();
        }

        public override PUErrorCode DoPoll(int timeout)
        {
            _poll.Pump(0);
            PollNetProtocolEvents();
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SyncInput(ref byte[] values, int size, ref int disconnect_flags)
        {
            // Wait until we've started to return inputs.
            if (_synchronizing)
            {
                return PUErrorCode.PU_ERRORCODE_NOT_SYNCHRONIZED;
            }

            var input = _inputs[_next_input_to_send % SPECTATOR_FRAME_BUFFER_SIZE];
            if (input.frame < _next_input_to_send)
            {
                // Haven't received the input from the host yet.  Wait
                return PUErrorCode.PU_ERRORCODE_PREDICTION_THRESHOLD;
            }

            if (input.frame > _next_input_to_send)
            {
                // The host is way way way far ahead of the spectator.  How'd this
                // happen?  Anyway, the input we need is gone forever.                
                return PUErrorCode.PU_ERRORCODE_GENERAL_FAILURE;
            }

            //memcpy stuff
            Array.Copy(values, input.bits, size);
            // input.bits = values;

            if (disconnect_flags != 0)
            {
                disconnect_flags = 0;
            }
            _next_input_to_send++;

            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode IncrementFrame()
        {
            Logger.Log("End of frame ({0})...\n", _next_input_to_send - 1);
            DoPoll(0);
            PollNetProtocolEvents();

            return PUErrorCode.PU_OK;
        }

        protected void PollNetProtocolEvents()
        {
            var evt = new NetProto.Event();
            while (_host.GetEvent(ref evt))
            {
                OnNetProtocolEvent(evt);
            }
        }

        protected void OnNetProtocolEvent(NetProto.Event evt)
        {
            switch (evt.type)
            {
                case NetProto.Event.Type.Connected:
                    _callbacks.OnEvent(new PUConnectedToPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTED_TO_PEER,
                        player = new PUPlayerHandle { handle = 0 }
                    });
                    break;
                case NetProto.Event.Type.Synchronizing:
                    var synchronizingEvent = evt as NetProto.SynchronizingEvent;
                    _callbacks.OnEvent(new PUSynchronizingWithPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_SYNCHRONIZING_WITH_PEER,
                        count = synchronizingEvent.count,
                        total = synchronizingEvent.total,
                        player = new PUPlayerHandle { handle = 0 }
                    });
                    break;
                case NetProto.Event.Type.Synchronzied:
                    if (_synchronizing)
                    {
                        _callbacks.OnEvent(new PUSynchronizedWithPeerEvent
                        {
                            code = PUEventCode.PU_EVENTCODE_SYNCHRONIZED_WITH_PEER,
                            player = new PUPlayerHandle { handle = 0 }
                        });
                        _callbacks.OnEvent(new PURunningEvent
                        {
                            code = PUEventCode.PU_EVENTCODE_RUNNING
                        });
                        _synchronizing = false;
                    }
                    break;
                case NetProto.Event.Type.NetworkInterrupted:
                    var networkInterruptedEvent = evt as NetProto.NetworkInterruptedEvent;
                    _callbacks.OnEvent(new PUConnectionInterruptedEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTION_INTERRUPTED,
                        player = new PUPlayerHandle { handle = 0 },
                        disconnect_timeout = networkInterruptedEvent.disconnect_timeout
                    });
                    break;
                case NetProto.Event.Type.NetworkResumed:
                    _callbacks.OnEvent(new PUConnectionResumedEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_CONNECTION_RESUMED,
                        player = new PUPlayerHandle { handle = 0 },
                    });
                    break;
                case NetProto.Event.Type.Disconnected:
                    _callbacks.OnEvent(new PUDisconnectedFromPeerEvent
                    {
                        code = PUEventCode.PU_EVENTCODE_DISCONNECTED_FROM_PEER,
                        player = new PUPlayerHandle { handle = 0 },
                    });
                    break;
                case NetProto.Event.Type.Input:
                    var input = (evt as NetProto.InputEvent).input;
                    _host.SetLocalFrameNumber(input.frame);
                    _host.SendInputAck();
                    _inputs[input.frame % SPECTATOR_FRAME_BUFFER_SIZE] = input;
                    break;
            }
        }
    }
}
