using System.Text.RegularExpressions;
using AIFramework.UnitTests.Dsp;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Храповик математического ядра: у каждого понятия один дом, копии не появляются заново.
/// </summary>
/// <remarks>
/// <para>
/// Каждая проверка считает по тексту <c>src</c> места, где понятие определено, и сравнивает с
/// разрешённым списком: дом понятия и поимённо перечисленные остатки — адаптеры без своей логики
/// под старыми публичными именами и случаи с записанной причиной. Новый файл или лишнее
/// определение в старом роняет тест. Число только уменьшается: когда адаптер удаляют, его строка
/// уходит из списка — тест напомнит об этом сам.
/// </para>
/// <para>
/// Проверяется текст, а не поведение: вторая реализация логарифма гамма-функции собирается и
/// проходит свои тесты, просто однажды начинает отвечать иначе, чем первая.
/// </para>
/// </remarks>
public sealed partial class MathOneHomeRatchetTests
{
    private static readonly Lazy<IReadOnlyList<(string Path, string Code)>> s_sources = new(LoadSources);

    // --- спецфункции ---

    /// <summary>
    /// Определения гамма-функции, её логарифма, неполных гамма- и бета-функций, функции ошибок и факториала.
    /// </summary>
    private static readonly Dictionary<string, int> s_specialFunctions = new(StringComparer.Ordinal)
    {
        // Дом: Gamma, LogGamma, LogBeta, RegularizedGammaP/Q, RegularizedBeta, Erf, Erfc, Factorial, LogFactorial
        // и их закрытые части (GammaPrefactor, GammaSeries, GammaFraction, BetaDirect, BetaFraction, LogGammaNearOne)
        ["src/AI/HighLevelFunctions/SpecialFunctions.cs"] = 16,
        // [Obsolete]-адаптеры Gamma, LogGamma, Erf, Erfc, Factorial и поэлементные Erf(Vector), Factorial(Vector)
        ["src/AI/HighLevelFunctions/FunctionsForEachElements.cs"] = 7,
        // [Obsolete]-адаптеры Erf, Erfc, LogGamma
        ["src/AI/Statistics/StatInference.cs"] = 3,
        // [Obsolete]-адаптер Factorial во вставленном NWaves
        ["src/AI.DSP/BackEnds/DSP/NWaves/Utils/MathUtils.cs"] = 1,
        // Привязка math.factorial к канону: имя функции скрипта, а не реализация
        ["src/AI.Script.Std/MathModule.cs"] = 1,
        // Узлы символьного выражения erf и erfc: конструкторы типов, значение считает канон
        ["src/AI.Solvers.Math/Core/Functions/Erf.cs"] = 1,
        ["src/AI.Solvers.Math/Core/Functions/Erfc.cs"] = 1,
        // Γ(k) в алгоритме Ремеза — множитель полинома Лагранжа, с гамма-функцией совпадает только именем
        ["src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/Remez.cs"] = 1,
    };

    [Fact]
    public void SpecialFunctions_AreDefinedInOneHome() =>
        AssertRatchet("спецфункции — в AI.HighLevelFunctions.SpecialFunctions", SpecialFunctionDefinition(), s_specialFunctions);

    /// <summary>
    /// Приметы самостоятельной реализации: коэффициенты Ланцоша и формулы Абрамовица — Стиган для erf.
    /// </summary>
    [Fact]
    public void SpecialFunctions_HaveNoHandRolledApproximations() =>
        AssertRatchet("приближения гамма-функции и erf вне SpecialFunctions", HandRolledApproximation(), new Dictionary<string, int>(StringComparer.Ordinal));

    // --- физические и геоконстанты ---

