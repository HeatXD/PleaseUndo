namespace PleaseUndo
{
    public struct GGPOSessionCallbacks
    {
        public delegate bool OnEventDelegate(GGPOEvent ev);
        public delegate bool BeginGameDelegate();
        public delegate bool AdvanceFrameDelegate();
        public delegate bool LoadGameStateDelegate(byte[] buffer, int lent);
        public delegate bool SaveGameStateDelegate(ref byte[] buffer, ref int len, ref int checksum, int frame);

        public OnEventDelegate OnEvent;
        public BeginGameDelegate OnBeginGame;
        public AdvanceFrameDelegate OnAdvanceFrame;
        public SaveGameStateDelegate OnSaveGameState;
        public LoadGameStateDelegate OnLoadGameState;
    }

    public abstract class GGPOSession<InputType>
    {
        public const uint GGPO_MAX_PLAYERS = 4;
        public const uint GGPO_MAX_SPECTATORS = 32;
        public const uint GGPO_MAX_PREDICTION_FRAMES = 8;
        public const uint GGPO_SPECTATOR_INPUT_INTERVAL = 4;

        public abstract GGPOErrorCode AddLocalPlayer(GGPOPlayer player, ref GGPOPlayerHandle handle);
        public abstract GGPOErrorCode AddRemotePlayer(GGPOPlayer player, ref GGPOPlayerHandle handle, IPeerNetAdapter<InputType> peerNetAdapter);
        public abstract GGPOErrorCode SyncInput(InputType[] values, int size, ref int disconnect_flags);
        public abstract GGPOErrorCode AddLocalInput(GGPOPlayerHandle player, InputType[] values, int size);
        public virtual GGPOErrorCode Chat(string text) { return GGPOErrorCode.GGPO_OK; }
        public virtual GGPOErrorCode DoPoll(int timeout) { return GGPOErrorCode.GGPO_OK; }
        public virtual GGPOErrorCode IncrementFrame() { return GGPOErrorCode.GGPO_OK; }
        public virtual GGPOErrorCode GetNetworkStats(GGPONetworkStats stats, GGPOPlayerHandle handle) { return GGPOErrorCode.GGPO_OK; }
        public virtual GGPOErrorCode DisconnectPlayer(GGPOPlayerHandle handle) { return GGPOErrorCode.GGPO_OK; }

        public virtual GGPOErrorCode SetFrameDelay(GGPOPlayerHandle player, int delay) { return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }
        public virtual GGPOErrorCode SetDisconnectTimeout(int timeout) { return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }
        public virtual GGPOErrorCode SetDisconnectNotifyStart(int timeout) { return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }

        #region TODO: Create facade pattern

        public static GGPOErrorCode ggpo_start_session(ref GGPOSession<InputType> session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, ushort localport) { session = null; return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }
        public static GGPOErrorCode ggpo_start_synctest(ref GGPOSession<InputType> session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, int frames) { session = null; return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }
        public static GGPOErrorCode ggpo_start_spectating(ref GGPOSession<InputType> session, GGPOSessionCallbacks cb, string game, int num_players, int input_size, ushort local_port, string host_ip, ushort host_port) { session = null; return GGPOErrorCode.GGPO_ERRORCODE_UNSUPPORTED; }

        #endregion

        #region TODO: Remove these public APIs for a proper C# API as they were only passthroughs to check if session (this) was null.

        // public GGPOErrorCode ggpo_add_player(/* GGPOSession session, */ GGPOPlayer player, ref GGPOPlayerHandle handle)
        // {
        //     return AddPlayer(player, ref handle);
        // }
        // public GGPOErrorCode ggpo_close_session()
        // {
        //     // not needed in C#, destroys the session by calling delete
        //     return GGPOErrorCode.GGPO_OK;
        // }
        // public GGPOErrorCode ggpo_set_frame_delay(/* GGPOSession session, */ GGPOPlayerHandle player, int frame_delay)
        // {
        //     return SetFrameDelay(player, frame_delay);
        // }
        // public GGPOErrorCode ggpo_idle(/* GGPOSession session, */ GGPOPlayerHandle player, int timeout)
        // {
        //     return DoPoll(timeout);
        // }
        // public GGPOErrorCode ggpo_add_local_input(/* GGPOSession session, */ GGPOPlayerHandle player, InputType values, int size)
        // {
        //     return AddLocalInput(player, values, size);
        // }
        // public GGPOErrorCode ggpo_synchronize_input(/* GGPOSession session, */ InputType values, int size, ref int disconnect_flags)
        // {
        //     return SyncInput(values, size, ref disconnect_flags);
        // }
        // public GGPOErrorCode ggpo_disconnect_player(/* GGPOSession session, */ GGPOPlayerHandle handle)
        // {
        //     return DisconnectPlayer(handle);
        // }
        // public GGPOErrorCode ggpo_advance_frame(/* GGPOSession session, */)
        // {
        //     return IncrementFrame();
        // }
        // public GGPOErrorCode ggpo_get_network_stats(/* GGPOSession session, */ GGPOPlayerHandle player, GGPONetworkStats stats)
        // {
        //     return GetNetworkStats(stats, player);
        // }
        // public GGPOErrorCode ggpo_set_disconnect_timeout(/* GGPOSession session, */ int timeout)
        // {
        //     return SetDisconnectTimeout(timeout);
        // }
        // public GGPOErrorCode ggpo_set_disconnect_notify_start(/* GGPOSession session, */ int timeout)
        // {
        //     return SetDisconnectNotifyStart(timeout);
        // }

        #endregion
    }
}
