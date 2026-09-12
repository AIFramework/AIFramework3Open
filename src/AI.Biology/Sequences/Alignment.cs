using AI.Insights;

namespace AI.Biology.Sequences;

/// <summary>Результат выравнивания двух последовательностей</summary>
/// <param name="Score">Итоговый счёт</param>
/// <param name="First">Первая последовательность с пропусками</param>
/// <param name="Second">Вторая последовательность с пропусками</param>
/// <param name="Identity">Доля совпавших позиций</param>
public readonly record struct AlignmentResult(double Score, string First, string Second, double Identity)
    : IInterpretable
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

    /// <summary>Число сплошных участков пропусков в обеих строках</summary>
    public int GapRuns => CountRuns(First) + CountRuns(Second);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int length = First?.Length ?? 0;
        int matches = length == 0 ? 0 : Matches;
        int gaps = length == 0 ? 0 : Gaps;
        int runs = length == 0 ? 0 : GapRuns;

        // Пропуск не выравнивается с пропуском, поэтому каждая позиция — ровно одно из трёх
        int mismatches = length - matches - gaps;

        return new InterpretationBuilder("Выравнивание последовательностей")
            .Summary(length == 0
                ? "Общего участка не найдено: выравнивание пустое."
                : $"Выравнено позиций {length}: совпадений {matches}, несовпадений {mismatches}, пропусков {gaps}. "
                  + $"Идентичность {Fmt.Pct(Identity)}, счёт {Fmt.Num(Score, 1)}.")
            .Metric("Длина выравнивания", length, null, "позиций, включая пропуски", MetricQuality.Unknown, 0)
            .Metric("Идентичность", Fmt.Pct(Identity), null, "доля совпавших позиций от длины выравнивания",
                Identity >= 0.5 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Совпадений", matches, null, null, MetricQuality.Unknown, 0)
            .Metric("Несовпадений", mismatches, null, null, MetricQuality.Unknown, 0)
            .Metric("Пропусков", gaps, null, $"сплошных участков: {runs}", MetricQuality.Unknown, 0)
            .Metric("Счёт", Score, null, "сумма наград и штрафов по схеме счёта", MetricQuality.Unknown, 1)
            .FindingIf(runs > 0,
                $"Пропуски собраны в сплошные участки: их {runs}, в среднем по {Fmt.Num((double)gaps / Math.Max(runs, 1), 1)} "
                + "позиции. При аффинном штрафе, где открытие пропуска дороже продления, так и выглядит одна вставка "
                + "или делеция, а не россыпь одиночных.")
            .WarningIf(length > 0 && Identity < 0.5,
                "Идентичность ниже половины. У нуклеотидов четверть позиций совпадает случайно даже без пропусков, "
                + "а оптимальное выравнивание случайных последовательностей с пропусками даёт ещё больше. "
                + "Без оценки значимости такое сходство ничего не доказывает.")
            .Warning("Счёт сам по себе не говорит о значимости: у любых двух последовательностей есть лучшее "
                + "выравнивание. Значимость оценивают сравнением со счётами перемешанных последовательностей "
                + "(E-значение); здесь она не вычислена.")
            .Build();
    }

    private static int CountRuns(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
            return 0;

        int runs = 0;

        for (int i = 0; i < sequence.Length; i++)
            if (sequence[i] == '-' && (i == 0 || sequence[i - 1] != '-'))
                runs++;

        return runs;
    }

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
/// Штраф за пропуск аффинный: пропуск длины L стоит <c>GapOpen + (L − 1)·GapExtend</c>, как в EMBOSS;
/// соглашение BLAST «открытие 11, продление 1» соответствует GapOpen = −12, GapExtend = −1. Это
/// не деталь — при равном штрафе за каждую позицию алгоритм рассыпает пропуски по всей длине
/// вместо одной вставки, а биологически вставка целого участка вероятнее множества одиночных.
/// </para>
/// <para>
/// Счёт ведётся по трём состояниям Гото: столбец — пара букв, пропуск в первой строке или во второй.
/// Пропуск может открыться после любого состояния, в том числе сразу после пропуска в другой
/// строке. Обратный ход идёт по состояниям, а не по наибольшему значению в клетке: прежде
/// выравнивание собиралось из максимумов клеток и при аффинном штрафе могло не совпадать с
/// найденным счётом, а переход «пропуск в одной строке — пропуск в другой» был запрещён,
/// и оптимум иногда терялся. То же ядро выравнивает профили в <see cref="ProgressiveAlignment"/>.
/// </para>
/// <para>
/// Память и время — произведение длин. Для последовательностей в миллионы нуклеотидов нужны
/// приближённые методы, основанные на общих словах; здесь их нет.
/// </para>
/// </remarks>
public static class Alignment
{
    /// <summary>Глобальное выравнивание по Нидлману — Вуншу</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="scoring">Схема счёта</param>
    public static AlignmentResult Global(string first, string second, ScoringScheme scoring = default)
    {
        ScoringScheme scheme = Resolve(scoring);

        return Align(first, second, (a, b) => a == b ? scheme.Match : scheme.Mismatch,
            scheme.GapOpen, scheme.GapExtend, local: false);
    }

