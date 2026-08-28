using System.Globalization;
using System.Text.RegularExpressions;

namespace AI.Solvers.Chem.Parsing;

/// <summary>
/// Разбор текстовой команды: определение типа и извлечение параметров.
/// Ключевые слова ищутся по границам слов (иначе "phenol" опознаётся как команда "pH"),
/// более специфичное ключевое слово имеет приоритет над общим.
/// </summary>
public class CommandParser
{
    private sealed class KeywordRule
    {
        public Regex Pattern { get; init; }
        public CommandType Type { get; init; }
        public int Priority { get; init; }
    }

    private readonly List<KeywordRule> _rules;

    /// <summary>
    /// Создаёт парсер с таблицей ключевых слов
    /// </summary>
    public CommandParser()
    {
        // priority: чем больше, тем раньше проверяется. По умолчанию - число слов и длина,
        // явное значение нужно для слишком общих слов ("calculate")
        var keywords = new (string Keyword, CommandType Type, int Priority)[]
        {
            // Неорганика
            ("balance", CommandType.Balance, 0),
            ("molar mass", CommandType.CalculateMass, 0),
            ("molecular weight", CommandType.CalculateMass, 0),
            ("molarity", CommandType.MolarityCalculation, 0),
            ("dilute", CommandType.Dilution, 0),
            ("mix solutions", CommandType.MixSolutions, 0),
            ("pH", CommandType.PhCalculation, 0),
            ("buffer", CommandType.BufferPH, 0),
            ("titration", CommandType.Titration, 0),
            ("oxidation states", CommandType.OxidationStates, 0),
            ("redox", CommandType.RedoxBalance, 0),
            ("calculate", CommandType.Stoichiometry, 1),

            // Физическая химия
            ("ideal gas", CommandType.IdealGas, 0),
            ("combined gas", CommandType.CombinedGas, 0),
            ("partial pressure", CommandType.PartialPressure, 0),
            ("delta H", CommandType.ThermoCalculation, 0),
            ("Hess", CommandType.HessLaw, 0),
            ("rate law", CommandType.RateLaw, 0),
            ("integrated rate law", CommandType.IntegratedRateLaw, 0),
            ("integrated rate", CommandType.IntegratedRateLaw, 0),
            ("determine order", CommandType.DetermineOrder, 0),
            ("half-life", CommandType.HalfLife, 0),
            ("Arrhenius", CommandType.Arrhenius, 0),
            ("Nernst", CommandType.NernstEquation, 0),
            ("Faraday", CommandType.FaradayLaw, 0),

            // Растворимость
            ("solubility", CommandType.Solubility, 0),
            ("common ion", CommandType.SolubilityCommonIon, 0),
            ("fractional precipitation", CommandType.FractionalPrecipitation, 0),
            ("precipitation", CommandType.PredictPrecipitation, 0),

            // Комплексы
            ("complexation at pH", CommandType.ComplexationAtPH, 0),
            ("stepwise complex", CommandType.StepwiseComplexation, 0),
            ("chelate", CommandType.ChelateEffect, 0),
            ("complex", CommandType.ComplexFormation, 0),

            // Органика
            ("SMILES to", CommandType.ParseSmiles, 0),
            ("structure to SMILES", CommandType.GenerateSmiles, 0),
            ("isomers", CommandType.GenerateIsomers, 0),
            ("functional groups", CommandType.FunctionalGroups, 0),
            ("predict product", CommandType.PredictProduct, 0),
            ("retrosynthesis", CommandType.Retrosynthesis, 0),
            ("IUPAC name", CommandType.IUPACNaming, 0),
            ("analyze", CommandType.Properties, 0),
            ("props", CommandType.Properties, 0),

            // Справочные
            ("properties of", CommandType.ElementInfo, 0),
            ("lookup", CommandType.CompoundLookup, 0),
            ("help", CommandType.Help, 0),

            // Медицина
            ("pharmacokinetics calculate_half_life", CommandType.PharmacokineticsHalfLife, 0),
            ("pharmacokinetics", CommandType.Pharmacokinetics, 0),
            ("dose", CommandType.CalculateDose, 0),
            ("blood gas", CommandType.BloodGasAnalysis, 0),
            ("bicarbonate", CommandType.CalculateBicarbonate, 0),
            ("base excess", CommandType.BaseExcess, 0),
            ("Michaelis-Menten", CommandType.MichaelisMenten, 0),
            ("Lineweaver-Burk", CommandType.LineweaverBurk, 0),
            ("enzyme inhibition", CommandType.EnzymeInhibition, 0),
            ("specific activity", CommandType.SpecificActivity, 0),

            // Спектроскопия
            ("Beer's law", CommandType.BeersLaw, 0),
            ("Beer law", CommandType.BeersLaw, 0),
            ("mixture analysis", CommandType.MixtureAnalysis, 0),
            ("calibration", CommandType.CalibrationCurve, 0)
        };

        _rules = keywords
            .Select(k => new KeywordRule
            {
                Pattern = BuildKeywordPattern(k.Keyword),
                Type = k.Type,
                Priority = k.Priority > 0 ? k.Priority : DefaultPriority(k.Keyword)
            })
            .OrderByDescending(r => r.Priority)
            .ToList();
    }

