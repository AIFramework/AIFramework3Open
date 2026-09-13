using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Simulation.Social;

/// <summary>
/// Модель Фридкина — Джонсена: агенты слушают других, но не забывают своих исходных убеждений.
/// </summary>
/// <remarks>
/// <para>
/// x(t + 1) = Λ·W·x(t) + (I − Λ)·u. Вектор u — исходные убеждения, λᵢ ∈ [0; 1] — восприимчивость
/// агента: при λᵢ = 1 он слушает только других, как у Де Грота, при λᵢ = 0 — упрям и держится своего.
/// Если хотя бы от кого-то влияние доходит до упрямца (λ &lt; 1), мнения сходятся не к согласию, а к
/// устойчивому разнообразию x* = (I − Λ·W)⁻¹·(I − Λ)·u — именно так модель объясняет, почему
/// общение сближает, но не уравнивает.
/// </para>
/// <para>
/// Равновесие считается решением линейной системы готовым LU-разложением, итерация — независимая
/// проверка. Если все восприимчивы полностью, система вырождена: это модель Де Грота.
/// </para>
/// </remarks>
public sealed class FriedkinJohnsenModel
{
    private readonly Matrix _influence;
    private readonly double[] _susceptibility;

    /// <summary>Создаёт модель</summary>
    /// <param name="influence">Матрица влияния, стохастическая по строкам</param>
    /// <param name="susceptibility">Восприимчивость каждого агента λᵢ ∈ [0; 1]</param>
    public FriedkinJohnsenModel(Matrix influence, IReadOnlyList<double> susceptibility)
    {
        _influence = InfluenceMatrix.Require(influence, nameof(influence));
        Size = _influence.Height;
        ArgumentNullException.ThrowIfNull(susceptibility);

        if (susceptibility.Count != Size)
            throw new ArgumentException($"Восприимчивостей {susceptibility.Count}, а агентов {Size}", nameof(susceptibility));

        if (susceptibility.Any(value => !(value >= 0 && value <= 1)))
            throw new ArgumentOutOfRangeException(nameof(susceptibility), "Восприимчивость лежит на [0; 1]");

        _susceptibility = [.. susceptibility];
    }

    /// <summary>Число агентов</summary>
    public int Size { get; }

    /// <summary>Восприимчивость агентов λ</summary>
    public IReadOnlyList<double> Susceptibility => _susceptibility;

    /// <summary>Один шаг: x′ = Λ·W·x + (I − Λ)·u</summary>
    /// <param name="opinions">Текущие мнения</param>
    /// <param name="prejudices">Исходные убеждения u</param>
    public double[] Step(IReadOnlyList<double> opinions, IReadOnlyList<double> prejudices)
    {
        InfluenceMatrix.RequireOpinions(opinions, Size, nameof(opinions));
        InfluenceMatrix.RequireOpinions(prejudices, Size, nameof(prejudices));

        double[] heard = InfluenceMatrix.Apply(_influence, opinions);

        return [.. heard.Select((value, i) => (_susceptibility[i] * value) + ((1 - _susceptibility[i]) * prejudices[i]))];
    }

    /// <summary>Траектория от исходных убеждений: начальные мнения и ещё steps шагов</summary>
    /// <param name="prejudices">Исходные убеждения, они же начальные мнения</param>
    /// <param name="steps">Число шагов</param>
    public double[][] Run(IReadOnlyList<double> prejudices, int steps)
    {
        InfluenceMatrix.RequireOpinions(prejudices, Size, nameof(prejudices));
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        var trajectory = new double[steps + 1][];
        trajectory[0] = [.. prejudices];

        for (int t = 1; t <= steps; t++)
            trajectory[t] = Step(trajectory[t - 1], prejudices);

        return trajectory;
    }

    /// <summary>Равновесие: x* = (I − Λ·W)⁻¹·(I − Λ)·u</summary>
    /// <param name="prejudices">Исходные убеждения u</param>
    /// <exception cref="InvalidOperationException">
    /// Есть замкнутая группа без упрямцев: её мнение зависит от начального, и единственного равновесия нет
    /// </exception>
    public double[] Equilibrium(IReadOnlyList<double> prejudices)
    {
        InfluenceMatrix.RequireOpinions(prejudices, Size, nameof(prejudices));

        var system = new Matrix(Size, Size);
        var right = new Vector(Size);

        for (int i = 0; i < Size; i++)
        {
            for (int j = 0; j < Size; j++)
                system[i, j] = (i == j ? 1 : 0) - (_susceptibility[i] * _influence[i, j]);

            right[i] = (1 - _susceptibility[i]) * prejudices[i];
        }

        try
        {
            return [.. LU.Solve(system, right)];
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException(
                "Единственного равновесия нет: в сети есть замкнутая группа, где все полностью восприимчивы. "
                + "Для неё это модель Де Грота, и её мнение зависит от начального", error);
        }
    }
}
