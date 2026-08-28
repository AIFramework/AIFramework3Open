using System.Text.RegularExpressions;
using System.Globalization;

namespace FractalAgentsAI.Solvers.Chem.Parsing;

public class CommandParser
{
    private readonly Dictionary<string, CommandType> _keywords;

    public CommandParser()
    {
        _keywords = new Dictionary<string, CommandType>(StringComparer.OrdinalIgnoreCase)
        {
            ["balance"] = CommandType.Balance,
            ["molar mass"] = CommandType.CalculateMass,
            ["molarity"] = CommandType.MolarityCalculation,
            ["dilute"] = CommandType.Dilution,
            ["mix"] = CommandType.MixSolutions,
            ["pH"] = CommandType.PhCalculation,
            ["buffer"] = CommandType.BufferPH,
            ["titration"] = CommandType.Titration,
            ["oxidation states"] = CommandType.OxidationStates,
            ["redox"] = CommandType.RedoxBalance,
            ["ideal gas"] = CommandType.IdealGas,
            ["combined gas"] = CommandType.CombinedGas,
            ["partial pressure"] = CommandType.PartialPressure,
            ["delta H"] = CommandType.ThermoCalculation,
            ["Hess"] = CommandType.HessLaw,
            ["rate law"] = CommandType.RateLaw,
            ["half-life"] = CommandType.HalfLife,
            ["Arrhenius"] = CommandType.Arrhenius,
            ["Nernst"] = CommandType.NernstEquation,
            ["Faraday"] = CommandType.FaradayLaw,
            ["SMILES to"] = CommandType.ParseSmiles,
            ["structure to SMILES"] = CommandType.GenerateSmiles,
            ["isomers"] = CommandType.GenerateIsomers,
            ["functional groups"] = CommandType.FunctionalGroups,
            ["predict product"] = CommandType.PredictProduct,
            ["retrosynthesis"] = CommandType.Retrosynthesis,
            ["IUPAC name"] = CommandType.IUPACNaming,
            ["properties of"] = CommandType.ElementInfo,
            ["lookup"] = CommandType.CompoundLookup,
            ["calculate"] = CommandType.Stoichiometry,
            ["analyze"] = CommandType.Properties,
            ["props"] = CommandType.Properties,
            ["help"] = CommandType.Help,
            
            // Медицинские расчёты
            ["pharmacokinetics calculate_half_life"] = CommandType.PharmacokineticsHalfLife, // Более специфичная команда сначала
            ["pharmacokinetics"] = CommandType.Pharmacokinetics,
            ["dose"] = CommandType.CalculateDose,
            ["blood gas"] = CommandType.BloodGasAnalysis,
            ["bicarbonate"] = CommandType.CalculateBicarbonate,
            ["base excess"] = CommandType.BaseExcess,
            ["Michaelis-Menten"] = CommandType.MichaelisMenten,
            ["Lineweaver-Burk"] = CommandType.LineweaverBurk,
            ["enzyme inhibition"] = CommandType.EnzymeInhibition,
            ["specific activity"] = CommandType.SpecificActivity,
            
            // Растворимость и комплексы
            ["solubility"] = CommandType.Solubility,
            ["common ion"] = CommandType.SolubilityCommonIon,
            ["precipitation"] = CommandType.PredictPrecipitation,
            ["fractional precipitation"] = CommandType.FractionalPrecipitation,
            ["complex"] = CommandType.ComplexFormation,
            ["stepwise complex"] = CommandType.StepwiseComplexation,
            ["chelate"] = CommandType.ChelateEffect,
            
            // Спектроскопия
            ["Beer's law"] = CommandType.BeersLaw,
            ["Beer law"] = CommandType.BeersLaw,
            ["mixture analysis"] = CommandType.MixtureAnalysis,
            ["calibration"] = CommandType.CalibrationCurve,
            
            // Расширенная кинетика
            ["determine order"] = CommandType.DetermineOrder,
            ["integrated rate"] = CommandType.IntegratedRateLaw
        };
    }

