namespace PleaseUndo
{
    public enum PUEventCode
    {
        PU_EVENTCODE_CONNECTED_TO_PEER = 1000,
        PU_EVENTCODE_SYNCHRONIZING_WITH_PEER = 1001,
        PU_EVENTCODE_SYNCHRONIZED_WITH_PEER = 1002,
        PU_EVENTCODE_RUNNING = 1003,
        PU_EVENTCODE_DISCONNECTED_FROM_PEER = 1004,
        PU_EVENTCODE_TIMESYNC = 1005,
        PU_EVENTCODE_CONNECTION_INTERRUPTED = 1006,
        PU_EVENTCODE_CONNECTION_RESUMED = 1007,
    }

    public class PUEvent
    {
        public PUEventCode code;
    }

    public class PUConnectedToPeerEvent : PUEvent
    {
        public PUPlayerHandle player;
    }

    public class PUSynchronizingWithPeerEvent : PUEvent
    {
        public PUPlayerHandle player;
        public int count;
        public int total;
    }

    public class PUSynchronizedWithPeerEvent : PUEvent
    {
        public PUPlayerHandle player;
    }

    public class PURunningEvent : PUEvent
    {

    }

    public class PUDisconnectedFromPeerEvent : PUEvent
    {
        public PUPlayerHandle player;
    }

    public class PUTimesyncEvent : PUEvent
    {
        public int frames_ahead;
    }

    public class PUConnectionInterruptedEvent : PUEvent
    {
        public PUPlayerHandle player;
        public int disconnect_timeout;
    }

    public class PUConnectionResumedEvent : PUEvent
    {
        public PUPlayerHandle player;
    }
}
