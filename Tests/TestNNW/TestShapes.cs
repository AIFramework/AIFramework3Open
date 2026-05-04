using System;
using AI.DataStructs.Shapes;

namespace TestNNW;

/// <summary>
/// Smoke-демо для семейства <see cref="Shape"/>/<see cref="Shape1D"/>/<see cref="Shape2D"/>/<see cref="Shape3D"/>.
/// </summary>
public static class TestShapes
{
    public static void Execute()
    {
        Console.WriteLine("=== Shapes ===");

        var shape1D = new Shape1D(3);
        Console.WriteLine($"Shape1D: {shape1D}, Count={shape1D.Count}");
        Console.WriteLine($"  -> 2D: {(Shape2D)shape1D}, -> 3D: {(Shape3D)shape1D}, expand(7): {shape1D.Expand(7)}");

        var shape2D = new Shape2D(2, 5);
        Console.WriteLine($"Shape2D: {shape2D}, Count={shape2D.Count}, Area={shape2D.Area}");
        Console.WriteLine($"  -> 3D: {(Shape3D)shape2D}, shrink: {shape2D.Shrink()}, expand(3): {shape2D.Expand(3)}");

        var shape3D = new Shape3D(4, 7, 3);
        Console.WriteLine($"Shape3D: {shape3D}, Count={shape3D.Count}, Volume={shape3D.Volume}");
        Console.WriteLine($"  shrink: {shape3D.Shrink()}, expand(5): {shape3D.Expand(5)}");

        var shape = new Shape(1, 2, 3, 4);
        Console.WriteLine($"Shape: {shape}, Count={shape.Count}");
        Console.WriteLine($"  shrink: {shape.Shrink()}, expand(5): {shape.Expand(5)}");

        var s1 = new Shape(1, 2, 3);
        var s2 = new Shape(1, 2, 3, 4);
        var s3 = new Shape(1, 2, 3, 4, 1, 1);
        var s4 = new Shape(2, 1, 3, 4);
        Console.WriteLine($"FuzzyEquals: vs (1,2,3)={shape.FuzzyEquals(s1)}, vs (1,2,3,4)={shape.FuzzyEquals(s2)}, vs +(1,1)={shape.FuzzyEquals(s3)}, vs (2,1,3,4)={shape.FuzzyEquals(s4)}");
        Console.WriteLine();
    }
}
