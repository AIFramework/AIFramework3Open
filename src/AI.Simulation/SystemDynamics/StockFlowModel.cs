using AI.DataStructs.Algebraic;
using AI.MathUtils.ODE;

namespace AI.Simulation.SystemDynamics;

/// <summary>Состояние модели в момент времени</summary>
/// <param name="Time">Время</param>
/// <param name="Levels">Значения запасов</param>
public readonly record struct SystemState(double Time, IReadOnlyList<double> Levels);

/// <summary>
/// Модель системной динамики: запасы, потоки и обратные связи.
/// </summary>
/// <remarks>
/// <para>
/// Запас меняется только потоками, а потоки зависят от запасов — в этом вся суть подхода.
/// Обратная связь возникает сама собой, как только поток начинает зависеть от того запаса,
/// который он наполняет, и именно она порождает и рост по экспоненте, и колебания,
/// и внезапные обвалы.
/// </para>
/// <para>
/// Уравнения интегрируются методом Рунге — Кутты из <c>AI.ClassicMath</c>. Классический
/// подход системной динамики использует метод Эйлера с малым шагом; здесь взят более точный
/// метод, потому что он уже есть и не требует подбирать шаг вручную.
/// </para>
/// </remarks>
public sealed class StockFlowModel
{
    private readonly List<string> _names = [];
    private readonly List<double> _initial = [];
    private readonly List<Func<double, IReadOnlyList<double>, double>> _rates = [];

    /// <summary>Создаёт модель</summary>
    /// <param name="name">Название</param>
    public StockFlowModel(string name = "Модель системной динамики") => Name = name;

    /// <summary>Название модели</summary>
    public string Name { get; }

    /// <summary>Названия запасов в порядке объявления</summary>
    public IReadOnlyList<string> Stocks => _names;

    /// <summary>
    /// Добавляет запас с правилом изменения
    /// </summary>
    /// <param name="name">Название запаса</param>
    /// <param name="initial">Начальное значение</param>
    /// <param name="netFlow">
    /// Чистый поток: разность притока и оттока в зависимости от времени и всех запасов
    /// </param>
    public StockFlowModel AddStock(
        string name, double initial, Func<double, IReadOnlyList<double>, double> netFlow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(netFlow);

        if (_names.Contains(name))
            throw new ArgumentException($"Запас «{name}» уже объявлен", nameof(name));

        _names.Add(name);
        _initial.Add(initial);
        _rates.Add(netFlow);

        return this;
    }

    /// <summary>Номер запаса по названию</summary>
    /// <param name="name">Название</param>
    public int IndexOf(string name)
    {
        int index = _names.IndexOf(name);

        return index < 0 ? throw new KeyNotFoundException($"Запаса «{name}» в модели нет") : index;
    }

    /// <summary>
    /// Рассчитывает поведение модели во времени
    /// </summary>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="points">Число точек вывода</param>
    /// <param name="stepsPerInterval">Число шагов интегрирования между точками</param>
    public IReadOnlyList<SystemState> Run(double finalTime, int points = 100, int stepsPerInterval = 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalTime);
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 2);

        if (_names.Count == 0)
            throw new InvalidOperationException("В модели нет ни одного запаса");

        var times = new double[points];
        double step = finalTime / (points - 1);

        for (int i = 0; i < points; i++)
            times[i] = i * step;

        Vector Derivative(double time, Vector levels)
        {
            var rates = new Vector(_rates.Count);
            double[] snapshot = levels.ToArray();

            for (int i = 0; i < _rates.Count; i++)
                rates[i] = _rates[i](time, snapshot);

            return rates;
        }

        Vector[] solution = RungeKutta.SolveSystem(
            Derivative, 0, new Vector(_initial.ToArray()), times, stepsPerInterval);

        var states = new SystemState[points];

        for (int i = 0; i < points; i++)
            states[i] = new SystemState(times[i], solution[i].ToArray());

        return states;
    }

    /// <summary>
    /// Значение запаса в конце расчёта
    /// </summary>
    /// <param name="states">Результат расчёта</param>
    /// <param name="stock">Название запаса</param>
    public double Final(IReadOnlyList<SystemState> states, string stock)
    {
        ArgumentNullException.ThrowIfNull(states);

        return states[^1].Levels[IndexOf(stock)];
    }
}
