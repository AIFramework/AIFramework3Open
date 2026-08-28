using AI.Solvers.Chem.Database;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AI.Solvers.Chem.Models;

/// <summary>
/// Разбор химической формулы: вложенные скобки, кристаллогидраты, заряд ионов,
/// агрегатное состояние и ведущий стехиометрический коэффициент.
/// Примеры: Ca(OH)2, K4[Fe(CN)6], CuSO4·5H2O, SO4^2-, 2H2O, H2O(l)
/// </summary>
public sealed class MolecularFormula
{
    private static readonly char[] HydrateSeparators = { '·', '•', '*', '×', '∙' };

    private static readonly HashSet<string> KnownStates = new(StringComparer.OrdinalIgnoreCase)
    { "g", "l", "s", "aq", "cr", "am", "gas", "liq", "sol", "solid", "тв", "ж", "г", "р-р" };

    private readonly Dictionary<string, int> _elements;

    /// <summary>
    /// Исходная строка формулы (как её передал пользователь)
    /// </summary>
    public string Formula { get; }

    /// <summary>
    /// Формула без коэффициента, заряда и состояния (например, "Ca(OH)2")
    /// </summary>
    public string CoreFormula { get; }

    /// <summary>
    /// Ведущий стехиометрический коэффициент ("2H2O" -> 2), по умолчанию 1
    /// </summary>
    public int Coefficient { get; }

    /// <summary>
    /// Заряд частицы ("SO4^2-" -> -2, "Cu2+" -> +2), 0 для нейтральных
    /// </summary>
    public int Charge { get; }

    /// <summary>
    /// Агрегатное состояние из суффикса ("H2O(l)" -> "l"), null если не указано
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Состав одной формульной единицы (без учёта <see cref="Coefficient"/>).
    /// Вода кристаллогидрата включена в состав.
    /// </summary>
    public IReadOnlyDictionary<string, int> Elements => _elements;

    /// <summary>
    /// Разбирает формулу, бросая <see cref="FormatException"/> при синтаксической ошибке
    /// </summary>
    public MolecularFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new FormatException("Empty chemical formula");

        Formula = formula.Trim();

        string body = Formula;
        State = ExtractState(ref body);
        Charge = ExtractCharge(ref body);
        Coefficient = ExtractCoefficient(ref body);
        CoreFormula = body;

        _elements = ParseComposition(body);

