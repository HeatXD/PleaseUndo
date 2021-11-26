namespace PleaseUndo
{
    enum GGPOPlayerType
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
        public int handle;
    }
}
