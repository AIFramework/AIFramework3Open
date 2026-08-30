namespace AI.Biology.Sequences;

/// <summary>Результат выравнивания двух последовательностей</summary>
/// <param name="Score">Итоговый счёт</param>
/// <param name="First">Первая последовательность с пропусками</param>
/// <param name="Second">Вторая последовательность с пропусками</param>
/// <param name="Identity">Доля совпавших позиций</param>
public readonly record struct AlignmentResult(double Score, string First, string Second, double Identity)
{
    /// <summary>Число совпавших позиций</summary>
    public int Matches
    {
        get
        {
            int count = 0;

            for (int i = 0; i < First.Length; i++)
                if (First[i] == Second[i] && First[i] != '-')
                    count++;

            return count;
        }
    }

    /// <summary>Число пропусков в обеих строках</summary>
    public int Gaps => First.Count(c => c == '-') + Second.Count(c => c == '-');

    /// <summary>Наглядная запись выравнивания в три строки</summary>
    public override string ToString()
    {
        var middle = new System.Text.StringBuilder(First.Length);

        for (int i = 0; i < First.Length; i++)
            _ = middle.Append(First[i] == Second[i] && First[i] != '-' ? '|' : ' ');

        return $"{First}{Environment.NewLine}{middle}{Environment.NewLine}{Second}";
    }
}

/// <summary>Параметры счёта при выравнивании</summary>
/// <param name="Match">Награда за совпадение</param>
/// <param name="Mismatch">Штраф за несовпадение</param>
/// <param name="GapOpen">Штраф за открытие пропуска</param>
/// <param name="GapExtend">Штраф за продление пропуска</param>
public readonly record struct ScoringScheme(double Match, double Mismatch, double GapOpen, double GapExtend)
{
    /// <summary>Схема по умолчанию для нуклеотидов</summary>
    public static ScoringScheme Nucleotide => new(1, -1, -2, -0.5);

    /// <summary>Простая схема с одинаковым штрафом за любой пропуск</summary>
    /// <param name="match">Награда за совпадение</param>
    /// <param name="mismatch">Штраф за несовпадение</param>
    /// <param name="gap">Штраф за пропуск</param>
    public static ScoringScheme Linear(double match, double mismatch, double gap)
        => new(match, mismatch, gap, gap);
}

/// <summary>
/// Выравнивание последовательностей динамическим программированием.
/// </summary>
/// <remarks>
/// <para>
/// Глобальное выравнивание по Нидлману — Вуншу растягивает обе последовательности целиком
/// и уместно для гомологичных белков близкой длины. Локальное по Смиту — Уотерману ищет
/// лучший общий участок и уместно, когда сходство ограничено доменом или мотивом.
/// </para>
/// <para>
/// Штраф за пропуск аффинный: открытие дороже продления. Это не деталь — при равном штрафе
/// за каждую позицию алгоритм рассыпает пропуски по всей длине вместо одной вставки,
/// а биологически вставка целого участка вероятнее, чем множество одиночных.
/// </para>
/// <para>
/// Память и время — произведение длин. Для последовательностей в миллионы нуклеотидов нужны
/// приближённые методы, основанные на общих словах; здесь их нет.
/// </para>
/// </remarks>
public static class Alignment
{
    private const double NegativeInfinity = -1e18;

    /// <summary>Глобальное выравнивание по Нидлману — Вуншу</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="scoring">Схема счёта</param>
    public static AlignmentResult Global(string first, string second, ScoringScheme scoring = default)
        => Align(first, second, Resolve(scoring), local: false);

    /// <summary>Локальное выравнивание по Смиту — Уотерману</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="scoring">Схема счёта</param>
    public static AlignmentResult Local(string first, string second, ScoringScheme scoring = default)
        => Align(first, second, Resolve(scoring), local: true);

    /// <summary>
    /// Расстояние Хэмминга: число различающихся позиций у последовательностей равной длины
    /// </summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    public static int HammingDistance(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Length != second.Length)
            throw new ArgumentException("Расстояние Хэмминга определено для строк равной длины", nameof(second));

        int distance = 0;

        for (int i = 0; i < first.Length; i++)
            if (first[i] != second[i])
                distance++;

