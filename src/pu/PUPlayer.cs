namespace PleaseUndo
{
    public enum PUPlayerType
    {
        LOCAL,
        REMOTE,
        SPECTATOR
    }

    public struct PUPlayer
    {
        public int player_num;
        public PUPlayerType type;
    }

    public struct PUPlayerHandle
    {
        public const int PU_INVALID_HANDLE = -1;

        public int handle;
    }
}