    /// <summary>Локальное выравнивание по Смиту — Уотерману</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="scoring">Схема счёта</param>
    public static AlignmentResult Local(string first, string second, ScoringScheme scoring = default)
    {
        ScoringScheme scheme = Resolve(scoring);

        return Align(first, second, (a, b) => a == b ? scheme.Match : scheme.Mismatch,
            scheme.GapOpen, scheme.GapExtend, local: true);
    }

    /// <summary>Глобальное выравнивание белков по матрице замен</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="matrix">Матрица замен, например <see cref="SubstitutionMatrix.Blosum62"/></param>
    /// <param name="gapOpen">Штраф за открытие пропуска; по умолчанию как в EMBOSS needle</param>
    /// <param name="gapExtend">Штраф за продление пропуска</param>
    public static AlignmentResult Global(
        string first, string second, SubstitutionMatrix matrix, double gapOpen = -10, double gapExtend = -0.5)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        return Align(first, second, (a, b) => matrix[a, b], gapOpen, gapExtend, local: false);
    }

    /// <summary>Локальное выравнивание белков по матрице замен</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность</param>
    /// <param name="matrix">Матрица замен</param>
    /// <param name="gapOpen">Штраф за открытие пропуска</param>
    /// <param name="gapExtend">Штраф за продление пропуска</param>
    public static AlignmentResult Local(
        string first, string second, SubstitutionMatrix matrix, double gapOpen = -10, double gapExtend = -0.5)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        return Align(first, second, (a, b) => matrix[a, b], gapOpen, gapExtend, local: true);
    }

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

    /// <summary>Схема по умолчанию вместо пустой</summary>
    internal static ScoringScheme Resolve(ScoringScheme scoring)
        => scoring.Match == 0 && scoring.Mismatch == 0 && scoring.GapOpen == 0 && scoring.GapExtend == 0
            ? ScoringScheme.Nucleotide
            : scoring;

    /// <summary>Выравнивание двух строк общим ядром Гото</summary>
    internal static AlignmentResult Align(
        string first, string second, Func<char, char, double> similarity, double open, double extend, bool local)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        AlignmentPath path = GotohAligner.Solve(
            first.Length, second.Length, (i, j) => similarity(first[i], second[j]), open, extend, local);

        int aligned = path.Columns.Count;
        var top = new char[aligned];
        var bottom = new char[aligned];
        int identical = 0;

        for (int c = 0; c < aligned; c++)
        {
            (int i, int j) = path.Columns[c];
            top[c] = i >= 0 ? first[i] : '-';
            bottom[c] = j >= 0 ? second[j] : '-';

            if (top[c] == bottom[c] && top[c] != '-')
                identical++;
        }

        return new AlignmentResult(path.Score, new string(top), new string(bottom), aligned == 0 ? 0 : (double)identical / aligned);
    }
}
