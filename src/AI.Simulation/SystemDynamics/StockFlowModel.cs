using AI.DataStructs.Algebraic;
using AI.MathUtils.ODE;
using AI.Statistics;

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
/// <para>
/// У запаса могут быть границы: склад не бывает меньше нуля, оценка по шкале — больше десяти.
/// На границе поток, который вывел бы запас наружу, отсекается, а поток внутрь действует как
/// обычно. У запаса может быть и шум — тогда модель становится стохастической:
/// dx = f·dt + g·dW, и расчёт с генератором идёт методом Эйлера — Маруямы.
/// </para>
/// </remarks>
public sealed class StockFlowModel
{
    private readonly List<string> _names = [];
    private readonly List<double> _initial = [];
    private readonly List<Func<double, IReadOnlyList<double>, double>> _rates = [];
    private readonly List<Func<double, IReadOnlyList<double>, double>?> _noises = [];
    private readonly List<double> _lower = [];
    private readonly List<double> _upper = [];

    /// <summary>Создаёт модель</summary>
    /// <param name="name">Название</param>
    public StockFlowModel(string name = "Модель системной динамики") => Name = name;

    /// <summary>Название модели</summary>
    public string Name { get; }

    /// <summary>Названия запасов в порядке объявления</summary>
    public IReadOnlyList<string> Stocks => _names;

    /// <summary>Есть ли у какого-нибудь запаса шум</summary>
    public bool IsStochastic => _noises.Any(noise => noise is not null);

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
        => AddStock(name, initial, netFlow, double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>
    /// Добавляет запас с границами и, если нужно, шумом
    /// </summary>
    /// <param name="name">Название запаса</param>
    /// <param name="initial">Начальное значение, внутри границ</param>
    /// <param name="netFlow">Чистый поток f: снос в зависимости от времени и всех запасов</param>
    /// <param name="lower">Нижняя граница; −∞ — без неё</param>
    /// <param name="upper">Верхняя граница; +∞ — без неё</param>
    /// <param name="noise">
    /// Интенсивность шума g: в единицах запаса на √время, в зависимости от времени и всех запасов;
    /// <c>null</c> — запас без шума
    /// </param>
    public StockFlowModel AddStock(
        string name, double initial, Func<double, IReadOnlyList<double>, double> netFlow,
        double lower, double upper, Func<double, IReadOnlyList<double>, double>? noise = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(netFlow);

        if (_names.Contains(name))
            throw new ArgumentException($"Запас «{name}» уже объявлен", nameof(name));

        if (double.IsNaN(lower) || double.IsNaN(upper) || lower > upper)
            throw new ArgumentException($"Границы запаса «{name}» заданы неверно: [{lower}; {upper}]");

        if (!(initial >= lower && initial <= upper))
            throw new ArgumentOutOfRangeException(nameof(initial), $"Начальное значение запаса «{name}» лежит вне границ [{lower}; {upper}]");

        _names.Add(name);
        _initial.Add(initial);
        _rates.Add(netFlow);
        _noises.Add(noise);
        _lower.Add(lower);
        _upper.Add(upper);

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
    /// Рассчитывает поведение модели во времени без шума
    /// </summary>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="points">Число точек вывода</param>
    /// <param name="stepsPerInterval">Число шагов интегрирования между точками</param>
    /// <remarks>Шум запасов, если он задан, здесь не учитывается: это расчёт ожидаемого сноса</remarks>
    public IReadOnlyList<SystemState> Run(double finalTime, int points = 100, int stepsPerInterval = 20)
    {
        double[] times = Times(finalTime, points);

        Vector Derivative(double time, Vector levels)
        {
            var rates = new Vector(_rates.Count);
            double[] snapshot = Clamp(levels.ToArray());

            for (int i = 0; i < _rates.Count; i++)
                rates[i] = Drift(i, time, snapshot);

            return rates;
        }

        Vector[] solution = RungeKutta.SolveSystem(
            Derivative, 0, new Vector(_initial.ToArray()), times, stepsPerInterval);

        var states = new SystemState[points];

        // Внутренний шаг Рунге — Кутты может чуть перешагнуть границу; наружу уровень выдаётся прижатым
        for (int i = 0; i < points; i++)
            states[i] = new SystemState(times[i], Clamp(solution[i].ToArray()));

        return states;
    }

    /// <summary>
    /// Рассчитывает одну случайную траекторию модели методом Эйлера — Маруямы
    /// </summary>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="random">Генератор: одинаковое зерно даёт одинаковую траекторию</param>
    /// <param name="points">Число точек вывода</param>
    /// <param name="stepsPerInterval">Число шагов интегрирования между точками</param>
    /// <remarks>
    /// Метод Эйлера — Маруямы сходится к решению уравнения с шумом со слабым порядком 1: моменты
    /// распределения ошибаются пропорционально шагу. Для запасов без шума он проигрывает методу
    /// Рунге — Кутты в точности, поэтому модель без шума считается методом <see cref="Run(double, int, int)"/>.
    /// </remarks>
    public IReadOnlyList<SystemState> Run(double finalTime, Random random, int points = 100, int stepsPerInterval = 20)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(stepsPerInterval, 1);

        if (!IsStochastic)
            return Run(finalTime, points, stepsPerInterval);

        double[] times = Times(finalTime, points);
        double step = times[1] / stepsPerInterval;
        double root = Math.Sqrt(step);
        double[] levels = [.. _initial];
        var drift = new double[levels.Length];
        var states = new SystemState[points];
        states[0] = new SystemState(0, [.. levels]);

        for (int point = 1; point < points; point++)
        {
            for (int k = 0; k < stepsPerInterval; k++)
            {
                double time = times[point - 1] + (k * step);

                for (int i = 0; i < levels.Length; i++)
                    drift[i] = Drift(i, time, levels);

                // Шум считается по уровням в начале шага: так записан интеграл Ито
                for (int i = 0; i < levels.Length; i++)
                {
                    double diffusion = _noises[i]?.Invoke(time, levels) ?? 0;
                    double shock = diffusion == 0 ? 0 : diffusion * root * RandomEngine.NextGaussian(random);
                    levels[i] += (drift[i] * step) + shock;
                }

                levels = Clamp(levels);
            }

            states[point] = new SystemState(times[point], [.. levels]);
        }

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

    private double[] Times(double finalTime, int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalTime);
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 2);

        if (_names.Count == 0)
            throw new InvalidOperationException("В модели нет ни одного запаса");

        var times = new double[points];
        double step = finalTime / (points - 1);

        for (int i = 0; i < points; i++)
            times[i] = i * step;

        return times;
    }

    /// <summary>Поток запаса с отсечением на границе: наружу из границ поток не течёт</summary>
    private double Drift(int i, double time, IReadOnlyList<double> levels)
    {
        double flow = _rates[i](time, levels);

        return (levels[i] <= _lower[i] && flow < 0) || (levels[i] >= _upper[i] && flow > 0) ? 0 : flow;
    }

    private double[] Clamp(double[] levels)
    {
        for (int i = 0; i < levels.Length; i++)
            levels[i] = Math.Clamp(levels[i], _lower[i], _upper[i]);

        return levels;
    }
}