        if (_elements.Count == 0)
            throw new FormatException($"No chemical elements recognized in '{Formula}'");
    }

    /// <summary>
    /// Безопасный разбор формулы
    /// </summary>
    /// <param name="formula">Строка формулы</param>
    /// <param name="result">Результат разбора</param>
    /// <param name="error">Описание ошибки, если разбор не удался</param>
    public static bool TryParse(string formula, out MolecularFormula result, out string error)
    {
        try
        {
            result = new MolecularFormula(formula);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Число атомов элемента в формульной единице
    /// </summary>
    public int GetCount(string element) => _elements.TryGetValue(element, out int count) ? count : 0;

    /// <summary>
    /// Молярная масса формульной единицы, г/моль.
    /// Бросает <see cref="InvalidOperationException"/>, если элемент отсутствует в базе.
    /// </summary>
    public double CalculateMolarMass(ChemDatabase database)
    {
        if (!TryCalculateMolarMass(database, out double mass, out string error))
            throw new InvalidOperationException(error);

        return mass;
    }

    /// <summary>
    /// Молярная масса без исключений: при неизвестном элементе возвращает false
    /// </summary>
    public bool TryCalculateMolarMass(ChemDatabase database, out double mass, out string error)
    {
        ArgumentNullException.ThrowIfNull(database);

        mass = 0;
        var unknown = new List<string>();

        foreach (var kvp in _elements)
        {
            var element = database.GetElement(kvp.Key);

            if (element == null)
                unknown.Add(kvp.Key);
            else
                mass += element.AtomicMass * kvp.Value;
        }

        if (unknown.Count > 0)
        {
            mass = 0;
            error = $"Unknown element(s) in '{Formula}': {string.Join(", ", unknown)}";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Нормализованная запись состава (в порядке элементов формулы)
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (Coefficient != 1)
            sb.Append(Coefficient.ToString(CultureInfo.InvariantCulture));

        foreach (var kvp in _elements)
        {
            sb.Append(kvp.Key);
            if (kvp.Value != 1)
                sb.Append(kvp.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Charge != 0)
        {
            int magnitude = Math.Abs(Charge);
            if (magnitude != 1)
                sb.Append(magnitude.ToString(CultureInfo.InvariantCulture));
            sb.Append(Charge > 0 ? '+' : '-');
        }

        if (!string.IsNullOrEmpty(State))
            sb.Append('(').Append(State).Append(')');

        return sb.ToString();
    }

    #region Разбор стороны уравнения

    /// <summary>
    /// Делит сторону уравнения на вещества по знаку "+", не разрывая заряды ионов
    /// ("MnO4- + 5Fe2+ + 8H+" -> три частицы)
    /// </summary>
    public static string[] SplitSide(string side)
    {
        if (string.IsNullOrWhiteSpace(side))
            return Array.Empty<string>();

        // "+" считается разделителем, если окружён пробелами либо стоит между
        // концом одной формулы и началом следующей ("Fe+O2"), но не в конце частицы ("Cu2+")
        var parts = Regex.Split(side.Trim(), @"\s+\+\s+|(?<=[A-Za-z0-9\)\]])\+(?=[A-Z(\[])");

        return parts
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Разбирает сторону уравнения в список формул
    /// </summary>
    public static List<MolecularFormula> ParseSide(string side)
    {
        var result = new List<MolecularFormula>();

        foreach (string part in SplitSide(side))
            result.Add(new MolecularFormula(part));

        if (result.Count == 0)
            throw new FormatException($"No species recognized in '{side}'");

        return result;
    }

    #endregion

    #region Разбор частей формулы

    // "H2O(l)" -> состояние "l", body становится "H2O"
    private static string ExtractState(ref string body)
    {
        var match = Regex.Match(body, @"\(([^()]+)\)\s*$");

        if (!match.Success || !KnownStates.Contains(match.Groups[1].Value.Trim()))
            return null;

        body = body.Substring(0, match.Index).TrimEnd();
        return match.Groups[1].Value.Trim().ToLowerInvariant();
    }

    // "SO4^2-" -> -2, "Cu2+" -> +2, "Ca++" -> +2, "Cl-" -> -1, "MnO4-" -> -1
    private static int ExtractCharge(ref string body)
    {
        var match = Regex.Match(body, @"(\^?)(\d*)(\++|-+)\s*$");

        if (!match.Success)
            return 0;

        // Заряд не может быть у пустого остатка ("+" сам по себе - не формула)
        string head = body.Substring(0, match.Index).TrimEnd();
        if (head.Length == 0)
            return 0;

        bool explicitMarker = match.Groups[1].Value.Length > 0;
        string digits = match.Groups[2].Value;
        string signs = match.Groups[3].Value;

        // Без "^" цифра перед знаком - величина заряда только у одноатомного иона ("Cu2+"),
        // иначе это индекс последнего элемента ("MnO4-" - это MnO4 с зарядом -1)
        bool digitsAreCharge = digits.Length > 0
            && (explicitMarker || Regex.IsMatch(head, @"^[A-Z][a-z]?$"));

        if (!digitsAreCharge)
            head = body.Substring(0, match.Index + match.Groups[1].Length + digits.Length).TrimEnd();

        int magnitude = digitsAreCharge
            ? int.Parse(digits, CultureInfo.InvariantCulture)
            : signs.Length;

        body = head;
        return signs[0] == '+' ? magnitude : -magnitude;
    }

    // "2H2O" -> коэффициент 2, body становится "H2O"
    private static int ExtractCoefficient(ref string body)
    {
        var match = Regex.Match(body, @"^(\d+)\s*(?=[A-Za-z(\[])");

        if (!match.Success)
            return 1;

        body = body.Substring(match.Length).Trim();
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // Состав с учётом кристаллогидратов: "CuSO4·5H2O"
    private static Dictionary<string, int> ParseComposition(string body)
    {
        var total = new Dictionary<string, int>();

        foreach (string part in body.Split(HydrateSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            string fragment = part.Trim();
            if (fragment.Length == 0)
                continue;

            int multiplier = ExtractCoefficient(ref fragment);

            foreach (var kvp in ParseFragment(fragment))
            {
                total.TryGetValue(kvp.Key, out int current);
                total[kvp.Key] = current + (kvp.Value * multiplier);
            }
        }

        return total;
    }

    // Рекурсивный спуск по одному фрагменту: элементы и вложенные скобки
    private static Dictionary<string, int> ParseFragment(string fragment)
    {
        var stack = new Stack<Dictionary<string, int>>();
        stack.Push(new Dictionary<string, int>());

        int i = 0;

        while (i < fragment.Length)
        {
            char c = fragment[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '(' || c == '[' || c == '{')
            {
                stack.Push(new Dictionary<string, int>());
                i++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                if (stack.Count == 1)
                    throw new FormatException($"Unbalanced brackets in '{fragment}'");

                var group = stack.Pop();
                i++;
                int count = ReadNumber(fragment, ref i);

                Merge(stack.Peek(), group, count);
            }
            else if (char.IsUpper(c))
            {
                int start = i;
                i++;

                while (i < fragment.Length && char.IsLower(fragment[i]))
                    i++;

                // Символы элементов не длиннее двух символов
                if (i - start > 2)
                    i = start + 2;

                string symbol = fragment.Substring(start, i - start);
                int count = ReadNumber(fragment, ref i);

                stack.Peek().TryGetValue(symbol, out int current);
                stack.Peek()[symbol] = current + count;
            }
            else
            {
                throw new FormatException($"Unexpected character '{c}' in formula '{fragment}'");
            }
        }

        if (stack.Count != 1)
            throw new FormatException($"Unbalanced brackets in '{fragment}'");

        return stack.Pop();
    }

    private static int ReadNumber(string source, ref int position)
    {
        int start = position;

        while (position < source.Length && char.IsDigit(source[position]))
            position++;

        return position == start
            ? 1
            : int.Parse(source.Substring(start, position - start), CultureInfo.InvariantCulture);
    }

    private static void Merge(Dictionary<string, int> target, Dictionary<string, int> source, int multiplier)
    {
        foreach (var kvp in source)
        {
            target.TryGetValue(kvp.Key, out int current);
            target[kvp.Key] = current + (kvp.Value * multiplier);
        }
    }

    #endregion
}
