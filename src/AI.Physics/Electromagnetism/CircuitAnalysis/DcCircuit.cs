#nullable enable
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Physics.Electromagnetism.CircuitAnalysis;

/// <summary>
/// Цепь постоянного тока в установившемся режиме, модифицированный узловой анализ: неизвестны потенциалы узлов
/// и токи источников напряжения.
/// </summary>
/// <remarks>
/// <para>
/// Все величины в СИ: омы, вольты, амперы, фарады, генри. Узел 0 есть земля, остальные создает <see cref="AddNode"/>.
/// В установившемся режиме постоянного тока конденсатор есть разрыв, а катушка есть перемычка: ее ток находится
/// как ток источника напряжения нулевой величины. Поэтому ток катушки и напряжение конденсатора известны,
/// а ток конденсатора и напряжение катушки равны нулю.
/// </para>
/// <para>
/// Потенциал узла, не связанного с землей через сопротивления, источники напряжения и катушки, не определен
/// (у конденсатора он зависит от начального заряда): такие узлы возвращает <see cref="FloatingNodes"/>,
/// и <see cref="Solve"/> для них бросает исключение. Контур только из источников напряжения и катушек
/// тоже не решается: токи в нем не определены или напряжения противоречат друг другу.
/// </para>
/// </remarks>
public sealed class DcCircuit
{
    /// <summary>Номер узла земли</summary>
    public const int Ground = 0;

    private readonly List<Branch> _branches = [];

    /// <summary>Число узлов вместе с землей</summary>
    public int NodeCount { get; private set; } = 1;

    /// <summary>Число элементов</summary>
    public int BranchCount => _branches.Count;

    /// <summary>Создает узел</summary>
    /// <returns>Номер узла</returns>
    public int AddNode() => NodeCount++;

    /// <summary>Добавляет сопротивление</summary>
    /// <param name="a">Первый узел</param>
    /// <param name="b">Второй узел</param>
    /// <param name="resistance">Сопротивление, Ом</param>
    /// <returns>Номер ветви</returns>
    public int AddResistor(int a, int b, double resistance)
    {
        if (!(resistance > 0) || double.IsInfinity(resistance))
            throw new ArgumentOutOfRangeException(nameof(resistance), "Сопротивление должно быть положительным и конечным");

        return Add(DcElementKind.Resistor, a, b, resistance);
    }

    /// <summary>Добавляет источник напряжения: потенциал плюса выше потенциала минуса на заданное напряжение</summary>
    /// <param name="plus">Узел плюса</param>
    /// <param name="minus">Узел минуса</param>
    /// <param name="voltage">Напряжение, В</param>
    /// <returns>Номер ветви</returns>
    public int AddVoltageSource(int plus, int minus, double voltage) => Add(DcElementKind.VoltageSource, plus, minus, voltage);

    /// <summary>Добавляет источник тока: ток течет через источник от узла from к узлу to и втекает во внешнюю цепь в узле to</summary>
    /// <param name="from">Узел, из которого источник забирает ток</param>
    /// <param name="to">Узел, в который источник отдает ток</param>
    /// <param name="current">Ток, А</param>
    /// <returns>Номер ветви</returns>
    public int AddCurrentSource(int from, int to, double current) => Add(DcElementKind.CurrentSource, from, to, current);

    /// <summary>Добавляет конденсатор: в установившемся режиме разрыв</summary>
    /// <param name="a">Первый узел</param>
    /// <param name="b">Второй узел</param>
    /// <param name="capacitance">Емкость, Ф; на режим постоянного тока не влияет</param>
    /// <returns>Номер ветви</returns>
    public int AddCapacitor(int a, int b, double capacitance)
    {
        if (!(capacitance > 0))
            throw new ArgumentOutOfRangeException(nameof(capacitance), "Емкость должна быть положительной");

        return Add(DcElementKind.Capacitor, a, b, capacitance);
    }

    /// <summary>Добавляет катушку индуктивности: в установившемся режиме перемычка</summary>
    /// <param name="a">Первый узел</param>
    /// <param name="b">Второй узел</param>
    /// <param name="inductance">Индуктивность, Гн; на режим постоянного тока не влияет</param>
    /// <returns>Номер ветви</returns>
    public int AddInductor(int a, int b, double inductance)
    {
        if (!(inductance > 0))
            throw new ArgumentOutOfRangeException(nameof(inductance), "Индуктивность должна быть положительной");

        return Add(DcElementKind.Inductor, a, b, inductance);
    }

    /// <summary>Вид элемента ветви</summary>
    /// <param name="branch">Номер ветви</param>
    public DcElementKind KindOf(int branch) => _branches[branch].Kind;

    /// <summary>
    /// Узлы без проводящего пути к земле через сопротивления, источники напряжения и катушки: их потенциал
    /// в режиме постоянного тока не определен
    /// </summary>
    public IReadOnlyList<int> FloatingNodes()
    {
        var parent = Enumerable.Range(0, NodeCount).ToArray();

        int Root(int node)
        {
            while (parent[node] != node)
                node = parent[node] = parent[parent[node]];

            return node;
        }

        foreach (Branch branch in _branches.Where(Conducts))
            parent[Root(branch.A)] = Root(branch.B);

        int ground = Root(Ground);
        return Enumerable.Range(1, NodeCount - 1).Where(node => Root(node) != ground).ToList();
    }

