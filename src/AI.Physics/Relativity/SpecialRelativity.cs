using AI.Units;

namespace AI.Physics.Relativity;

/// <summary>
/// Кинематика специальной теории относительности: лоренц-фактор, сложение скоростей,
/// замедление времени, сокращение длины, эффект Доплера.
/// </summary>
/// <remarks>
/// Ньютоновская механика модуля — предел этих формул при β → 0. Разница становится заметной
/// раньше, чем кажется: уже при десятой доле скорости света лоренц-фактор отличается от единицы
/// на полпроцента, а для электрона с энергией в несколько мегаэлектронвольт классическая
/// кинетическая энергия ошибается в разы.
/// </remarks>
public static class SpecialRelativity
{
    /// <summary>Скорость в долях скорости света</summary>
    /// <param name="speed">Скорость; по модулю меньше скорости света</param>
    public static double Beta(Quantity speed)
    {
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));

        return RequireBeta(v / PhysicalConstants.SpeedOfLight.SiValue, nameof(speed));
    }

    /// <summary>Лоренц-фактор <c>γ = 1/√(1 − v²/c²)</c></summary>
    /// <param name="speed">Скорость</param>
    public static double LorentzFactor(Quantity speed) => LorentzFactor(Beta(speed));

    /// <summary>Лоренц-фактор по скорости в долях c</summary>
    /// <param name="beta">Скорость в долях скорости света</param>
    public static double LorentzFactor(double beta)
    {
        RequireBeta(beta, nameof(beta));

        // (1 − β)(1 + β) точнее, чем 1 − β², когда β близка к единице
        return 1 / Math.Sqrt((1 - beta) * (1 + beta));
    }

    /// <summary>
    /// Быстрота <c>artanh β</c> — величина, которая при последовательных бустах вдоль одной оси
    /// просто складывается
    /// </summary>
    /// <param name="beta">Скорость в долях скорости света</param>
    public static double Rapidity(double beta) => Math.Atanh(RequireBeta(beta, nameof(beta)));

    /// <summary>
    /// Релятивистское сложение скоростей вдоль одной прямой: <c>(u + v)/(1 + uv/c²)</c>
    /// </summary>
    /// <remarks>Сумма двух досветовых скоростей всегда меньше скорости света.</remarks>
    /// <param name="first">Скорость тела в движущейся системе</param>
    /// <param name="second">Скорость самой системы</param>
    public static Quantity AddVelocities(Quantity first, Quantity second)
    {
        double u = Beta(first);
        double v = Beta(second);

        return new Quantity((u + v) / (1 + (u * v)) * PhysicalConstants.SpeedOfLight.SiValue, Dimension.Velocity);
    }

    /// <summary>Замедление времени: интервал по часам наблюдателя <c>γτ</c></summary>
    /// <param name="properTime">Собственное время движущихся часов</param>
    /// <param name="speed">Скорость часов</param>
    public static Quantity DilatedTime(Quantity properTime, Quantity speed)
        => new(properTime.RequireSi(Dimension.TimeDim, nameof(properTime)) * LorentzFactor(speed), Dimension.TimeDim);

    /// <summary>Сокращение длины вдоль движения: <c>L₀/γ</c></summary>
    /// <param name="properLength">Длина в системе покоя тела</param>
    /// <param name="speed">Скорость тела</param>
    public static Quantity ContractedLength(Quantity properLength, Quantity speed)
        => new(properLength.RequireSi(Dimension.LengthDim, nameof(properLength)) / LorentzFactor(speed), Dimension.LengthDim);

    /// <summary>
    /// Продольный эффект Доплера: отношение принятой частоты к излучённой, <c>√((1 − β)/(1 + β))</c>
    /// </summary>
    /// <param name="beta">Скорость источника в долях c; положительная — источник удаляется</param>
    public static double DopplerFactor(double beta)
    {
        RequireBeta(beta, nameof(beta));

        return Math.Sqrt((1 - beta) / (1 + beta));
    }

    private static double RequireBeta(double beta, string paramName)
    {
        if (!(Math.Abs(beta) < 1))
            throw new ArgumentOutOfRangeException(paramName,
                $"Скорость β = {beta:G6} не меньше скорости света: тело с массой её не достигает");

        return beta;
    }
}
