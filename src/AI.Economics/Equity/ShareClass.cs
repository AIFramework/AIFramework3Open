namespace AI.Economics.Equity;

/// <summary>Тип участия привилегированных акций в остатке после выплаты преференции.</summary>
public enum PreferenceType
{
    /// <summary>
    /// Неучаствующие: держатель выбирает либо преференцию, либо конвертацию
    /// в обыкновенные акции — но не то и другое сразу.
    /// </summary>
    NonParticipating,

    /// <summary>
    /// Участвующие: держатель получает преференцию и вдобавок долю в остатке
    /// наравне с обыкновенными акциями («double dip»).
    /// </summary>
    Participating,
}

/// <summary>
/// Класс акций: обыкновенные либо привилегированные со своими условиями
/// ликвидационной преференции.
/// </summary>
/// <remarks>
/// Именно эти четыре поля определяют, кто и сколько получит при выходе, —
/// и именно они обычно не учитываются в таблице долей, где у всех просто
/// «проценты». При выходе ниже ожиданий проценты не значат ничего:
/// сначала выплачиваются преференции, и основателям может не остаться
/// вовсе, даже если формально им принадлежит 60 % компании.
/// </remarks>
public sealed record ShareClass
{
    /// <summary>Название класса, например «Series A Preferred».</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Обыкновенные ли это акции (включая опционы).</summary>
    public bool IsCommon { get; init; }

    /// <summary>
    /// Старшинство: чем больше значение, тем раньше класс получает выплату.
    /// Классы с одинаковым старшинством делят деньги пропорционально
    /// (структура pari passu).
    /// </summary>
    public int Seniority { get; init; }

    /// <summary>Кратность ликвидационной преференции к вложенной сумме.</summary>
    public double LiquidationMultiple { get; init; } = 1.0;

    /// <summary>Тип участия в остатке.</summary>
    public PreferenceType Preference { get; init; } = PreferenceType.NonParticipating;

    /// <summary>
    /// Потолок суммарной выплаты участвующего класса, в кратностях вложенной
    /// суммы. <c>NaN</c> — потолка нет. Достигнув потолка, класс обычно
    /// выгоднее конвертировать в обыкновенные акции.
    /// </summary>
    public double ParticipationCap { get; init; } = double.NaN;
}
