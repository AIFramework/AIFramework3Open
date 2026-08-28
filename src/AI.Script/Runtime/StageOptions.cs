using AI.Script.Syntax.Ast;

namespace AI.Script.Runtime;

/// <summary>
/// Атрибуты стадии в разобранном виде.
/// </summary>
/// <remarks>
/// Атрибуты разбираются один раз — при подъёме объявлений, а не при каждом вызове. Иначе
/// стадия, вызванная в цикле, платила бы за разбор <c>@timeout(90s)</c> на каждой итерации.
/// <para>
/// Разбор намеренно принимает только литералы: <c>@retry(n)</c>, где <c>n</c> — выражение,
/// потребовал бы вычислять его в области, которой у атрибута нет.
/// </para>
/// </remarks>
public sealed class StageOptions
{
    /// <summary>Настройки стадии без атрибутов.</summary>
    public static readonly StageOptions Default = new();

    /// <summary>Кэшировать ли результат (<c>@cache</c>).</summary>
    public bool Cache { get; init; }

    /// <summary>Запрещён ли кэш явно (<c>@nocache</c>).</summary>
    public bool NoCache { get; init; }

    /// <summary>Сколько раз повторять при отказе (<c>@retry(n)</c>); 1 — без повторов.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Собственный таймаут стадии (<c>@timeout(d)</c>); <c>null</c> — только общий.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Объявлена ли стадия чистой (<c>@pure</c>).</summary>
    public bool Pure { get; init; }

    /// <summary>Причина устаревания (<c>@deprecated("...")</c>); <c>null</c> — не устарела.</summary>
    public string? Deprecated { get; init; }

    /// <summary>Есть ли хоть один атрибут, влияющий на исполнение.</summary>
    public bool IsPlain => !Cache && Attempts == 1 && Timeout == null;

    /// <summary>
    /// Разбирает атрибуты объявления.
    /// </summary>
    /// <param name="attributes">Атрибуты перед <c>stage</c>.</param>
    /// <returns>Разобранные настройки; неизвестное и неверное молча игнорируется.</returns>
    /// <remarks>
    /// Ошибки здесь не докладываются: их выдаёт проверка до запуска
    /// (<see cref="Semantics.Checker"/>), у которой есть и позиции, и подсказки. Рантайм
    /// разбирает уже проверенное и потому просто берёт то, что понимает.
    /// </remarks>
    public static StageOptions From(IReadOnlyList<AttributeNode> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        if (attributes.Count == 0) return Default;

        bool cache = false;
        bool noCache = false;
        bool pure = false;
        int attempts = 1;
        TimeSpan? timeout = null;
        string? deprecated = null;

        foreach (AttributeNode attribute in attributes)
        {
            switch (attribute.Name)
            {
                case "cache":
                    cache = true;
                    break;

                case "nocache":
                    noCache = true;
                    break;

                case "pure":
                    pure = true;
                    break;

                case "retry":
                    if (Literal(attribute) is { Type: ScriptType.Num } count)
                        attempts = Math.Max(1, (int)count.RawNumber);

                    break;

                case "timeout":
                    if (Literal(attribute) is { } duration) timeout = AsDuration(duration);
                    break;

                case "deprecated":
                    if (Literal(attribute) is { Type: ScriptType.Str } reason)
                        deprecated = reason.AsString("@deprecated");

                    break;

                default:
                    break;
            }
        }

        return new StageOptions
        {
            // '@nocache' сильнее '@cache': запрет должен побеждать разрешение, если написаны оба.
            Cache = cache && !noCache,
            NoCache = noCache,
            Attempts = attempts,
            Timeout = timeout,
            Pure = pure,
            Deprecated = deprecated,
        };
    }

    private static ScriptValue? Literal(AttributeNode attribute) =>
        attribute.Arguments.Count == 1 && attribute.Arguments[0] is LiteralExpr literal
            ? literal.Value
            : null;

    private static TimeSpan? AsDuration(ScriptValue value) => value.Type switch
    {
        ScriptType.Dur => value.AsDuration("@timeout"),
        ScriptType.Num => TimeSpan.FromSeconds(value.RawNumber),
        _ => null,
    };
}
