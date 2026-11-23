namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Один словарь машинных кодов исхода инструмента (<see cref="ToolResult.ErrorCode"/>): его читают гейт эффектов,
/// движок, хост и интерфейс. Новый случай — новая строка словаря, а не новый формат результата.
/// </summary>
/// <remarks>
/// <para>Код уточняет статус, а не заменяет его. Пары, которые имеют смысл:</para>
/// <list type="table">
/// <listheader><term>Статус</term><description>Коды</description></listheader>
/// <item><term><see cref="ToolStatus.Ok"/></term><description><see cref="Partial"/> (сделано не все, сделанное в
/// <see cref="ToolResult.Data"/>), <see cref="Pending"/> (действие начато, итог придет позже по
/// <see cref="ToolResult.Handle"/>)</description></item>
/// <item><term><see cref="ToolStatus.Unknown"/></term><description><see cref="UnknownResult"/></description></item>
/// <item><term><see cref="ToolStatus.Failed"/></term><description><see cref="Transient"/>,
/// <see cref="NotConfigured"/>, <see cref="NeedsReconnect"/>, <see cref="NoRights"/>, <see cref="NotFound"/>,
/// <see cref="TooLong"/>, <see cref="BadMarkup"/>, <see cref="RepeatUnsafe"/>, <see cref="UnknownTool"/> и свои
/// коды инструмента</description></item>
/// <item><term><see cref="ToolStatus.NeedsInput"/></term><description><see cref="NeedsInput"/></description></item>
/// </list>
/// <para>
/// Отказ «до действия» (<see cref="NotPerformed(string?)"/>) значит, что мир не изменился: гейт эффектов снимает
/// запись, и тот же вызов можно повторить. Отказ по существу без такого кода гейт записывает, и повтор идет по
/// правилу <see cref="ToolIdempotency"/>. Неизвестный исход (<see cref="ToolStatus.Unknown"/>) успехом не
/// считается никогда.
/// </para>
/// <para>Свой код инструмента допустим: строчные латинские буквы, цифры и «_». Совпадать со словом словаря с другим
/// смыслом он не должен.</para>
/// </remarks>
public static class ToolCodes
{
    /// <summary>
    /// Временный отказ до действия: 429, 503 с <c>Retry-After</c>, соединение не установлено, запрос не отправлен.
    /// Сервер ничего не сделал; повтор позже уместен.
    /// </summary>
    public const string Transient = "transient";

    /// <summary>
    /// Исход неизвестен: тайм-аут после отправки, обрыв, 502 или 504. Идет со статусом
    /// <see cref="ToolStatus.Unknown"/>: действие могло состояться.
    /// </summary>
    public const string UnknownResult = "unknown_result";

    /// <summary>Сделано не все: что именно сделано, лежит в <see cref="ToolResult.Data"/>. Идет со статусом <see cref="ToolStatus.Ok"/>.</summary>
    public const string Partial = "partial";

    /// <summary>
    /// Действие начато, итог придет позже: забрать его можно по <see cref="ToolResult.Handle"/>. Идет со статусом
    /// <see cref="ToolStatus.Ok"/>: начатое действие повторно не запускается.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>Инструмент или интеграция не настроены: нет ключа, адреса, подключения. Действия не было.</summary>
    public const string NotConfigured = "not_configured";

    /// <summary>Подключение есть, но секрет не читается или отозван: нужно подключиться заново. Действия не было.</summary>
    public const string NeedsReconnect = "needs_reconnect";

    /// <summary>Не хватает данных от человека или модели; обычно со статусом <see cref="ToolStatus.NeedsInput"/>. Действия не было.</summary>
    public const string NeedsInput = "needs_input";

    /// <summary>Материал длиннее предела площадки. Действия не было.</summary>
    public const string TooLong = "too_long";

    /// <summary>Площадка не приняла разметку. Действия не было.</summary>
    public const string BadMarkup = "bad_markup";

    /// <summary>У бота или учетной записи нет права на действие. Действия не было.</summary>
    public const string NoRights = "no_rights";

    /// <summary>Названный объект не существует: файл, шаблон, канал, запись. Действия не было.</summary>
    public const string NotFound = "not_found";

    /// <summary>
    /// Гейт эффектов не повторил вызов: он уже начинался и не кончился успехом, а повтор не объявлен безопасным.
    /// </summary>
    public const string RepeatUnsafe = "repeat_unsafe";

    /// <summary>Инструмента с таким именем в наборе нет. Действия не было.</summary>
    public const string UnknownTool = "unknown_tool";

    private static readonly HashSet<string> NotPerformedCodes = new(StringComparer.Ordinal)
    {
        Transient, NotConfigured, NeedsReconnect, NeedsInput, TooLong, BadMarkup, NoRights, NotFound, UnknownTool,
    };

    /// <summary>Все коды словаря в порядке объявления.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Transient, UnknownResult, Partial, Pending, NotConfigured, NeedsReconnect, NeedsInput, TooLong, BadMarkup,
        NoRights, NotFound, RepeatUnsafe, UnknownTool,
    ];

    /// <summary>
    /// Значит ли код, что действие точно не исполнялось и мир не изменился (отказ до действия).
    /// </summary>
    /// <param name="code">Код из <see cref="ToolResult.ErrorCode"/>.</param>
    /// <returns><see langword="true"/> для кодов отказа до действия; для <see langword="null"/>, своих кодов
    /// инструмента и кодов исхода — <see langword="false"/>.</returns>
    public static bool NotPerformed(string? code) => code != null && NotPerformedCodes.Contains(code);
}
