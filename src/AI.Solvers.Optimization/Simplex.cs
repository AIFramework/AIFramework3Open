namespace AI.Solvers.Optimization;

/// <summary>
/// Симплекс-метод в табличной форме для задачи в стандартном виде:
/// <c>min cᵀy</c> при <c>Ay = b</c>, <c>y ≥ 0</c>, <c>b ≥ 0</c>.
/// </summary>
/// <remarks>
/// <para>
/// Две фазы: сначала минимизируется сумма искусственных переменных, что даёт допустимый
/// базис либо доказывает несовместность; затем минимизируется исходная функция.
/// Искусственная переменная заводится только там, где без неё не обойтись: строка,
/// в которой есть столбец-одиночка с положительным коэффициентом (обычно балансовая
/// переменная), берёт его в начальный базис. Строку с нулевой правой частью можно
/// домножить на минус единицу, чтобы знак коэффициента стал подходящим. На задачах,
/// где почти у каждой строки есть своя балансовая переменная, первая фаза от этого
/// сжимается с числа строк до числа «неудобных» строк.
/// </para>
/// <para>
/// Правило выбора ведущего столбца — Данцига (наиболее отрицательная оценка), но после
/// <see cref="BlandAfter"/> итераций подряд без улучшения целевой функции решатель
/// переключается на правило Бланда до первого улучшения. Правило Данцига быстрее,
/// но на вырожденных задачах зацикливается; правило Бланда медленнее, зато
/// зацикливание исключено. Переключение даёт скорость первого и гарантию второго.
/// </para>
/// </remarks>
internal sealed class Simplex
{
    /// <summary>Порог, ниже которого число считается нулём</summary>
    internal const double Tolerance = 1e-9;

    /// <summary>После скольких итераций подряд без улучшения включается правило Бланда</summary>
    private const int BlandAfter = 200;

    private readonly int _rows;
    private readonly int _columns;
    private readonly double[,] _tableau;
    private readonly int[] _basis;
    private readonly int _maxIterations;

    private Simplex(double[,] tableau, int[] basis, int rows, int columns, int maxIterations)
    {
        _tableau = tableau;
        _basis = basis;
        _rows = rows;
        _columns = columns;
        _maxIterations = maxIterations;
    }

    /// <summary>Итог работы метода</summary>
    internal readonly record struct Result(SolverStatus Status, double[] Values, double Objective, int Iterations);

    /// <summary>
    /// Решает задачу в стандартном виде
    /// </summary>
    /// <param name="a">Матрица ограничений размера m×n</param>
    /// <param name="b">Правые части, неотрицательные</param>
    /// <param name="c">Коэффициенты минимизируемой функции</param>
    /// <param name="maxIterations">Предел числа итераций на обе фазы</param>
    internal static Result Solve(double[,] a, double[] b, double[] c, int maxIterations = 100_000)
    {
        int rows = b.Length;
        int columns = c.Length;

        // Нормализация знака строк: правые части неотрицательны
        var rowSign = new double[rows];
        for (int i = 0; i < rows; i++)
            rowSign[i] = b[i] < 0 ? -1.0 : 1.0;

        // Столбцы-одиночки: единственный ненулевой коэффициент во всей матрице
        var nonzeroCount = new int[columns];
        var nonzeroRow = new int[columns];

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < columns; j++)
                if (Math.Abs(a[i, j]) > Tolerance)
                {
                    nonzeroCount[j]++;
                    nonzeroRow[j] = i;
                }

        // Начальный базис из одиночек, где это возможно; счёт с конца — там обычно
        // стоят балансовые переменные, добавленные при приведении к стандартному виду
        var basisOfRow = new int[rows];
        Array.Fill(basisOfRow, -1);

        for (int j = columns - 1; j >= 0; j--)
        {
            if (nonzeroCount[j] != 1)
                continue;

            int i = nonzeroRow[j];

            if (basisOfRow[i] >= 0)
                continue;

            double coefficient = rowSign[i] * a[i, j];

            if (coefficient < -Tolerance && Math.Abs(b[i]) <= Tolerance)
            {
                // Правая часть нулевая: строку можно домножить на минус единицу
                rowSign[i] = -rowSign[i];
                coefficient = -coefficient;
            }

            if (coefficient > Tolerance)
                basisOfRow[i] = j;
        }

        // Искусственные переменные — только для строк, оставшихся без базиса
        int artificialCount = 0;
        for (int i = 0; i < rows; i++)
            if (basisOfRow[i] < 0)
                artificialCount++;

        int total = columns + artificialCount;
        var tableau = new double[rows + 1, total + 1];
        var basis = new int[rows];
        int artificial = columns;

        for (int i = 0; i < rows; i++)
        {
            // Строка делится на базисный коэффициент, чтобы столбец стал единичным;
            // знак строки при этом выправляется сам: коэффициент подобран знака rowSign
            double factor = basisOfRow[i] >= 0 ? 1.0 / a[i, basisOfRow[i]] : rowSign[i];

            for (int j = 0; j < columns; j++)
                tableau[i, j] = factor * a[i, j];

            tableau[i, total] = factor * b[i];

            if (basisOfRow[i] >= 0)
            {
                basis[i] = basisOfRow[i];
            }
            else
            {
                tableau[i, artificial] = 1.0;
                basis[i] = artificial;
                artificial++;
            }
        }

        var simplex = new Simplex(tableau, basis, rows, total, maxIterations);
        int iterations = 0;

