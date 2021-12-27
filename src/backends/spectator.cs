using System;

namespace PleaseUndo
{
    public class SpectatorBackend<InputType> : GGPOSession<InputType>
    {
        const int SPECTATOR_FRAME_BUFFER_SIZE = 64;

        protected GGPOSessionCallbacks _callbacks;
        protected Poll _poll;
        protected NetProto<InputType> _host;
        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_input_to_send;
        protected GameInput<InputType>[] _inputs;

        public SpectatorBackend(ref GGPOSessionCallbacks cb, int num_players, ref IPeerNetAdapter net_adapter)
        {
            _callbacks = cb;
            _synchronizing = true;
            _num_players = num_players;
            _next_input_to_send = 0;
            _inputs = new GameInput<InputType>[SPECTATOR_FRAME_BUFFER_SIZE];

            for (int i = 0; i < _inputs.Length; i++)
            {
                _inputs[i].frame = -1;
            }
            /*
             * Init the host endpoint
             */
            _host = new NetProto<InputType>(0, ref net_adapter, ref _poll);
            _host.Synchronize();
            /*
             * Preload the ROM
             */
            _callbacks.OnBeginGame();
        }

        public override GGPOErrorCode DoPoll(int timeout)
        {
            _poll.Pump(0);
            PollNetProtocolEvents();
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SyncInput(ref InputType[] values, int size, ref int disconnect_flags)
        {
            // Wait until we've started to return inputs.
            if (_synchronizing)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_NOT_SYNCHRONIZED;
            }

            GameInput<InputType> input = _inputs[_next_input_to_send % SPECTATOR_FRAME_BUFFER_SIZE];
            if (input.frame < _next_input_to_send)
            {
                // Haven't received the input from the host yet.  Wait
                return GGPOErrorCode.GGPO_ERRORCODE_PREDICTION_THRESHOLD;
            }

            if (input.frame > _next_input_to_send)
            {
                // The host is way way way far ahead of the spectator.  How'd this
                // happen?  Anyway, the input we need is gone forever.                
                return GGPOErrorCode.GGPO_ERRORCODE_GENERAL_FAILURE;
            }

            //memcpy stuff
            Array.Copy(input.inputs, values, input.inputs.Length);

            if (disconnect_flags != 0)
            {
                disconnect_flags = 0;
            }
            _next_input_to_send++;

            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode IncrementFrame()
        {
            Logger.Log("End of frame ({0})...\n", _next_input_to_send - 1);
            DoPoll(0);
            PollNetProtocolEvents();

            return GGPOErrorCode.GGPO_OK;
        }

        protected void PollNetProtocolEvents()
        {
            var evt = new NetProto<InputType>.Event();
            while (_host.GetEvent(ref evt))
            {
                OnNetProtocolEvent(evt);
            }
        }

        protected void OnNetProtocolEvent(NetProto<InputType>.Event evt)
        {
            GGPOEvent info = new GGPOEvent();
        }
    }
}