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
        GGPOEventCode code;
    }

    public class GGPOConnectedEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
    }

    public class GGPOSynchronizingEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
        int count;
        int total;
    }

    public class GGPOSynchronizedEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
    }

    public class GGPODisconnectedEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
    }

    public class GGPOTimesyncEvent : GGPOEvent
    {
        int frames_ahead;
    }

    public class GGPOConnectionInterruptedEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
        int disconnect_timeout;
    }

    public class GGPOConnectionResumedEvent : GGPOEvent
    {
        GGPOPlayerHandle player;
    }
}
