using AI.DataStructs.Algebraic;
using AI.Fuzzy.Control;
using AI.Fuzzy.Inference;

namespace FuzzyInferenceConsole;

internal static class Program
{
    private static int _failures;

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Консольные проверки нечёткого вывода (AI.Fuzzy) ===\n");

        Run("Мамдани: центроид (max-min)", TestMamdaniCentroid);
        Run("Ларсен: центроид (max-product)", TestLarsenCentroid);
        Run("Сугено: взвешенные синглтоны", TestSugenoSingletons);
        Run("Цукамото: обратная монотонная μ и среднее", TestTsukamoto);
        Run("Нечёткий PID (Сугено / Мамдани), шаги", TestFuzzyPid);

        Console.WriteLine();
        if (_failures == 0)
        {
            Console.WriteLine("Итог: все проверки пройдены.");
            return 0;
        }

        Console.WriteLine($"Итог: ошибок: {_failures}.");
        return 1;
    }

    private static void Run(string title, Func<bool> test)
    {
        Console.WriteLine($"-- {title}");
        try
        {
            bool ok = test();
            Console.WriteLine(ok ? "   OK\n" : "   НЕ ПРОЙДЕНО\n");
            if (!ok)
                _failures++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ИСКЛЮЧЕНИЕ: {ex.Message}\n");
            _failures++;
        }
    }

    private static Vector Universe(int n, double uMin, double uMax)
    {
        var u = new Vector(n);
        for (int k = 0; k < n; k++)
            u[k] = uMin + (uMax - uMin) * k / Math.Max(1, n - 1);
        return u;
    }

    private static Vector TriangularTerm(Vector u, double peak, double halfWidth)
    {
        var v = new Vector(u.Count);
        for (int k = 0; k < u.Count; k++)
            v[k] = FuzzyMembershipShapes.Triangular(u[k], peak - halfWidth, peak, peak + halfWidth);
        return v;
    }

    /// <summary>Симметричные правила: два терма; центроид должен быть около 0.</summary>
    private static bool TestMamdaniCentroid()
    {
        Vector u = Universe(41, -1, 1);
        double hw = 0.35;
        Vector termLeft = TriangularTerm(u, -0.4, hw);
        Vector termRight = TriangularTerm(u, 0.4, hw);

        double w1 = 0.6;
        double w2 = 0.6;
        var weights = new List<double> { w1, w2 };
        var terms = new List<Vector> { termLeft, termRight };

        double z = FuzzyMamdaniInference.InferCentroid(weights, terms, u);
        Console.WriteLine($"   z (Мамдани) = {z:F6} (ожидается около 0)");
        return Math.Abs(z) < 0.08;
    }

    private static bool TestLarsenCentroid()
    {
        Vector u = Universe(41, -1, 1);
        double hw = 0.35;
        Vector termLeft = TriangularTerm(u, -0.4, hw);
        Vector termRight = TriangularTerm(u, 0.4, hw);

        double w1 = 0.6;
        double w2 = 0.6;
        var weights = new List<double> { w1, w2 };
        var terms = new List<Vector> { termLeft, termRight };

        double z = FuzzyLarsenInference.InferCentroid(weights, terms, u);
        Console.WriteLine($"   z (Ларсен)  = {z:F6} (ожидается около 0)");
        return Math.Abs(z) < 0.08;
    }

    private static bool TestSugenoSingletons()
    {
        var w = new List<double> { 0.2, 0.5, 0.3 };
        var c = new List<double> { -1, 0, 1 };
        double z = FuzzySugenoInference.WeightedAverageSingletons(w, c);
        double expected = 0.2 * (-1) + 0.5 * 0 + 0.3 * 1;
        Console.WriteLine($"   z (Сугено) = {z:F6}, эталон = {expected:F6}");
        return Math.Abs(z - expected) < 1e-9;
    }

    private static bool TestTsukamoto()
    {
        // μ(z) = z на [0,1], возрастание; при α=0.25 ожидается z=0.25
        double zInv = FuzzyTsukamotoInference.InverseMonotoneMembership(
            z => z,
            0.25,
            0,
            1,
            TsukamotoOutputMonotonicity.Increasing);
        Console.WriteLine($"   μ⁻¹(0.25) при μ(z)=z: z = {zInv:F6}");

        var weights = new List<double> { 0.5, 0.5 };
        var mus = new List<Func<double, double>>
        {
            z => z,
            z => 1 - z
        };
        // Правило 1: возрастающее на [0,1]; правило 2: убывающее на [0,1]
        double z1 = FuzzyTsukamotoInference.InverseMonotoneMembership(mus[0], 0.4, 0, 1, TsukamotoOutputMonotonicity.Increasing);
        double z2 = FuzzyTsukamotoInference.InverseMonotoneMembership(mus[1], 0.4, 0, 1, TsukamotoOutputMonotonicity.Decreasing);
        double zTs = FuzzySugenoInference.WeightedAverageSingletons(
            new List<double> { 0.5, 0.5 },
            new List<double> { z1, z2 });
        Console.WriteLine($"   Цукамото (два правила, α=0.4): z1={z1:F4}, z2={z2:F4}, среднее={zTs:F6}");

        bool ok1 = Math.Abs(zInv - 0.25) < 1e-4;
        bool ok2 = Math.Abs(z1 - 0.4) < 1e-4 && Math.Abs(z2 - 0.6) < 1e-4;
        return ok1 && ok2;
    }

    private static bool TestFuzzyPid()
    {
        var pidS = new FuzzyPIDController
        {
            Ke = 0.5,
            Kde = 0.5,
            Kie = 0.1,
            OutputGain = 1,
            Mode = FuzzyPIDOutputMode.Sugeno,
            AccumulateOutput = false
        };

        double u1 = pidS.Compute(1.0, 0.5, 0.1);
        double u2 = pidS.Compute(1.0, 0.8, 0.1);
        Console.WriteLine($"   PID Сугено: u при e=0.5: {u1:F4}; после второго шага: {u2:F4}");

        pidS.Reset();
        var pidM = new FuzzyPIDController
        {
            Ke = 0.5,
            Kde = 0.5,
            Kie = 0.1,
            Mode = FuzzyPIDOutputMode.Mamdani,
            AccumulateOutput = false
        };
        double m1 = pidM.Compute(1.0, 0.5, 0.1);
        Console.WriteLine($"   PID Мамдани: u при e=0.5: {m1:F4}");

        return !double.IsNaN(u1) && !double.IsNaN(m1) && !double.IsInfinity(u1);
    }
}
