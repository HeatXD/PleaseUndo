namespace PleaseUndo
{
    public struct GGPOSessionCallbacks
    {
        public delegate bool begin_game(string game);
        public delegate bool save_game_state(out byte[] buffer, out int len, out int checksum, int frame);
        public delegate bool load_game_state(byte[] buffer, int lent);
        public delegate bool log_game_state(string filename, byte[] buffer, int len);
        public delegate bool free_buffer(byte[] buffer); // no need in C# but kept for sanity
        public delegate bool advance_frame(int flags); // flags -> unused
        public delegate bool on_event(GGPOEvent ev);

        public event begin_game OnBeginGame;
        public event save_game_state OnSaveGameState;
        public event load_game_state OnLoadGameState;
        public event log_game_state OnLogGameState;
        public event free_buffer OnFreeBuffer;
        public event advance_frame OnAdvanceFrame;
        public event on_event OnEvent;
    }

    public abstract class GGPOSession<InputType>
    {
        public const uint GGPO_MAX_PLAYERS = 4;
        public const uint GGPO_MAX_PREDICTION_FRAMES = 8;
        public const uint GGPO_MAX_SPECTATORS = 32;
        public const uint GGPO_SPECTATOR_INPUT_INTERVAL = 4;

        public GGPOSessionCallbacks Callbacks;

        public GGPOErrorCode DoPoll(int timeout) { return GGPOErrorCode::GGPO_OK; }
        public GGPOErrorCode AddPlayer(GGPOPlayer player, GGPOPlayerHandle handle);
        public GGPOErrorCode AddLocalInput(GGPOPlayerHandle player, GameInput<InputType> values, int size);
        public GGPOErrorCode SyncInput(GameInput<InputType> values, int size, int[] disconnect_flags);
        public GGPOErrorCode IncrementFrame() { return GGPOErrorCode::GGPO_OK; }
        public GGPOErrorCode Chat(string text) { return GGPOErrorCode::GGPO_OK; }
        public GGPOErrorCode DisconnectPlayer(GGPOPlayerHandle handle) { return GGPOErrorCode::GGPO_OK; }
        public GGPOErrorCode GetNetworkStats(GGPONetworkStats stats, GGPOPlayerHandle handle) { return GGPOErrorCode::GGPO_OK; }
        public GGPOErrorCode Logv(string fmt, params string[] list) { /* ::Logv(fmt, list); */ return GGPOErrorCode::GGPO_OK; }

        public GGPOErrorCode SetFrameDelay(GGPOPlayerHandle player, int delay) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }
        public GGPOErrorCode SetDisconnectTimeout(int timeout) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }
        public GGPOErrorCode SetDisconnectNotifyStart(int timeout) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }

        #region TODO: Create facade pattern

        public static GGPOErrorCode ggpo_start_session(out GGPOSession session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, ushort localport);
        public static GGPOErrorCode ggpo_start_synctest(out GGPOSession session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, int frames);
        public static GGPOErrorCode ggpo_start_spectating(out GGPOSession session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, ushort local_port, string host_ip, ushort host_port);

        #endregion

        public GGPOErrorCode ggpo_add_player(/* GGPOSession session, */ GGPOPlayer player, GGPOPlayerHandle handle);
        public GGPOErrorCode ggpo_close_session();
        public GGPOErrorCode ggpo_set_frame_delay(/* GGPOSession session, */ GGPOPlayerHandle player, int frame_delay);
        public GGPOErrorCode ggpo_idle(/* GGPOSession session, */ GGPOPlayerHandle player, int timeout);
        public GGPOErrorCode ggpo_add_local_input(/* GGPOSession session, */ GGPOPlayerHandle player, GameInput<InputType> values, int size);
        public GGPOErrorCode ggpo_synchronize_input(/* GGPOSession session, */ GameInput<InputType> values, int size, out int disconnect_flags);
        public GGPOErrorCode ggpo_disconnect_player(/* GGPOSession session, */ GGPOPlayerHandle handle);
        public GGPOErrorCode ggpo_advance_frame(/* GGPOSession session, */);
        public GGPOErrorCode ggpo_get_network_stats(/* GGPOSession session, */ GGPOPlayerHandle player, GGPONetworkStats stats);
        public GGPOErrorCode ggpo_set_disconnect_timeout(/* GGPOSession session, */ int timeout);
        public GGPOErrorCode ggpo_set_disconnect_notify_start(/* GGPOSession session, */ int timeout);
        public GGPOErrorCode ggpo_log(/* GGPOSession session, */ string fmt, params string[] list);
        public GGPOErrorCode ggpo_logv(/* GGPOSession session, */ string fmt, params string[] list); // no need in C# but kept for sanity
    }
}
