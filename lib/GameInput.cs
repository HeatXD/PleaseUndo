using System.Diagnostics;
using System.Linq;

namespace PleaseUndo
{
    public struct GameInput<T>
    {
        const int GAMEINPUT_MAX_PLAYERS = 2;

        T[] inputs;
        int frame;

        enum Constants
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
                Log("frames don't match");
            }
            if (Enumerable.SequenceEqual(inputs, game_input.inputs))
            {
                Log("inputs don't match\n");
            }
            return (inputs_only ||
             game_input.frame == frame &&
             Enumerable.SequenceEqual(inputs, game_input.inputs));
        }

        public void Erase() => inputs = new T[GAMEINPUT_MAX_PLAYERS];
        public bool IsNull() => frame == (int)Constants.NullFrame;
        public bool Value(int i) => inputs[i] != null;
        public void Set(int i, T input) => inputs[i] = input;
        public void Clear(int i) => inputs[i] = default(T);
        public void Desc() => System.Console.WriteLine("TODO");
        public void Log(string msg) => System.Console.WriteLine(msg);
    }
}