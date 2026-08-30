using AI.DataStructs.Algebraic;

namespace AI.Solvers.Pde.Numerics;

/// <summary>
/// Разреженная матрица в строчном формате, собираемая по элементам.
/// </summary>
/// <remarks>
/// <para>
/// Матрицы конечных элементов и конечных разностей почти пусты: в строке пять-семь
/// ненулевых при любом размере задачи. Плотное хранение сетки 200×200 потребовало бы
/// 13 гигабайт, разреженное — единицы мегабайт.
/// </para>
/// <para>
/// Сборка идёт в словарь по координатам, затем матрица «замораживается» в строчный формат:
/// так удобно накапливать вклады элементов, которые приходят вразнобой и суммируются.
/// </para>
/// </remarks>
public sealed class SparseMatrix
{
    private readonly Dictionary<(int Row, int Column), double> _entries = [];
    private int[]? _rowStart;
    private int[]? _columns;
    private double[]? _values;

    /// <summary>Создаёт матрицу заданного размера</summary>
    /// <param name="rows">Число строк</param>
    /// <param name="columns">Число столбцов</param>
    public SparseMatrix(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);

        Rows = rows;
        Columns = columns;
    }

    /// <summary>Число строк</summary>
    public int Rows { get; }

    /// <summary>Число столбцов</summary>
    public int Columns { get; }

    /// <summary>Число накопленных ненулевых элементов</summary>
    public int NonZeroCount => _values?.Length ?? _entries.Count;

    /// <summary>
    /// Добавляет вклад в элемент. Повторные обращения к той же позиции суммируются —
    /// именно так собирается матрица жёсткости из вкладов отдельных элементов.
    /// </summary>
    /// <param name="row">Строка</param>
    /// <param name="column">Столбец</param>
    /// <param name="value">Добавляемое значение</param>
    public void Add(int row, int column, double value)
    {
        if (_values is not null)
            throw new InvalidOperationException("Матрица уже собрана: изменять её нельзя");

        if (value == 0.0)
            return;

        _entries[(row, column)] = _entries.TryGetValue((row, column), out double existing)
            ? existing + value
            : value;
    }

    /// <summary>Значение элемента</summary>
    /// <param name="row">Строка</param>
    /// <param name="column">Столбец</param>
    public double this[int row, int column]
    {
        get
        {
            if (_values is null)
                return _entries.GetValueOrDefault((row, column));

            for (int index = _rowStart![row]; index < _rowStart[row + 1]; index++)
                if (_columns![index] == column)
                    return _values[index];

            return 0.0;
        }
    }

    /// <summary>
    /// Обнуляет строку и ставит единицу на диагональ — так задаётся условие Дирихле
    /// </summary>
    /// <param name="row">Номер строки</param>
    public void SetIdentityRow(int row)
    {
        if (_values is not null)
            throw new InvalidOperationException("Матрица уже собрана: изменять её нельзя");

        foreach ((int Row, int Column) key in _entries.Keys.Where(k => k.Row == row).ToList())
            _ = _entries.Remove(key);

        _entries[(row, row)] = 1.0;
    }

    /// <summary>
    /// Исключает известное неизвестное: строка и столбец обнуляются, на диагональ ставится
    /// единица, а вклад известного значения переносится в правую часть.
    /// </summary>
    /// <remarks>
    /// Обнулять одну лишь строку нельзя: матрица потеряет симметрию, а метод сопряжённых
    /// градиентов на несимметричной матрице расходится. Поэтому столбец исключается тоже.
    /// </remarks>
    /// <param name="index">Номер неизвестного</param>
    /// <param name="value">Известное значение</param>
    /// <param name="rightHandSide">Правая часть, изменяется на месте</param>
    public void EliminateKnown(int index, double value, Vector rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(rightHandSide);

        if (_values is not null)
            throw new InvalidOperationException("Матрица уже собрана: изменять её нельзя");

        foreach (((int row, int column), double entry) in _entries.ToList())
        {
            if (column != index || row == index)
                continue;

            rightHandSide[row] -= entry * value;
            _ = _entries.Remove((row, column));
        }

        foreach ((int Row, int Column) key in _entries.Keys.Where(k => k.Row == index).ToList())
            _ = _entries.Remove(key);

        _entries[(index, index)] = 1.0;
        rightHandSide[index] = value;
    }

    /// <summary>Переводит матрицу в строчный формат; после этого изменения запрещены</summary>
    public void Compress()
    {
        if (_values is not null)
            return;

        var counts = new int[Rows + 1];

        foreach ((int row, _) in _entries.Keys)
            counts[row + 1]++;

        for (int row = 0; row < Rows; row++)
            counts[row + 1] += counts[row];

        _rowStart = counts;
        _columns = new int[_entries.Count];
        _values = new double[_entries.Count];

        var position = (int[])counts.Clone();

        foreach (((int row, int column), double value) in _entries.OrderBy(e => e.Key.Row).ThenBy(e => e.Key.Column))
        {
            int index = position[row]++;
            _columns[index] = column;
            _values[index] = value;
        }

        _entries.Clear();
    }

    /// <summary>Произведение матрицы на вектор</summary>
    /// <param name="vector">Вектор длины <see cref="Columns"/></param>
    public Vector Multiply(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Count != Columns)
            throw new ArgumentException("Длина вектора не совпадает с числом столбцов", nameof(vector));

        Compress();

        var result = new Vector(Rows);

        for (int row = 0; row < Rows; row++)
        {
            double sum = 0;

            for (int index = _rowStart![row]; index < _rowStart[row + 1]; index++)
                sum += _values![index] * vector[_columns![index]];

            result[row] = sum;
        }

        return result;
    }

    /// <summary>Диагональ матрицы — нужна для простейшего предобусловливателя</summary>
    public Vector Diagonal()
    {
        Compress();

        var diagonal = new Vector(Rows);

        for (int row = 0; row < Rows; row++)
            diagonal[row] = this[row, row];

        return diagonal;
    }

    /// <summary>Плотное представление — только для малых матриц и отладки</summary>
    public Matrix ToDense()
    {
        Compress();

        var dense = new Matrix(Rows, Columns);

        for (int row = 0; row < Rows; row++)
            for (int index = _rowStart![row]; index < _rowStart[row + 1]; index++)
                dense[row, _columns![index]] = _values![index];

        return dense;
    }

    /// <summary>Краткое описание матрицы</summary>
    public override string ToString() => $"разреженная {Rows}×{Columns}, ненулевых {NonZeroCount}";
}
