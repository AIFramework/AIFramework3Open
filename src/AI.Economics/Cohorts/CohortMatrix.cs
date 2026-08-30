using System;
using AI.DataStructs.Algebraic;

using AI.Insights;

namespace AI.Economics.Cohorts;

/// <summary>
/// Когортная матрица: строки — когорты по дате привлечения, столбцы — возраст
/// когорты в периодах, значения — число активных клиентов (или выручка).
/// </summary>
/// <remarks>
/// Матрица треугольная: молодые когорты ещё не дожили до старших возрастов.
/// Непронаблюдённые ячейки помечаются <c>NaN</c> — и это принципиально.
/// Если считать их нулями, средняя кривая удержания на длинных возрастах
/// обрушится вниз просто потому, что данных нет, а не потому, что клиенты ушли.
/// </remarks>
public sealed partial class CohortMatrix
{
    private readonly double[,] _values;

    /// <summary>Создаёт когортную матрицу.</summary>
    /// <param name="values">
    /// Матрица «когорта x возраст»; непронаблюдённые ячейки — <c>NaN</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    /// <exception cref="ArgumentException">Матрица пуста.</exception>
    public CohortMatrix(Matrix values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Height == 0 || values.Width == 0)
            throw new ArgumentException("Когортная матрица пуста.", nameof(values));

        _values = new double[values.Height, values.Width];
        for (int i = 0; i < values.Height; i++)
            for (int j = 0; j < values.Width; j++)
                _values[i, j] = values[i, j];
    }

    /// <summary>Число когорт.</summary>
    public int CohortCount => _values.GetLength(0);

    /// <summary>Максимальный возраст в периодах (число столбцов минус один).</summary>
    public int MaxAge => _values.GetLength(1) - 1;

    /// <summary>Значение ячейки; <c>NaN</c> для непронаблюдённых.</summary>
    /// <param name="cohort">Индекс когорты.</param>
    /// <param name="age">Возраст в периодах.</param>
    public double this[int cohort, int age] => _values[cohort, age];

    /// <summary>Пронаблюдена ли ячейка.</summary>
    /// <param name="cohort">Индекс когорты.</param>
    /// <param name="age">Возраст в периодах.</param>
    public bool IsObserved(int cohort, int age) => !double.IsNaN(_values[cohort, age]);

    /// <summary>Размеры когорт — значения в нулевом возрасте.</summary>
    public Vector CohortSizes()
    {
        var v = new Vector(CohortCount);
        for (int i = 0; i < CohortCount; i++) v[i] = _values[i, 0];
        return v;
    }

    /// <summary>Кривая удержания одной когорты, нормированная на её размер.</summary>
    /// <param name="cohort">Индекс когорты.</param>
    /// <returns>Вектор долей доживания до последнего пронаблюдённого возраста.</returns>
    public Vector RetentionOf(int cohort)
    {
        double size = _values[cohort, 0];
        int len = 0;
        while (len <= MaxAge && IsObserved(cohort, len)) len++;

        var v = new Vector(len);
        for (int t = 0; t < len; t++) v[t] = size > 0 ? _values[cohort, t] / size : 0;
        return v;
    }

    /// <summary>
    /// Сводная кривая удержания: по каждому возрасту суммируются только те
    /// когорты, которые до него дожили, и делятся на их же суммарный размер.
    /// </summary>
    /// <returns>Вектор долей доживания <c>S(0) = 1, S(1), ...</c>.</returns>
    public Vector PooledRetention()
    {
        int len = LastObservedAge() + 1;
        var v = new Vector(len);

        for (int t = 0; t < len; t++)
        {
            double alive = 0, baseSize = 0;
            for (int c = 0; c < CohortCount; c++)
            {
                if (!IsObserved(c, t)) continue;
                alive += _values[c, t];
                baseSize += _values[c, 0];
            }
            v[t] = baseSize > 0 ? alive / baseSize : 0;
        }

        return v;
    }

    /// <summary>
    /// Число клиентов, стоящих за каждой точкой сводной кривой.
    /// Падает с возрастом: старших возрастов достигли не все когорты.
    /// </summary>
    public Vector ObservationBase()
    {
        int len = LastObservedAge() + 1;
        var v = new Vector(len);

        for (int t = 0; t < len; t++)
        {
            double baseSize = 0;
            for (int c = 0; c < CohortCount; c++)
                if (IsObserved(c, t)) baseSize += _values[c, 0];
            v[t] = baseSize;
        }

        return v;
    }

    /// <summary>
    /// Эффективный размер когорты для подгонки кривой — база самого старшего
    /// пронаблюдённого возраста.
    /// </summary>
    /// <remarks>
    /// Взята консервативная оценка: ширину доверительного интервала
    /// экстраполяции определяет именно хвост данных, а не многочисленные
    /// наблюдения первых месяцев. На полностью заполненной матрице
    /// эта величина совпадает с общим числом клиентов.
    /// </remarks>
    public double EffectiveCohortSize()
    {
        Vector b = ObservationBase();
        return b.Count > 0 ? b[b.Count - 1] : 0;
    }

    /// <summary>Матрица долей удержания: каждая строка поделена на свой размер когорты.</summary>
    public Matrix RetentionMatrix()
    {
        var m = new Matrix(CohortCount, MaxAge + 1);
        for (int c = 0; c < CohortCount; c++)
        {
            double size = _values[c, 0];
            for (int t = 0; t <= MaxAge; t++)
                m[c, t] = IsObserved(c, t) && size > 0 ? _values[c, t] / size : double.NaN;
        }
        return m;
    }

    /// <summary>Последний возраст, пронаблюдённый хотя бы одной когортой.</summary>
    public int LastObservedAge()
    {
        for (int t = MaxAge; t >= 0; t--)
            for (int c = 0; c < CohortCount; c++)
                if (IsObserved(c, t)) return t;

        return 0;
    }

    /// <summary>
    /// Строит треугольную когортную матрицу: когорта <c>i</c> пронаблюдена
    /// до возраста <c>CohortCount - 1 - i</c>.
    /// </summary>
    /// <param name="counts">Полная прямоугольная матрица значений.</param>
    /// <returns>Матрица, у которой ненаблюдаемая часть заменена на <c>NaN</c>.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    public static CohortMatrix Triangular(Matrix counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var m = new Matrix(counts.Height, counts.Width);
        for (int c = 0; c < counts.Height; c++)
            for (int t = 0; t < counts.Width; t++)
                m[c, t] = t <= counts.Height - 1 - c ? counts[c, t] : double.NaN;

        return new CohortMatrix(m);
    }
}
