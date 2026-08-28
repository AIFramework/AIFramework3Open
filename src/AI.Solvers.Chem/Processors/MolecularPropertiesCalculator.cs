using FractalAgentsAI.Solvers.Chem.Core;
using NCDK;
using NCDK.Aromaticities;
using NCDK.Graphs;
using NCDK.SMARTS;
using NCDK.Smiles;
using NCDK.Tools.Manipulator;
using System.Text;

namespace FractalAgentsAI.Solvers.Chem.Processors;

/// <summary>
/// Калькулятор молекулярных свойств из SMILES
/// </summary>
public class MolecularPropertiesCalculator
{
    private readonly SmilesParser _parser;
    private readonly VerbosityLevel _verbosity;

    public MolecularPropertiesCalculator(VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _verbosity = verbosity;
        _parser = new SmilesParser(NCDK.Default.ChemObjectBuilder.Instance);
    }

    public ChemResult CalculateProperties(string smiles)
    {
        try
        {
            var molecule = _parser.ParseSmiles(smiles);
            AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);
            CDK.HydrogenAdder.AddImplicitHydrogens(molecule);
            AtomContainerManipulator.ConvertImplicitToExplicitHydrogens(molecule);
            Aromaticity.CDKLegacy.Apply(molecule);

            var result = new StringBuilder();
            result.AppendLine("═══════════════════════════════════════════════════════════════");
            result.AppendLine($"  МОЛЕКУЛЯРНЫЕ СВОЙСТВА");
            result.AppendLine("═══════════════════════════════════════════════════════════════");
            result.AppendLine();

            // 1. БАЗОВАЯ ИНФОРМАЦИЯ
            result.AppendLine("┌─ БАЗОВАЯ ИНФОРМАЦИЯ ─────────────────────────────────────┐");
            result.AppendLine($"  SMILES: {smiles}");
            
            var formula = MolecularFormulaManipulator.GetMolecularFormula(molecule);
            var formulaString = MolecularFormulaManipulator.GetString(formula);
            result.AppendLine($"  Молекулярная формула: {formulaString}");
            
            var mass = MolecularFormulaManipulator.GetMass(formula);
            result.AppendLine($"  Молекулярная масса: {mass:F2} г/моль");
            result.AppendLine();

            // 2. СТРУКТУРНЫЕ ХАРАКТЕРИСТИКИ
            result.AppendLine("┌─ СТРУКТУРА ──────────────────────────────────────────────┐");
            var totalAtoms = molecule.Atoms.Count;
            var heavyAtomCount = molecule.Atoms.Count(a => a.Symbol != "H");
            var hydrogenCount = totalAtoms - heavyAtomCount;
            result.AppendLine($"  Тяжёлых атомов: {heavyAtomCount}");
            result.AppendLine($"  Атомов водорода: {hydrogenCount}");
            result.AppendLine($"  Всего атомов: {totalAtoms}");
            
            // Подсчёт связей между тяжёлыми атомами и связей с водородом
            var heavyBonds = molecule.Bonds.Count(b => b.Begin.Symbol != "H" && b.End.Symbol != "H");
            var hydrogenBonds = molecule.Bonds.Count - heavyBonds;
            result.AppendLine($"  Связей (тяжёлые атомы): {heavyBonds}");
            result.AppendLine($"  Связей (с водородом): {hydrogenBonds}");
            result.AppendLine($"  Связей всего: {molecule.Bonds.Count}");
            
            // Подсчёт элементов
            var elementCounts = new Dictionary<string, int>();
            foreach (var atom in molecule.Atoms)
            {
                var symbol = atom.Symbol;
                if (!elementCounts.ContainsKey(symbol))
                    elementCounts[symbol] = 0;
                elementCounts[symbol]++;
            }
            
            result.Append("  Состав: ");
            result.AppendLine(string.Join(", ", elementCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value}")));
            result.AppendLine();

            // 3. ТОПОЛОГИЯ
            result.AppendLine("┌─ ТОПОЛОГИЯ ──────────────────────────────────────────────┐");
            
            // Степень ненасыщенности (DBE)
            var C = elementCounts.ContainsKey("C") ? elementCounts["C"] : 0;
            var H = elementCounts.ContainsKey("H") ? elementCounts["H"] : 0;
            var N = elementCounts.ContainsKey("N") ? elementCounts["N"] : 0;
            var X = elementCounts.Where(kv => new[] { "F", "Cl", "Br", "I" }.Contains(kv.Key)).Sum(kv => kv.Value);
            
            double DBE = (2.0 * C + 2 + N - H - X) / 2.0;
            result.AppendLine($"  Степень ненасыщенности (DBE): {DBE:F1}");
            
            // Циклы
            var cycles = Cycles.FindAll(molecule);
            result.AppendLine($"  Количество циклов: {cycles.GetPaths().Length}");
            
            // Ароматичность
            var aromaticAtoms = molecule.Atoms.Count(a => a.IsAromatic);
            var aromaticBonds = molecule.Bonds.Count(b => b.IsAromatic);
            result.AppendLine($"  Ароматических атомов: {aromaticAtoms}");
            result.AppendLine($"  Ароматических связей: {aromaticBonds}");
            
            // Вращающиеся связи
            var rotatableBonds = CountRotatableBonds(molecule);
            result.AppendLine($"  Вращающихся связей: {rotatableBonds}");
            result.AppendLine();

            // 4. ФУНКЦИОНАЛЬНЫЕ ГРУППЫ
            result.AppendLine("┌─ ФУНКЦИОНАЛЬНЫЕ ГРУППЫ ──────────────────────────────────┐");
            var functionalGroups = IdentifyFunctionalGroups(molecule);
            if (functionalGroups.Any())
            {
                foreach (var group in functionalGroups)
                    result.AppendLine($"  • {group}");
            }
            else
            {
                result.AppendLine("  Специфических функциональных групп не обнаружено");
            }
            result.AppendLine();

            // 5. ПРОГНОЗИРУЕМЫЕ ФИЗИЧЕСКИЕ СВОЙСТВА
            result.AppendLine("┌─ ПРОГНОЗИРУЕМЫЕ СВОЙСТВА (приблизительно) ───────────────┐");
            
            // LogP (липофильность) - упрощённая формула Wildman-Crippen
            var logP = EstimateLogP(molecule, elementCounts);
            result.AppendLine($"  LogP (липофильность): {logP:F2}");
            
            // Подсчитываем полярные группы для оценки
            var polarGroupsList = functionalGroups.Where(g => 
                g.Contains("Фенол") || g.Contains("Катехол") || g.Contains("Гидроксил") || 
                g.Contains("Амин") || g.Contains("Карбоксил")).ToList();
            
            int totalPolarGroups = polarGroupsList.Count;
            foreach (var group in polarGroupsList)
            {
                var match = System.Text.RegularExpressions.Regex.Match(group, @"×(\d+)");
                if (match.Success)
                {
                    int count = int.Parse(match.Groups[1].Value);
                    totalPolarGroups += (count - 1);
                }
            }
            
            // Температура кипения/разложения
            var boilingPoint = EstimateBoilingPoint(mass, aromaticAtoms > 0, totalPolarGroups);
            
            if (mass > 100 && totalPolarGroups >= 2)
            {
                result.AppendLine($"  Т. разложения: ~{boilingPoint:F0}°C (термически нестабильно)");
            }
            else if (mass > 150)
            {
                result.AppendLine($"  Т. кипения/разложения: ~{boilingPoint:F0}°C");
            }
            else
            {
                result.AppendLine($"  Температура кипения (прогноз): ~{boilingPoint:F0}°C");
            }
            
            // Растворимость в воде
            var solubility = EstimateWaterSolubility(logP, functionalGroups);
            result.AppendLine($"  Растворимость в воде: {solubility}");
            
            // Полярность
            var polarity = EstimatePolarity(functionalGroups, aromaticAtoms, heavyAtomCount);
            result.AppendLine($"  Полярность: {polarity}");
            result.AppendLine();

            // 6. ПРАВИЛО ЛИПИНСКОГО (Rule of Five)
            result.AppendLine("┌─ ПРАВИЛО ЛИПИНСКОГО (лекарственноподобность) ────────────┐");
            var lipinski = CheckLipinskiRule(mass, logP, CountHBondDonors(molecule), CountHBondAcceptors(molecule));
            foreach (var rule in lipinski)
                result.AppendLine($"  {rule}");
            result.AppendLine();

            var chemResult = ChemResult.Ok(result.ToString());
            chemResult.Data["smiles"] = smiles;
            chemResult.Data["formula"] = formulaString;
            chemResult.Data["mass"] = mass;
            chemResult.Data["logP"] = logP;
            chemResult.Data["dbe"] = DBE;
            
            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Ошибка анализа SMILES: {ex.Message}");
        }
    }

