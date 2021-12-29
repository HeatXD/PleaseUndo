namespace PleaseUndo
{
    public struct PUSessionCallbacks
    {
        public delegate bool OnEventDelegate(PUEvent ev);
        public delegate bool BeginGameDelegate();
        public delegate bool AdvanceFrameDelegate();
        public delegate bool LoadGameStateDelegate(byte[] buffer, int len);
        public delegate bool SaveGameStateDelegate(ref byte[] buffer, ref int len, ref int checksum, int frame);

        public OnEventDelegate OnEvent;
        public BeginGameDelegate OnBeginGame;
        public AdvanceFrameDelegate OnAdvanceFrame;
        public SaveGameStateDelegate OnSaveGameState;
        public LoadGameStateDelegate OnLoadGameState;
    }

    public abstract class PUSession
    {
        public const uint PU_MAX_PLAYERS = 4;
        public const uint PU_MAX_SPECTATORS = 32;
        public const uint PU_MAX_PREDICTION_FRAMES = 8;
        public const uint PU_SPECTATOR_INPUT_INTERVAL = 4;

        public virtual PUErrorCode AddLocalPlayer(PUPlayer player, ref PUPlayerHandle handle) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode AddRemotePlayer(PUPlayer player, ref PUPlayerHandle handle, /* ref */ IPeerNetAdapter peerNetAdapter) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode SyncInput(ref byte[] values, int size, ref int disconnect_flags) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode AddLocalInput(PUPlayerHandle player, byte[] values, int size) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode Chat(string text) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode DoPoll(int timeout) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode IncrementFrame() { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode GetNetworkStats(ref PUNetworkStats stats, PUPlayerHandle handle) { return PUErrorCode.PU_OK; }
        public virtual PUErrorCode DisconnectPlayer(PUPlayerHandle handle) { return PUErrorCode.PU_OK; }

        public virtual PUErrorCode SetFrameDelay(PUPlayerHandle player, int delay) { return PUErrorCode.PU_ERRORCODE_UNSUPPORTED; }
        public virtual PUErrorCode SetDisconnectTimeout(int timeout) { return PUErrorCode.PU_ERRORCODE_UNSUPPORTED; }
        public virtual PUErrorCode SetDisconnectNotifyStart(int timeout) { return PUErrorCode.PU_ERRORCODE_UNSUPPORTED; }
    }
}
