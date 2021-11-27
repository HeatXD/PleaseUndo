using Godot;
using PleaseUndo;

public class TestScript : Node
{
    public override void _Ready()
    {
        GameInput<int> input = new GameInput<int>();
        input.Init(10, new int[] { 1, 3 });
        input.Log("Loggin", true);
        input.Log("Loggin", true);
    }
}
