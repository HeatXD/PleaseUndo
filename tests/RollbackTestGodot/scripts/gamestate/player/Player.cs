using Godot;
using AF = Abacus.Fixed64Precision;
using MessagePack;
[MessagePackObject]
public class Player
{
    [Key(0)]
    public byte ID; // ID of the player corresponds to the player's index in the array. an id shouldnt be negative
    [Key(1)]
    public AF.Vector2 Position;
    [Key(2)]
    public AF.Vector2 Velocity;
    [Key(3)]
    public AF.Vector2 Acceleration;
    [Key(4)]
    public PlayerInput GameInput;
    [Key(5)]
    public int MoveSpeed;

    public Player(byte ID)
    {
        this.ID = ID;
        this.Position = new AF.Vector2(400, 400);
        this.Velocity = new AF.Vector2();
        this.Acceleration = new AF.Vector2();
        this.GameInput = new PlayerInput();
        this.MoveSpeed = 100;
    }
    public Player(Player p)
    {
        this.ID = p.ID;
        this.Position = p.Position;
        this.Velocity = p.Velocity;
        this.Acceleration = p.Acceleration;
        this.GameInput = p.GameInput;
        this.MoveSpeed = p.MoveSpeed;
    }

    public void Update(AF.Fixed64 dt, byte[] playerInputs)
    {
        GetInput(playerInputs);
        UseInput();
        ProcessMotion(dt);
    }

    private void ProcessMotion(AF.Fixed64 dt)
    {
        Velocity += Acceleration * dt;
        Position += Velocity * dt;
    }

    private void UseInput()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        Velocity *= 0.96;

        var dir = new AF.Vector2();

        if (GameInput.IsInputBitSet(0))
            dir += new AF.Vector2(0, -1);
        if (GameInput.IsInputBitSet(1))
            dir += new AF.Vector2(0, 1);
        if (GameInput.IsInputBitSet(2))
            dir += new AF.Vector2(-1, 0);
        if (GameInput.IsInputBitSet(3))
            dir += new AF.Vector2(1, 0);

        if (dir != AF.Vector2.Zero)
        {
            Velocity += dir.Normalise() * MoveSpeed;
        }
    }

    private void GetInput(byte[] playerInputs)
    {
        GameInput.InputState = playerInputs[ID - 1];
    }
}
