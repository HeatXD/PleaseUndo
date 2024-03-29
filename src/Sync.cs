using System.Runtime.CompilerServices;

namespace PleaseUndo
{
    public class Sync
    {
        public const int MAX_PREDICTION_FRAMES = 8;

        public struct Event
        {
            public int type;
            public GameInput input;
        }
        public struct Config
        {
            public PUSessionCallbacks callbacks;
            public int num_prediction_frames;
            public int num_players;
            public int input_size;
        }

        public class SavedFrame
        {
            public byte[] buf;
            public int cbuf = 0;
            public int frame = -1;
            public int checksum = 0;
        };

        protected class SavedState
        {
            public SavedFrame[] frames = new SavedFrame[MAX_PREDICTION_FRAMES + 2];
            public int head = 0;

            public SavedState()
            {
                for (var i = 0; i < frames.Length; i++)
                {
                    frames[i] = new SavedFrame();
                }
            }
        }

        protected Config _config;
        protected SavedState _savedstate = new SavedState(); // be cautious: should be a struct
        protected PUSessionCallbacks _callbacks;

        protected bool _rollingback;
        protected int _framecount = 0;
        protected int _last_confirmed_frame = -1;
        protected int _max_prediction_frames = 0;

        protected NetMsg.ConnectStatus[] _local_connect_status;
        protected RingBuffer<Event> _event_queue = new RingBuffer<Event>(32);
        protected InputQueue[] _input_queues = null;

        public Sync(ref NetMsg.ConnectStatus[] connect_status)
        {
            _local_connect_status = connect_status;
        }

        public void Init(Config config)
        {
            _config = config;
            _callbacks = config.callbacks;
            _framecount = 0;
            _rollingback = false;

            _max_prediction_frames = config.num_prediction_frames;

            CreateQueues(config);
        }

        public void SetLastConfirmedFrame(int frame)
        {
            _last_confirmed_frame = frame;
            if (_last_confirmed_frame > 0)
            {
                for (int i = 0; i < _config.num_players; i++)
                {
                    _input_queues[i].DiscardConfirmedFrames(frame - 1);
                }
            }
        }

        public bool AddLocalInput(int queue, ref GameInput input)
        {
            int frames_behind = _framecount - _last_confirmed_frame;
            if (_framecount >= _max_prediction_frames && frames_behind >= _max_prediction_frames)
            {
                Logger.Log("Rejecting input from emulator: reached prediction barrier.\n");
                return false;
            }

            if (_framecount == 0)
            {
                SaveCurrentFrame();
            }

            Logger.Log("Sending undelayed local frame {0} to queue {1}.\n", _framecount, queue);
            input.frame = _framecount;
            _input_queues[queue].AddInput(ref input);

            return true;
        }

        public void AddRemoteInput(int queue, ref GameInput input)
        {
            _input_queues[queue].AddInput(ref input);
        }

        public int GetConfirmedInputs(byte[] values, int size, int frame)
        {
            int disconnect_flags = 0;

            Logger.Assert(values.Length >= _config.num_players * _config.input_size);

            Unsafe.InitBlock(ref values[0], 0, (uint)values.Length);
            for (int i = 0; i < _config.num_players; i++)
            {
                var input = new GameInput((int)GameInput.Constants.NullFrame, null, (uint)_config.input_size);
                if (_local_connect_status[i].disconnected != 0 && frame > _local_connect_status[i].last_frame)
                {
                    disconnect_flags |= (1 << i);
                }
                else
                {
                    _input_queues[i].GetConfirmedInput(frame, ref input);
                }
                Unsafe.CopyBlock(ref values[i * _config.input_size], ref input.bits[0], (uint)_config.input_size);
            }

            return disconnect_flags;
        }

        public int SynchronizeInputs(ref byte[] values, int size)
        {
            int disconnect_flags = 0;

            Logger.Assert(values.Length >= _config.num_players * _config.input_size);

            Unsafe.InitBlock(ref values[0], 0, (uint)values.Length);
            for (int i = 0; i < _config.num_players; i++)
            {
                var input = new GameInput((int)GameInput.Constants.NullFrame, null, (uint)_config.input_size);
                if (_local_connect_status[i].disconnected != 0 && _framecount > _local_connect_status[i].last_frame)
                {
                    disconnect_flags |= (1 << i);
                }
                else
                {
                    _input_queues[i].GetInput(_framecount, ref input);
                }
                Unsafe.CopyBlock(ref values[i * _config.input_size], ref input.bits[0], (uint)_config.input_size);
            }
            return disconnect_flags;
        }

        public void CheckSimulation(int timeout)
        {
            if (!CheckSimulationConsistency(out int seek_to))
            {
                AdjustSimulation(seek_to);
            }
        }

