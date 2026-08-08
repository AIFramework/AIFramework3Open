using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Fuzzy.Inference;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Globalization;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Fuzzy;

/// <summary>
/// Общая для всех демо задача — нечёткий термостат.
///
/// Вход:  температура $t \in [0, 40]$ °C, три терма «Холодно», «Норма», «Жарко».
/// Выход: мощность нагревателя $u \in [0, 100]$ %, три терма «Низкая», «Средняя», «Высокая».
/// Правила:
///   R1: если Холодно -> Высокая
///   R2: если Норма   -> Средняя
///   R3: если Жарко   -> Низкая
///
/// Одна база правил на все четыре схемы вывода — иначе различия между ними
/// невозможно приписать самим схемам.
/// </summary>
public static partial class FuzzyDemoRunner
{
    // -- Палитра ---------------------------------------------------------

    private static readonly SKColor ColdColor   = new(0x38, 0xBD, 0xF8);
    private static readonly SKColor NormColor   = new(0x4A, 0xDE, 0x80);
    private static readonly SKColor HotColor    = new(0xF8, 0x71, 0x71);
    private static readonly SKColor AggColor    = new(0xFB, 0xBF, 0x24);
    private static readonly SKColor CrispColor  = new(0xA7, 0x8B, 0xFA);
    private static readonly SKColor SecondColor = new(0xE8, 0x79, 0xF9);

    // -- Универсумы ------------------------------------------------------

    private const double TempMin = 0, TempMax = 40;
    private const double PowMin  = 0, PowMax  = 100;

    private static readonly string[] InputTerms  = ["Холодно", "Норма", "Жарко"];
    private static readonly string[] OutputTerms = ["Низкая", "Средняя", "Высокая"];

    /// <summary>Правило i связывает входной терм i с выходным термом RuleMap[i].</summary>
    private static readonly int[] RuleMap = [2, 1, 0];   // Холодно->Высокая, Норма->Средняя, Жарко->Низкая

    /// <summary>Синглтоны Сугено 0-го порядка: центры выходных термов.</summary>
    private static readonly double[] Singletons = [10, 50, 90];

    // -- Точка входа -----------------------------------------------------

    public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp, DemoSettings s)
    {
        var cv  = MakeView(s);
        var rep = new ReportBuilder();
        string txt;

        try
        {
            txt = key switch
            {
                "fuzzy_membership" => DoMembership(p, cv, rep),
                "fuzzy_mamdani"    => DoMamdaniOrLarsen(p, cv, rep, larsen: false),
                "fuzzy_larsen"     => DoMamdaniOrLarsen(p, cv, rep, larsen: true),
                "fuzzy_sugeno"     => DoSugeno(p, cv, rep),
                "fuzzy_tsukamoto"  => DoTsukamoto(p, cv, rep),
                "fuzzy_compare"    => DoCompare(p, cv, rep),
                _                  => $"Неизвестный ключ «{key}»",
            };
        }
        catch (Exception ex)
        {
            rep = new ReportBuilder();
            txt = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}";
        }

        return Png(cv, s, textOutput: txt, report: rep.Build());
    }

    // -- Фаззификация ----------------------------------------------------

    /// <summary>
    /// Границы трёх входных термов на [0, 40] с настраиваемым перекрытием.
    /// overlap = 0 — термы стыкуются краями, overlap = 1 — вершина соседа
    /// попадает на край текущего терма.
    /// </summary>
    private static (double a, double b, double c, double d)[] InputTermBounds(double overlap)
    {
        double step = (TempMax - TempMin) / 3.0;          // 13.33
        double ov   = overlap * step;                      // ширина захода на соседа
        var bounds = new (double, double, double, double)[3];

        for (int i = 0; i < 3; i++)
        {
            double centre = TempMin + step * (i + 0.5);
            double a = centre - step / 2 - ov;
            double d = centre + step / 2 + ov;
            // Для трапеции плато занимает среднюю треть между скатами
            double b = centre - step / 6;
            double c = centre + step / 6;
            bounds[i] = (a, b, c, d);
        }

        return bounds;
    }

    /// <summary>Степень принадлежности x терму index при выбранной форме.</summary>
    private static double InputMu(double x, int index, int shape, double overlap)
    {
        var (a, b, c, d) = InputTermBounds(overlap)[index];
        return shape == 1
            ? FuzzyMembershipShapes.Trapezoidal(x, a, b, c, d)
            : FuzzyMembershipShapes.Triangular(x, a, (b + c) / 2, d);
    }

    /// <summary>Степени срабатывания всех трёх правил для входа x.</summary>
    private static double[] FiringStrengths(double x, int shape, double overlap)
    {
        var w = new double[3];
        for (int i = 0; i < 3; i++) w[i] = InputMu(x, i, shape, overlap);
        return w;
    }

    /// <summary>Треугольные выходные термы «Низкая / Средняя / Высокая» на [0, 100].</summary>
    private static double OutputMu(double u, int term)
    {
        double centre = Singletons[term];
        return FuzzyMembershipShapes.Triangular(u, centre - 40, centre, centre + 40);
    }

    /// <summary>Сетка универсума выхода.</summary>
    private static Vector PowerGrid(int n)
    {
        var g = new Vector(n);
        for (int i = 0; i < n; i++) g[i] = PowMin + (PowMax - PowMin) * i / (n - 1.0);
        return g;
    }

    private static Vector OutputTermSamples(int term, Vector grid)
    {
        var v = new Vector(grid.Count);
        for (int i = 0; i < grid.Count; i++) v[i] = OutputMu(grid[i], term);
        return v;
    }

    // -- Утилиты ---------------------------------------------------------

    private static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
    private static string F2(double v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static void Axes(ChartView cv, string x, string y)
    {
        cv.LabelX = x;
        cv.LabelY = y;
    }

    /// <summary>Вертикальная линия-указатель на графике: чёткий результат.</summary>
    private static void AddVerticalMarker(ChartView cv, double x, double yMax, string name, SKColor color)
    {
        var vx = new Vector(2); vx[0] = x; vx[1] = x;
        var vy = new Vector(2); vy[0] = 0; vy[1] = yMax;
        cv.AddPlot(vx, vy, name, color, 3);
    }
}