    /// <summary>Находит установившийся режим</summary>
    /// <exception cref="InvalidOperationException">Есть плавающие узлы или контур из источников напряжения и катушек</exception>
    public DcCircuitSolution Solve()
    {
        IReadOnlyList<int> floating = FloatingNodes();
        if (floating.Count > 0)
        {
            throw new InvalidOperationException(
                $"Потенциал узлов {string.Join(", ", floating)} не определен: у них нет проводящего пути к земле");
        }

        int free = NodeCount - 1;
        int[] extraRow = new int[_branches.Count];
        int size = free;

        for (int k = 0; k < _branches.Count; k++)
            extraRow[k] = _branches[k].Kind is DcElementKind.VoltageSource or DcElementKind.Inductor ? size++ : -1;

        var matrix = new Matrix(size, size);
        var rhs = new Vector(size);

        void Stamp(int row, int column, double value)
        {
            if (row > 0 && column > 0)
                matrix[row - 1, column - 1] += value;
        }

        for (int k = 0; k < _branches.Count; k++)
        {
            (DcElementKind kind, int a, int b, double value) = _branches[k];

            switch (kind)
            {
                case DcElementKind.Resistor:
                    double conductance = 1 / value;
                    Stamp(a, a, conductance);
                    Stamp(b, b, conductance);
                    Stamp(a, b, -conductance);
                    Stamp(b, a, -conductance);
                    break;

                case DcElementKind.CurrentSource:
                    if (b > 0)
                        rhs[b - 1] += value;
                    if (a > 0)
                        rhs[a - 1] -= value;
                    break;

                case DcElementKind.VoltageSource:
                case DcElementKind.Inductor:
                    // Неизвестная: ток через элемент от a к b; уравнение: φa − φb = U (у катушки U = 0)
                    int row = extraRow[k];
                    if (a > 0)
                        (matrix[a - 1, row], matrix[row, a - 1]) = (matrix[a - 1, row] + 1, matrix[row, a - 1] + 1);
                    if (b > 0)
                        (matrix[b - 1, row], matrix[row, b - 1]) = (matrix[b - 1, row] - 1, matrix[row, b - 1] - 1);
                    rhs[row] = kind == DcElementKind.VoltageSource ? value : 0;
                    break;
            }
        }

        Vector solution;
        try
        {
            solution = size == 0 ? new Vector(0) : LU.Solve(matrix, rhs);
        }
        catch (Exception error) when (error is ArithmeticException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException("Цепь не решается: есть контур только из источников напряжения и катушек", error);
        }

        if (!solution.All(double.IsFinite))
            throw new InvalidOperationException("Цепь не решается: есть контур только из источников напряжения и катушек");

        double Potential(int node) => node == Ground ? 0 : solution[node - 1];

        var voltages = new double[_branches.Count];
        var currents = new double[_branches.Count];

        for (int k = 0; k < _branches.Count; k++)
        {
            (DcElementKind kind, int a, int b, double value) = _branches[k];
            voltages[k] = Potential(a) - Potential(b);
            currents[k] = kind switch
            {
                DcElementKind.Resistor => voltages[k] / value,
                DcElementKind.CurrentSource => value,
                DcElementKind.VoltageSource or DcElementKind.Inductor => solution[extraRow[k]],
                _ => 0,
            };
        }

        // Катушка есть перемычка: ее напряжение ровно ноль, а не остаток округления
        for (int k = 0; k < _branches.Count; k++)
        {
            if (_branches[k].Kind == DcElementKind.Inductor)
                voltages[k] = 0;
        }

        return new DcCircuitSolution(
            new Vector(Enumerable.Range(0, NodeCount).Select(Potential)),
            new Vector(currents),
            new Vector(voltages),
            new Vector(voltages.Select((voltage, k) => voltage * currents[k])),
            _branches.Select(branch => branch.Kind).ToList());
    }

    private static bool Conducts(Branch branch)
        => branch.Kind is DcElementKind.Resistor or DcElementKind.VoltageSource or DcElementKind.Inductor;

    private int Add(DcElementKind kind, int a, int b, double value)
    {
        if (a < 0 || a >= NodeCount)
            throw new ArgumentOutOfRangeException(nameof(a), $"Узла {a} нет");
        if (b < 0 || b >= NodeCount)
            throw new ArgumentOutOfRangeException(nameof(b), $"Узла {b} нет");
        if (a == b)
            throw new ArgumentException("Элемент должен соединять разные узлы", nameof(b));
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Величина элемента должна быть конечной");

        _branches.Add(new Branch(kind, a, b, value));
        return _branches.Count - 1;
    }

    private sealed record Branch(DcElementKind Kind, int A, int B, double Value);
}
