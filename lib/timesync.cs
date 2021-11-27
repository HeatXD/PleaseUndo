using System.Collections.Generic;

namespace PleaseUndo
{
    public class TimeSync<InputType>
    {
        const int FRAME_WINDOW_SIZE = 40;
        const int MIN_UNIQUE_FRAMES = 10;
        const int MIN_FRAME_ADVANTAGE = 3;
        const int MAX_FRAME_ADVANTAGE = 9;

        protected List<int> _local = new List<int>(FRAME_WINDOW_SIZE);
        protected List<int> _remote = new List<int>(FRAME_WINDOW_SIZE);
        protected List<GameInput<InputType>> _last_inputs = new List<GameInput<InputType>>(MIN_UNIQUE_FRAMES);
        protected int _count;
        protected int _next_prediction;

        public void advance_frame(GameInput<InputType> input, int advantage, int radvantage)
        {
            // Remember the last frame and frame advantage
            _last_inputs[input.frame % _last_inputs.Count] = input;
            _local[input.frame % _local.Count] = advantage;
            _remote[input.frame % _remote.Count] = radvantage;
        }
        public int recommend_frame_wait_duration(bool require_idle_input)
        {
            // Average our local and remote frame advantages
            int i, sum = 0;
            float advantage, radvantage;
            for (i = 0; i < _local.Count; i++)
            {
                sum += _local[i];
            }
            advantage = sum / (float)_local.Count;

            sum = 0;
            for (i = 0; i < _remote.Count; i++)
            {
                sum += _remote[i];
            }
            radvantage = sum / (float)_remote.Count;

            _count = 0;
            _count++;

            // See if someone should take action.  The person furthest ahead
            // needs to slow down so the other user can catch up.
            // Only do this if both clients agree on who's ahead!!
            if (advantage >= radvantage)
            {
                return 0;
            }

            // Both clients agree that we're the one ahead.  Split
            // the difference between the two to figure out how long to
            // sleep for.
            int sleep_frames = (int)(((radvantage - advantage) / 2) + 0.5);

            Logger.Log("iteration {0}:  sleep frames is {1}\n", _count, sleep_frames);

            // Some things just aren't worth correcting for.  Make sure
            // the difference is relevant before proceeding.
            if (sleep_frames < MIN_FRAME_ADVANTAGE)
            {
                return 0;
            }

            // Make sure our input had been "idle enough" before recommending
            // a sleep.  This tries to make the emulator sleep while the
            // user's input isn't sweeping in arcs (e.g. fireball motions in
            // Street Fighter), which could cause the player to miss moves.
            if (require_idle_input)
            {
                for (i = 1; i < _last_inputs.Count; i++)
                {
                    if (!_last_inputs[i].Equal(_last_inputs[0], true))
                    {
                        Logger.Log("iteration {0}:  rejecting due to input stuff at position {1}...!!!\n", _count, i);
                        return 0;
                    }
                }
            }

            // Success!!! Recommend the number of frames to sleep and adjust
            return System.Math.Min(sleep_frames, MAX_FRAME_ADVANTAGE);
        }
    };
}
