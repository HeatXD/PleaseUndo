namespace PleaseUndo
{
   public enum GGPOPlayerType
    {
        LOCAL,
        REMOTE,
        SPECTATOR
    }

    public struct GGPOPlayer
    {
        public int player_num;
        public GGPOPlayerType type;
    }

    public struct GGPOPlayerHandle
    {
        public const int GGPO_INVALID_HANDLE = -1;

        public int handle;
    }
}
