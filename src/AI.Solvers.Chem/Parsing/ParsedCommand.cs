using System.Globalization;

namespace AI.Solvers.Chem.Parsing;

/// <summary>
/// Отсутствует обязательный параметр команды
/// </summary>
public sealed class MissingParameterException : Exception
{
    /// <summary>
    /// Имена (алиасы), под которыми параметр искали
    /// </summary>
    public IReadOnlyList<string> ExpectedNames { get; }

    /// <summary>
    /// Создаёт исключение об отсутствующем параметре
    /// </summary>
    public MissingParameterException(IReadOnlyList<string> expectedNames)
        : base(expectedNames.Count == 1
            ? $"Missing required parameter '{expectedNames[0]}'"
            : $"Missing required parameter: expected one of {string.Join(", ", expectedNames.Select(n => $"'{n}'"))}")
    {
        ExpectedNames = expectedNames;
    }
}

/// <summary>
/// Разобранная команда: тип и словарь параметров.
/// Чтение параметров идёт через методы Get*/Try*, которые понимают алиасы имён
/// (документированный синтаксис команды и внутреннее имя параметра часто различаются).
/// </summary>
public class ParsedCommand
{
    /// <summary>
    /// Признак успешного разбора
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Тип команды
    /// </summary>
    public CommandType CommandType { get; set; }

    /// <summary>
    /// Исходная строка команды (регистр сохранён - важно для SMILES)
    /// </summary>
    public string OriginalCommand { get; set; }

    /// <summary>
    /// Параметры команды. Ключи концентраций хранятся вместе со скобками: "[metal]", "[Cu2+]"
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Сообщение об ошибке разбора
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Ошибка разбора команды
    /// </summary>
    public static ParsedCommand Error(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    #region Чтение параметров

    /// <summary>
    /// Ищет параметр по списку имён: сначала точное совпадение, затем без учёта регистра
    /// </summary>
    /// <param name="value">Найденное значение</param>
    /// <param name="names">Имена-алиасы в порядке приоритета</param>
    public bool TryGet(out string value, params string[] names)
    {
        foreach (string name in names)
        {
            if (Parameters.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value))
                return true;
        }

        foreach (string name in names)
        {
            foreach (var kvp in Parameters)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    value = kvp.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Задан ли хотя бы один из параметров
    /// </summary>
    public bool Has(params string[] names) => TryGet(out _, names);

    /// <summary>
    /// Строковое значение параметра
    /// </summary>
    /// <exception cref="MissingParameterException">Ни один из алиасов не задан</exception>
    public string GetString(params string[] names)
        => TryGet(out string value, names) ? value : throw new MissingParameterException(names);

    /// <summary>
    /// Строковое значение параметра со значением по умолчанию
    /// </summary>
    public string GetStringOrDefault(string fallback, params string[] names)
        => TryGet(out string value, names) ? value : fallback;

    /// <summary>
    /// Числовое значение параметра (инвариантная культура)
    /// </summary>
    /// <exception cref="MissingParameterException">Ни один из алиасов не задан</exception>
    /// <exception cref="FormatException">Значение не является числом</exception>
    public double GetDouble(params string[] names)
    {
        string raw = GetString(names);

        if (!TryParseNumber(raw, out double value))
            throw new FormatException($"Parameter '{names[0]}' is not a number: '{raw}'");

        return value;
    }

    /// <summary>
    /// Числовое значение параметра со значением по умолчанию
    /// </summary>
    public double GetDoubleOrDefault(double fallback, params string[] names)
        => TryGetDouble(out double value, names) ? value : fallback;

    /// <summary>
    /// Пытается прочитать число, не бросая исключений
    /// </summary>
    public bool TryGetDouble(out double value, params string[] names)
    {
        if (TryGet(out string raw, names))
            return TryParseNumber(raw, out value);

        value = 0;
        return false;
    }

    /// <summary>
    /// Целочисленное значение параметра
    /// </summary>
    public int GetInt(params string[] names) => (int)Math.Round(GetDouble(names));

    /// <summary>
    /// Целочисленное значение параметра со значением по умолчанию
    /// </summary>
    public int GetIntOrDefault(int fallback, params string[] names)
        => TryGetDouble(out double value, names) ? (int)Math.Round(value) : fallback;

    /// <summary>
    /// Массив чисел, записанный через запятую ("1,2,3")
    /// </summary>
    /// <exception cref="MissingParameterException">Ни один из алиасов не задан</exception>
    /// <exception cref="FormatException">Элемент списка не является числом</exception>
    public double[] GetArray(params string[] names)
    {
        string raw = GetString(names);
        var items = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new double[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            if (!TryParseNumber(items[i], out result[i]))
                throw new FormatException($"Parameter '{names[0]}' contains a non-numeric item: '{items[i].Trim()}'");
        }

        if (result.Length == 0)
            throw new FormatException($"Parameter '{names[0]}' is an empty list");

        return result;
    }

    /// <summary>
    /// Параметры-концентрации, записанные в квадратных скобках ("[Cu2+]=0.01"),
    /// в порядке появления в команде
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> ConcentrationParameters
        => Parameters.Where(kvp => kvp.Key.Length > 2 && kvp.Key[0] == '[' && kvp.Key[^1] == ']');

    /// <summary>
    /// Концентрация частицы: "[X]", "X_concentration" либо "X_conc"
    /// </summary>
    /// <param name="value">Найденное значение</param>
    /// <param name="species">Имя частицы без скобок (metal, ligand, Cu2+, ...)</param>
    public bool TryGetConcentration(out double value, string species)
        => TryGetDouble(out value, $"[{species}]", $"{species}_concentration", $"{species}_conc", $"c_{species}");

    private static bool TryParseNumber(string raw, out double value)
        => double.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    #endregion
}
