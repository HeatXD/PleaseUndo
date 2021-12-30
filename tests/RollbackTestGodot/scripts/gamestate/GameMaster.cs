using Godot;
using System;
using AF = Abacus.Fixed64Precision;
using PleaseUndo;
using MessagePack;

public class GameMaster : Node2D
{
    private GameState GameState;
    private AF.Fixed64 Time;
    private AF.Fixed64 DeltaTime;
    private DynamicFont DrawFont;

    // network
    private byte LocalID;
    private GodotUdpPeer SessionAdapter;

    //PleaseUndo
    private PUSession GameSession;
    private PUSessionCallbacks GameCallbacks;
    private PUPlayerHandle[] PlayerHandles;
    private byte[] buffer;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // setup network
        this.SessionAdapter = new GodotUdpPeer(
            (int)GetNode("/root/NetworkGlobals").Get("local_port"),
            (string)GetNode("/root/NetworkGlobals").Get("remote_addr"),
            (int)GetNode("/root/NetworkGlobals").Get("remote_port"));

        this.LocalID = Convert.ToByte(GetNode("/root/NetworkGlobals").Get("player_id"));

        //setup game
        this.GameState = new GameState(2);
        this.Time = 0.0;
        this.DeltaTime = 1.0 / 60.0;
        this.DrawFont = new DynamicFont();

        DrawFont.FontData = ResourceLoader.Load("res://assets/fonts/Roboto-Bold.ttf") as DynamicFontData;
        DrawFont.Size = 16;

        // setup PleaseUndo
        this.GameCallbacks.OnEvent += OnEvent;
        this.GameCallbacks.OnBeginGame += OnBeginGame;
        this.GameCallbacks.OnAdvanceFrame += OnAdvanceFrame;
        this.GameCallbacks.OnLoadGameState += OnLoadGameState;
        this.GameCallbacks.OnSaveGameState += OnSaveGameState;
        this.GameSession = new Peer2PeerBackend(ref GameCallbacks, 2, sizeof(ushort));
        // for now we only support 2 balls
        this.PlayerHandles = new PUPlayerHandle[2];
        for (int i = 1; i < PlayerHandles.Length + 1; i++)
        {
            if (i == LocalID)
            {
                this.GameSession.AddLocalPlayer(new PUPlayer { player_num = i }, ref PlayerHandles[i - 1]);
            }
            else
            {
                this.GameSession.AddRemotePlayer(new PUPlayer { player_num = i }, ref PlayerHandles[i - 1], SessionAdapter);
            }
        }
    }

    private bool OnSaveGameState(ref byte[] buffer, ref int len, ref int checksum, int frame)
    {
        var bytes = MessagePackSerializer.Serialize(GameState);
        len = bytes.Length;
        buffer = new byte[len];
        Array.Copy(bytes, buffer, len);
        //GD.Print("State Saved");
        return true;
    }

    private bool OnLoadGameState(byte[] buffer, int len)
    {
        GameState = MessagePackSerializer.Deserialize<GameState>(buffer);
        //GD.Print("State Loaded");
        return true;
    }

    private bool OnAdvanceFrame()
    {
        return true;
    }

    private bool OnBeginGame()
    {
        return true;
    }

    private bool OnEvent(PUEvent ev)
    {
        return true;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {
        Time += delta;
        if (Time >= DeltaTime)
        {
            AF.Fixed64 diff = Time - DeltaTime;
            GameState.UpdateState(DeltaTime, LocalID, GetViewportRect().Size);
            Time = diff;
        }
        // if (Input.IsActionPressed("load_game"))
        // {
        //     if (buffer != null)
        //     {
        //         OnLoadGameState(buffer, buffer.Length);
        //     }
        // }

        // if (Input.IsActionPressed("save_game"))
        // {
        //     int len = 0;
        //     int check = 0;
        //     int frame = 0;
        //     OnSaveGameState(ref buffer, ref len, ref check, frame);
        // }
        // draw
        Update();
    }
    public override void _Draw()
    {
        DrawGameState();
    }

    private void DrawGameState()
    {
        DrawPlayers();
    }

    private void DrawPlayers()
    {
        for (int i = 0; i < GameState.Players.Length; i++)
        {
            var playerPos = new Vector2(GameState.Players[i].Position.X.ToSingle(), GameState.Players[i].Position.Y.ToSingle());
            DrawCircle(playerPos, 60, Colors.Cornflower);
            DrawString(DrawFont, playerPos, GameState.Players[i].ID.ToString());
        }
    }
}
