using System.Collections.Generic;

namespace PleaseUndo
{
    public class Sync<InputType>
    {
        const int MAX_PREDICTION_FRAMES = 8;

        public struct Event
        {
            public int type;
            public GameInput<InputType> input;
        }
        public struct Config
        {
            public GGPOSessionCallbacks callbacks;
            public int num_prediction_frames;
            public int num_players;
            public int input_size;
        }
        protected class SavedFrame
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
        }

        protected GGPOSessionCallbacks _callbacks;
        protected SavedState _savedstate = new SavedState(); // be cautious: should be a struct
        protected Config _config;

        protected bool _rollingback;
        protected int _last_confirmed_frame = -1;
        protected int _framecount = 0;
        protected int _max_prediction_frames = 0;

        protected List<InputQueue<InputType>> _input_queues = null;

        protected RingBuffer<Event> _event_queue = new RingBuffer<Event>(32);
        //protected UdpMsg::connect_status[] _local_connect_status;

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

        public void SetFrameDelay(int queue, int delay)
        {
            _input_queues[queue].SetFrameDelay(delay);
        }

        public bool AddLocalInput(int queue, GameInput<InputType> input)
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

            Logger.Log("Sending undelayed local frame %d to queue %d.\n", _framecount, queue);
            input.frame = _framecount;
            _input_queues[queue].AddInput(input);

            return true;
        }
        public void AddRemoteInput(int queue, GameInput<InputType> input)
        {
            _input_queues[queue].AddInput(input);
        }
        public int GetConfirmedInputs(InputType values, int size, int frame)
        {
            // int disconnect_flags = 0;
            // char* output = (char*)values;

            // Logger.Assert(size >= _config.num_players * _config.input_size);

            // memset(output, 0, size);
            // for (int i = 0; i < _config.num_players; i++)
            // {
            //     GameInput input;
            //     if (_local_connect_status[i].disconnected && frame > _local_connect_status[i].last_frame)
            //     {
            //         disconnect_flags |= (1 << i);
            //         input.erase();
            //     }
            //     else
            //     {
            //         _input_queues[i].GetConfirmedInput(frame, &input);
            //     }
            //     memcpy(output + (i * _config.input_size), input.bits, _config.input_size);
            // }
            // return disconnect_flags;
            return 0;
        }
        public int SynchronizeInputs(InputType values, int size)
        {
            // int disconnect_flags = 0;
            // char* output = (char*)values;

            // Logger.Assert(size >= _config.num_players * _config.input_size);

            // memset(output, 0, size);
            // for (int i = 0; i < _config.num_players; i++)
            // {
            //     GameInput input;
            //     if (_local_connect_status[i].disconnected && _framecount > _local_connect_status[i].last_frame)
            //     {
            //         disconnect_flags |= (1 << i);
            //         input.erase();
            //     }
            //     else
            //     {
            //         _input_queues[i].GetInput(_framecount, &input);
            //     }
            //     memcpy(output + (i * _config.input_size), input.bits, _config.input_size);
            // }
            // return disconnect_flags;
            return 0;
        }
        public void CheckSimulation(int timeout)
        {
            int seek_to;
            if (!CheckSimulationConsistency(out seek_to))
            {
                AdjustSimulation(seek_to);
            }
        }
        public void AdjustSimulation(int seek_to)
        {
            int framecount = _framecount;
            int count = _framecount - seek_to;

            Logger.Log("Catching up\n");
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
                // _callbacks.advance_frame(0);
            }
            Logger.Assert(_framecount == framecount);

            _rollingback = false;

            Logger.Log("---\n");
        }
        public void IncrementFrame()
        {
            _framecount++;
            SaveCurrentFrame();
        }
        public int GetFrameCount() { return _framecount; }
        public bool InRollback() { return _rollingback; }
        public bool GetEvent(out Event? e)
        {
            if (_event_queue.size() > 0)
            {
                e = _event_queue.front();
                _event_queue.pop();
                return true;
            }
            e = null;
            return false;
        }

        protected void LoadFrame(int frame)
        {
            if (frame == _framecount)
            {
                Logger.Log("Skipping NOP.\n");
                return;
            }

            // Move the head pointer back and load it up
            _savedstate.head = FindSavedFrameIndex(frame);
            SavedFrame state = _savedstate.frames[_savedstate.head]; // SavedFrame* state = _savedstate.frames + _savedstate.head;

            Logger.Log("=== Loading frame info %d (size: %d  checksum: %08x).\n", state.frame, state.cbuf, state.checksum);

            Logger.Assert(state.buf != null && state.cbuf != 0);
            // _callbacks.load_game_state(state->buf, state->cbuf);

            // Reset framecount and the head of the state ring-buffer to point in
            // advance of the current frame (as if we had just finished executing it).
            _framecount = state.frame;
            _savedstate.head = (_savedstate.head + 1) % _savedstate.frames.GetLength(0);
        }
        protected void SaveCurrentFrame()
        {
            SavedFrame state = _savedstate.frames[_savedstate.head]; // SavedFrame* state = _savedstate.frames + _savedstate.head;
            if (state.buf != null)
            {
                //  _callbacks.free_buffer(state.buf); // not needed in C#
                state.buf = null;
            }
            state.frame = _framecount;
            // _callbacks.save_game_state(out state.buf, out state.cbuf, out state.checksum, state.frame);

            Logger.Log("=== Saved frame info {0} (size: {1}  checksum: {2}).\n", state.frame, state.cbuf, state.checksum);
            _savedstate.head = (_savedstate.head + 1) % _savedstate.frames.GetLength(0);
        }
        protected int FindSavedFrameIndex(int frame)
        {
            int i, count = _savedstate.frames.GetLength(0);
            for (i = 0; i < count; i++)
            {
                if (_savedstate.frames[i].frame == frame)
                {
                    break;
                }
            }
            if (i == count)
            {
                Logger.Assert(false);
            }
            return i;
        }
        protected SavedFrame GetLastSavedFrame()
        {
            int i = _savedstate.head - 1;
            if (i < 0)
            {
                i = _savedstate.frames.GetLength(0) - 1;
            }
            return _savedstate.frames[i];
        }
        protected bool CreateQueues(Config config)
        {
            _input_queues = new List<InputQueue<InputType>>(_config.num_players);

            for (int i = 0; i < _config.num_players; i++)
            {
                _input_queues[i] = new InputQueue<InputType>();
                _input_queues[i].Init(i, _config.input_size);
            }
            return true;
        }
        protected bool CheckSimulationConsistency(out int seekTo)
        {
            int first_incorrect = (int)GameInput<InputType>.Constants.NullFrame;
            for (int i = 0; i < _config.num_players; i++)
            {
                int incorrect = _input_queues[i].GetFirstIncorrectFrame();
                Logger.Log("considering incorrect frame %d reported by queue %d.\n", incorrect, i);

                if (incorrect != (int)GameInput<InputType>.Constants.NullFrame && (first_incorrect == (int)GameInput<InputType>.Constants.NullFrame || incorrect < first_incorrect))
                {
                    first_incorrect = incorrect;
                }
            }

            if (first_incorrect == (int)GameInput<InputType>.Constants.NullFrame)
            {
                Logger.Log("prediction ok.  proceeding.\n");
                seekTo = 0; // ??
                return true;
            }
            seekTo = first_incorrect;
            return false;
        }
        protected void ResetPrediction(int frameNumber)
        {
            for (int i = 0; i < _config.num_players; i++)
            {
                _input_queues[i].ResetPrediction(frameNumber);
            }
        }
    }
}
