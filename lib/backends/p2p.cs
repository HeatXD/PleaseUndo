namespace PleaseUndo
{
    public class Peer2PeerBackend<InputType> : GGPOSession<InputType>
    {
        const int UDP_MSG_MAX_PLAYERS = 4;
        const int RECOMMENDATION_INTERVAL = 240;
        const int DEFAULT_DISCONNECT_TIMEOUT = 5000;
        const int DEFAULT_DISCONNECT_NOTIFY_START = 750;

        protected GGPOSessionCallbacks _callbacks;
        //   protected  Poll                  _poll;
        protected Sync<InputType> _sync = null;
        //   protected  Udp                   _udp;
        //   protected  UdpProtocol           *_endpoints;
        //   protected  UdpProtocol           _spectators[GGPO_MAX_SPECTATORS];
        protected int _num_spectators;
        protected int _input_size;

        protected bool _synchronizing;
        protected int _num_players;
        protected int _next_recommended_sleep;

        protected int _next_spectator_frame;
        protected int _disconnect_timeout;
        protected int _disconnect_notify_start;

        NetMsg.ConnectStatus[] _local_connect_status = new NetMsg.ConnectStatus[UDP_MSG_MAX_PLAYERS];

        public Peer2PeerBackend(GGPOSessionCallbacks cb, int num_players)
        {
            _num_players = num_players;
            _sync = new Sync<InputType>(ref _local_connect_status);
            _disconnect_timeout = DEFAULT_DISCONNECT_TIMEOUT;
            _disconnect_notify_start = DEFAULT_DISCONNECT_NOTIFY_START;
            _num_spectators = 0;
            _next_spectator_frame = 0;

            _callbacks = cb;
            _synchronizing = true;
            _next_recommended_sleep = 0;

            /*
             * Initialize the synchronziation layer
             */
            _sync.Init(new Sync<InputType>.Config
            {
                num_players = num_players,
                callbacks = _callbacks,
                num_prediction_frames = Sync<InputType>.MAX_PREDICTION_FRAMES,
            });

            /*
             * Initialize the UDP port
             */
            //    _udp.Init(localport, &_poll, this);

            //    _endpoints = new UdpProtocol[_num_players];
            //    memset(_local_connect_status, 0, sizeof(_local_connect_status));
            for (int i = 0; i < _local_connect_status.Length; i++)
            {
                _local_connect_status[i].last_frame = -1;
            }

            /*
             * Preload the ROM
             */
            cb.OnBeginGame();
        }

        public override GGPOErrorCode AddPlayer(GGPOPlayer player, ref GGPOPlayerHandle handle)
        {
            if (player.type == GGPOPlayerType.SPECTATOR)
            {
                // return AddSpectator(player->u.remote.ip_address, player->u.remote.port);
            }

            int queue = player.player_num - 1;
            if (player.player_num < 1 || player.player_num > _num_players)
            {
                return GGPOErrorCode.GGPO_ERRORCODE_PLAYER_OUT_OF_RANGE;
            }
            handle = QueueToPlayerHandle(queue);

            if (player.type == GGPOPlayerType.REMOTE)
            {
                // AddRemotePlayer(player->u.remote.ip_address, player->u.remote.port, queue);
            }
            return GGPOErrorCode.GGPO_OK;
        }

        public override GGPOErrorCode SyncInput(InputType values, int size, ref int disconnect_flags)
        {
            throw new System.NotImplementedException();
        }

        public override GGPOErrorCode AddLocalInput(GGPOPlayerHandle player, InputType values, int size)
        {
            throw new System.NotImplementedException();
        }

        GGPOPlayerHandle QueueToPlayerHandle(int queue)
        {
            return new GGPOPlayerHandle { handle = queue + 1 };
        }
    }
}
