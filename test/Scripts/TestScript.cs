using Godot;
using System;

public class TestScript : Node
{
    public override void _Ready()
    {
        GD.Print("OK!");
        PleaseUndo.GameInput<int> gameInput;
    }
}
