using MessagePack;

[MessagePackObject]
public struct PlayerInput
{
    [Key(0)]
    public byte InputState;// 8 bits so 8 bools/buttons. index 0 to 7
    public PlayerInput(byte input = 0)
    {
        this.InputState = input;
    }
    public PlayerInput(PlayerInput input)
    {
        this.InputState = input.InputState;
    }
    public void SetInputBit(int bitIndex, bool state)
    {
        if (bitIndex < 0 || bitIndex > 7) return; // invalid bit dont do any actions
        if (state)
        {
            //set the bit to true
            this.InputState |= (byte)(1 << bitIndex);
        }
        else
        {
            //set the bit to false
            //~ will return a negative number, so casting to int is necessary
            int i = this.InputState;
            i &= ~(1 << bitIndex);
            this.InputState = (byte)i;
        }
    }
    public bool IsInputBitSet(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > 7) return false;
        return (this.InputState & (1 << bitIndex)) > 0;
    }

}