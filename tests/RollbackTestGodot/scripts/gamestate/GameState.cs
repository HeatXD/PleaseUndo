using Godot;
using MessagePack;

[MessagePackObject]
public class GameState
{
    [KeyAttribute(0)]
    public Player[] Players;
    [KeyAttribute(1)]
    public int FrameNumber;

    public GameState(Player[] players, int frameNumber)
    {
        Players = new Player[players.Length];
        for (int i = 0; i < Players.Length; i++)
        {
            Players[i] = new Player(players[i]);
        }

        FrameNumber = frameNumber;
    }
    public GameState(int playerCount)
    {
        Players = new Player[playerCount];
        for (int i = 1; i < Players.Length + 1; i++)
        {
            Players[i - 1] = new Player(i);
        }
        FrameNumber = 0;
    }
    public void UpdateState(byte[] playerInputs, Vector2 screenSize)
    {
        FrameNumber++;
        foreach (var player in Players)
        {
            player.Update(playerInputs);
            ScreenWrap(player, screenSize);
        }
        foreach (var input in playerInputs)
        {
            GD.Print(input);
        }
        GD.Print("-----");
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
