using System;

namespace PleaseUndo
{
    struct GameInput<T>
    {
        const int GAMEINPUT_MAX_PLAYERS = 2;
        enum Constants
        {
            NullFrame = -1
        }
        T[] inputs = new T[GAMEINPUT_MAX_PLAYERS];
        int frame;
        bool IsNull() => frame == Constants.NullFrame;
        void Init(int frame, T input, int offset)
        {

        }
        void Init(int frame, T[] inputs)
        {
            this.frame = frame;
        }
        bool Value(int i) => input[i] != null;
        void Set(int i, T input) => inputs[i] = input;
        void Clear(int i) => inputs[i] = null;
        void Erase() => Array.forEach(inputs, input = null);
        void Desc();
        void Log();
        bool Equal();
    }
}