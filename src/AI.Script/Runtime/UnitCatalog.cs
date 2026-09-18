using AI.Units;

namespace AI.Script.Runtime;

/// <summary>
/// Откуда язык берёт единицы: физические — из реестра СИ фреймворка, деньги и штуки — свои.
/// </summary>
/// <remarks>
/// Физика у фреймворка уже есть целиком (<see cref="UnitRegistry"/>): приставки, производные
/// единицы, тонны и литры. Второй такой справочник разошёлся бы с первым. Своё здесь только то,
/// чего в СИ нет и быть не может: валюты — каждая своей размерностью, потому что курса у языка
/// нет, — штуки и русские обозначения, которыми пишут сметы.
/// <para>
/// Единицы со смещением (градусы Цельсия) не принимаются: <c>10 °C + 10 °C</c> не равно
/// двадцати градусам, и арифметика величин с таким смещением давала бы неверный ответ молча.
/// </para>
/// </remarks>
public static class UnitCatalog
{
    /// <summary>Размерности, которых нет в СИ: каждая валюта и штуки.</summary>
    private static readonly string[] s_own = ["rub", "usd", "eur", "pcs"];

    /// <summary>Русские обозначения — к обозначениям реестра.</summary>
    private static readonly Dictionary<string, string> s_aliases = new(StringComparer.Ordinal)
    {
        ["кг"] = "kg", ["г"] = "g", ["мг"] = "mg", ["т"] = "t",
        ["м"] = "m", ["км"] = "km", ["см"] = "cm", ["мм"] = "mm",
        ["л"] = "l", ["мл"] = "ml",
        ["шт"] = "pcs", ["руб"] = "rub",
        ["кВт"] = "kW", ["Вт"] = "W",
    };

    /// <summary>Ключи базовых единиц СИ — в порядке показателей <see cref="Dimension"/>.</summary>
    private static readonly string[] s_base = ["m", "kg", "s", "A", "K", "mol", "cd"];

    /// <summary>Ищет единицу по обозначению: своё, русское либо из реестра СИ.</summary>
    public static bool TryGet(string symbol, out MeasureUnit unit)
    {
        unit = null!;

        if (string.IsNullOrWhiteSpace(symbol)) return false;

        if (Array.IndexOf(s_own, symbol) >= 0)
        {
            unit = MeasureUnit.Simple(symbol, symbol, 1, 1);
            return true;
        }

        string latin = s_aliases.TryGetValue(symbol, out string? alias) ? alias : symbol;

        if (latin == "pcs" || latin == "rub")
        {
            unit = MeasureUnit.Simple(symbol, latin, 1, 1);
            return true;
        }

        if (!UnitRegistry.TryParse(latin, out Unit? si) || si is null || si.IsAffine) return false;

        if (!Dimensions(si.Dimension, out Dictionary<string, int> dimensions)) return false;

        unit = MeasureUnit.From(dimensions, symbol, si.Factor);

        return true;
    }

    /// <summary>
    /// Единица литерала <c>5 kg</c>: то же, что <see cref="TryGet"/>, но без чистого времени.
    /// </summary>
    /// <remarks>
    /// Время у языка — длительность (<c>30s</c>, <c>2h</c>), и <c>5 h</c> рядом с <c>5h</c> давало
    /// бы две несовместимые записи одного часа, различающиеся пробелом.
    /// </remarks>
    public static bool TryLiteral(string symbol, out MeasureUnit unit) =>
        TryGet(symbol, out unit) && !IsPureTime(unit);

    private static bool IsPureTime(MeasureUnit unit) =>
        unit.Dimensions.Count == 1 && unit.Dimensions.TryGetValue("s", out int power) && power == 1;

    /// <summary>Показатели СИ — словарём; дробные степени (√Гц) величиной языка не записываются.</summary>
    private static bool Dimensions(Dimension dimension, out Dictionary<string, int> dimensions)
    {
        double[] powers =
        [
            dimension.Length, dimension.Mass, dimension.Time, dimension.Current,
            dimension.Temperature, dimension.Amount, dimension.LuminousIntensity,
        ];

        dimensions = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < powers.Length; i++)
        {
            if (powers[i] != Math.Floor(powers[i])) return false;
            if (powers[i] != 0) dimensions[s_base[i]] = (int)powers[i];
        }

        return true;
    }
}
