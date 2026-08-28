using AI.LLM.Agents.Tools;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Safety;
using System.Globalization;
using System.Text;

namespace AI.Script.Chem;

/// <summary>
/// Химические инструменты агента: детерминированная проверка того,
/// что языковая модель склонна выдумывать.
/// </summary>
/// <remarks>
/// Модель уверенно пишет несбалансированные уравнения и молярные массы «по памяти».
/// Эти инструменты не рассуждают: они считают по таблице элементов и проверяют
/// законы сохранения, поэтому их ответ можно предъявлять как результат, а не как мнение.
/// Для сложных цепочек расчётов есть <c>run_script</c> с модулем <c>chem</c>.
/// </remarks>
public sealed class ChemAgentTools
{
    /// <summary>
    /// Молярная масса и состав соединения по формуле
    /// </summary>
    [AgentTool("chem_molar_mass", "Точно вычислить молярную массу и элементный состав по химической формуле. "
        + "Понимает скобки, кристаллогидраты и заряд: Ca(OH)2, CuSO4·5H2O, SO4^2-")]
    public string MolarMass(
        [ToolParameter("Химическая формула, например H2SO4 или K4[Fe(CN)6]")] string formula)
    {
        if (!MolecularFormula.TryParse(formula, out var parsed, out string error))
            return $"Формула '{formula}' не разобрана: {error}";

        if (!parsed.TryCalculateMolarMass(ChemContext.Database, out double mass, out string massError))
            return $"Формула '{formula}' разобрана, но масса не вычислена: {massError}";

        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"{parsed.CoreFormula}: M = {mass.ToString("F3", culture)} г/моль");
        text.Append("Состав: ");
        text.AppendLine(string.Join(", ", parsed.Elements.Select(e => $"{e.Key}×{e.Value}")));

        if (parsed.Charge != 0)
            text.AppendLine($"Заряд: {parsed.Charge:+#;-#;0}");

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Балансировка уравнения с проверкой сохранения атомов и заряда
    /// </summary>
    [AgentTool("chem_balance", "Расставить коэффициенты в уравнении реакции. Результат проверен на сохранение "
        + "атомов каждого элемента и суммарного заряда; если уравнение свести нельзя, инструмент об этом сообщает")]
    public string Balance(
        [ToolParameter("Уравнение реакции без коэффициентов, например 'Cu + HNO3 = Cu(NO3)2 + NO + H2O'")] string equation)
    {
        var result = ChemContext.Engine.Execute($"balance {equation}");

        return result.Success
            ? $"{result.Result.Trim()}\n(атомы и заряд проверены)"
            : $"Сбалансировать не удалось: {result.ErrorMessage}";
    }

    /// <summary>
    /// Проверка уже написанного уравнения
    /// </summary>
    [AgentTool("chem_check_equation", "Проверить, сбалансировано ли уравнение так, как оно записано. "
        + "Используй перед тем, как выдать пользователю уравнение реакции")]
    public string CheckEquation(
        [ToolParameter("Уравнение с коэффициентами, например '2H2 + O2 = 2H2O'")] string equation)
    {
        if (!MolecularFormula.TrySplitEquation(equation, out string left, out string right))
            return "В уравнении нет стрелки или знака равенства";

        List<MolecularFormula> reactants, products;

        try
        {
            reactants = MolecularFormula.ParseSide(left);
            products = MolecularFormula.ParseSide(right);
        }
        catch (FormatException ex)
        {
            return $"Уравнение не разобрано: {ex.Message}";
        }

        var mismatches = new List<string>();

        var elements = reactants.Concat(products)
            .SelectMany(f => f.Elements.Keys)
            .Distinct()
            .OrderBy(e => e, StringComparer.Ordinal);

        foreach (string element in elements)
        {
            long inLeft = reactants.Sum(f => (long)f.Coefficient * f.GetCount(element));
            long inRight = products.Sum(f => (long)f.Coefficient * f.GetCount(element));

            if (inLeft != inRight)
                mismatches.Add($"{element}: слева {inLeft}, справа {inRight}");
        }

        long chargeLeft = reactants.Sum(f => (long)f.Coefficient * f.Charge);
        long chargeRight = products.Sum(f => (long)f.Coefficient * f.Charge);

        if (chargeLeft != chargeRight)
            mismatches.Add($"заряд: слева {chargeLeft:+#;-#;0}, справа {chargeRight:+#;-#;0}");

        if (mismatches.Count == 0)
            return "Уравнение сбалансировано: атомы и заряд сходятся";

        var balanced = ChemContext.Engine.Execute($"balance {equation}");

        return $"Уравнение НЕ сбалансировано ({string.Join("; ", mismatches)})"
            + (balanced.Success ? $"\nПравильный вариант: {balanced.Result.Trim()}" : string.Empty);
    }

    /// <summary>
    /// Классификация смеси по составу
    /// </summary>
    [AgentTool("chem_classify_mixture", "Классифицировать смесь по СГС (CLP/ТР ТС) на основании состава: "
        + "вернуть сигнальное слово, пиктограммы, H-фразы и обоснование каждого отнесения. "
        + "Классификацию считают правила, а не языковая модель")]
    public string ClassifyMixture(
        [ToolParameter("Состав, по компоненту в строке: наименование | содержание в % | классификации через ';'. "
            + "Пример: 'метанол | 60 | Flam. Liq. 2; Acute Tox. 3 (oral); STOT SE 1'")] string composition)
    {
        if (string.IsNullOrWhiteSpace(composition))
            return "Не задан состав смеси";

        var mixture = new Mixture { Name = "смесь" };
        var unknown = new List<string>();

        foreach (string line in composition.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|');

            if (parts.Length < 2)
                continue;

            if (!double.TryParse(parts[1].Trim().TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double content))
                return $"Не разобрано содержание в строке: {line.Trim()}";

            var classifications = new List<HazardCategory>();

            if (parts.Length > 2)
            {
                foreach (string item in parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (HazardCatalog.TryParse(item, out HazardCategory category))
                        classifications.Add(category);
                    else if (!string.IsNullOrWhiteSpace(item))
                        unknown.Add(item.Trim());
                }
            }

            mixture.Add(new MixtureComponent
            {
                Name = parts[0].Trim(),
                ContentPercent = content,
                Classifications = classifications
            });
        }

        if (mixture.Components.Count == 0)
            return "Состав не разобран: ожидается по компоненту в строке в виде 'наименование | % | классификации'";

        var text = new StringBuilder(mixture.Classify().Report());

        if (unknown.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"Не распознаны классификации: {string.Join(", ", unknown.Distinct())}");
            text.AppendLine("Используйте нотацию CLP, например 'Skin Corr. 1B' или 'Aquatic Chronic 2'.");
        }

        return text.ToString();
    }

    /// <summary>
    /// Произвольный расчёт химическим движком
    /// </summary>
    [AgentTool("chem_calculate", "Выполнить химический расчёт: pH, растворимость, газовые законы, кинетика, "
        + "спектрофотометрия, фармакокинетика. Команда пишется по-английски, например 'pH of 0.01M HCl' "
        + "или 'Beer\\'s law A=0.45 eps=1500 l=1'. Список команд выдаёт команда 'help'")]
    public string Calculate(
        [ToolParameter("Команда химического движка")] string command)
    {
        var result = ChemContext.Engine.Execute(command);

        if (!result.Success)
            return $"Расчёт не выполнен: {result.ErrorMessage}";

        var text = new StringBuilder(result.Result?.Trim());

        foreach (string step in result.Steps.Take(20))
            text.AppendLine().Append("  ").Append(step);

        return text.ToString();
    }
}
