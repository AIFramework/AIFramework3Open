using AI.Geometry.Primitives;

namespace AI.Physics.Relativity;

/// <summary>
/// Четырёхвектор пространства Минковского: временная компонента и пространственная часть.
/// </summary>
/// <remarks>
/// <para>
/// Сигнатура (+, −, −, −): квадрат четырёхвектора <c>T² − |S|²</c> одинаков во всех инерциальных
/// системах. Для четырёхимпульса это <c>(mc²)²</c> — масса не зависит от того, кто на частицу
/// смотрит, в отличие от энергии и импульса по отдельности.
/// </para>
/// <para>
/// Компоненты хранятся в одних единицах. Четырёхимпульсы модуля — в джоулях: энергия и <c>pc</c>;
/// так все четыре компоненты однородны, и скорость света не путается в формулах.
/// Пространственная часть — общий <see cref="Vector3"/> из <c>AI.Geometry</c>.
/// </para>
/// </remarks>
/// <param name="T">Временная компонента; для четырёхимпульса — полная энергия</param>
/// <param name="Space">Пространственная часть; для четырёхимпульса — импульс, умноженный на c</param>
public readonly record struct FourVector(double T, Vector3 Space)
{
    /// <summary>Нулевой четырёхвектор</summary>
    public static FourVector Zero => new(0, Vector3.Zero);

    /// <summary>Сумма — например, полный четырёхимпульс системы частиц</summary>
    /// <param name="a">Первое слагаемое</param>
    /// <param name="b">Второе слагаемое</param>
    public static FourVector operator +(FourVector a, FourVector b) => new(a.T + b.T, a.Space + b.Space);

    /// <summary>Разность</summary>
    /// <param name="a">Уменьшаемое</param>
    /// <param name="b">Вычитаемое</param>
    public static FourVector operator -(FourVector a, FourVector b) => new(a.T - b.T, a.Space - b.Space);

    /// <summary>Умножение на число</summary>
    /// <param name="a">Четырёхвектор</param>
    /// <param name="k">Множитель</param>
    public static FourVector operator *(FourVector a, double k) => new(a.T * k, a.Space * k);

    /// <summary>Скалярное произведение Минковского <c>T·T′ − S·S′</c></summary>
    /// <param name="other">Второй четырёхвектор</param>
    public double Dot(FourVector other) => (T * other.T) - Space.Dot(other.Space);

    /// <summary>Квадрат четырёхвектора — инвариант преобразований Лоренца</summary>
    public double Square => Dot(this);

    /// <summary>
    /// Скорость в долях скорости света, соответствующая четырёхимпульсу: <c>β = pc/E</c>
    /// </summary>
    public Vector3 Velocity => T > 0
        ? Space / T
        : throw new InvalidOperationException("Скорость определена только для четырёхимпульса с положительной энергией");

    /// <summary>
    /// Тот же четырёхвектор в системе отсчёта, движущейся со скоростью <paramref name="beta"/>
    /// относительно исходной
    /// </summary>
    /// <remarks>
    /// Общее преобразование Лоренца без поворота осей. Чтобы перейти в систему покоя частицы,
    /// достаточно передать её собственную скорость: <c>p.Boost(p.Velocity)</c> даёт <c>(mc², 0)</c>.
    /// Множитель <c>(γ − 1)/β²</c> вычисляется как <c>γ²/(γ + 1)</c> — без вычитания близких чисел
    /// при малых скоростях.
    /// </remarks>
    /// <param name="beta">Скорость новой системы в долях скорости света</param>
    public FourVector Boost(Vector3 beta)
    {
        double b2 = beta.LengthSquared;

        if (!(b2 < 1))
            throw new ArgumentOutOfRangeException(nameof(beta),
                "Скорость системы отсчёта должна быть меньше скорости света");

        if (b2 == 0)
            return this;

        double gamma = 1 / Math.Sqrt(1 - b2);
        double parallel = beta.Dot(Space);
        double time = gamma * (T - parallel);
        Vector3 space = Space + (beta * (((gamma * gamma / (gamma + 1)) * parallel) - (gamma * T)));

        return new FourVector(time, space);
    }
}
