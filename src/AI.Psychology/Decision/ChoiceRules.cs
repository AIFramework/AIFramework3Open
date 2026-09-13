using AI.Psychology.Internal;

namespace AI.Psychology.Decision;

/// <summary>
/// Вероятностные правила выбора между вариантами.
/// </summary>
/// <remarks>
/// <para>
/// Люди выбирают лучший вариант не всегда, а тем чаще, чем больше он лучше. Правило Люса (1959):
/// вероятность выбора пропорциональна силе варианта, pᵢ = sᵢ/Σsⱼ; оно вытекает из аксиомы
/// независимости от посторонних альтернатив. Softmax — то же правило с силами exp(β·vᵢ): β — обратная
/// температура, при β = 0 выбор случаен, при β → ∞ всегда берётся лучший. Это та же модель, что
/// мультиномиальный логит в экономике и выбор действия в обучении с подкреплением.
/// </para>
/// <para>
/// Аксиома независимости нарушается у похожих вариантов (парадокс «красного и синего автобуса»):
/// добавление копии варианта отнимает долю у всех, а не только у оригинала.
/// </para>
/// </remarks>
public static class ChoiceRules
{
    /// <summary>Softmax: pᵢ = exp(β·vᵢ)/Σexp(β·vⱼ)</summary>
    /// <param name="values">Ценности вариантов</param>
    /// <param name="inverseTemperature">Обратная температура β ≥ 0</param>
    public static double[] Softmax(IReadOnlyList<double> values, double inverseTemperature)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
            throw new ArgumentException("Нужен хотя бы один вариант", nameof(values));

        if (!(inverseTemperature >= 0) || double.IsInfinity(inverseTemperature))
            throw new ArgumentOutOfRangeException(nameof(inverseTemperature), "Обратная температура — конечное неотрицательное число");

        foreach (double v in values)
            Numerics.RequireFinite(v, nameof(values));

        double max = values.Max();
        double[] exp = values.Select(v => Math.Exp(inverseTemperature * (v - max))).ToArray();
        double sum = exp.Sum();

        return exp.Select(e => e / sum).ToArray();
    }

    /// <summary>Правило Люса: pᵢ = sᵢ/Σsⱼ</summary>
    /// <param name="strengths">Положительные силы вариантов</param>
    public static double[] Luce(IReadOnlyList<double> strengths)
    {
        ArgumentNullException.ThrowIfNull(strengths);

        if (strengths.Count == 0)
            throw new ArgumentException("Нужен хотя бы один вариант", nameof(strengths));

        foreach (double s in strengths)
            Numerics.RequirePositive(s, nameof(strengths));

        double sum = strengths.Sum();

        return strengths.Select(s => s / sum).ToArray();
    }
}
