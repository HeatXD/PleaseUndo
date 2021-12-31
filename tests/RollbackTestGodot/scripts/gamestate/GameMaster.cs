using Godot;
using System;
using AF = Abacus.Fixed64Precision;
using PleaseUndo;
using MessagePack;

public class GameMaster : Node2D
{
	private const int FRAME_DELAY = 2;

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
		System.Environment.SetEnvironmentVariable("PU_LOG_IGNORE", null);
		System.Environment.SetEnvironmentVariable("PU_LOG_FILE_PATH", "logs.txt");
		System.Environment.SetEnvironmentVariable("PU_LOG_USE_TIMESTAMP", "x");
		System.Environment.SetEnvironmentVariable("PU_LOG_CREATE_FILE", "x");

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
				var player = new PUPlayer { player_num = i };
				GameSession.AddLocalPlayer(player, ref PlayerHandles[i - 1]);
				//GameSession.SetFrameDelay(PlayerHandles[i - 1], FRAME_DELAY); FIX PLEASE UNDO DOESNT LIKE IT :(
			}
			else
			{
				GameSession.AddRemotePlayer(new PUPlayer { player_num = i }, ref PlayerHandles[i - 1], SessionAdapter);
			}
		}
		//GameSession.SetDisconnectTimeout(3000); FIX PLEASEUNDO DOESNT LIKE IT :(
		//GameSession.SetDisconnectNotifyStart(1000); FIX PLEASEUNDO DOESNT LIKE IT :(
	}

	private bool OnSaveGameState(ref byte[] buffer, ref int len, ref int checksum, int frame)
	{
		var bytes = MessagePackSerializer.Serialize(GameState);
		len = bytes.Length;

		buffer = new byte[len];
		Array.Copy(bytes, buffer, len);

		checksum = (int)FletcherChecksum.GetChecksumFromBytes(bytes, 32);
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
		byte[] inputs = new byte[sizeof(ushort) * 2]; // a ushort is 2 bytes
		int disconnect_flags = 0;
		GameSession.SyncInput(ref inputs, sizeof(ushort) * 2, ref disconnect_flags);

		GameState.UpdateState(inputs, DeltaTime, GetViewportRect().Size);
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
		return true;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		Time += delta;
		if (Time >= DeltaTime)
		{
			GameSession.DoPoll(69); // nice

			PUErrorCode result = PUErrorCode.PU_OK;
			int disconnect_flags = 0;
			byte[] inputs = new byte[sizeof(ushort) * 2];

			byte[] input = new byte[1] { GetLocalInput() };
			result = GameSession.AddLocalInput(PlayerHandles[LocalID - 1], input, input.Length);

			if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
			{
				result = GameSession.SyncInput(ref inputs, sizeof(ushort) * 2, ref disconnect_flags);
				if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
				{
					GameState.UpdateState(inputs, DeltaTime, GetViewportRect().Size);
					GameSession.IncrementFrame();
				}
			}
			// loop timing stuff.
			AF.Fixed64 diff = Time - DeltaTime;
			Time = diff;
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
