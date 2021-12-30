using Godot;
using System;
using AF = Abacus.Fixed64Precision;
using MessagePack;

[MessagePackObject]
public class GameState
{
    [Key(0)]
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
}