        public void IncrementFrame()
        {
            _framecount++;
            SaveCurrentFrame();
        }

        public void AdjustSimulation(int seek_to)
        {
            int framecount = _framecount;
            int count = _framecount - seek_to;

            Logger.Log("Catching up");
            _rollingback = true;

            /*
             * Flush our input queue and load the last frame.
             */
            LoadFrame(seek_to);
            Logger.Assert(_framecount == seek_to);

            /*
             * Advance frame by frame (stuffing notifications back to 
             * the master).
             */
            ResetPrediction(_framecount);
            for (int i = 0; i < count; i++)
            {
                _callbacks.OnAdvanceFrame(/*0*/);
            }
            Logger.Assert(_framecount == framecount);

            _rollingback = false;

            Logger.Log("---");
        }

        public void LoadFrame(int frame)
        {
            if (frame == _framecount)
            {
                Logger.Log("Skipping NOP.");
                return;
            }

            // Move the head pointer back and load it up
            _savedstate.head = FindSavedFrameIndex(frame);
            ref SavedFrame state = ref _savedstate.frames[_savedstate.head]; // SavedFrame* state = _savedstate.frames + _savedstate.head;

            Logger.Log("=== Loading frame info {0} (size: {1}  checksum: {2}).", state.frame, state.cbuf, state.checksum);

            Logger.Assert(state.buf != null && state.cbuf != 0);
            _callbacks.OnLoadGameState(state.buf, state.cbuf);

            // Reset framecount and the head of the state ring-buffer to point in
            // advance of the current frame (as if we had just finished executing it).
            _framecount = state.frame;
            _savedstate.head = (_savedstate.head + 1) % _savedstate.frames.Length;
        }

        public void SaveCurrentFrame()
        {
            ref SavedFrame state = ref _savedstate.frames[_savedstate.head]; // SavedFrame* state = _savedstate.frames + _savedstate.head;
            state.buf = null;
            state.frame = _framecount;
            _callbacks.OnSaveGameState(ref state.buf, ref state.cbuf, ref state.checksum, state.frame);

            Logger.Log("=== Saved frame info {0} (size: {1}  checksum: {2}).\n", state.frame, state.cbuf, state.checksum);
            _savedstate.head = (_savedstate.head + 1) % _savedstate.frames.Length;
        }

        public ref SavedFrame GetLastSavedFrame()
        {
            int i = _savedstate.head - 1;
            if (i < 0)
            {
                i = _savedstate.frames.Length - 1;
            }
            return ref _savedstate.frames[i];
        }

        protected int FindSavedFrameIndex(int frame)
        {
            int i, count = _savedstate.frames.Length;
            for (i = 0; i < count; i++)
            {
                if (_savedstate.frames[i].frame == frame)
                {
                    break;
                }
            }
            Logger.Assert(i != count);
            return i;
        }

        protected bool CreateQueues(Config config)
        {
            _input_queues = new InputQueue[_config.num_players];

            for (int i = 0; i < _config.num_players; i++)
            {
                _input_queues[i] = new InputQueue((uint)config.input_size, i);
            }
            return true;
        }

        protected bool CheckSimulationConsistency(out int seekTo)
        {
            int first_incorrect = (int)GameInput.Constants.NullFrame;
            for (int i = 0; i < _config.num_players; i++)
            {
                int incorrect = _input_queues[i].GetFirstIncorrectFrame();
                Logger.Log("considering incorrect frame {0} reported by queue {1}.\n", incorrect, i);

                if (incorrect != (int)GameInput.Constants.NullFrame && (first_incorrect == (int)GameInput.Constants.NullFrame || incorrect < first_incorrect))
                {
                    first_incorrect = incorrect;
                }
            }

            if (first_incorrect == (int)GameInput.Constants.NullFrame)
            {
                Logger.Log("prediction ok.  proceeding.\n");
                seekTo = 0;
                return true;
            }

            seekTo = first_incorrect;
            return false;
        }

        public void SetFrameDelay(int queue, int delay)
        {
            _input_queues[queue].SetFrameDelay(delay);
        }

        protected void ResetPrediction(int frameNumber)
        {
            for (int i = 0; i < _config.num_players; i++)
            {
                _input_queues[i].ResetPrediction(frameNumber);
            }
        }

        public bool GetEvent(ref Event e)
        {
            if (_event_queue.Size() > 0)
            {
                e = _event_queue.Front();
                _event_queue.Pop();
                return true;
            }
            return false;
        }

        public int GetFrameCount()
        {
            return _framecount;
        }

        public bool InRollback()
        {
            return _rollingback;
        }
    }
}
