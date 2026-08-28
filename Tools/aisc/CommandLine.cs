using System.Globalization;

namespace AiFramework.Tools.Aisc;

/// <summary>
/// Разбор аргументов командной строки.
/// </summary>
/// <remarks>
/// Свой разбор, а не библиотека: у утилиты четыре команды и восемь флагов, и тянуть ради них
/// зависимость в репозиторий, где утилиты собираются вместе с фреймворком, незачем.
/// </remarks>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);
    private readonly List<string> _positional = [];

    private CommandLine()
    {
    }

    /// <summary>Команда: первый позиционный аргумент.</summary>
    public string Command => _positional.Count > 0 ? _positional[0] : string.Empty;

    /// <summary>Позиционные аргументы после команды.</summary>
    public IReadOnlyList<string> Arguments => _positional.Count > 1 ? _positional[1..] : [];

    /// <summary>Разбирает аргументы.</summary>
    public static CommandLine Parse(string[] args)
    {
        var line = new CommandLine();

        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                line._positional.Add(argument);
                continue;
            }

            string name = argument[2..];
            int equals = name.IndexOf('=', StringComparison.Ordinal);

            if (equals >= 0)
            {
                line._options[name[..equals]] = name[(equals + 1)..];
                continue;
            }

            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);

            line._options[name] = hasValue ? args[++i] : null;
        }

        return line;
    }

    /// <summary>Задан ли флаг.</summary>
    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>Значение флага либо значение по умолчанию.</summary>
    public string? Value(string name, string? fallback = null) =>
        _options.TryGetValue(name, out string? value) && value != null ? value : fallback;

    /// <summary>Целочисленное значение флага.</summary>
    public int? Number(string name)
    {
        string? text = Value(name);

        return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }

    /// <summary>
    /// Длительность вида <c>30s</c>, <c>5m</c>, <c>2h</c> либо число секунд.
    /// </summary>
    public TimeSpan? Duration(string name)
    {
        string? text = Value(name);

        if (string.IsNullOrWhiteSpace(text)) return null;

        char unit = text[^1];
        string digits = char.IsLetter(unit) ? text[..^1] : text;

        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) return null;

        return char.ToLowerInvariant(unit) switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            _ => TimeSpan.FromSeconds(amount),
        };
    }

    /// <summary>Имена всех заданных флагов.</summary>
    public IReadOnlyCollection<string> Names => _options.Keys;
}
