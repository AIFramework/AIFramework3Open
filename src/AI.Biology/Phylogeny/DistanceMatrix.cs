using System.Globalization;
using System.Text;

namespace AI.Biology.Phylogeny;

/// <summary>
/// Матрица попарных расстояний между таксонами
/// </summary>
/// <remarks>
/// Симметрична, с нулевой диагональю. Бесконечные расстояния допускаются — так модели замен
/// сообщают о насыщении, — но построить по такой матрице дерево нельзя, и методы построения
/// об этом скажут.
/// </remarks>
public sealed class DistanceMatrix
{
    private readonly string[] _labels;
    private readonly double[,] _values;
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    /// <summary>Создаёт матрицу</summary>
    /// <param name="labels">Имена таксонов</param>
    /// <param name="values">Расстояния</param>
    public DistanceMatrix(IReadOnlyList<string> labels, double[,] values)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(values);

        int n = labels.Count;

        if (n == 0)
            throw new ArgumentException("Нужен хотя бы один таксон", nameof(labels));

        if (values.GetLength(0) != n || values.GetLength(1) != n)
            throw new ArgumentException($"Матрица должна быть {n}×{n}", nameof(values));

        for (int i = 0; i < n; i++)
        {
            if (string.IsNullOrWhiteSpace(labels[i]) || !_index.TryAdd(labels[i], i))
                throw new ArgumentException($"Имя таксона «{labels[i]}» пусто или повторяется", nameof(labels));
        }

        _values = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double value = values[i, j];
                double mirror = values[j, i];

                if (double.IsNaN(value) || value < 0)
                    throw new ArgumentException($"Расстояние {labels[i]}–{labels[j]} должно быть неотрицательным числом", nameof(values));

                if (i == j && value != 0)
                    throw new ArgumentException($"Расстояние таксона {labels[i]} до себя должно быть нулевым", nameof(values));

                bool bothInfinite = double.IsPositiveInfinity(value) && double.IsPositiveInfinity(mirror);

                if (!bothInfinite && !(Math.Abs(value - mirror) <= 1e-9 * Math.Max(1, Math.Abs(value))))
                    throw new ArgumentException($"Матрица несимметрична: {labels[i]}–{labels[j]}", nameof(values));

                _values[i, j] = value;
            }
        }

        _labels = labels.ToArray();
    }

    /// <summary>Число таксонов</summary>
    public int Count => _labels.Length;

    /// <summary>Имена таксонов</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Расстояние по номерам</summary>
    /// <param name="i">Первый таксон</param>
    /// <param name="j">Второй таксон</param>
    public double this[int i, int j] => _values[i, j];

    /// <summary>Расстояние по именам</summary>
    /// <param name="first">Первый таксон</param>
    /// <param name="second">Второй таксон</param>
    public double this[string first, string second] => _values[IndexOf(first), IndexOf(second)];

    /// <summary>Все ли расстояния конечны</summary>
    public bool IsFinite
    {
        get
        {
            foreach (double value in _values)
            {
                if (double.IsInfinity(value))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Номер таксона по имени</summary>
    /// <param name="label">Имя</param>
    public int IndexOf(string label)
        => _index.TryGetValue(label, out int index)
            ? index
            : throw new KeyNotFoundException($"Таксона «{label}» в матрице нет");

    /// <summary>
    /// Матрица расстояний между выровненными последовательностями
    /// </summary>
    /// <param name="labels">Имена</param>
    /// <param name="alignedSequences">Выровненные последовательности одной длины</param>
    /// <param name="model">Модель замен</param>
    public static DistanceMatrix FromSequences(
        IReadOnlyList<string> labels, IReadOnlyList<string> alignedSequences, DistanceModel model = DistanceModel.JukesCantor)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(alignedSequences);

        if (labels.Count != alignedSequences.Count)
            throw new ArgumentException("Имён должно быть столько же, сколько последовательностей", nameof(alignedSequences));

        int n = labels.Count;
        var values = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double distance = EvolutionaryDistance.Distance(alignedSequences[i], alignedSequences[j], model);

                if (double.IsNaN(distance))
                    throw new ArgumentException($"У {labels[i]} и {labels[j]} нет ни одной сравнимой позиции", nameof(alignedSequences));

                values[i, j] = distance;
                values[j, i] = distance;
            }
        }

        return new DistanceMatrix(labels, values);
    }

    /// <summary>
    /// Аддитивна ли матрица — порождается ли длинами путей в каком-то дереве
    /// </summary>
    /// <param name="tolerance">Допуск</param>
    /// <remarks>
    /// Условие четырёх точек: для любых четырёх таксонов из трёх сумм d(i,j)+d(k,l),
    /// d(i,k)+d(j,l), d(i,l)+d(j,k) две наибольшие равны. На аддитивной матрице метод
    /// присоединения соседей восстанавливает дерево точно.
    /// </remarks>
    public bool IsAdditive(double tolerance = 1e-9)
    {
        int n = Count;

        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                for (int k = j + 1; k < n; k++)
                    for (int l = k + 1; l < n; l++)
                    {
                        double[] sums =
                        [
                            _values[i, j] + _values[k, l],
                            _values[i, k] + _values[j, l],
                            _values[i, l] + _values[j, k]
                        ];

                        Array.Sort(sums);

                        if (Math.Abs(sums[2] - sums[1]) > tolerance * Math.Max(1, sums[2]))
                            return false;
                    }

        return true;
    }

    /// <summary>
    /// Ультраметрична ли матрица — порождается ли деревом с равными расстояниями от корня до листьев
    /// </summary>
    /// <param name="tolerance">Допуск</param>
    /// <remarks>
    /// Условие трёх точек: из трёх попарных расстояний два наибольших равны. Это допущение
    /// молекулярных часов, на котором стоит UPGMA.
    /// </remarks>
    public bool IsUltrametric(double tolerance = 1e-9)
    {
        int n = Count;

        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                for (int k = j + 1; k < n; k++)
                {
                    double[] sides = [_values[i, j], _values[i, k], _values[j, k]];
                    Array.Sort(sides);

                    if (Math.Abs(sides[2] - sides[1]) > tolerance * Math.Max(1, sides[2]))
                        return false;
                }

        return true;
    }

    internal double[,] ToArray() => (double[,])_values.Clone();

    /// <summary>Матрица в виде таблицы, как в формате PHYLIP</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.AppendLine(Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < Count; i++)
        {
            text.Append(_labels[i].PadRight(10));

            for (int j = 0; j < Count; j++)
                text.Append(' ').Append(_values[i, j].ToString("F5", CultureInfo.InvariantCulture));

            text.AppendLine();
        }

        return text.ToString();
    }
}
