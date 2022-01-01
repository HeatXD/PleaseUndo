using AF = Abacus.Fixed64Precision;
using MessagePack;

[MessagePackObject]
public class Player
{
    [Key(0)]
    public int ID; // ID of the player corresponds to the player's index in the array. an id shouldnt be negative
    [Key(1)]
    public AF.Vector2 Position;
    [Key(2)]
    public AF.Vector2 Velocity;
    [Key(3)]
    public AF.Vector2 Acceleration;
    [Key(4)]
    public AF.Fixed64 MoveSpeed;

    public Player(int ID)
    {
        this.ID = ID;
        this.Position = new AF.Vector2(ID == 1 ? 300 : 500, 400);
        this.Velocity = new AF.Vector2();
        this.Acceleration = new AF.Vector2();
        this.MoveSpeed = AF.Fixed64.CreateFrom(5);
    }
    public Player(Player p)
    {
        this.ID = p.ID;
        this.Position = p.Position;
        this.Velocity = p.Velocity;
        this.Acceleration = p.Acceleration;
        this.MoveSpeed = p.MoveSpeed;
    }

    public void Update(byte[] playerInputs)
    {
        UseInput(playerInputs);
        ProcessMotion();
    }

    private void ProcessMotion()
    {
        Velocity += Acceleration;
        Position += Velocity;
    }

    private void UseInput(byte[] playerInputs)
    {
        MovePlayer(playerInputs);
    }

    private void MovePlayer(byte[] playerInputs)
    {
        Velocity *= AF.Fixed64.CreateFrom(0.96);

        var dir = new AF.Vector2();
        var input = new PlayerInput(playerInputs[ID - 1]);

        if (input.IsInputBitSet(0))
            dir += new AF.Vector2(0, -1);
        if (input.IsInputBitSet(1))
            dir += new AF.Vector2(0, 1);
        if (input.IsInputBitSet(2))
            dir += new AF.Vector2(-1, 0);
        if (input.IsInputBitSet(3))
            dir += new AF.Vector2(1, 0);

        if (dir != AF.Vector2.Zero)
        {
            Velocity += dir.Normalise() * MoveSpeed;
        }
    }
}