    /// <summary>
    /// Числа фундаментальных констант и параметров Земли вне таблиц AI.Units.
    /// </summary>
    private static readonly Dictionary<string, int> s_constantLiterals = new(StringComparer.Ordinal)
    {
        // Таблицы: PhysicalConstants (Values и неопределённости), EarthConstants, определения единиц в Si
        ["src/AI/Units/PhysicalConstants.cs"] = 12,
        ["src/AI/Units/EarthConstants.cs"] = 5,
        ["src/AI/Units/Si.cs"] = 2,
        // Стандартная атмосфера 1976 года: R* = 8.31432 — число стандарта, а не CODATA;
        // с другим значением давления расходятся с таблицами стандарта
        ["src/AI.Physics/Fluids/StandardAtmosphere.cs"] = 1,
        // Рекомендация МСЭ-R P.1812: радиус Земли 6371 км входит в её формулы
        ["src/AI.Microwave/Propagation/ItuP1812.cs"] = 1,
        // Примеры вызова в справке AI-скрипта: числа пишет человек в своём скрипте
        ["src/AI.Script.Science/PhysModule.cs"] = 1,
        ["src/AI.Script.Science/PhysModule.Matter.cs"] = 1,
    };

    [Fact]
    public void PhysicalConstants_AreWrittenOnce() =>
        AssertRatchet("числа физических и геоконстант — в AI.Units.PhysicalConstants и EarthConstants", ConstantLiteral(), s_constantLiterals);

    // --- механика храповика ---

    private static void AssertRatchet(string concept, Regex pattern, IReadOnlyDictionary<string, int> allowed)
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach ((string path, string code) in s_sources.Value)
        {
            int count = pattern.Matches(code).Count;

            if (count > 0) found[path] = count;
        }

        string[] extra = [.. found
            .Where(pair => pair.Value > allowed.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} (разрешено {allowed.GetValueOrDefault(pair.Key)})")
            .Order(StringComparer.Ordinal)];

        Assert.True(extra.Length == 0, $"Новая копия: {concept}.\n  " + string.Join("\n  ", extra));

        string[] slack = [.. allowed
            .Where(pair => found.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key}: стало {found.GetValueOrDefault(pair.Key)}, в списке {pair.Value}")
            .Order(StringComparer.Ordinal)];

        Assert.True(slack.Length == 0, $"Копий стало меньше — уменьшите число в храповике ({concept}).\n  " + string.Join("\n  ", slack));
    }

    /// <summary>Исходники <c>src</c> без комментариев: упоминание в описании — не определение.</summary>
    private static IReadOnlyList<(string Path, string Code)> LoadSources()
    {
        string root = DspGolden.RepositoryRoot;
        var sources = new List<(string, string)>();

        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string path = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light")) continue;

            sources.Add((path, Comments().Replace(File.ReadAllText(file), string.Empty)));
        }

        return sources;
    }

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\b(?:public|private|internal|protected)\s+(?:static\s+)?[\w<>\[\],. ]*?\b(?:Gamma|LogGamma|LnGamma|GammaLn|LogBeta|LnBeta|Erf|Erfc|ErfInv|Factorial|LogFactorial|RegularizedGamma[PQ]|IncompleteGamma\w*|GammaPrefactor|GammaSeries|GammaFraction|RegularizedBeta|RegularizedIncompleteBeta|IncompleteBeta\w*|BetaCF|BetaFraction|BetaDirect|LogGammaNearOne)\s*\(")]
    private static partial Regex SpecialFunctionDefinition();

    [GeneratedRegex(@"676\.5203681218851|0\.254829592|1\.061405429|0\.3275911|LanczosCoefficients")]
    private static partial Regex HandRolledApproximation();

    // c, h, e, k, N_A, R, F, σ, g, атмосфера, G, ε₀, μ₀; радиусы и масса Земли, сжатие WGS 84
    [GeneratedRegex(@"(?<![\w.])(?:299_?792_?458|6\.626\d*e-34|1\.602\d*e-19|1\.380\d*e-23|6\.022\d*e\+?23|8\.314\d*|96_?485(?:\.\d+)?|5\.670\d*e-0?8|9\.80665|101_?325(?:\.0)?|6\.67\d*e-11|8\.854\d*e-12|1\.2566\d*e-0?6|6_?371(?:_?\d{3})?(?:\.\d+)?(?:e\d)?|6\.371e6|6_?378_?137(?:\.0)?|6_?378_?245(?:\.0)?|298\.257\d*|5\.972\d*e24)(?![\w.]*\d)")]
    private static partial Regex ConstantLiteral();
}