    /// <summary>
    /// Разбирает команду
    /// </summary>
    public ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ParsedCommand.Error("Empty command");

        input = input.Trim();

        var commandType = DetectCommandType(input);

        if (commandType == CommandType.Unknown)
            return ParsedCommand.Error("Unknown command type");

        var parameters = ExtractParameters(input, commandType);
        commandType = RefineCommandType(commandType, parameters);

        return new ParsedCommand
        {
            Success = true,
            CommandType = commandType,
            OriginalCommand = input,
            Parameters = parameters
        };
    }

    #region Определение типа команды

    // "common ion" -> \bcommon\s+ion\b (границы только там, где на краю буква/цифра)
    private static Regex BuildKeywordPattern(string keyword)
    {
        string escaped = Regex.Escape(keyword.Trim()).Replace("\\ ", @"\s+");

        string prefix = char.IsLetterOrDigit(keyword[0]) || keyword[0] == '_' ? @"\b" : string.Empty;
        string suffix = char.IsLetterOrDigit(keyword[^1]) || keyword[^1] == '_' ? @"\b" : string.Empty;

        return new Regex(prefix + escaped + suffix, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static int DefaultPriority(string keyword)
    {
        int words = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (words * 100) + keyword.Length;
    }

    private CommandType DetectCommandType(string input)
    {
        foreach (var rule in _rules)
        {
            if (rule.Pattern.IsMatch(input))
                return rule.Type;
        }

        // Уравнение реакции без ключевых слов
        if (Regex.IsMatch(input, @"^\s*\d*\s*[A-Z][A-Za-z0-9()\[\]·\s+]*[=→>⇌].*[A-Z]"))
            return CommandType.Balance;

        return CommandType.Unknown;
    }

    // Уточнение типа по фактически заданным параметрам
    private static CommandType RefineCommandType(CommandType type, Dictionary<string, string> parameters)
    {
        bool Has(params string[] names) => names.Any(parameters.ContainsKey);

        return type switch
        {
            // "complex metal=Ca ligand=EDTA pH=10 ..." - расчёт с учётом протонирования лиганда
            CommandType.ComplexFormation when Has("pH", "ph") => CommandType.ComplexationAtPH,

            // "solubility of AgCl ion=Cl concentration=0.1" - общий ионный эффект
            CommandType.Solubility when Has("ion", "common_ion") => CommandType.SolubilityCommonIon,

            _ => type
        };
    }

    #endregion

    #region Извлечение параметров

    private Dictionary<string, string> ExtractParameters(string input, CommandType type)
    {
        var parameters = new Dictionary<string, string>();

        switch (type)
        {
            case CommandType.Balance:
            case CommandType.RedoxBalance:
                ExtractReactionEquation(input, parameters);
                ExtractConditions(input, parameters);
                break;

            case CommandType.CalculateMass:
                ExtractMolarMassParams(input, parameters);
                break;

            case CommandType.Stoichiometry:
                ExtractStoichiometryParams(input, parameters);
                break;

            case CommandType.MolarityCalculation:
                ExtractMolarityParams(input, parameters);
                break;

            case CommandType.Dilution:
                ExtractDilutionParams(input, parameters);
                break;

            case CommandType.PhCalculation:
                ExtractPHParams(input, parameters);
                break;

            case CommandType.OxidationStates:
                ExtractOxidationStateParams(input, parameters);
                break;

            case CommandType.ThermoCalculation:
            case CommandType.HessLaw:
                ExtractReactionEquation(input, parameters);
                ExtractConditions(input, parameters);
                break;

            case CommandType.IdealGas:
            case CommandType.CombinedGas:
            case CommandType.PartialPressure:
                ExtractGasLawParams(input, parameters);
                break;

            case CommandType.ParseSmiles:
            case CommandType.GenerateSmiles:
                ExtractSmilesParams(input, parameters);
                break;

            case CommandType.GenerateIsomers:
                ExtractIsomerParams(input, parameters);
                break;

            case CommandType.FunctionalGroups:
            case CommandType.PredictProduct:
                ExtractReactionParams(input, parameters);
                break;

            case CommandType.Retrosynthesis:
                ExtractRetrosynthesisParams(input, parameters);
                break;

            case CommandType.IUPACNaming:
                ExtractIUPACParams(input, parameters);
                break;

            case CommandType.ElementInfo:
                ExtractElementParams(input, parameters);
                break;

            case CommandType.CompoundLookup:
                ExtractLookupParams(input, parameters);
                break;

            case CommandType.Properties:
                ExtractPropertiesParams(input, parameters);
                break;

            case CommandType.Solubility:
            case CommandType.SolubilityCommonIon:
            case CommandType.PredictPrecipitation:
            case CommandType.FractionalPrecipitation:
                ExtractKeyValueParameters(input, parameters);
                ExtractSolubilityParams(input, parameters);
                break;

            case CommandType.FaradayLaw:
                ExtractKeyValueParameters(input, parameters);
                ExtractFaradayParams(input, parameters);
                break;

            default:
                // Команды в формате key=value: медицина, кинетика, спектроскопия, комплексы
                ExtractKeyValueParameters(input, parameters);
                break;
        }

        return parameters;
    }

    /// <summary>
    /// Универсальное извлечение параметров вида key=value.
    /// Ключи в квадратных скобках сохраняются вместе со скобками ("[Cu2+]"),
    /// чтобы концентрация частицы не затирала её название ("metal=Cu [metal]=0.1M").
    /// </summary>
    private static void ExtractKeyValueParameters(string input, Dictionary<string, string> parameters)
    {
        // Списки значений: concentrations=1,2,3
        foreach (Match match in Regex.Matches(input, @"([\w\[\]+-]+)\s*=\s*([\d.eE+-]+(?:\s*,\s*[\d.eE+-]+)+)"))
            parameters[match.Groups[1].Value] = Regex.Replace(match.Groups[2].Value, @"\s+", string.Empty);

        // Концентрации в квадратных скобках: [metal]=0.1M, [Cu2+]=0.01
        foreach (Match match in Regex.Matches(input, @"\[([^\]\s=]+)\]\s*=\s*([^\s=]+)"))
        {
            string key = $"[{match.Groups[1].Value}]";

            if (!parameters.ContainsKey(key))
                StoreValue(parameters, key, match.Groups[2].Value.Trim());
        }

        // Обычные пары key=value
        foreach (Match match in Regex.Matches(input, @"(?<![\[\w])(\w+)\s*=\s*([^\s=]+)"))
        {
            string key = match.Groups[1].Value;

            if (!parameters.ContainsKey(key))
                StoreValue(parameters, key, match.Groups[2].Value.Trim());
        }
    }

    // Числовое значение очищается от единиц ("55mmHg" -> "55"), остальное сохраняется как есть
    private static void StoreValue(Dictionary<string, string> parameters, string key, string value)
    {
        string clean = CleanNumericValue(value);
        parameters[key] = clean;

        if (clean.Length != value.Length)
            parameters[key + "_unit"] = value.Substring(clean.Length);
    }

    /// <summary>
    /// Отделяет число от единиц измерения: "55mmHg" -> "55", "0.5M" -> "0.5", "AgCl" -> "AgCl"
    /// </summary>
    private static string CleanNumericValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var match = Regex.Match(value, @"^([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)");

        return match.Success && match.Groups[1].Value.Length > 0
            ? match.Groups[1].Value
            : value;
    }

    private static void ExtractReactionEquation(string input, Dictionary<string, string> parameters)
    {
        string cleaned = input;
        var prefixes = new[] { "balance", "redox", "calculate delta H for", "delta H for", "delta H", "Hess law for", "Hess", "calculate" };

        foreach (string prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(prefix.Length).TrimStart();
                break;
            }
        }

        // Стрелка ищется явно, чтобы не спутать её со знаком заряда ("Cl-", "Ag+")
        var arrow = Regex.Match(cleaned, @"(?:=>|->|→|⟶|⇌|=)");

        if (!arrow.Success)
            return;

        string left = cleaned.Substring(0, arrow.Index).Trim();
        string right = cleaned.Substring(arrow.Index + arrow.Length).Trim();

        // Условия после уравнения: "... in acidic medium"
        var tail = Regex.Match(right, @"\s+in\s+(?:acidic|basic|alkaline|neutral)\b", RegexOptions.IgnoreCase);

        if (tail.Success)
            right = right.Substring(0, tail.Index).Trim();

        if (left.Length > 0 && right.Length > 0)
        {
            parameters["reactants"] = left;
            parameters["products"] = right;
        }
    }

    private static void ExtractConditions(string input, Dictionary<string, string> parameters)
    {
        if (input.Contains("acidic", StringComparison.OrdinalIgnoreCase))
            parameters["medium"] = "acidic";
        else if (input.Contains("basic", StringComparison.OrdinalIgnoreCase) ||
                 input.Contains("alkaline", StringComparison.OrdinalIgnoreCase))
            parameters["medium"] = "basic";

        var tempMatch = Regex.Match(input, @"(\d+)\s*[°]?\s*[CK]\b");

        if (tempMatch.Success)
            parameters["temperature"] = tempMatch.Groups[1].Value;
    }

    private static void ExtractMolarMassParams(string input, Dictionary<string, string> parameters)
    {
        // "molar mass of H2SO4", "molar mass of CuSO4·5H2O"
        var match = Regex.Match(input, @"(?:molar\s+mass|molecular\s+weight)\s+(?:of\s+)?(\S+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["formula"] = match.Groups[1].Value;
    }

    private static void ExtractStoichiometryParams(string input, Dictionary<string, string> parameters)
    {
        // "calculate mass of Fe2O3 from 10g Fe + O2"
        var match = Regex.Match(input,
            @"calculate\s+mass\s+of\s+(\S+)\s+from\s+([\d.]+)\s*g\s+(\S+)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            parameters["target"] = match.Groups[1].Value;
            parameters["mass"] = match.Groups[2].Value;
            parameters["source"] = match.Groups[3].Value.Split('+')[0].Trim();
        }

        ExtractReactionEquation(input, parameters);
        ExtractKeyValueParameters(input, parameters);
    }

    private static void ExtractMolarityParams(string input, Dictionary<string, string> parameters)
    {
        // "molarity of 10g NaOH in 500ml"
        var match = Regex.Match(input,
            @"([\d.]+)\s*g\s+([A-Za-z0-9()\[\]·]+)\s+in\s+([\d.]+)\s*(ml|l\b)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            parameters["mass"] = match.Groups[1].Value;
            parameters["substance"] = match.Groups[2].Value;

            bool inLiters = match.Groups[4].Value.Trim().Equals("l", StringComparison.OrdinalIgnoreCase);
            double volume = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            parameters["volume"] = (inLiters ? volume * 1000 : volume).ToString(CultureInfo.InvariantCulture);
        }

        ExtractKeyValueParameters(input, parameters);
    }

    private static void ExtractDilutionParams(string input, Dictionary<string, string> parameters)
    {
        // "dilute 2M HCl to 0.5M, volume 100ml"
        var match = Regex.Match(input,
            @"([\d.]+)\s*M.*?to\s+([\d.]+)\s*M.*?([\d.]+)\s*ml", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            parameters["C1"] = match.Groups[1].Value;
            parameters["C2"] = match.Groups[2].Value;
            parameters["V2"] = match.Groups[3].Value;
        }

        ExtractKeyValueParameters(input, parameters);
    }

    private static void ExtractPHParams(string input, Dictionary<string, string> parameters)
    {
        // "pH of 0.01M HCl", "pH of 0.1M CH3COOH Ka=1.8e-5"
        var match = Regex.Match(input,
            @"([\d.]+(?:[eE][+-]?\d+)?)\s*M\s+([A-Za-z0-9()\[\]·]+)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            parameters["concentration"] = match.Groups[1].Value;
            parameters["substance"] = match.Groups[2].Value;
        }

        ExtractKeyValueParameters(input, parameters);
    }

    private static void ExtractOxidationStateParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input, @"oxidation\s+states?\s+(?:of\s+|in\s+)?(\S+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["formula"] = match.Groups[1].Value;
    }

    private static void ExtractGasLawParams(string input, Dictionary<string, string> parameters)
    {
        // "ideal gas P=2atm V=10L T=300K find n".
        // Единица измерения должна примыкать к числу, иначе "P=2 V=10" съедает "V" как единицу
        foreach (Match match in Regex.Matches(input, @"\b([PVNT])\s*=\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)([A-Za-z°]*)", RegexOptions.IgnoreCase))
        {
            string key = match.Groups[1].Value.ToUpperInvariant();
            parameters[key] = match.Groups[2].Value;

            if (match.Groups[3].Value.Length > 0)
                parameters[key + "_unit"] = match.Groups[3].Value;
        }

        var findMatch = Regex.Match(input, @"find\s+([PVNT])\b", RegexOptions.IgnoreCase);

        if (findMatch.Success)
            parameters["find"] = findMatch.Groups[1].Value.ToUpperInvariant();
    }

    private static void ExtractSmilesParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input, @"SMILES\s+to\s+structure\s+(\S+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value;

        match = Regex.Match(input, @"structure\s+to\s+SMILES\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["name"] = match.Groups[1].Value.Trim();
    }

    private static void ExtractIsomerParams(string input, Dictionary<string, string> parameters)
    {
        // "isomers of C4H10"
        var match = Regex.Match(input, @"isomers\s+(?:of\s+)?(\S+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["formula"] = match.Groups[1].Value;
    }

    private static void ExtractReactionParams(string input, Dictionary<string, string> parameters)
    {
        // "predict product CH3CH=CH2 + HBr", "functional groups CC(=O)O"
        var match = Regex.Match(input,
            @"(?:predict\s+product|functional\s+groups)\s+(?:of\s+|in\s+)?(.+?)(?:\s+with\s+|\s+conditions\s+|$)",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var reactants = match.Groups[1].Value.Split('+').Select(r => r.Trim()).Where(r => r.Length > 0).ToArray();

            for (int i = 0; i < reactants.Length; i++)
                parameters[$"reactant{i + 1}"] = reactants[i];

            parameters["reactant_count"] = reactants.Length.ToString(CultureInfo.InvariantCulture);

            if (reactants.Length > 0)
                parameters["smiles"] = reactants[0];
        }

        ExtractConditions(input, parameters);
    }

    private static void ExtractElementParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input,
            @"(?:properties|info)\s+(?:of\s+)?(?:element\s+)?([A-Z][a-z]?)\b", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["element"] = match.Groups[1].Value;
    }

    private static void ExtractLookupParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input, @"lookup\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["compound"] = match.Groups[1].Value.Trim();
    }

    private static void ExtractRetrosynthesisParams(string input, Dictionary<string, string> parameters)
    {
        // Регистр сохраняется: для SMILES "C" и "c" - разные атомы
        var match = Regex.Match(input, @"retrosynthesis\s+(.+?)\s+from\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            parameters["target"] = match.Groups[1].Value.Trim();
            parameters["starting"] = match.Groups[2].Value.Trim();
            return;
        }

        match = Regex.Match(input, @"retrosynthesis\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["target"] = match.Groups[1].Value.Trim();
    }

    private static void ExtractIUPACParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input, @"IUPAC\s+name\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value.Trim();
    }

    private static void ExtractPropertiesParams(string input, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(input, @"(?:analyze|props)\s+(.+)", RegexOptions.IgnoreCase);

        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value.Trim();
    }

    private static void ExtractSolubilityParams(string input, Dictionary<string, string> parameters)
    {
        // "solubility of AgCl", "common ion compound=AgCl ..."
        var match = Regex.Match(input, @"(?:solubility|precipitation)\s+of\s+(\S+)", RegexOptions.IgnoreCase);

        if (match.Success && !parameters.ContainsKey("compound"))
            parameters["compound"] = match.Groups[1].Value;

        // "with common ion Cl"
        match = Regex.Match(input, @"with\s+(?:common\s+)?ion\s+(\S+)", RegexOptions.IgnoreCase);

        if (match.Success && !parameters.ContainsKey("ion"))
            parameters["ion"] = match.Groups[1].Value;
    }

    private static void ExtractFaradayParams(string input, Dictionary<string, string> parameters)
    {
        // "Faraday 2A 3600s Cu2+"
        var match = Regex.Match(input, @"Faraday\s+([\d.]+)\s*A\b", RegexOptions.IgnoreCase);

        if (match.Success && !parameters.ContainsKey("I"))
            parameters["I"] = match.Groups[1].Value;
    }

    #endregion
}