    private List<string> IdentifyFunctionalGroups(IAtomContainer molecule)
    {
        var groups = new List<string>();
        
        var patterns = new Dictionary<string, string>
        {
            ["Фенол"] = "[OX2H][c]", // Фенольный OH (на ароматическом кольце)
            ["Катехол"] = "[OX2H][c][c][OX2H]", // Два соседних фенольных OH
            ["Гидроксил (спиртовой)"] = "[OX2H][CX4]", // Спиртовой OH (на алифатическом C)
            ["Карбонил (кетон)"] = "[#6][CX3](=O)[#6]",
            ["Альдегид"] = "[CX3H1](=O)[#6]",
            ["Карбоксил (кислота)"] = "[CX3](=O)[OX2H1]",
            ["Сложный эфир"] = "[CX3](=[OX1])[OX2][#6]",
            ["Амин (первичный)"] = "[NX3;H2;!$(NC=O)]", // Исключаем амиды
            ["Амин (вторичный)"] = "[NX3;H1;!$(NC=O)]",
            ["Амин (третичный)"] = "[NX3;H0;!$(NC=O);!$(N=*)]",
            ["Амид"] = "[CX3](=[OX1])[NX3]",
            ["Нитрогруппа"] = "[$([NX3](=O)=O),$([NX3+](=O)[O-])]",
            ["Простой эфир"] = "[OD2]([CX4])[CX4]", // Только алифатические эфиры
            ["Тиол"] = "[SX2H]",
            ["Дисульфид"] = "[SX2][SX2]",
            ["Циано (нитрил)"] = "[CX2]#[NX1]",
            ["Изоцианат"] = "[NX2]=C=O",
            ["Галоген"] = "[F,Cl,Br,I]"
        };

        foreach (var kvp in patterns)
        {
            try
            {
                var pattern = SmartsPattern.Create(kvp.Value);
                if (pattern.Matches(molecule))
                {
                    // Подсчитываем количество вхождений
                    var matches = pattern.MatchAll(molecule);
                    int count = matches.Count();
                    if (count > 1 && !kvp.Key.Contains("Катехол"))
                        groups.Add($"{kvp.Key} (×{count})");
                    else
                        groups.Add(kvp.Key);
                }
            }
            catch { }
        }

        return groups;
    }

