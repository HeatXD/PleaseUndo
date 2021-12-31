using Godot;
using System;
using PleaseUndo;
using MessagePack;

public class GameMaster : Node2D
{

    private GameState GameState;
    private DynamicFont DrawFont;

    // loop timing
    private int WaitFrames;
    private bool SkippedLastFrame;

    // network
    private byte LocalID;
    private GodotUdpPeer SessionAdapter;

    //PleaseUndo
    private PUSession GameSession;
    private PUSessionCallbacks GameCallbacks;
    private PUPlayerHandle[] PlayerHandles;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // setup loop timing
        // setup network
        this.SessionAdapter = new GodotUdpPeer(
            (int)GetNode("/root/NetworkGlobals").Get("local_port"),
            (string)GetNode("/root/NetworkGlobals").Get("remote_addr"),
            (int)GetNode("/root/NetworkGlobals").Get("remote_port"));

        this.LocalID = Convert.ToByte(GetNode("/root/NetworkGlobals").Get("player_id"));

        //setup game
        this.GameState = new GameState(2);
        this.DrawFont = new DynamicFont();

        DrawFont.FontData = ResourceLoader.Load("res://assets/fonts/Roboto-Bold.ttf") as DynamicFontData;
        DrawFont.Size = 16;

        // setup PleaseUndo
        System.Environment.SetEnvironmentVariable("PU_LOG_IGNORE", null);
        System.Environment.SetEnvironmentVariable("PU_LOG_USE_TIMESTAMP", "x");
        System.Environment.SetEnvironmentVariable("PU_LOG_CREATE_FILE", null);

        this.GameCallbacks.OnEvent += OnEvent;
        this.GameCallbacks.OnBeginGame += OnBeginGame;
        this.GameCallbacks.OnAdvanceFrame += OnAdvanceFrame;
        this.GameCallbacks.OnLoadGameState += OnLoadGameState;
        this.GameCallbacks.OnSaveGameState += OnSaveGameState;

        this.GameSession = new SyncTestBackend(ref GameCallbacks, 2, 2, sizeof(byte));
        // for now we only support 2 balls
        this.PlayerHandles = new PUPlayerHandle[2];

        for (int i = 1; i < PlayerHandles.Length + 1; i++)
        {
            if (i == LocalID)
            {
                var player = new PUPlayer { player_num = i, type = PUPlayerType.LOCAL };
                GameSession.AddLocalPlayer(player, ref PlayerHandles[i - 1]);
                GD.Print(player.player_num);
                GameSession.SetFrameDelay(PlayerHandles[i - 1], (int)GetNode("/root/NetworkGlobals").Get("local_delay"));
            }
            else
            {
                GameSession.AddRemotePlayer(new PUPlayer { player_num = i, type = PUPlayerType.REMOTE }, ref PlayerHandles[i - 1], SessionAdapter);
            }
        }

        GameSession.SetDisconnectTimeout(3000);
        GameSession.SetDisconnectNotifyStart(1000);
    }

    private bool OnSaveGameState(ref byte[] buffer, ref int len, ref int checksum, int frame)
    {
        var save = MessagePackSerializer.Serialize(GameState);
        buffer = new byte[save.Length];
        Array.Copy(save, buffer, save.Length);
        len = save.Length;
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
        byte[] inputs = new byte[sizeof(byte) * 2]; //  2 bytes
        int disconnect_flags = 0;
        GameSession.SyncInput(ref inputs, sizeof(byte), ref disconnect_flags);

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
        GD.Print(ev.code.ToString());
        switch (ev.code)
        {
            case PUEventCode.PU_EVENTCODE_TIMESYNC:
                var ts_event = (PUTimesyncEvent)ev;
                WaitFrames = ts_event.frames_ahead;
                break;
        }
        return true;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {

        if (WaitFrames <= 0 || SkippedLastFrame == true)
        {
            SkippedLastFrame = false;

            GameSession.DoPoll(69);// nice

            PUErrorCode result = PUErrorCode.PU_OK;
            int disconnect_flags = 0;
            byte[] inputs = new byte[sizeof(byte) * 2];

            byte[] input = new byte[1] { GetLocalInput() };
            result = GameSession.AddLocalInput(PlayerHandles[LocalID - 1], input, sizeof(byte));

            if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
            {
                result = GameSession.SyncInput(ref inputs, sizeof(byte) * 2, ref disconnect_flags);
                if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
                {
                    GameState.UpdateState(inputs, GetViewportRect().Size);
                    GameSession.IncrementFrame();
                }
            }

        }
        else
        {
            SkippedLastFrame = true;
            WaitFrames--;
        }
        // draw
        Update();
    }

    private byte GetLocalInput()
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
        return game_input.InputState;
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
