using System;
using System.Linq;
using System.Text;

namespace PleaseUndo
{
    public struct GameInput
    {
        // GAMEINPUT_MAX_PLAYERS * GAMEINPUT_MAX_BYTES * 8 must be less than
        // 2^NibbleSize (see BitVector)

        public const int GAMEINPUT_MAX_PLAYERS = 2;
        public const int GAMEINPUT_MAX_BYTES = 8;

        public byte[] bits;
        public uint size;
        public int frame;

        public enum Constants
        {
            NullFrame = -1
        }

        public GameInput(int frame, byte[] bits, uint size, int offset)
        {
            Logger.Assert(size > 0);
            Logger.Assert(size <= GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS);

            this.frame = frame;
            this.size = size;
            this.bits = new byte[GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS];

            if (bits != null)
            {
                Array.Copy(bits, 0, this.bits, offset, size);
            }
        }

        public GameInput(int frame, byte[] bits, uint size)
        {
            Logger.Assert(size > 0);
            Logger.Assert(size <= GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS);

            this.frame = frame;
            this.size = size;
            this.bits = new byte[GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS];

            if (bits != null)
            {
                Array.Copy(bits, this.bits, size);
            }
        }

        public bool Equal(GameInput other, bool bits_only)
        {
            if (!bits_only && frame != other.frame)
            {
                Logger.Log("frames don't match: {0}, {1}", frame, other.frame);
            }
            if (size != other.size)
            {
                Logger.Log("sizes don't match: {0}, {1}", size, other.size);
            }
            if (!bits.SequenceEqual(other.bits))
            {
                Logger.Log("bits don't match\n");
            }

            Logger.Assert(size > 0 && other.size > 0);
            return (bits_only || frame == other.frame) &&
                   size == other.size &&
                   bits.SequenceEqual(other.bits);
        }

        public void Log(string prefix, bool show_frame)
        {
            Logger.Log("{0} {1}\n", prefix, Desc(show_frame));
        }

        public string Desc(bool show_frame)
        {
            Logger.Assert(size > 0);

            string ret;
            if (show_frame)
            {
                ret = $"(frame:{frame} size:{size} ";
            }
            else
            {
                ret = $"(size:{size} ";
            }

            var builder = new StringBuilder(ret);

            for (var i = 0; i < size; i++)
            {
                builder.AppendFormat("{0}", bits[i]);
            }

            builder.Append(")");
            return builder.ToString();
        }

        public void Erase() => Array.Clear(bits, 0, bits.Length);
        public bool IsNull() => frame == (int)Constants.NullFrame;
        public bool Value(int i) => (bits[i / 8] & (1 << (i % 8))) != 0;
        public void Set(int i) => bits[i / 8] |= (byte)(1 << (i % 8));
        public void Clear(int i) => bits[i / 8] &= (byte)~(1 << (i % 8));
    }
}