    private int CountRotatableBonds(IAtomContainer molecule)
    {
        int count = 0;
        foreach (var bond in molecule.Bonds)
        {
            // Только одинарные связи
            if (bond.Order != BondOrder.Single) continue;
            if (bond.IsAromatic) continue;
            
            var atom1 = bond.Begin;
            var atom2 = bond.End;
            
            // Пропускаем связи с водородом
            if (atom1.Symbol == "H" || atom2.Symbol == "H") continue;
            
            // Пропускаем терминальные связи
            var neighbors1 = molecule.GetConnectedAtoms(atom1).Count(a => a.Symbol != "H");
            var neighbors2 = molecule.GetConnectedAtoms(atom2).Count(a => a.Symbol != "H");
            
            if (neighbors1 <= 1 || neighbors2 <= 1) continue;
            
            // Пропускаем связи с ароматическими атомами (кроме цепочек)
            if (atom1.IsAromatic && atom2.IsAromatic) continue;
            
            // Пропускаем связи типа C-OH, C-NH2 (малоподвижные)
            bool isTerminalFunctionalGroup = false;
            if ((atom1.Symbol == "O" || atom1.Symbol == "N") && neighbors1 == 1)
                isTerminalFunctionalGroup = true;
            if ((atom2.Symbol == "O" || atom2.Symbol == "N") && neighbors2 == 1)
                isTerminalFunctionalGroup = true;
                
            if (isTerminalFunctionalGroup) continue;
            
            count++;
        }
        return count;
    }