    public ParsedCommand Parse(string input)
    {
        input = input.Trim();

        // Определение типа команды
        var commandType = DetectCommandType(input);

        if (commandType == CommandType.Unknown)
            return ParsedCommand.Error("Unknown command type");

        // Извлечение параметров в зависимости от типа
        var parameters = ExtractParameters(input, commandType);

        return new ParsedCommand
        {
            Success = true,
            CommandType = commandType,
            OriginalCommand = input,
            Parameters = parameters
        };
    }

    private CommandType DetectCommandType(string input)
    {
        foreach (var kvp in _keywords.OrderByDescending(k => k.Key.Length))
        {
            if (input.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        // Специальные паттерны
        if (Regex.IsMatch(input, @"^\s*[A-Z][a-z]*.*?[+=].*?[A-Z]", RegexOptions.IgnoreCase))
            return CommandType.Balance;

        return CommandType.Unknown;
    }

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

            case CommandType.IdealGas:
                ExtractGasLawParams(input, parameters);
                break;

            case CommandType.ParseSmiles:
            case CommandType.GenerateSmiles:
                ExtractSmilesParams(input, parameters);
                break;

            case CommandType.GenerateIsomers:
                ExtractIsomerParams(input, parameters);
                break;

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
                
            case CommandType.Properties:
                ExtractPropertiesParams(input, parameters);
                break;
                
            // Медицинские расчёты - используют key=value формат
            case CommandType.Pharmacokinetics:
            case CommandType.PharmacokineticsHalfLife: // NEW
            case CommandType.CalculateDose:
            case CommandType.BloodGasAnalysis:
            case CommandType.CalculateBicarbonate:
            case CommandType.BaseExcess:
            case CommandType.MichaelisMenten:
            case CommandType.LineweaverBurk:
            case CommandType.EnzymeInhibition:
            case CommandType.SpecificActivity:
                ExtractKeyValueParameters(input, parameters);
                break;
                
            // Растворимость и комплексы
            case CommandType.Solubility:
            case CommandType.SolubilityCommonIon:
            case CommandType.PredictPrecipitation:
            case CommandType.FractionalPrecipitation:
            case CommandType.ComplexFormation:
            case CommandType.StepwiseComplexation:
            case CommandType.ComplexationAtPH:
            case CommandType.ChelateEffect:
                ExtractKeyValueParameters(input, parameters);
                ExtractSpecialFormats(input, parameters, type);
                break;
                
            // Спектроскопия
            case CommandType.BeersLaw:
            case CommandType.MixtureAnalysis:
            case CommandType.CalibrationCurve:
                ExtractKeyValueParameters(input, parameters);
                break;
                
            // Расширенная кинетика
            case CommandType.DetermineOrder:
            case CommandType.IntegratedRateLaw:
            case CommandType.RateLaw: // NEW
                ExtractKeyValueParameters(input, parameters);
                break;

            // Электрохимия
            case CommandType.NernstEquation:
            case CommandType.FaradayLaw:
                ExtractKeyValueParameters(input, parameters);
                ExtractSpecialFormats(input, parameters, type);
                break;
                
            // Буферы и титрование
            case CommandType.BufferPH:
            case CommandType.Titration:
                ExtractKeyValueParameters(input, parameters);
                break;
        }

        return parameters;
    }
    
