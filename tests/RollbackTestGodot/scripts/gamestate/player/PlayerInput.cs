using MessagePack;

[MessagePackObject]
public struct PlayerInput
{
    [Key(0)]
    public ushort InputState;// 16 bits so 16 bools/buttons. index 0 to 15
    public PlayerInput(ushort input = 0)
    {
        this.InputState = input;
    }
    public PlayerInput(PlayerInput input)
    {
        this.InputState = input.InputState;
    }
    public void SetInputBit(int bitIndex, bool state)
    {
        if (bitIndex < 0 || bitIndex > 15) return; // invalid bit dont do any actions
        if (state)
        {
            //set the bit to true
            this.InputState |= (ushort)(1 << bitIndex);
        }
        else
        {
            //set the bit to false
            //~ will return a negative number, so casting to int is necessary
            int i = this.InputState;
            i &= ~(1 << bitIndex);
            this.InputState = (ushort)i;
        }
    }
    public bool IsInputBitSet(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > 15) return false;
        return (this.InputState & (1 << bitIndex)) > 0;
    }

}