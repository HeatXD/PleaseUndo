namespace PleaseUndo
{
    public class SpectatorBackend<InputType>
    {
        const int SPECTATOR_FRAME_BUFFER_SIZE = 64;

        protected GGPOSessionCallbacks _callbacks;
        protected Poll _poll;
        protected NetProto<InputType> _host;
        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_input_to_send;
        protected GameInput<InputType>[] _inputs;

        public SpectatorBackend(ref GGPOSessionCallbacks cb, int num_players, ref IPeerNetAdapter<InputType> net_adapter)
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
    }
}