        if (artificialCount > 0)
        {
            // Целевая строка первой фазы: минимум суммы искусственных переменных
            for (int j = 0; j <= total; j++)
            {
                double sum = 0;

                for (int i = 0; i < rows; i++)
                    if (basis[i] >= columns)
                        sum += tableau[i, j];

                tableau[rows, j] = j < columns || j == total ? -sum : 0.0;
            }

            SolverStatus phaseOne = simplex.Optimize(total, ref iterations);

            if (phaseOne == SolverStatus.LimitReached)
                return new Result(SolverStatus.LimitReached, new double[columns], double.NaN, iterations);

            if (-tableau[rows, total] > Tolerance * Math.Max(1, Math.Abs(tableau[rows, total])))
                return new Result(SolverStatus.Infeasible, new double[columns], double.NaN, iterations);

            simplex.DriveOutArtificials(columns);
        }

        // Фаза 2: целевая строка исходной задачи, выраженная через небазисные переменные
        for (int j = 0; j <= total; j++)
            tableau[rows, j] = 0.0;

        for (int j = 0; j < columns; j++)
            tableau[rows, j] = c[j];

        // Строка целей хранит приведённые оценки c_j - z_j, а последний столбец - минус значение
        // функции. Поэтому вклад базисных переменных вычитается, а не прибавляется.
        for (int i = 0; i < rows; i++)
        {
            int basic = basis[i];

            if (basic >= columns || Math.Abs(c[basic]) <= Tolerance)
                continue;

            double factor = c[basic];

            for (int j = 0; j <= total; j++)
                tableau[rows, j] -= factor * tableau[i, j];
        }

        SolverStatus phaseTwo = simplex.Optimize(columns, ref iterations);

        var values = new double[columns];

        for (int i = 0; i < rows; i++)
            if (basis[i] < columns)
                values[basis[i]] = tableau[i, total];

        double objective = -tableau[rows, total];

        return phaseTwo switch
        {
            SolverStatus.Unbounded => new Result(SolverStatus.Unbounded, values, double.NegativeInfinity, iterations),
            SolverStatus.LimitReached => new Result(SolverStatus.LimitReached, values, objective, iterations),
            _ => new Result(SolverStatus.Optimal, values, objective, iterations)
        };
    }

    /// <summary>Итерации до оптимума по первым <paramref name="activeColumns"/> столбцам</summary>
    /// <param name="activeColumns">Сколько столбцов допускается вводить в базис</param>
    /// <param name="iterations">Счётчик итераций, общий на обе фазы</param>
    private SolverStatus Optimize(int activeColumns, ref int iterations)
    {
        // Правило Бланда включается после серии вырожденных шагов и выключается,
        // как только целевая функция сдвинулась: зацикливание возможно только
        // на месте, а стоять на месте с правилом Бланда нельзя
        int stalled = 0;
        double reached = _tableau[_rows, _columns];

        while (true)
        {
            if (iterations >= _maxIterations)
                return SolverStatus.LimitReached;

            bool bland = stalled >= BlandAfter;
            int entering = ChooseEntering(activeColumns, bland);

            if (entering < 0)
                return SolverStatus.Optimal;

            int leaving = ChooseLeaving(entering, bland);

            if (leaving < 0)
                return SolverStatus.Unbounded;

            Pivot(leaving, entering);
            iterations++;

            // В последнем столбце целевой строки минус значение функции:
            // при минимизации он растёт
            double value = _tableau[_rows, _columns];
            if (value > reached + Tolerance)
            {
                reached = value;
                stalled = 0;
            }
            else
            {
                stalled++;
            }
        }
    }

    private int ChooseEntering(int activeColumns, bool bland)
    {
        int best = -1;
        double bestValue = -Tolerance;

        for (int j = 0; j < activeColumns; j++)
        {
            double reduced = _tableau[_rows, j];

            if (reduced >= -Tolerance)
                continue;

            if (bland)
                return j;

            if (reduced < bestValue)
            {
                bestValue = reduced;
                best = j;
            }
        }

        return best;
    }

    private int ChooseLeaving(int entering, bool bland)
    {
        int leaving = -1;
        double bestRatio = double.PositiveInfinity;

        for (int i = 0; i < _rows; i++)
        {
            double pivot = _tableau[i, entering];

            if (pivot <= Tolerance)
                continue;

            double ratio = _tableau[i, _columns] / pivot;

            if (ratio < bestRatio - Tolerance)
            {
                bestRatio = ratio;
                leaving = i;
            }
            else if (Math.Abs(ratio - bestRatio) <= Tolerance && leaving >= 0)
            {
                // Разрешение ничьей по наименьшему номеру базисной переменной — правило Бланда
                if (bland && _basis[i] < _basis[leaving])
                    leaving = i;
            }
        }

        return leaving;
    }

    private void Pivot(int row, int column)
    {
        double pivot = _tableau[row, column];

        for (int j = 0; j <= _columns; j++)
            _tableau[row, j] /= pivot;

        for (int i = 0; i <= _rows; i++)
        {
            if (i == row)
                continue;

            double factor = _tableau[i, column];

            if (Math.Abs(factor) <= Tolerance)
                continue;

            for (int j = 0; j <= _columns; j++)
                _tableau[i, j] -= factor * _tableau[row, j];
        }

        _basis[row] = column;
    }

    /// <summary>
    /// Выводит искусственные переменные из базиса. Если строка вырождена и вывести нечего,
    /// она линейно зависима от остальных и обнуляется.
    /// </summary>
    private void DriveOutArtificials(int structuralColumns)
    {
        for (int i = 0; i < _rows; i++)
        {
            if (_basis[i] < structuralColumns)
                continue;

            int replacement = -1;

            for (int j = 0; j < structuralColumns; j++)
            {
                if (Math.Abs(_tableau[i, j]) > Tolerance)
                {
                    replacement = j;
                    break;
                }
            }

            if (replacement >= 0)
            {
                Pivot(i, replacement);
                continue;
            }

            for (int j = 0; j <= _columns; j++)
                _tableau[i, j] = 0.0;
        }
    }
}
