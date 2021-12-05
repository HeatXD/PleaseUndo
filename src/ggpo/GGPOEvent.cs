namespace PleaseUndo
{
    public enum GGPOEventCode
    {
        GGPO_EVENTCODE_CONNECTED_TO_PEER = 1000,
        GGPO_EVENTCODE_SYNCHRONIZING_WITH_PEER = 1001,
        GGPO_EVENTCODE_SYNCHRONIZED_WITH_PEER = 1002,
        GGPO_EVENTCODE_RUNNING = 1003,
        GGPO_EVENTCODE_DISCONNECTED_FROM_PEER = 1004,
        GGPO_EVENTCODE_TIMESYNC = 1005,
        GGPO_EVENTCODE_CONNECTION_INTERRUPTED = 1006,
        GGPO_EVENTCODE_CONNECTION_RESUMED = 1007,
    }

    public abstract class GGPOEvent
    {
        public GGPOEventCode code;
    }

    public class GGPOConnectedEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
    }

    public class GGPOSynchronizingEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
        public int count;
        public int total;
    }

    public class GGPOSynchronizedEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
    }

    public class GGPODisconnectedEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
    }

    public class GGPOTimesyncEvent : GGPOEvent
    {
        public int frames_ahead;
    }

    public class GGPOConnectionInterruptedEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
        public int disconnect_timeout;
    }

    public class GGPOConnectionResumedEvent : GGPOEvent
    {
        public GGPOPlayerHandle player;
    }
}
