using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Нарушение правил контрольной карты
/// </summary>
/// <param name="Index">Индекс точки</param>
/// <param name="Value">Значение</param>
/// <param name="Rule">Название сработавшего правила</param>
public readonly record struct ControlViolation(int Index, double Value, string Rule)
{
    /// <summary>Текстовое описание нарушения</summary>
    public override string ToString()
        => $"точка {Index + 1} ({Value.ToString("G6", CultureInfo.InvariantCulture)}): {Rule}";
}

/// <summary>
/// Контрольная карта Шухарта для индивидуальных значений (X-mR):
/// стандартный инструмент внутрилабораторного контроля стабильности методики.
/// </summary>
/// <remarks>
/// Границы строятся по среднему скользящему размаху: σ ≈ mR̄/1.128. Оценка по
/// скользящему размаху устойчивее выборочного СКО, поскольку не «впитывает»
/// медленный дрейф процесса в оценку разброса.
/// </remarks>
public sealed class ControlChart
{
    private const double D2ForPairs = 1.128; // d2 для подгрупп размера 2

    /// <summary>Значения контрольных измерений</summary>
    public double[] Values { get; }

    /// <summary>Центральная линия</summary>
    public double CenterLine { get; }

    /// <summary>Оценка стандартного отклонения процесса</summary>
    public double Sigma { get; }

    /// <summary>Скользящие размахи</summary>
    public double[] MovingRanges { get; }

    /// <summary>Верхняя контрольная граница (+3σ)</summary>
    public double UpperControlLimit => CenterLine + (3 * Sigma);

    /// <summary>Нижняя контрольная граница (-3σ)</summary>
    public double LowerControlLimit => CenterLine - (3 * Sigma);

    /// <summary>Верхняя предупреждающая граница (+2σ)</summary>
    public double UpperWarningLimit => CenterLine + (2 * Sigma);

    /// <summary>Нижняя предупреждающая граница (-2σ)</summary>
    public double LowerWarningLimit => CenterLine - (2 * Sigma);

    /// <summary>
    /// Строит карту по серии контрольных измерений
    /// </summary>
    /// <param name="values">Результаты контрольных измерений в порядке получения</param>
    /// <param name="centerLine">Аттестованное значение; null - среднее по серии</param>
    /// <param name="sigma">Известное СКО; null - оценка по скользящему размаху</param>
    public ControlChart(double[] values, double? centerLine = null, double? sigma = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length < 2)
            throw new ArgumentException("A control chart requires at least two points");

        Values = (double[])values.Clone();
        MovingRanges = new double[values.Length - 1];

        for (int i = 1; i < values.Length; i++)
            MovingRanges[i - 1] = Math.Abs(values[i] - values[i - 1]);

        CenterLine = centerLine ?? values.Average();
        Sigma = sigma ?? MovingRanges.Average() / D2ForPairs;

        if (Sigma <= 0)
            Sigma = double.Epsilon;
    }

    /// <summary>
    /// Проверка правил Western Electric: выход за 3σ, 2 из 3 за 2σ, 4 из 5 за 1σ,
    /// 8 подряд по одну сторону, 7 подряд по тренду
    /// </summary>
    public IReadOnlyList<ControlViolation> Violations()
    {
        var violations = new List<ControlViolation>();
        int n = Values.Length;

        // Зоны в единицах сигма относительно центра
        var zone = new double[n];
        for (int i = 0; i < n; i++)
            zone[i] = (Values[i] - CenterLine) / Sigma;

        for (int i = 0; i < n; i++)
        {
            if (Math.Abs(zone[i]) > 3)
                violations.Add(new ControlViolation(i, Values[i], "выход за контрольную границу 3σ"));
        }

        // 2 из 3 подряд за пределами 2σ с одной стороны
        for (int i = 2; i < n; i++)
        {
            if (CountBeyond(zone, i - 2, 3, 2, positive: true) >= 2)
                violations.Add(new ControlViolation(i, Values[i], "2 из 3 точек за 2σ (сверху)"));
            else if (CountBeyond(zone, i - 2, 3, 2, positive: false) >= 2)
                violations.Add(new ControlViolation(i, Values[i], "2 из 3 точек за 2σ (снизу)"));
        }

        // 4 из 5 подряд за пределами 1σ с одной стороны
        for (int i = 4; i < n; i++)
        {
            if (CountBeyond(zone, i - 4, 5, 1, positive: true) >= 4)
                violations.Add(new ControlViolation(i, Values[i], "4 из 5 точек за 1σ (сверху)"));
            else if (CountBeyond(zone, i - 4, 5, 1, positive: false) >= 4)
                violations.Add(new ControlViolation(i, Values[i], "4 из 5 точек за 1σ (снизу)"));
        }

        // 8 подряд по одну сторону от центральной линии
        for (int i = 7; i < n; i++)
        {
            bool above = true, below = true;

            for (int j = i - 7; j <= i; j++)
            {
                above &= zone[j] > 0;
                below &= zone[j] < 0;
            }

            if (above || below)
                violations.Add(new ControlViolation(i, Values[i], $"8 точек подряд {(above ? "выше" : "ниже")} центральной линии"));
        }

        // 7 подряд монотонно (дрейф)
        for (int i = 6; i < n; i++)
        {
            bool rising = true, falling = true;

            for (int j = i - 6; j < i; j++)
            {
                rising &= Values[j + 1] > Values[j];
                falling &= Values[j + 1] < Values[j];
            }

            if (rising || falling)
                violations.Add(new ControlViolation(i, Values[i], $"7 точек подряд {(rising ? "возрастают" : "убывают")}"));
        }

        return violations.OrderBy(v => v.Index).ToList();
    }

    /// <summary>Находится ли процесс в статистически управляемом состоянии</summary>
    public bool InControl => Violations().Count == 0;

    /// <summary>Отчёт по контрольной карте</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Контрольная карта Шухарта (индивидуальные значения)");
        text.AppendLine($"  Точек: {Values.Length}");
        text.AppendLine($"  Центральная линия: {CenterLine.ToString("G6", culture)}");
        text.AppendLine($"  σ (по скользящему размаху): {Sigma.ToString("G4", culture)}");
        text.AppendLine($"  Контрольные границы: [{LowerControlLimit.ToString("G6", culture)}; {UpperControlLimit.ToString("G6", culture)}]");
        text.AppendLine($"  Предупреждающие границы: [{LowerWarningLimit.ToString("G6", culture)}; {UpperWarningLimit.ToString("G6", culture)}]");

        var violations = Violations();

        if (violations.Count == 0)
        {
            text.AppendLine("  Процесс статистически управляем: нарушений нет");
        }
        else
        {
            text.AppendLine($"  Нарушений: {violations.Count}");

            foreach (var violation in violations)
                text.AppendLine($"    {violation}");
        }

        return text.ToString();
    }

    private static int CountBeyond(double[] zone, int start, int length, double threshold, bool positive)
    {
        int count = 0;

        for (int i = start; i < start + length && i < zone.Length; i++)
        {
            if (positive ? zone[i] > threshold : zone[i] < -threshold)
                count++;
        }

        return count;
    }
}