    /// <summary>
    /// Универсальный метод для извлечения параметров вида key=value
    /// </summary>
    private void ExtractKeyValueParameters(string input, Dictionary<string, string> parameters)
    {
        // Паттерн: key=value, где value - это всё до следующего пробела (без захвата следующих ключей)
        // Используем негативный lookahead чтобы остановиться перед следующим key=
        var matches = Regex.Matches(input, @"(\w+)\s*=\s*([^\s=]+)", RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            
            // Очищаем значение от единиц измерения и сохраняем только число
            string cleanValue = CleanNumericValue(value);
            
            // DEBUG: Вывод в консоль для отладки парсера
            Console.WriteLine($"[Parser] Key: {key}, Raw: '{value}', Clean: '{cleanValue}'");
            
            parameters[key] = cleanValue;
            
            // Сохраняем оригинальное значение с единицами под отдельным ключом (если нужно)
            if (cleanValue != value)
            {
                parameters[key + "_unit"] = value.Substring(cleanValue.Length);
            }
        }
        
        // Дополнительно ищем параметры со значениями в квадратных скобках: [metal]=0.01M
        var bracketMatches = Regex.Matches(input, @"\[(\w+)\]\s*=\s*([^\s=]+)", RegexOptions.IgnoreCase);
        foreach (Match match in bracketMatches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            string cleanValue = CleanNumericValue(value);
            Console.WriteLine($"[Parser] Key: [{key}], Raw: '{value}', Clean: '{cleanValue}'");
            parameters[key] = cleanValue;
        }
        
        // Ищем массивы значений: concentrations=1,2,3,4 (могут содержать запятые)
        var arrayMatches = Regex.Matches(input, @"(\w+)\s*=\s*([\d.,e+-]+(?:,[\d.,e+-]+)+)", RegexOptions.IgnoreCase);
        foreach (Match match in arrayMatches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            Console.WriteLine($"[Parser] Array detected: {key}='{value}'");
            // Перезаписываем, если это массив
            parameters[key] = value;
        }
    }
    
    /// <summary>
    /// Очищает значение от единиц измерения, оставляя только число
    /// Примеры: "7.25" -> "7.25", "55mmHg" -> "55", "18mEq/L" -> "18", "0.5M" -> "0.5"
    /// </summary>
    private string CleanNumericValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        // Если это список (содержит несколько запятых), возвращаем как есть
        if (value.Count(c => c == ',') > 1) return value;

        // Нормализуем: заменяем запятую на точку для универсальности,
        // НО только если это похоже на одно число (одна запятая между цифрами)
        string normalized = value;
        if (Regex.IsMatch(value, @"^\d+,\d+([a-zA-Z%]*)$"))
        {
            normalized = value.Replace(',', '.');
        }
        else 
        {
            // Если есть запятая, но это не похоже на "1,2" (например "1, 2" с пробелом или список),
            // то возможно это список из 2 элементов, который не попал в regex массивов
            // Оставим запятую как есть.
        }

        // Более надежный Regex для double чисел
        var match = Regex.Match(normalized, @"^([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)");
        
        if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
        {
            return match.Groups[1].Value;
        }
        
