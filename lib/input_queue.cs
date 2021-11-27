using System.Collections.Generic;

namespace PleaseUndo
{
    public class InputQueue<InputType>
    {
        const int INPUT_QUEUE_LENGTH = 128;
        const int DEFAULT_INPUT_SIZE = 4;

        protected int _id;
        protected int _head;
        protected int _tail;
        protected int _length;
        protected bool _first_frame;

        protected int _last_user_added_frame;
        protected int _last_added_frame;
        protected int _first_incorrect_frame;
        protected int _last_frame_requested;

        protected int _frame_delay;

        protected List<GameInput<InputType>> _inputs = new List<GameInput<InputType>>(INPUT_QUEUE_LENGTH);
        protected GameInput<InputType> _prediction;

        public InputQueue(int input_size = DEFAULT_INPUT_SIZE)
        {
            Init(-1, input_size);
        }

        public void Init(int id, int input_size)
        {
            _id = id;
            _head = 0;
            _tail = 0;
            _length = 0;
            _frame_delay = 0;
            _first_frame = true;
            _last_user_added_frame = (int)GameInput<InputType>.Constants.NullFrame;
            _first_incorrect_frame = (int)GameInput<InputType>.Constants.NullFrame;
            _last_frame_requested = (int)GameInput<InputType>.Constants.NullFrame;
            _last_added_frame = (int)GameInput<InputType>.Constants.NullFrame;

            _prediction.Init((int)GameInput<InputType>.Constants.NullFrame, null, input_size);

            // memset(_inputs, 0, sizeof _inputs);
            for (int i = 0; i < _inputs.Count; i++)
            {
                // _inputs[i].size = input_size; // Not needed in C#
            }
        }
        public int GetLastConfirmedFrame()
        {
            Logger.Log("returning last confirmed frame {0}.\n", _last_added_frame);
            return _last_added_frame;
        }
        public int GetFirstIncorrectFrame()
        {
            return _first_incorrect_frame;
        }
        public int GetLength()
        {
            return _length;
        }

