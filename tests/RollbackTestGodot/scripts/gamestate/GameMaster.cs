using Godot;
using System;
using AF = Abacus.Fixed64Precision;

public class GameMaster : Node2D
{
	private GameState GS;
	private byte LocalID;
	private AF.Fixed64 Time;
	private AF.Fixed64 DeltaTime;
	private DynamicFont DrawFont;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		this.GS = new GameState(2);
		this.LocalID = Convert.ToByte(GetNode("/root/NetworkGlobals").Get("player_id"));
		this.Time = 0.0;
		this.DeltaTime = 1.0 / 60.0;
		this.DrawFont = new DynamicFont();

		DrawFont.FontData = ResourceLoader.Load("res://assets/fonts/Roboto-Bold.ttf") as DynamicFontData;
		DrawFont.Size = 16;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		Time += delta;
		if (Time >= DeltaTime)
		{
			AF.Fixed64 diff = Time - DeltaTime;
			GS.UpdateState(DeltaTime, LocalID, GetViewportRect().Size);
			Time = diff;
		}
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
		for (int i = 0; i < GS.Players.Length; i++)
		{
			var playerPos = new Vector2(GS.Players[i].Position.X.ToSingle(), GS.Players[i].Position.Y.ToSingle());
			DrawCircle(playerPos, 60, Colors.Cornflower);
			DrawString(DrawFont, playerPos, GS.Players[i].ID.ToString());
		}
	}
}
