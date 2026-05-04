using System;
using System.Text;
using AI.DataStructs.Algebraic;

namespace TestNNW;

/// <summary>
/// Демо парсинга <see cref="Vector"/> и <see cref="Matrix"/> из строкового представления.
/// </summary>
public static class TestParse
{
    private static readonly Random _rand = new(7);

    public static void Execute()
    {
        TestVector();
        TestMatrix();
    }

    private static void TestVector()
    {
        Console.WriteLine("=== Parse: Vector ===");
        var vector = new Vector(3);
        vector.Clear();
        for (int i = 0; i < _rand.Next(4, 10); i++)
            vector.Add(_rand.Next(10));

        string str = vector.ToString();
        Console.WriteLine(str);
        Console.WriteLine(Vector.Parse(str).ToString());

        const string norm = "[5 7.2 3.4 50]";
        Console.WriteLine($"Parse \"{norm}\": ok={Vector.TryParse(norm, out var ok)}, result={ok ?? new Vector(3)}");

        const string bad = "[5 7.2 3.4 50}";
        Console.WriteLine($"Parse \"{bad}\": ok={Vector.TryParse(bad, out var fail)}, result={fail ?? new Vector(3)}");
        Console.WriteLine();
    }

    private static void TestMatrix()
    {
        Console.WriteLine("=== Parse: Matrix ===");
        var matrix = new Matrix(_rand.Next(4, 10), _rand.Next(4, 10));
        for (int i = 0; i < matrix.Data.Length; i++)
            matrix[i] = _rand.Next(4, 10);

        string str = matrix.ToString();
        Console.WriteLine(str);
        Console.WriteLine("---");
        Console.WriteLine(Matrix.Parse(str));

        var sb = new StringBuilder();
        int width = _rand.Next(4, 10);
        for (int i = 0; i < _rand.Next(4, 10); i++)
        {
            var v = new Vector(3);
            v.Clear();
            for (int j = 0; j < width; j++)
                v.Add(_rand.Next(4, 10));
            sb.AppendLine(v.ToString());
        }
        string norm = sb.ToString();
        Console.WriteLine($"Multi-line parse: ok={Matrix.TryParse(norm, out var res)}, result:{Environment.NewLine}{res ?? new Matrix()}");
        Console.WriteLine();
    }
}