    private double EstimateLogP(IAtomContainer molecule, Dictionary<string, int> elementCounts)
    {
        // Упрощённая оценка липофильности (базовая формула Wildman-Crippen)
        double logP = 0;
        
        // Вклад атомов углерода
        logP += elementCounts.GetValueOrDefault("C", 0) * 0.45;
        
        // Вклад кислорода (сильно уменьшает липофильность)
        logP -= elementCounts.GetValueOrDefault("O", 0) * 1.2;
        
        // Вклад азота (уменьшает липофильность)
        logP -= elementCounts.GetValueOrDefault("N", 0) * 0.9;
        
        // Вклад галогенов (увеличивают липофильность)
        logP += elementCounts.GetValueOrDefault("Cl", 0) * 0.5;
        logP += elementCounts.GetValueOrDefault("Br", 0) * 0.7;
        logP += elementCounts.GetValueOrDefault("F", 0) * 0.1;
        logP += elementCounts.GetValueOrDefault("I", 0) * 0.9;
        
        // Коррекция для ароматических колец
        var aromaticAtoms = molecule.Atoms.Count(a => a.IsAromatic);
        if (aromaticAtoms >= 6)
            logP += 0.5; // Бензольное кольцо добавляет липофильность
        
        return logP;
    }

    private double EstimateBoilingPoint(double molecularMass, bool aromatic, int polarGroupCount)
    {
        // Базовая оценка
        double bp = molecularMass * 1.5 + (aromatic ? 40 : 0);
        
        // Множественные полярные группы значительно увеличивают риск разложения
        // и снижают реальную температуру разложения
        if (polarGroupCount >= 3)
        {
            bp *= 0.6; // Сильное снижение из-за нестабильности
        }
        else if (polarGroupCount >= 2)
        {
            bp *= 0.75; // Умеренное снижение
        }
        
        // Аминофенолы и катехолы особенно нестабильны
        if (molecularMass > 100 && polarGroupCount >= 2)
        {
            return Math.Min(bp, 200); // Обычно разлагаются ниже 200°C
        }
        
        return Math.Min(bp, 350);
    }

    private string EstimateWaterSolubility(double logP, List<string> functionalGroups)
    {
        // Подсчитываем все гидрофильные группы
        var polarGroups = functionalGroups.Count(g => 
            g.Contains("Гидроксил") || g.Contains("Фенол") || g.Contains("Катехол") ||
            g.Contains("Карбоксил") || g.Contains("Амин"));
        
        // Учитываем кратность (например "Фенол (×2)")
        int totalPolarGroups = polarGroups;
        foreach (var group in functionalGroups)
        {
            var match = System.Text.RegularExpressions.Regex.Match(group, @"×(\d+)");
            if (match.Success)
            {
                int count = int.Parse(match.Groups[1].Value);
                totalPolarGroups += (count - 1); // Добавляем дополнительные
            }
        }
        
        // Более точная оценка
        if (logP < -1 || totalPolarGroups >= 3)
            return "Хорошая (>10 г/100 мл)";
        else if (logP < 0.5 || totalPolarGroups >= 2)
            return "Умеренная (1-10 г/100 мл)";
        else if (logP < 2 || totalPolarGroups >= 1)
            return "Слабая (0.1-1 г/100 мл)";
        else
            return "Практически нерастворима (<0.1 г/100 мл)";
    }

