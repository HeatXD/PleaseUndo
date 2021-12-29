using Godot;
using System;
using AF = Abacus.Fixed64Precision;

public struct SaveState
{
	public Player[] Players;

	public SaveState(Player[] players)
	{
		this.Players = new Player[players.Length];
		for (int i = 0; i < players.Length; i++)
		{
			this.Players[i] = new Player(players[i]);
		}
	}
}
public class GameState
{
	public SaveState SaveState;
	public Player[] Players;
	public GameState(byte playerCount)
	{
		this.Players = new Player[playerCount];
		for (byte i = 0; i < Players.Length; i++)
		{
			Players[i] = new Player(i);
		}
	}
	public void UpdateState(AF.Fixed64 dt, byte localID, Vector2 screenSize)
	{
		foreach (var player in Players)
		{
			player.Update(dt, localID);
			ScreenWrap(player, screenSize);
		}

		if (Input.IsActionPressed("save_game"))
			SaveGame();
		if (Input.IsActionPressed("load_game"))
			LoadGame();

	}

	private void ScreenWrap(Player player, Vector2 screenSize)
	{
		if (player.Position.X > screenSize.x)
			player.Position.X = 0;
		if (player.Position.X < 0)
			player.Position.X = screenSize.x;
		if (player.Position.Y > screenSize.y)
			player.Position.Y = 0;
		if (player.Position.Y < 0)
			player.Position.Y = screenSize.y;
	}

	public void SaveGame()
	{
		SaveState = new SaveState(Players);
		GD.Print("Saved");
	}
	public void LoadGame()
	{
		if (SaveState.Players != null)
		{
			for (int i = 0; i < Players.Length; i++)
			{
				Players[i] = new Player(SaveState.Players[i]);
			}
			GD.Print("Loaded");
		}
	}
}
