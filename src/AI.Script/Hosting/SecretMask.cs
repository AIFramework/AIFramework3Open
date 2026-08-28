namespace AI.Script.Hosting;

/// <summary>
/// Замена секретов на заглушку в тексте, который увидит человек или модель.
/// </summary>
/// <remarks>
/// Ключ попадает в вывод не потому, что его печатают нарочно, а потому, что он оказывается
/// внутри сообщения об ошибке от библиотеки — в адресе запроса, в заголовке, в тексте отказа
/// службы. Печать проходит через одно место, поэтому маска ставится там же.
/// <para>
/// Это не защита от злонамеренного скрипта: тот может разрезать строку и напечатать по частям.
/// Это защита от случайной утечки в транскрипт, лог и контекст модели — единственного способа,
/// которым ключи утекают на практике.
/// </para>
/// </remarks>
public sealed class SecretMask
{
    /// <summary>Маска, которой нечего скрывать.</summary>
    public static readonly SecretMask None = new([]);

    /// <summary>Чем заменяется секрет.</summary>
    public const string Replacement = "***";

    private readonly string[] _secrets;

    /// <summary>Создаёт маску для перечисленных значений.</summary>
    /// <param name="secrets">Секреты; пустые и слишком короткие значения игнорируются.</param>
    public SecretMask(IEnumerable<string>? secrets)
    {
        if (secrets == null)
        {
            _secrets = [];

            return;
        }

        var kept = new List<string>();

        foreach (string secret in secrets)
        {
            // Короткое значение замаскировало бы половину осмысленного текста: строка "1" в
            // секретах превратила бы любой вывод в звёздочки.
            if (string.IsNullOrEmpty(secret) || secret.Length < 8) continue;

            kept.Add(secret);
        }

        // От длинных к коротким: иначе подстрока более длинного секрета съела бы его первой и
        // оставила хвост в открытом виде.
        kept.Sort(static (a, b) => b.Length.CompareTo(a.Length));

        _secrets = [.. kept];
    }

    /// <summary>Есть ли что маскировать.</summary>
    public bool IsEmpty => _secrets.Length == 0;

    /// <summary>Заменяет все известные секреты в тексте.</summary>
    public string Apply(string text)
    {
        if (_secrets.Length == 0 || string.IsNullOrEmpty(text)) return text;

        string result = text;

        foreach (string secret in _secrets)
            result = result.Replace(secret, Replacement, StringComparison.Ordinal);

        return result;
    }
}