        return distance;
    }

    private static ScoringScheme Resolve(ScoringScheme scoring)
        => scoring.Match == 0 && scoring.Mismatch == 0 && scoring.GapOpen == 0 && scoring.GapExtend == 0
            ? ScoringScheme.Nucleotide
            : scoring;

    private static AlignmentResult Align(string first, string second, ScoringScheme scoring, bool local)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int n = first.Length;
        int m = second.Length;

        // Три матрицы: выравнивание кончается совпадением, пропуском в первой либо во второй
        var match = new double[n + 1, m + 1];
        var gapFirst = new double[n + 1, m + 1];
        var gapSecond = new double[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            for (int j = 0; j <= m; j++)
            {
                match[i, j] = NegativeInfinity;
                gapFirst[i, j] = NegativeInfinity;
                gapSecond[i, j] = NegativeInfinity;
            }
        }

        match[0, 0] = 0;

        if (!local)
        {
            for (int i = 1; i <= n; i++)
                gapSecond[i, 0] = scoring.GapOpen + ((i - 1) * scoring.GapExtend);

            for (int j = 1; j <= m; j++)
                gapFirst[0, j] = scoring.GapOpen + ((j - 1) * scoring.GapExtend);
        }
        else
        {
            for (int i = 0; i <= n; i++)
                match[i, 0] = 0;

            for (int j = 0; j <= m; j++)
                match[0, j] = 0;
        }

        double best = local ? 0 : NegativeInfinity;
        int bestI = n, bestJ = m;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                double similarity = first[i - 1] == second[j - 1] ? scoring.Match : scoring.Mismatch;
                double previous = Max(match[i - 1, j - 1], gapFirst[i - 1, j - 1], gapSecond[i - 1, j - 1]);

                match[i, j] = previous <= NegativeInfinity / 2 ? similarity : previous + similarity;

                if (local && match[i, j] < 0)
                    match[i, j] = 0;

                gapFirst[i, j] = Math.Max(
                    match[i, j - 1] + scoring.GapOpen,
                    gapFirst[i, j - 1] + scoring.GapExtend);

                gapSecond[i, j] = Math.Max(
                    match[i - 1, j] + scoring.GapOpen,
                    gapSecond[i - 1, j] + scoring.GapExtend);

                if (!local)
                    continue;

                if (match[i, j] > best)
                {
                    best = match[i, j];
                    bestI = i;
                    bestJ = j;
                }
            }
        }

        if (!local)
            best = Max(match[n, m], gapFirst[n, m], gapSecond[n, m]);

        (string alignedFirst, string alignedSecond) = Traceback(
            first, second, match, gapFirst, gapSecond, scoring, local, bestI, bestJ);

        int aligned = alignedFirst.Length;
        int identical = 0;

        for (int i = 0; i < aligned; i++)
            if (alignedFirst[i] == alignedSecond[i] && alignedFirst[i] != '-')
                identical++;

        return new AlignmentResult(best, alignedFirst, alignedSecond, aligned == 0 ? 0 : (double)identical / aligned);
    }

    private static (string First, string Second) Traceback(
        string first, string second,
        double[,] match, double[,] gapFirst, double[,] gapSecond,
        ScoringScheme scoring, bool local, int i, int j)
    {
        var top = new System.Text.StringBuilder();
        var bottom = new System.Text.StringBuilder();

        while (i > 0 || j > 0)
        {
            if (local && match[i, j] <= 0)
                break;

            double current = Max(match[i, j], gapFirst[i, j], gapSecond[i, j]);

            if (i > 0 && j > 0 && Math.Abs(current - match[i, j]) < 1e-9)
            {
                _ = top.Append(first[i - 1]);
                _ = bottom.Append(second[j - 1]);
                i--;
                j--;
                continue;
            }

            if (j > 0 && Math.Abs(current - gapFirst[i, j]) < 1e-9)
            {
                _ = top.Append('-');
                _ = bottom.Append(second[j - 1]);
                j--;
                continue;
            }

            if (i > 0)
            {
                _ = top.Append(first[i - 1]);
                _ = bottom.Append('-');
                i--;
                continue;
            }

            _ = top.Append('-');
            _ = bottom.Append(second[j - 1]);
            j--;
        }

        char[] topArray = top.ToString().ToCharArray();
        char[] bottomArray = bottom.ToString().ToCharArray();

        Array.Reverse(topArray);
        Array.Reverse(bottomArray);

        return (new string(topArray), new string(bottomArray));
    }

    private static double Max(double a, double b, double c) => Math.Max(a, Math.Max(b, c));
}
