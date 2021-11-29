namespace PleaseUndo
{
    public interface IPollSink
    {
        bool OnMsgPoll(ref object cookie) => true;
        bool OnLoopPoll(ref object cookie) => true;
        bool OnPeriodicPoll(ref object cookie, int last_fired) => true;
    }

    public class Poll
    {
        const int SINK_SIZE = 16;

        protected int _start_time;

        protected StaticBuffer<PollSinkCb> _msg_sinks;
        protected StaticBuffer<PollSinkCb> _loop_sinks;
        protected StaticBuffer<PollPeriodicSinkCb> _periodic_sinks;

        public Poll()
        {
            _start_time = 0;
            _msg_sinks = new StaticBuffer<PollSinkCb>(SINK_SIZE);
            _loop_sinks = new StaticBuffer<PollSinkCb>(SINK_SIZE);
            _periodic_sinks = new StaticBuffer<PollPeriodicSinkCb>(SINK_SIZE);
        }

        public void RegisterMsgLoop(ref IPollSink sink, object cookie = null)
        {
            _msg_sinks.PushBack(new PollSinkCb(ref sink, cookie));
        }

        public void RegisterPeriodic(ref IPollSink sink, int interval, object cookie = null)
        {
            _periodic_sinks.PushBack(new PollPeriodicSinkCb(ref sink, cookie, interval));
        }

        public void RegisterLoop(ref IPollSink sink, object cookie = null)
        {
            _loop_sinks.PushBack(new PollSinkCb(ref sink, cookie));
        }

        public void Run()
        {
            // this function does not get used in ggpo source. might remove later.
        }

        public bool Pump(int timeout)
        {
            int idx;
            bool finished = false;

            if (_start_time == 0)
            {
                _start_time = Platform.GetCurrentTimeMS();
            }

            int elapsed = Platform.GetCurrentTimeMS() - _start_time;
            int max_wait = ComputeWaitTime(elapsed);

            if (max_wait != int.MaxValue)
            {
                timeout = System.Math.Min(timeout, max_wait);
            }

            for (idx = 0; idx < _msg_sinks.Size(); idx++)
            {
                PollSinkCb cb = _msg_sinks[idx];
                finished = !cb.sink.OnMsgPoll(ref cb.cookie) || finished;

            }
            for (idx = 0; idx < _periodic_sinks.Size(); idx++)
            {
                PollPeriodicSinkCb cb = _periodic_sinks[idx];
                if (cb.interval + cb.last_fired <= elapsed)
                {
                    cb.last_fired = (elapsed / cb.interval) * cb.interval;
                    finished = !cb.sink.OnPeriodicPoll(ref cb.cookie, cb.last_fired) || finished;
                }
            }
            for (idx = 0; idx < _loop_sinks.Size(); idx++)
            {
                PollSinkCb cb = _loop_sinks[idx];
                finished = !cb.sink.OnLoopPoll(ref cb.cookie) || finished;
            }

            return finished;
        }

        protected int ComputeWaitTime(int elapsed)
        {
            int wait_time = int.MaxValue;
            for (int i = 0; i < _periodic_sinks.Size(); i++)
            {
                PollPeriodicSinkCb cb = _periodic_sinks[i];
                int timeout = (cb.interval + cb.last_fired) - elapsed;
                if (wait_time == int.MaxValue || timeout < wait_time)
                {
                    wait_time = System.Math.Max(timeout, 0);
                }
            }
            return wait_time;
        }

        protected class PollSinkCb
        {
            public IPollSink sink;
            public object cookie;

            public PollSinkCb(ref IPollSink sink, object cookie = null)
            {
                this.sink = sink;
                this.cookie = cookie;
            }
        }

        protected class PollPeriodicSinkCb : PollSinkCb
        {
            public int interval;
            public int last_fired;

            public PollPeriodicSinkCb(ref IPollSink sink, object cookie = null, int interval = 0)
                : base(ref sink, cookie)
            {
                this.interval = interval;
                this.last_fired = 0;
            }
        }

    }
}