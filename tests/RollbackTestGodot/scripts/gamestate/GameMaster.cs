using Godot;
using System;
using PleaseUndo;
using MessagePack;

public class GameMaster : Node2D
{

	private GameState GameState;
	private DynamicFont DrawFont;

	// loop timing
	private float Accumulator;
	private const float DELTA = 0.167f;

	// network
	private byte LocalID;
	private GodotUdpPeer SessionAdapter;

	//PleaseUndo
	private PUSession GameSession;
	private PUSessionCallbacks GameCallbacks;
	private PUPlayerHandle[] PlayerHandles;
	private const int FRAME_DELAY = 2;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// setup loop timing
		this.Accumulator = -0.5f;
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
		System.Environment.SetEnvironmentVariable("PU_LOG_FILE_PATH", "logs.txt");
		System.Environment.SetEnvironmentVariable("PU_LOG_USE_TIMESTAMP", "x");
		System.Environment.SetEnvironmentVariable("PU_LOG_CREATE_FILE", "x");

		this.GameCallbacks.OnEvent += OnEvent;
		this.GameCallbacks.OnBeginGame += OnBeginGame;
		this.GameCallbacks.OnAdvanceFrame += OnAdvanceFrame;
		this.GameCallbacks.OnLoadGameState += OnLoadGameState;
		this.GameCallbacks.OnSaveGameState += OnSaveGameState;

		this.GameSession = new Peer2PeerBackend(ref GameCallbacks, 2, sizeof(byte));
		// for now we only support 2 balls
		this.PlayerHandles = new PUPlayerHandle[2];

		for (int i = 1; i < PlayerHandles.Length + 1; i++)
		{
			if (i == LocalID)
			{
				var player = new PUPlayer { player_num = i, type = PUPlayerType.LOCAL };
				GameSession.AddLocalPlayer(player, ref PlayerHandles[i - 1]);
				GD.Print(player.player_num);
				//GameSession.SetFrameDelay(PlayerHandles[i - 1], FRAME_DELAY);
			}
			else
			{
				GameSession.AddRemotePlayer(new PUPlayer { player_num = i, type = PUPlayerType.REMOTE }, ref PlayerHandles[i - 1], SessionAdapter);
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
		byte[] inputs = new byte[sizeof(byte) * 2]; //  2 bytes
		int disconnect_flags = 0;
		GameSession.SyncInput(ref inputs, sizeof(byte) * 2, ref disconnect_flags);

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
				// var time_sync_event = (PUTimesyncEvent)ev;
				// OS.DelayMsec(1000 * time_sync_event.frames_ahead / 60);
				break;
		}
		return true;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		Accumulator += delta;
		if (Accumulator >= DELTA)
		{
			GameSession.DoPoll(69); // nice

			PUErrorCode result = PUErrorCode.PU_OK;
			int disconnect_flags = 0;
			byte[] inputs = new byte[sizeof(byte) * 2];

			byte[] input = new byte[1] { GetLocalInput() };
			result = GameSession.AddLocalInput(PlayerHandles[LocalID - 1], input, input.Length);

			if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
			{
				result = GameSession.SyncInput(ref inputs, sizeof(byte) * 2, ref disconnect_flags);
				if (result == PUErrorCode.PU_ERRORCODE_SUCCESS)
				{
					GameState.UpdateState(inputs, GetViewportRect().Size);
					GameSession.IncrementFrame();
				}
			}
			Accumulator -= DELTA;
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
			DrawCircle(playerPos, 60, Colors.Cornflower);
			DrawString(DrawFont, playerPos, GameState.Players[i].ID.ToString());
		}
	}
}