        public void SetFrameDelay(int delay)
        {
            _frame_delay = delay;
        }
        public void ResetPrediction(int frame)
        {
            Logger.Assert(_first_incorrect_frame == (int)GameInput<InputType>.Constants.NullFrame || frame <= _first_incorrect_frame);

            Logger.Log("resetting all prediction errors back to frame {0}.\n", frame);

            /*
             * There's nothing really to do other than reset our prediction
             * state and the incorrect frame counter...
             */
            _prediction.frame = (int)GameInput<InputType>.Constants.NullFrame;
            _first_incorrect_frame = (int)GameInput<InputType>.Constants.NullFrame;
            _last_frame_requested = (int)GameInput<InputType>.Constants.NullFrame;
        }
        public void DiscardConfirmedFrames(int frame)
        {
            Logger.Assert(frame >= 0);

            if (_last_frame_requested != (int)GameInput<InputType>.Constants.NullFrame)
            {
                frame = System.Math.Min(frame, _last_frame_requested);
            }

            Logger.Log("discarding confirmed frames up to {0} (last_added:{1} length:{2} [head:{3} tail:{4}]).\n", frame, _last_added_frame, _length, _head, _tail);
            if (frame >= _last_added_frame)
            {
                _tail = _head;
            }
            else
            {
                int offset = frame - _inputs[_tail].frame + 1;

                Logger.Log("difference of {0} frames.\n", offset);
                Logger.Assert(offset >= 0);

                _tail = (_tail + offset) % INPUT_QUEUE_LENGTH;
                _length -= offset;
            }

            Logger.Log("after discarding, new tail is {0} (frame:{1}).\n", _tail, _inputs[_tail].frame);
            Logger.Assert(_length >= 0);
        }
        public bool GetConfirmedInput(int requested_frame, out GameInput<InputType>? input)
        {
            Logger.Assert(_first_incorrect_frame == (int)GameInput<InputType>.Constants.NullFrame || requested_frame < _first_incorrect_frame);

            int offset = requested_frame % INPUT_QUEUE_LENGTH;
            if (_inputs[offset].frame != requested_frame)
            {
                input = null;
                return false;
            }
            input = _inputs[offset];
            return true;
        }
        public bool GetInput(int requested_frame, out GameInput<InputType>? input)
        {
            Logger.Log("requesting input frame {0}.\n", requested_frame);

            /*
             * No one should ever try to grab any input when we have a prediction
             * error.  Doing so means that we're just going further down the wrong
             * path.  Logger.Assert this to verify that it's true.
             */
            Logger.Assert(_first_incorrect_frame == (int)GameInput<InputType>.Constants.NullFrame);

            /*
             * Remember the last requested frame number for later.  We'll need
             * this in AddInput() to drop out of prediction mode.
             */
            _last_frame_requested = requested_frame;

            Logger.Assert(requested_frame >= _inputs[_tail].frame);

            if (_prediction.frame == (int)GameInput<InputType>.Constants.NullFrame)
            {
                /*
                 * If the frame requested is in our range, fetch it out of the queue and
                 * return it.
                 */
                int offset = requested_frame - _inputs[_tail].frame;

                if (offset < _length)
                {
                    offset = (offset + _tail) % INPUT_QUEUE_LENGTH;
                    Logger.Assert(_inputs[offset].frame == requested_frame);
                    input = _inputs[offset];
                    Logger.Log("returning confirmed frame number {0}.\n", ((GameInput<InputType>)input).frame);
                    return true;
                }

                /*
                 * The requested frame isn't in the queue.  Bummer.  This means we need
                 * to return a prediction frame.  Predict that the user will do the
                 * same thing they did last time.
                 */
                if (requested_frame == 0)
                {
                    Logger.Log("basing new prediction frame from nothing, you're client wants frame 0.\n");
                    _prediction.Erase();
                }
                else if (_last_added_frame == (int)GameInput<InputType>.Constants.NullFrame)
                {
                    Logger.Log("basing new prediction frame from nothing, since we have no frames yet.\n");
                    _prediction.Erase();
                }
                else
                {
                    Logger.Log("basing new prediction frame from previously added frame (queue entry:{0}, frame:{1}).\n", PREVIOUS_FRAME(_head), _inputs[PREVIOUS_FRAME(_head)].frame);
                    _prediction = _inputs[PREVIOUS_FRAME(_head)];
                }
                _prediction.frame++;
            }

            Logger.Assert(_prediction.frame >= 0);

            /*
             * If we've made it this far, we must be predicting.  Go ahead and
             * forward the prediction frame contents.  Be sure to return the
             * frame number requested by the client, though.
             */
            input = _prediction;
            _prediction.frame = requested_frame; // was input.frame = requested_frame, but should be equivalent?
            Logger.Log("returning prediction frame number {0} ({1}).\n", ((GameInput<InputType>)input).frame, _prediction.frame);

            return false;
        }
        public void AddInput(GameInput<InputType> input)
        {
            int new_frame;

            Logger.Log("adding input frame number {0} to queue.\n", input.frame);

            /*
             * These next two lines simply verify that inputs are passed in 
             * sequentially by the user, regardless of frame delay.
             */
            Logger.Assert(_last_user_added_frame == (int)GameInput<InputType>.Constants.NullFrame || input.frame == _last_user_added_frame + 1);
            _last_user_added_frame = input.frame;

            /*
             * Move the queue head to the correct point in preparation to
             * input the frame into the queue.
             */
            new_frame = AdvanceQueueHead(input.frame);
            if (new_frame != (int)GameInput<InputType>.Constants.NullFrame)
            {
                AddDelayedInputToQueue(input, new_frame);
            }

            /*
             * Update the frame number for the input.  This will also set the
             * frame to GameInput::NullFrame for frames that get dropped (by
             * design).
             */
            input.frame = new_frame;
        }


