namespace PleaseUndo
{
    public static class BitVector
    {
        const int NibbleSize = 8;

        public static void SetBit(byte[] vector, ref int offset)
        {
            vector[offset / 8] |= (byte)(1 << (offset % 8));
            offset++;
        }

        public static void ClearBit(byte[] vector, ref int offset)
        {
            vector[offset / 8] &= (byte)~(1 << (offset % 8));
            offset++;
        }

        public static void WriteNibblet(byte[] vector, int nibble, ref int offset)
        {
            Logger.Assert(nibble < (1 << NibbleSize));
            for (int i = 0; i < NibbleSize; i++)
            {
                if ((nibble & (1 << i)) != 0)
                {
                    SetBit(vector, ref offset);
                }
                else
                {
                    ClearBit(vector, ref offset);
                }
            }
        }

        public static int ReadBit(byte[] vector, ref int offset)
        {
            int retval = (vector[offset / 8] & (1 << (offset % 8))) > 0 ? 1 : 0;
            offset++;
            return retval;
        }

        public static int ReadNibblet(byte[] vector, ref int offset)
        {
            int nibblet = 0;
            for (int i = 0; i < NibbleSize; i++)
            {
                nibblet |= ReadBit(vector, ref offset) << i;
            }
            return nibblet;
        }
    }
}
