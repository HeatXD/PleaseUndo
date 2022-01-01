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
    private const int INPUT_SIZE = 8;

    //PleaseUndo
    private PUSession GameSession;
    public PUPlayerHandle HandleOne;
    private PUPlayerHandle HandleTwo;
    private PUSessionCallbacks GameCallbacks;

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

        this.GameSession = new SyncTestBackend(ref GameCallbacks, 5, 2, INPUT_SIZE);
        // for now we only support 2 balls
        this.HandleOne = new PUPlayerHandle { };
        this.HandleTwo = new PUPlayerHandle { };
        for (int i = 1; i < 3; i++)
        {
            var player = new PUPlayer { player_num = i };
            if (i == LocalID)
            {
                GameSession.AddLocalPlayer(player, ref HandleOne);
                //GameSession.SetFrameDelay(PlayerHandles[i - 1], (int)GetNode("/root/NetworkGlobals").Get("local_delay"));
            }
            else
            {
                GameSession.AddRemotePlayer(player, ref HandleTwo, null);
                //GD.Print(player.player_num);
            }
        }

        GD.Print("Handles: ", HandleOne.handle, ",", HandleTwo.handle);

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
        byte[] inputs = new byte[INPUT_SIZE * 2];
        int disconnect_flags = 0;

        GameSession.SyncInput(ref inputs, INPUT_SIZE, ref disconnect_flags);
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

            byte[] inputs = new byte[INPUT_SIZE * 2];
            byte[] input = new byte[INPUT_SIZE];

            Array.Copy(GetLocalInput(), input, INPUT_SIZE);

            result = GameSession.AddLocalInput(HandleOne, input, INPUT_SIZE);

            if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
            {
                result = GameSession.SyncInput(ref inputs, INPUT_SIZE, ref disconnect_flags);
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

    private byte[] GetLocalInput()
    {
        // PlayerInput game_input = new PlayerInput();
        // if (Input.IsActionPressed("move_up"))
        // 	game_input.SetInputBit(0, true);
        // if (Input.IsActionPressed("move_down"))
        // 	game_input.SetInputBit(1, true);
        // if (Input.IsActionPressed("move_left"))
        // 	game_input.SetInputBit(2, true);
        // if (Input.IsActionPressed("move_right"))
        // 	game_input.SetInputBit(3, true);
        // return game_input.InputState;
        Random rnd = new Random();
        byte[] b = new byte[1];
        rnd.NextBytes(b);
        var ret = new byte[INPUT_SIZE] { b[0], 0, 0, 0, 0, 0, 0, 0 };
        return ret;
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
