using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.ML;

public static partial class MlDemoRunner
{
    #region Генераторы датасетов

    /// <summary>Разные распределения для кластеризации: 0=blobs, 1=rings, 2=spiral, 3=aniso.</summary>
    private static Vector[] MakeClusterData(int n, int k, int seed, int kind) => kind switch
    {
        1 => MakeRings(n, k, seed),
        2 => MakeSpiral(n, k, seed),
        3 => MakeAnisotropic(n, k, seed),
        _ => MakeBlobs(n, k, seed),
    };

    private static Vector[] MakeBlobs(int n, int k, int seed)
    {
        var rng = new Random(seed);
        var result = new Vector[n];
        var cx = new double[] { -2.2,  2.2,  0.0, -2.2,  2.2, 0.0 };
        var cy = new double[] {  2.0,  2.0, -2.5, -2.0,  0.0, 2.5 };
        for (int i = 0; i < n; i++)
        {
            int c = i % k;
            double x = cx[c % cx.Length] + Gauss(rng) * 0.55;
            double y = cy[c % cy.Length] + Gauss(rng) * 0.55;
            result[i] = new Vector(new[] { x, y });
        }
        Shuffle(result, rng);
        return result;
    }

    private static Vector[] MakeRings(int n, int k, int seed)
    {
        var rng = new Random(seed);
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
        {
            int c = i % k;
            double r = 0.7 + c * 1.0 + Gauss(rng) * 0.12;
            double theta = rng.NextDouble() * 2 * Math.PI;
            result[i] = new Vector(new[] { r * Math.Cos(theta), r * Math.Sin(theta) });
        }
        Shuffle(result, rng);
        return result;
    }

    private static Vector[] MakeSpiral(int n, int k, int seed)
    {
        var rng = new Random(seed);
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
        {
            int c = i % k;
            double t = (double)i / n * 3 * Math.PI;
            double r = 0.5 + t * 0.3;
            double phase = c * 2 * Math.PI / k;
            result[i] = new Vector(new[]
            {
                r * Math.Cos(t + phase) + Gauss(rng) * 0.15,
                r * Math.Sin(t + phase) + Gauss(rng) * 0.15
            });
        }
        Shuffle(result, rng);
        return result;
    }

    private static Vector[] MakeAnisotropic(int n, int k, int seed)
    {
        var rng = new Random(seed);
        var result = new Vector[n];
        var cx = new double[] { -2.5, 2.5, 0, -2, 2 };
        var cy = new double[] {  0,   0,  2.5, -2.5, 2.5 };
        for (int i = 0; i < n; i++)
        {
            int c = i % k;
            double u = Gauss(rng) * 1.5, v = Gauss(rng) * 0.35;
            double angle = c * Math.PI / 4;
            result[i] = new Vector(new[]
            {
                cx[c % cx.Length] + u * Math.Cos(angle) - v * Math.Sin(angle),
                cy[c % cy.Length] + u * Math.Sin(angle) + v * Math.Cos(angle)
            });
        }
        Shuffle(result, rng);
        return result;
    }

    /// <summary>Датасеты для бинарной классификации: 0=линейно разделимый, 1=полумесяцы, 2=круги, 3=шахматка.</summary>
    private static (Vector[] feats, int[] labels) MakeClassificationData(int n, int seed, int kind) => kind switch
    {
        1 => MakeMoons(n, seed),
        2 => MakeCircles(n, seed),
        3 => MakeCheckerboard(n, seed),
        _ => MakeLinearlySeparable(n, seed),
    };

    private static (Vector[], int[]) MakeLinearlySeparable(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var feats = new Vector[n]; var labels = new int[n];
        for (int i = 0; i < n0; i++) { feats[i] = new Vector(new[] { -1.7 + Gauss(rng) * 0.8, Gauss(rng) * 0.8 }); labels[i] = 0; }
        for (int i = 0; i < n1; i++) { feats[n0 + i] = new Vector(new[] { 1.7 + Gauss(rng) * 0.8, Gauss(rng) * 0.8 }); labels[n0 + i] = 1; }
        return (feats, labels);
    }

    private static (Vector[], int[]) MakeMoons(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var feats = new Vector[n]; var labels = new int[n];
        for (int i = 0; i < n0; i++)
        {
            double t = Math.PI * i / Math.Max(1, n0 - 1);
            feats[i]  = new Vector(new[] { Math.Cos(t) + Gauss(rng) * 0.12, Math.Sin(t) + Gauss(rng) * 0.12 });
            labels[i] = 0;
        }
        for (int i = 0; i < n1; i++)
        {
            double t = Math.PI * i / Math.Max(1, n1 - 1);
            feats[n0 + i]  = new Vector(new[] { 1.0 - Math.Cos(t) + Gauss(rng) * 0.12, 0.5 - Math.Sin(t) + Gauss(rng) * 0.12 });
            labels[n0 + i] = 1;
        }
        return (feats, labels);
    }

    private static (Vector[], int[]) MakeCircles(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var feats = new Vector[n]; var labels = new int[n];
        for (int i = 0; i < n0; i++)
        {
            double t = 2 * Math.PI * rng.NextDouble(), r = 1.0 + Gauss(rng) * 0.08;
            feats[i]  = new Vector(new[] { r * Math.Cos(t), r * Math.Sin(t) }); labels[i] = 0;
        }
        for (int i = 0; i < n1; i++)
        {
            double t = 2 * Math.PI * rng.NextDouble(), r = 2.2 + Gauss(rng) * 0.08;
            feats[n0 + i]  = new Vector(new[] { r * Math.Cos(t), r * Math.Sin(t) }); labels[n0 + i] = 1;
        }
        return (feats, labels);
    }

    private static (Vector[], int[]) MakeCheckerboard(int n, int seed)
    {
        var rng = new Random(seed);
        var feats = new Vector[n]; var labels = new int[n];
        for (int i = 0; i < n; i++)
        {
            double x = (rng.NextDouble() - 0.5) * 6, y = (rng.NextDouble() - 0.5) * 6;
            int cellX = (int)Math.Floor(x / 1.5), cellY = (int)Math.Floor(y / 1.5);
            feats[i]  = new Vector(new[] { x, y });
            labels[i] = ((cellX + cellY) % 2 + 2) % 2;
        }
        return (feats, labels);
    }

    private static (Vector[], int[]) MakeCorrData(int n, int seed)
    {
        var rng = new Random(seed);
        int n0 = n / 2, n1 = n - n0;
        var feats = new Vector[n]; var labels = new int[n];
        for (int i = 0; i < n0; i++)
        {
            feats[i]  = new Vector(new[] { -2.0 - rng.NextDouble() * 1.5,  2.0 + rng.NextDouble() * 1.5 });
            labels[i] = 0;
        }
        for (int i = 0; i < n1; i++)
        {
            feats[n0 + i]  = new Vector(new[] {  2.0 + rng.NextDouble() * 1.5, -2.0 - rng.NextDouble() * 1.5 });
            labels[n0 + i] = 1;
        }
        return (feats, labels);
    }

    #endregion

    #region Утилиты генерации данных

    private static double Gauss(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static void Shuffle<T>(T[] arr, Random rng)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    #endregion
}
