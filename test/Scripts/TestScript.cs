using Godot;
using PleaseUndo;

public class TestScript : Node
{
    public override void _Ready()
    {
        System.Environment.SetEnvironmentVariable("PU_LOG_IGNORE", null);
        System.Environment.SetEnvironmentVariable("PU_LOG_FILE_PATH", "logs.txt");
        System.Environment.SetEnvironmentVariable("PU_LOG_CREATE_FILE", "x");

        GameInput<int> input = new GameInput<int>();
        input.Init(10, new int[] { 3, 7 });
        input.Log("GameInput", true);
        input.Log("GameInput", true);
    }
}