    private string EstimatePolarity(List<string> functionalGroups, int aromaticAtoms, int heavyAtoms)
    {
        // Подсчитываем все полярные группы с учётом кратности
        var polarGroups = functionalGroups.Count(g => 
            g.Contains("Гидроксил") || g.Contains("Фенол") || g.Contains("Катехол") ||
            g.Contains("Карбоксил") || g.Contains("Амин") || 
            g.Contains("Карбонил") || g.Contains("Амид"));
        
        int totalPolarGroups = polarGroups;
        foreach (var group in functionalGroups)
        {
            var match = System.Text.RegularExpressions.Regex.Match(group, @"×(\d+)");
            if (match.Success)
            {
                int count = int.Parse(match.Groups[1].Value);
                totalPolarGroups += (count - 1);
            }
        }
        
        double polarityScore = (double)totalPolarGroups / heavyAtoms;
        
        // Более строгая оценка с учётом количества полярных групп
        if (polarityScore > 0.25 || totalPolarGroups >= 3)
            return "Сильнополярное";
        else if (polarityScore > 0.12 || totalPolarGroups >= 2)
            return "Умеренно полярное";
        else if (polarityScore > 0.04 || totalPolarGroups >= 1 || aromaticAtoms > 0)
            return "Слабополярное";
        else
            return "Неполярное";
    }

    private int CountHBondDonors(IAtomContainer molecule)
    {
        // OH и NH группы - считаем атомы O и N, которые связаны с H
        int count = 0;
        foreach (var atom in molecule.Atoms)
        {
            if (atom.Symbol == "O" || atom.Symbol == "N")
            {
                // Проверяем, есть ли связи с водородом
                var connectedAtoms = molecule.GetConnectedAtoms(atom);
                if (connectedAtoms.Any(a => a.Symbol == "H"))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private int CountHBondAcceptors(IAtomContainer molecule)
    {
        // O и N атомы (считаем только тяжёлые атомы)
        return molecule.Atoms.Count(a => a.Symbol == "O" || a.Symbol == "N");
    }

    private List<string> CheckLipinskiRule(double mass, double logP, int hDonors, int hAcceptors)
    {
        var results = new List<string>
        {
            $"  Молекулярная масса < 500: {(mass < 500 ? "[ДА]" : "[НЕТ]")} ({mass:F1} г/моль)",
            $"  LogP < 5: {(logP < 5 ? "[ДА]" : "[НЕТ]")} ({logP:F2})",
            $"  H-донор < 5: {(hDonors < 5 ? "[ДА]" : "[НЕТ]")} ({hDonors})",
            $"  H-акцептор < 10: {(hAcceptors < 10 ? "[ДА]" : "[НЕТ]")} ({hAcceptors})"
        };
        
        int violations = 0;
        if (mass >= 500) violations++;
        if (logP >= 5) violations++;
        if (hDonors >= 5) violations++;
        if (hAcceptors >= 10) violations++;
        
        results.Add("");
        if (violations == 0)
            results.Add("  ВЕРДИКТ: Лекарственноподобное соединение");
        else if (violations <= 1)
            results.Add($"  ВЕРДИКТ: Вероятно лекарственноподобное ({violations} нарушение)");
        else
            results.Add($"  ВЕРДИКТ: Не лекарственноподобное ({violations} нарушений)");
        
        return results;
    }
}