        protected int AdvanceQueueHead(int frame)
        {
            Logger.Log("advancing queue head to frame {0}.\n", frame);

            int expected_frame = _first_frame ? 0 : _inputs[PREVIOUS_FRAME(_head)].frame + 1;

            frame += _frame_delay;

            if (expected_frame > frame)
            {
                /*
                 * This can occur when the frame delay has dropped since the last
                 * time we shoved a frame into the system.  In this case, there's
                 * no room on the queue.  Toss it.
                 */
                Logger.Log("Dropping input frame {0} (expected next frame to be {1}).\n", frame, expected_frame);
                return (int)GameInput<InputType>.Constants.NullFrame;
            }

            while (expected_frame < frame)
            {
                /*
                 * This can occur when the frame delay has been increased since the last
                 * time we shoved a frame into the system.  We need to replicate the
                 * last frame in the queue several times in order to fill the space
                 * left.
                 */
                Logger.Log("Adding padding frame {0} to account for change in frame delay.\n", expected_frame);
                var last_frame = _inputs[PREVIOUS_FRAME(_head)];
                AddDelayedInputToQueue(last_frame, expected_frame);
                expected_frame++;
            }

            Logger.Assert(frame == 0 || frame == _inputs[PREVIOUS_FRAME(_head)].frame + 1);
            return frame;
        }
        protected void AddDelayedInputToQueue(GameInput<InputType> input, int frame_number)
        {
            Logger.Log("adding delayed input frame number {0} to queue.\n", frame_number);

            // Logger.Assert(input.size == _prediction.size); // Not needed in C#

            Logger.Assert(_last_added_frame == (int)GameInput<InputType>.Constants.NullFrame || frame_number == _last_added_frame + 1);

            Logger.Assert(frame_number == 0 || _inputs[PREVIOUS_FRAME(_head)].frame == frame_number - 1);

            /*
             * Add the frame to the back of the queue
             */
            var input_with_frame = input;
            input_with_frame.frame = frame_number; // _inputs[_head].frame = frame_number;
            _inputs[_head] = input_with_frame;
            _head = (_head + 1) % INPUT_QUEUE_LENGTH;
            _length++;
            _first_frame = false;

            _last_added_frame = frame_number;

            if (_prediction.frame != (int)GameInput<InputType>.Constants.NullFrame)
            {
                Logger.Assert(frame_number == _prediction.frame);

                /*
                 * We've been predicting...  See if the inputs we've gotten match
                 * what we've been predicting.  If so, don't worry about it.  If not,
                 * remember the first input which was incorrect so we can report it
                 * in GetFirstIncorrectFrame()
                 */
                if (_first_incorrect_frame == (int)GameInput<InputType>.Constants.NullFrame && !_prediction.Equal(input, true))
                {
                    Logger.Log("frame {0} does not match prediction.  marking error.\n", frame_number);
                    _first_incorrect_frame = frame_number;
                }

                /*
                 * If this input is the same frame as the last one requested and we
                 * still haven't found any mis-predicted inputs, we can dump out
                 * of predition mode entirely!  Otherwise, advance the prediction frame
                 * count up.
                 */
                if (_prediction.frame == _last_frame_requested && _first_incorrect_frame == (int)GameInput<InputType>.Constants.NullFrame)
                {
                    Logger.Log("prediction is correct!  dumping out of prediction mode.\n");
                    _prediction.frame = (int)GameInput<InputType>.Constants.NullFrame;
                }
                else
                {
                    _prediction.frame++;
                }
            }
            Logger.Assert(_length <= INPUT_QUEUE_LENGTH);
        }

        int PREVIOUS_FRAME(int offset) => (((offset) == 0) ? (INPUT_QUEUE_LENGTH - 1) : ((offset) - 1));
    }
}
