using Godot;
using System;
using PleaseUndo;
using MessagePack;

public class GameMaster : Node2D
{

    private GameState GameState;
    private DynamicFont DrawFont;

    // network
    private byte LocalID;
    private IPeerNetAdapter SessionAdapter;
    private const int INPUT_SIZE = 1;
    private const int PLAYER_COUNT = 2;

    //PleaseUndo
    private PUSession GameSession;
    private PUPlayerHandle LocalHandle;
    private PUPlayerHandle RemoteHandle;
    private PUSessionCallbacks GameCallbacks;

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
        this.GameState = new GameState(PLAYER_COUNT);
        this.DrawFont = new DynamicFont();

        DrawFont.FontData = ResourceLoader.Load("res://assets/fonts/Roboto-Bold.ttf") as DynamicFontData;
        DrawFont.Size = 16;

        // setup PleaseUndo
        System.Environment.SetEnvironmentVariable("PU_LOG_IGNORE", "x");

        this.GameCallbacks.OnEvent += OnEvent;
        this.GameCallbacks.OnBeginGame += OnBeginGame;
        this.GameCallbacks.OnAdvanceFrame += OnAdvanceFrame;
        this.GameCallbacks.OnLoadGameState += OnLoadGameState;
        this.GameCallbacks.OnSaveGameState += OnSaveGameState;

        this.GameSession = new Peer2PeerBackend(ref GameCallbacks, PLAYER_COUNT, INPUT_SIZE);
        // for now we only support 2 balls
        this.LocalHandle = new PUPlayerHandle();
        this.RemoteHandle = new PUPlayerHandle();

        GameSession.AddLocalPlayer(new PUPlayer { player_num = LocalID }, ref LocalHandle);
        GameSession.SetFrameDelay(LocalHandle, (int)GetNode("/root/NetworkGlobals").Get("local_delay"));
        GameSession.AddRemotePlayer(new PUPlayer { player_num = LocalID == 1 ? 2 : 1 }, ref RemoteHandle, SessionAdapter);
        GameSession.SetDisconnectTimeout(3000);
        GameSession.SetDisconnectNotifyStart(1000);
    }

    private bool OnSaveGameState(ref byte[] buffer, ref int len, ref int checksum, int frame)
    {
        var bytes = MessagePackSerializer.Serialize(GameState);
        len = bytes.Length;
        buffer = new byte[len];
        Array.Copy(bytes, buffer, len);
        checksum = (int)FletcherChecksum.GetChecksumFromBytes(buffer, 16);
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
        byte[] inputs = new byte[INPUT_SIZE * PLAYER_COUNT];
        int disconnect_flags = 0;

        GameSession.SyncInput(ref inputs, INPUT_SIZE * PLAYER_COUNT, ref disconnect_flags);
        GameState.UpdateState(inputs, GetViewportRect().Size);
        GameSession.IncrementFrame();
        return true;
    }

    private bool OnBeginGame()
    {
        return true;
    }

    private bool OnEvent(PUEvent ev)
    {
        GD.Print("Player: ", LocalID, " Event: ", ev.code.ToString());
        switch (ev.code)
        {
            case PUEventCode.PU_EVENTCODE_TIMESYNC:
                var ts_event = (PUTimesyncEvent)ev;
                break;
        }
        return true;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {
        // it might be smart to put all the gamestate into a seperate thread.
        // will look into that
        GameSession.DoPoll(69);// nice

        PUErrorCode result = PUErrorCode.PU_OK;
        int disconnect_flags = 0;

        byte[] inputs = new byte[INPUT_SIZE * PLAYER_COUNT];
        byte[] input = new byte[INPUT_SIZE];

        Array.Copy(GetLocalInput(), input, INPUT_SIZE);
        result = GameSession.AddLocalInput(LocalHandle, input, INPUT_SIZE);

        if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
        {
            result = GameSession.SyncInput(ref inputs, INPUT_SIZE * PLAYER_COUNT, ref disconnect_flags);
            if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
            {
                GameState.UpdateState(inputs, GetViewportRect().Size);
                GameSession.IncrementFrame();
            }
        }
        // draw
        Update();
    }

    private byte[] GetLocalInput()
    {
        PlayerInput game_input = new PlayerInput();
        if (Input.IsActionPressed("move_up"))
            game_input.SetInputBit(0, true);
        if (Input.IsActionPressed("move_down"))
            game_input.SetInputBit(1, true);
        if (Input.IsActionPressed("move_left"))
            game_input.SetInputBit(2, true);
        if (Input.IsActionPressed("move_right"))
            game_input.SetInputBit(3, true);
        return new byte[1] { game_input.InputState };
        // Random rnd = new Random();
        // byte[] b = new byte[1];
        // rnd.NextBytes(b);
        // return b;
    }

    public override void _Draw()
    {
        DrawPlayers();
    }

    private void DrawPlayers()
    {
        for (int i = 0; i < GameState.Players.Length; i++)
        {
            var playerPos = new Vector2(GameState.Players[i].Position.X.ToSingle(), GameState.Players[i].Position.Y.ToSingle());
            DrawCircle(playerPos, 60, i == 0 ? Colors.Blue : Colors.Red);
            DrawString(DrawFont, playerPos, GameState.Players[i].ID.ToString());
        }
    }
}
