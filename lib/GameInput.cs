using System.Linq;

namespace PleaseUndo
{
    public struct GameInput<T>
    {
        const int GAMEINPUT_MAX_PLAYERS = 2;

        T[] inputs;
        public int frame;

        public enum Constants
        {
            NullFrame = -1
        }

        public void Init(int frame, T[] game_inputs, int offset)
        {
            this.inputs = new T[GAMEINPUT_MAX_PLAYERS];
            this.frame = frame;
        }

        public void Init(int frame, T[] game_inputs)
        {
            this.inputs = game_inputs;
            this.frame = frame;
        }

        public bool Equal(GameInput<T> game_input, bool inputs_only)
        {
            if (!inputs_only && frame != game_input.frame)
            {
                Logger.Log("frames don't match: {0}, {1}\n", frame, game_input.frame);
            }
            if (Enumerable.SequenceEqual(inputs, game_input.inputs))
            {
                Logger.Log("inputs don't match: {0}, {1}\n", inputs, game_input.inputs);
            }
            return (inputs_only ||
             game_input.frame == frame &&
             Enumerable.SequenceEqual(inputs, game_input.inputs));
        }

        public void Log(string prefix, bool show_frame)
        {
            Logger.Log("{0} {1}\n", prefix, Desc(show_frame));
        }

        public string Desc(bool show_frame)
        {
            string desc = "";
            if (show_frame)
            {
                desc += string.Format("frame: {0} ", frame);
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                if (Value(i))
                {
                    desc += string.Format("{0} ", inputs[i]);
                }
            }
            return desc;
        }
        public void Erase() => inputs = new T[GAMEINPUT_MAX_PLAYERS];
        public bool IsNull() => frame == (int)Constants.NullFrame;
        public bool Value(int i) => inputs[i] != null;
        public void Set(int i, T input) => inputs[i] = input;
        public void Clear(int i) => inputs[i] = default(T);
    }
}