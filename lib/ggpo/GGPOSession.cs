namespace PleaseUndo
{
    public abstract class GGPOSession<InputType>
    {
        GGPOErrorCode DoPoll(int timeout) { return GGPOErrorCode::GGPO_OK; }
        GGPOErrorCode AddPlayer(GGPOPlayer player, GGPOPlayerHandle handle);
        GGPOErrorCode AddLocalInput(GGPOPlayerHandle player, GameInput<InputType> values, int size);
        GGPOErrorCode SyncInput(GameInput<InputType> values, int size, int[] disconnect_flags);
        GGPOErrorCode IncrementFrame() { return GGPOErrorCode::GGPO_OK; }
        GGPOErrorCode Chat(string text) { return GGPOErrorCode::GGPO_OK; }
        GGPOErrorCode DisconnectPlayer(GGPOPlayerHandle handle) { return GGPOErrorCode::GGPO_OK; }
        GGPOErrorCode GetNetworkStats(GGPONetworkStats stats, GGPOPlayerHandle handle) { return GGPOErrorCode::GGPO_OK; }
        GGPOErrorCode Logv(string format, params string[] list) { /* ::Logv(fmt, list); */ return GGPOErrorCode::GGPO_OK; }

        GGPOErrorCode SetFrameDelay(GGPOPlayerHandle player, int delay) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }
        GGPOErrorCode SetDisconnectTimeout(int timeout) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }
        GGPOErrorCode SetDisconnectNotifyStart(int timeout) { return GGPOErrorCode::GGPO_ERRORCODE_UNSUPPORTED; }
    }
}
