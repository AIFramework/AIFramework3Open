using AI.DataStructs.Algebraic;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Генераторы датасетов

    private static (Vector[] feats, int[] labels) MakeClassificationData(int n, int seed, int kind) =>
        kind switch
        {
            1 => MakeMoons(n, seed),
            2 => MakeCircles(n, seed),
            3 => MakeCheckerboard(n, seed),
            _ => MakeLinearlySeparable(n, seed)
        };

    private static (Vector[], int[]) MakeLinearlySeparable(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var f = new Vector[n]; var l = new int[n];
        for (int i = 0; i < n0; i++) { f[i] = new Vector(new[] { -1.7 + Gauss(rng) * 0.8, Gauss(rng) * 0.8 }); l[i] = 0; }
        for (int i = 0; i < n1; i++) { f[n0 + i] = new Vector(new[] { 1.7 + Gauss(rng) * 0.8, Gauss(rng) * 0.8 }); l[n0 + i] = 1; }
        return (f, l);
    }

    private static (Vector[], int[]) MakeMoons(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var f = new Vector[n]; var l = new int[n];
        for (int i = 0; i < n0; i++) { double t = Math.PI * i / Math.Max(1, n0 - 1); f[i] = new Vector(new[] { Math.Cos(t) + Gauss(rng) * 0.12, Math.Sin(t) + Gauss(rng) * 0.12 }); l[i] = 0; }
        for (int i = 0; i < n1; i++) { double t = Math.PI * i / Math.Max(1, n1 - 1); f[n0 + i] = new Vector(new[] { 1 - Math.Cos(t) + Gauss(rng) * 0.12, 0.5 - Math.Sin(t) + Gauss(rng) * 0.12 }); l[n0 + i] = 1; }
        return (f, l);
    }

    private static (Vector[], int[]) MakeCircles(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var f = new Vector[n]; var l = new int[n];
        for (int i = 0; i < n0; i++) { double t = 2 * Math.PI * rng.NextDouble(), r = 1.0 + Gauss(rng) * 0.08; f[i] = new Vector(new[] { r * Math.Cos(t), r * Math.Sin(t) }); l[i] = 0; }
        for (int i = 0; i < n1; i++) { double t = 2 * Math.PI * rng.NextDouble(), r = 2.2 + Gauss(rng) * 0.08; f[n0 + i] = new Vector(new[] { r * Math.Cos(t), r * Math.Sin(t) }); l[n0 + i] = 1; }
        return (f, l);
    }

    private static (Vector[], int[]) MakeCheckerboard(int n, int seed)
    {
        var rng = new Random(seed);
        var f = new Vector[n]; var l = new int[n];
        for (int i = 0; i < n; i++)
        {
            double x = (rng.NextDouble() - 0.5) * 6, y = (rng.NextDouble() - 0.5) * 6;
            f[i] = new Vector(new[] { x, y });
            int sum = (int)Math.Floor(x / 1.5) + (int)Math.Floor(y / 1.5);
            l[i] = ((sum % 2) + 2) % 2;
        }
        return (f, l);
    }

    private static Vector[] MakeManifoldData(int n, int kind, int seed)
    {
        var rng  = new Random(seed);
        var data = new Vector[n];
        switch (kind)
        {
            case 1:
                for (int i = 0; i < n; i++) { double t = i * 3 * Math.PI / n, r = 0.3 + t * 0.25; data[i] = new Vector(new[] { r * Math.Cos(t) + Gauss(rng) * 0.06, r * Math.Sin(t) + Gauss(rng) * 0.06 }); }
                break;
            case 2:
                for (int i = 0; i < n; i++) { double t = 2 * Math.PI * i / n; data[i] = new Vector(new[] { 2.2 * Math.Cos(t) + Gauss(rng) * 0.08, 1.0 * Math.Sin(t) + Gauss(rng) * 0.08 }); }
                break;
            default:
                for (int i = 0; i < n; i++) { double t = 2 * Math.PI * i / n, r = 1.5 + Gauss(rng) * 0.05; data[i] = new Vector(new[] { r * Math.Cos(t), r * Math.Sin(t) }); }
                break;
        }
        return data;
    }

    #endregion

    #region Базовые утилиты датасетов

    private static double Gauss(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    #endregion
}
