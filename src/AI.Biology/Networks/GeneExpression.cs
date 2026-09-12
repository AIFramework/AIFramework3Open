using AI.Solvers.Chem.Kinetics;

namespace AI.Biology.Networks;

/// <summary>Стационарные моменты числа молекул в двухстадийной модели экспрессии</summary>
/// <param name="MeanMrna">Среднее число мРНК</param>
/// <param name="MeanProtein">Среднее число белка</param>
/// <param name="MrnaFano">Фактор Фано мРНК — дисперсия, делённая на среднее</param>
/// <param name="ProteinFano">Фактор Фано белка</param>
public readonly record struct GeneExpressionMoments(double MeanMrna, double MeanProtein, double MrnaFano, double ProteinFano);

/// <summary>
/// Стохастическая экспрессия гена: транскрипция, трансляция и распад как отдельные случайные события.
/// </summary>
/// <remarks>
/// <para>
/// Двухстадийная модель: мРНК синтезируется с частотой kₘ и распадается со скоростью γₘ на
/// молекулу, каждая мРНК производит белок с частотой kₚ, белок распадается со скоростью γₚ.
/// В стационаре число мРНК распределено по Пуассону со средним kₘ/γₘ, а белок шумит сильнее:
/// фактор Фано <c>1 + kₚ/(γₘ + γₚ)</c> (Таттай и ван Ауденарден, 2001). Каждая мРНК выпускает
/// «вспышку» белков, и именно вспышки, а не редкость белка, делают клетки непохожими друг на друга.
/// </para>
/// <para>
/// Схема собирается из <see cref="KineticScheme"/> химического модуля и разыгрывается методом
/// Гиллеспи из <see cref="StochasticKinetics"/>; своей кинетики здесь нет. Та же схема
/// интегрируется детерминированно — это предел больших чисел молекул.
/// </para>
/// </remarks>
public static class GeneExpression
{
    /// <summary>Имя вещества «мРНК»</summary>
    public const string Mrna = "mRNA";

    /// <summary>Имя вещества «белок»</summary>
    public const string Protein = "protein";

    /// <summary>
    /// Схема двухстадийной экспрессии; константы по порядку: kₘ, γₘ, kₚ, γₚ
    /// </summary>
    public static KineticScheme TwoStage() => new(
        [Mrna, Protein],
        [
            new ReactionStep
            {
                Name = "транскрипция",
                Reactants = new Dictionary<string, double>(),
                Products = new Dictionary<string, double> { [Mrna] = 1 },
                RateConstantIndex = 0
            },
            new ReactionStep
            {
                Name = "распад мРНК",
                Reactants = new Dictionary<string, double> { [Mrna] = 1 },
                Products = new Dictionary<string, double>(),
                RateConstantIndex = 1
            },
            new ReactionStep
            {
                Name = "трансляция",
                Reactants = new Dictionary<string, double> { [Mrna] = 1 },
                Products = new Dictionary<string, double> { [Mrna] = 1, [Protein] = 1 },
                RateConstantIndex = 2
            },
            new ReactionStep
            {
                Name = "распад белка",
                Reactants = new Dictionary<string, double> { [Protein] = 1 },
                Products = new Dictionary<string, double>(),
                RateConstantIndex = 3
            }
        ]);

    /// <summary>Стационарные средние и факторы Фано</summary>
    /// <param name="transcription">Частота транскрипции kₘ</param>
    /// <param name="mrnaDecay">Скорость распада мРНК γₘ</param>
    /// <param name="translation">Частота трансляции kₚ на одну мРНК</param>
    /// <param name="proteinDecay">Скорость распада белка γₚ</param>
    public static GeneExpressionMoments StationaryMoments(
        double transcription, double mrnaDecay, double translation, double proteinDecay)
    {
        RequirePositive(transcription, nameof(transcription));
        RequirePositive(mrnaDecay, nameof(mrnaDecay));
        RequirePositive(translation, nameof(translation));
        RequirePositive(proteinDecay, nameof(proteinDecay));

        double mrna = transcription / mrnaDecay;

        return new GeneExpressionMoments(
            mrna,
            mrna * translation / proteinDecay,
            1,
            1 + (translation / (mrnaDecay + proteinDecay)));
    }

    /// <summary>Разыгрывает одну реализацию экспрессии</summary>
    /// <param name="transcription">Частота транскрипции kₘ</param>
    /// <param name="mrnaDecay">Скорость распада мРНК γₘ</param>
    /// <param name="translation">Частота трансляции kₚ</param>
    /// <param name="proteinDecay">Скорость распада белка γₚ</param>
    /// <param name="finalTime">Время моделирования</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="initialMrna">Начальное число мРНК</param>
    /// <param name="initialProtein">Начальное число белка</param>
    public static StochasticTrajectory Simulate(
        double transcription, double mrnaDecay, double translation, double proteinDecay,
        double finalTime, Random random, int initialMrna = 0, int initialProtein = 0)
        => StochasticKinetics.Simulate(
            TwoStage(),
            [initialMrna, initialProtein],
            [transcription, mrnaDecay, translation, proteinDecay],
            finalTime,
            random);

    private static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Константа должна быть положительным числом");
    }
}
