using System.Buffers;

namespace AI.Biology.Sequences;

/// <summary>Схема счёта для пары символов: подобие и аффинный штраф за пропуск</summary>
/// <param name="Similarity">Счёт за пару символов</param>
/// <param name="GapOpen">Штраф за открытие пропуска</param>
/// <param name="GapExtend">Штраф за продление пропуска</param>
internal sealed record PairScoring(Func<char, char, double> Similarity, double GapOpen, double GapExtend);

/// <summary>Путь выравнивания: столбцы как пары номеров позиций, −1 — пропуск</summary>
/// <param name="Score">Счёт</param>
/// <param name="Columns">Столбцы слева направо</param>
internal readonly record struct AlignmentPath(double Score, IReadOnlyList<(int First, int Second)> Columns);

/// <summary>
/// Динамическое программирование Гото: три состояния и аффинный штраф за пропуск.
/// </summary>
/// <remarks>
/// <para>
/// Одно ядро на все выравнивания: пары последовательностей и пары профилей множественного
/// выравнивания отличаются только тем, как считается счёт столбца. Пропуск может открыться после
/// любого состояния, обратный ход идёт по состояниям.
/// </para>
/// <para>
/// Таблицы плоские и берутся из общего пула массивов: прежде двумерные таблицы выделялись заново
/// на каждое выравнивание, и множественное выравнивание восьми последовательностей по 800
/// нуклеотидов тратило почти пять секунд на выделение памяти и адресацию.
/// </para>
/// </remarks>
internal static class GotohAligner
{
    private const double NegativeInfinity = -1e18;

    private enum State
    {
        Pair,
        GapInFirst,
        GapInSecond
    }

    /// <summary>Оптимальное выравнивание по функции счёта столбца</summary>
    /// <param name="n">Длина первой последовательности</param>
    /// <param name="m">Длина второй</param>
    /// <param name="similarity">Счёт столбца из позиций i первой и j второй, с нуля</param>
    /// <param name="open">Штраф за открытие пропуска</param>
    /// <param name="extend">Штраф за продление пропуска</param>
    /// <param name="local">Локальное ли выравнивание</param>
    public static AlignmentPath Solve(int n, int m, Func<int, int, double> similarity, double open, double extend, bool local)
    {
        ArgumentNullException.ThrowIfNull(similarity);

        double[] scores = ArrayPool<double>.Shared.Rent(Math.Max(1, CellCount(n, m)));

        try
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                    scores[(i * m) + j] = similarity(i, j);
            }

            return Solve(n, m, scores, open, extend, local);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(scores);
        }
    }

    /// <summary>Оптимальное выравнивание по готовой таблице счетов столбцов</summary>
    /// <param name="n">Длина первой последовательности</param>
    /// <param name="m">Длина второй</param>
    /// <param name="scores">Счёт столбца (i, j) в элементе i·m + j</param>
    /// <param name="open">Штраф за открытие пропуска</param>
    /// <param name="extend">Штраф за продление пропуска</param>
    /// <param name="local">Локальное ли выравнивание</param>
    public static AlignmentPath Solve(int n, int m, double[] scores, double open, double extend, bool local)
    {
        if (double.IsNaN(open) || double.IsNaN(extend))
            throw new ArgumentException("Штрафы за пропуск не заданы");

        int width = m + 1;
        int size = CellCount(n + 1, width);
        double[] pair = ArrayPool<double>.Shared.Rent(size);
        double[] gapFirst = ArrayPool<double>.Shared.Rent(size);
        double[] gapSecond = ArrayPool<double>.Shared.Rent(size);

        try
        {
            Array.Fill(pair, NegativeInfinity, 0, size);
            Array.Fill(gapFirst, NegativeInfinity, 0, size);
            Array.Fill(gapSecond, NegativeInfinity, 0, size);

            pair[0] = 0;

            if (local)
            {
                for (int i = 0; i <= n; i++)
                    pair[i * width] = 0;

                for (int j = 0; j <= m; j++)
                    pair[j] = 0;
            }

            double best = local ? 0 : NegativeInfinity;
            int bestCell = 0;

            for (int i = 0; i <= n; i++)
            {
                int row = i * width;
                int above = row - width;

                for (int j = 0; j <= m; j++)
                {
                    int cell = row + j;

                    if (i > 0 && j > 0)
                    {
                        int diagonalCell = above + j - 1;
                        double diagonal = scores[((i - 1) * m) + j - 1]
                            + Max(pair[diagonalCell], gapFirst[diagonalCell], gapSecond[diagonalCell]);

                        pair[cell] = local ? Math.Max(0, diagonal) : diagonal;
                    }

                    if (j > 0)
                        gapFirst[cell] = Max(pair[cell - 1] + open, gapFirst[cell - 1] + extend, gapSecond[cell - 1] + open);

                    if (i > 0)
                    {
                        int upper = above + j;
                        gapSecond[cell] = Max(pair[upper] + open, gapSecond[upper] + extend, gapFirst[upper] + open);
                    }

                    if (local && pair[cell] > best)
                    {
                        best = pair[cell];
                        bestCell = cell;
                    }
                }
            }

            State start = State.Pair;
            int endI = n, endJ = m;

            if (local)
            {
                endI = bestCell / width;
                endJ = bestCell % width;
            }
            else
            {
                int last = (n * width) + m;
                best = Max(pair[last], gapFirst[last], gapSecond[last]);
                start = Close(best, pair[last]) ? State.Pair : Close(best, gapFirst[last]) ? State.GapInFirst : State.GapInSecond;
            }

            var columns = new List<(int, int)>(n + m);
            int a = endI, b = endJ;
            State state = start;

            while (a > 0 || b > 0)
            {
                int cell = (a * width) + b;

                if (state == State.Pair)
                {
                    // Локальное выравнивание начинается там, где счёт впервые стал положительным
                    if ((local && pair[cell] <= 0) || a == 0 || b == 0)
                        break;

                    double previous = pair[cell] - scores[((a - 1) * m) + b - 1];

                    columns.Add((a - 1, b - 1));
                    a--;
                    b--;

                    int next = (a * width) + b;
                    state = Close(previous, pair[next]) ? State.Pair
                        : Close(previous, gapFirst[next]) ? State.GapInFirst
                        : State.GapInSecond;
                }
                else if (state == State.GapInFirst)
                {
                    double value = gapFirst[cell];

                    columns.Add((-1, b - 1));
                    b--;

                    int next = cell - 1;
                    state = Close(value, pair[next] + open) ? State.Pair
                        : Close(value, gapFirst[next] + extend) ? State.GapInFirst
                        : State.GapInSecond;
                }
                else
                {
                    double value = gapSecond[cell];

                    columns.Add((a - 1, -1));
                    a--;

                    int next = cell - width;
                    state = Close(value, pair[next] + open) ? State.Pair
                        : Close(value, gapSecond[next] + extend) ? State.GapInSecond
                        : State.GapInFirst;
                }
            }

            columns.Reverse();

            return new AlignmentPath(best, columns);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(pair);
            ArrayPool<double>.Shared.Return(gapFirst);
            ArrayPool<double>.Shared.Return(gapSecond);
        }
    }

    private static int CellCount(int rows, int columns)
    {
        long cells = (long)rows * columns;

        return cells <= int.MaxValue / 2
            ? (int)cells
            : throw new ArgumentException($"Таблица выравнивания {rows}×{columns} слишком велика для памяти");
    }

    private static bool Close(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(a));

    private static double Max(double a, double b, double c) => Math.Max(a, Math.Max(b, c));
}