        return value;
    }
    
    /// <summary>
    /// Извлечение специальных форматов для конкретных команд
    /// </summary>
    private void ExtractSpecialFormats(string input, Dictionary<string, string> parameters, CommandType type)
    {
        switch (type)
        {
            case CommandType.FaradayLaw:
                // "Faraday 2A"
                var matchCurrent = Regex.Match(input, @"Faraday\s+([\d.]+)\s*A", RegexOptions.IgnoreCase);
                if (matchCurrent.Success) 
                    parameters["I"] = matchCurrent.Groups[1].Value;
                break;

            case CommandType.Solubility:
            case CommandType.SolubilityCommonIon:
                // "solubility of AgCl"
                var match = Regex.Match(input, @"solubility\s+of\s+(\w+)", RegexOptions.IgnoreCase);
                if (match.Success)
                    parameters["compound"] = match.Groups[1].Value;
                    
                // "with common ion Cl"
                match = Regex.Match(input, @"with\s+(?:common\s+)?ion\s+(\w+)", RegexOptions.IgnoreCase);
                if (match.Success)
                    parameters["ion"] = match.Groups[1].Value;
                break;
                
            case CommandType.ComplexFormation:
            case CommandType.StepwiseComplexation:
                // "complex metal=Cu2+"
                // Уже обрабатывается в ExtractKeyValueParameters
                break;
        }
    }

    private void ExtractReactionEquation(string input, Dictionary<string, string> parameters)
    {
        // Удаляем ключевые слова команд перед парсингом уравнения
        string cleaned = input;
        var keywords = new[] { "balance", "redox", "calculate delta H for", "delta H for", "calculate" };
        foreach (var keyword in keywords)
        {
            if (cleaned.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(keyword.Length).TrimStart();
                break;
            }
        }

        // Паттерн: A + B = C + D или A + B -> C + D
        var match = Regex.Match(cleaned, @"([^=→>]+)\s*[=→>]\s*(.+?)(?:\s+in\s+|$)");

        if (match.Success)
        {
            parameters["reactants"] = match.Groups[1].Value.Trim();
            parameters["products"] = match.Groups[2].Value.Trim();
        }
    }

    private void ExtractConditions(string input, Dictionary<string, string> parameters)
    {
        // in acidic medium / in basic medium
        if (input.Contains("acidic", StringComparison.OrdinalIgnoreCase))
            parameters["medium"] = "acidic";
        else if (input.Contains("basic", StringComparison.OrdinalIgnoreCase))
            parameters["medium"] = "basic";

        // Температура
        var tempMatch = Regex.Match(input, @"(\d+)\s*[°]?[CK]");
        if (tempMatch.Success)
            parameters["temperature"] = tempMatch.Groups[1].Value;
    }

    private void ExtractMolarMassParams(string input, Dictionary<string, string> parameters)
    {
        // "molar mass of H2SO4"
        var match = Regex.Match(input, @"(?:molar mass|molecular weight)\s+of\s+([A-Z][a-z0-9()\[\]]+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["formula"] = match.Groups[1].Value;
    }

    private void ExtractStoichiometryParams(string input, Dictionary<string, string> parameters)
    {
        // "calculate mass of Fe2O3 from 10g Fe + O2"
        var match = Regex.Match(input, @"calculate\s+mass\s+of\s+(\w+)\s+from\s+([\d.]+)\s*g\s+(\w+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            parameters["target"] = match.Groups[1].Value;
            parameters["mass"] = match.Groups[2].Value;
            parameters["source"] = match.Groups[3].Value;
        }

        ExtractReactionEquation(input, parameters);
    }

    private void ExtractMolarityParams(string input, Dictionary<string, string> parameters)
    {
        // "molarity of 10g NaOH in 500ml"
        var match = Regex.Match(input, @"(\d+\.?\d*)\s*g\s+(\w+)\s+in\s+(\d+\.?\d*)\s*ml", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            parameters["mass"] = match.Groups[1].Value;
            parameters["substance"] = match.Groups[2].Value;
            parameters["volume"] = match.Groups[3].Value;
        }
    }

    private void ExtractDilutionParams(string input, Dictionary<string, string> parameters)
    {
        // "dilute 2M HCl to 0.5M, volume 100ml"
        var match = Regex.Match(input, @"(\d+\.?\d*)\s*M.*?to\s+(\d+\.?\d*)\s*M.*?(\d+\.?\d*)\s*ml", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            parameters["C1"] = match.Groups[1].Value;
            parameters["C2"] = match.Groups[2].Value;
            parameters["V2"] = match.Groups[3].Value;
        }
    }

    private void ExtractPHParams(string input, Dictionary<string, string> parameters)
    {
        // "pH of 0.01M HCl" или "pH of 0.1M CH3COOH, Ka=1.8e-5"
        var match = Regex.Match(input, @"(\d+\.?\d*(?:e[+-]?\d+)?)\s*M\s+(\w+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            parameters["concentration"] = match.Groups[1].Value;
            parameters["substance"] = match.Groups[2].Value;
        }

        var kaMatch = Regex.Match(input, @"Ka\s*=\s*([\d.e+-]+)", RegexOptions.IgnoreCase);
        if (kaMatch.Success)
            parameters["Ka"] = kaMatch.Groups[1].Value;
    }

    private void ExtractGasLawParams(string input, Dictionary<string, string> parameters)
    {
        // "ideal gas P=2atm, V=10L, T=300K, find n"
        var matches = Regex.Matches(input, @"([PVNT])\s*=\s*([\d.]+)\s*(\w*)", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            parameters[match.Groups[1].Value.ToUpper()] = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                parameters[match.Groups[1].Value.ToUpper() + "_unit"] = match.Groups[3].Value;
        }

        var findMatch = Regex.Match(input, @"find\s+([PVNT])", RegexOptions.IgnoreCase);
        if (findMatch.Success)
            parameters["find"] = findMatch.Groups[1].Value.ToUpper();
    }

    private void ExtractSmilesParams(string input, Dictionary<string, string> parameters)
    {
        // "SMILES to structure CC(C)CCO"
        var match = Regex.Match(input, @"SMILES\s+to\s+structure\s+([^\s]+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value;

        // "structure to SMILES 2-methylbutanol"
        match = Regex.Match(input, @"structure\s+to\s+SMILES\s+(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["name"] = match.Groups[1].Value.Trim();
    }

    private void ExtractIsomerParams(string input, Dictionary<string, string> parameters)
    {
        // "isomers of C4H10"
        var match = Regex.Match(input, @"isomers\s+of\s+([A-Z][a-z0-9]+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["formula"] = match.Groups[1].Value;
    }

    private void ExtractReactionParams(string input, Dictionary<string, string> parameters)
    {
        // "predict product CH3CH=CH2 + HBr"
        var match = Regex.Match(input, @"predict\s+product\s+(.+?)(?:\s+with\s+|\s+conditions\s+|$)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var reactantsStr = match.Groups[1].Value;
            var reactants = reactantsStr.Split('+').Select(r => r.Trim()).ToArray();

            for (int i = 0; i < reactants.Length; i++)
                parameters[$"reactant{i + 1}"] = reactants[i];

            parameters["reactant_count"] = reactants.Length.ToString();
        }

        ExtractConditions(input, parameters);
    }

    private void ExtractElementParams(string input, Dictionary<string, string> parameters)
    {
        // "properties of element Fe"
        var match = Regex.Match(input, @"(?:properties|info)\s+(?:of\s+)?(?:element\s+)?([A-Z][a-z]?)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["element"] = match.Groups[1].Value;
    }

    private void ExtractRetrosynthesisParams(string input, Dictionary<string, string> parameters)
    {
        // "retrosynthesis aspirin from benzene" или "retrosynthesis CC(=O)OCC from benzene"
        var match = Regex.Match(input, @"retrosynthesis\s+(.+?)\s+from\s+(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            parameters["target"] = match.Groups[1].Value.Trim();
            parameters["starting"] = match.Groups[2].Value.Trim();
        }
        else
        {
            // "retrosynthesis aspirin" или "retrosynthesis CC(=O)OCC"
            // Захватываем всё после "retrosynthesis" до конца строки
            match = Regex.Match(input, @"retrosynthesis\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
                parameters["target"] = match.Groups[1].Value.Trim();
        }
    }

    private void ExtractIUPACParams(string input, Dictionary<string, string> parameters)
    {
        // "IUPAC name CC(C)CCO" - захватываем весь SMILES до конца
        var match = Regex.Match(input, @"IUPAC\s+name\s+(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value.Trim();
    }
    
    private void ExtractPropertiesParams(string input, Dictionary<string, string> parameters)
    {
        // "analyze CC(=O)OCC" или "props CC(=O)OCC"
        var match = Regex.Match(input, @"(?:analyze|props)\s+(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["smiles"] = match.Groups[1].Value.Trim();
    }
}