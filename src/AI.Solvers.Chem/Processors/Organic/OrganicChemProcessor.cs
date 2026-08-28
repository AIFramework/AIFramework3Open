using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using FractalAgentsAI.Solvers.Chem.Models;
using FractalAgentsAI.Solvers.Chem.Processors.Organic.Graph;
using NCDK;
using NCDK.Aromaticities;
using NCDK.Default;
using NCDK.SMARTS;
using NCDK.Smiles;
using NCDK.Tools.Manipulator;
using System.Text;

namespace FractalAgentsAI.Solvers.Chem.Processors.Organic;

// ═══════════════════════════════════════════════════════════
// ОРГАНИЧЕСКАЯ ХИМИЯ
// ═══════════════════════════════════════════════════════════
public class OrganicChemProcessor
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;
    private SmilesParser _smilesParser;
    private SmilesGenerator _smilesGenerator;
    private SynthesisDatabaseManager _synthesisDb;
    private GraphRetrosynthesisSolver _graphSolver;

    public OrganicChemProcessor(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;

        // Инициализация NCDK
        _smilesParser = new SmilesParser(ChemObjectBuilder.Instance);
        _smilesGenerator = new SmilesGenerator(SmiFlavors.Absolute);
        
        // Инициализация базы данных синтезов
        _synthesisDb = new SynthesisDatabaseManager();
        
        // Инициализация графового решателя
        _graphSolver = new GraphRetrosynthesisSolver();
    }

    public ChemResult ParseSmiles(ParsedCommand cmd)
    {
        try
        {
            var smiles = cmd.Parameters["smiles"];
            var molecule = _smilesParser.ParseSmiles(smiles);

            // Добавляем неявные водороды
            AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);
            CDK.HydrogenAdder.AddImplicitHydrogens(molecule);

            var formula = MolecularFormulaManipulator.GetMolecularFormula(molecule);
            var formulaString = MolecularFormulaManipulator.GetString(formula);
            var mass = MolecularFormulaManipulator.GetMass(formula);

            var result = new StringBuilder();
            result.AppendLine($"SMILES: {smiles}");
            result.AppendLine($"Molecular Formula: {formulaString}");
            result.AppendLine($"Molar Mass: {mass:F2} g/mol");
            result.AppendLine($"Number of Atoms: {molecule.Atoms.Count}");
            result.AppendLine($"Number of Bonds: {molecule.Bonds.Count}");

            // Определение ароматичности
            Aromaticity.CDKLegacy.Apply(molecule);
            var aromaticAtoms = molecule.Atoms.Count(a => a.IsAromatic);
            if (aromaticAtoms > 0)
                result.AppendLine($"Aromatic atoms: {aromaticAtoms}");

            var chemResult = ChemResult.Ok(result.ToString());
            chemResult.Data["smiles"] = smiles;
            chemResult.Data["formula"] = formulaString;
            chemResult.Data["mass"] = mass;
            chemResult.Data["atom_count"] = molecule.Atoms.Count;

            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"SMILES parsing failed: {ex.Message}");
        }
    }

    public ChemResult GenerateSmiles(ParsedCommand cmd)
    {
        try
        {
            var name = cmd.Parameters["name"].ToLower().Trim();

            // 1. Попытка найти в базе данных
            var compound = _database.LookupCompound(name);
            if (compound != null && !string.IsNullOrEmpty(compound.SMILES))
            {
                var result = ChemResult.Ok($"SMILES: {compound.SMILES}");
                result.Data["smiles"] = compound.SMILES;
                result.Data["name"] = compound.CommonName;
                return result;
            }

            // 2. Встроенный словарь распространенных веществ
            var commonSmiles = new Dictionary<string, string>
            {
                ["methane"] = "C",
                ["ethane"] = "CC",
                ["propane"] = "CCC",
                ["butane"] = "CCCC",
                ["pentane"] = "CCCCC",
                ["hexane"] = "CCCCCC",
                ["heptane"] = "CCCCCCC",
                ["octane"] = "CCCCCCCC",
                
                ["benzene"] = "c1ccccc1",
                ["toluene"] = "Cc1ccccc1",
                ["phenol"] = "c1ccccc1O",
                ["aniline"] = "Nc1ccccc1",
                ["benzoic acid"] = "c1ccccc1C(=O)O",
                
                ["acetic acid"] = "CC(=O)O",
                ["ethanoic acid"] = "CC(=O)O",
                ["formic acid"] = "C(=O)O",
                ["methanoic acid"] = "C(=O)O",
                
                ["ethanol"] = "CCO",
                ["methanol"] = "CO",
                ["propanol"] = "CCCO",
                ["isopropanol"] = "CC(O)C",
                ["2-propanol"] = "CC(O)C",
                
                ["acetone"] = "CC(=O)C",
                ["formaldehyde"] = "C=O",
                ["acetaldehyde"] = "CC=O",
                
                ["aspirin"] = "CC(=O)Oc1ccccc1C(=O)O",
                ["caffeine"] = "Cn1cnc2c1c(=O)n(C)c(=O)n2C",
                
                // Изомеры (пример из запроса пользователя)
                ["2-methylbutanol"] = "CCC(C)CO",
                ["isobutane"] = "CC(C)C",
                ["neopentane"] = "CC(C)(C)C",
                ["isopentane"] = "CC(C)CC"
            };

            if (commonSmiles.ContainsKey(name))
            {
                var result = ChemResult.Ok($"SMILES: {commonSmiles[name]}");
                result.Data["smiles"] = commonSmiles[name];
                result.Data["name"] = name;
                return result;
            }

            return ChemResult.Error($"Compound '{name}' not found in database. Advanced structure generation requires IUPAC parsing library.");
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"SMILES generation failed: {ex.Message}");
        }
    }

    public ChemResult GenerateIsomers(ParsedCommand cmd)
    {
        try
        {
            var formula = cmd.Parameters["formula"];

            // Упрощенная генерация изомеров для малых молекул
            var molecular = new MolecularFormula(formula);

            // Расчет степени ненасыщенности
            var C = molecular.Elements.ContainsKey("C") ? molecular.Elements["C"] : 0;
            var H = molecular.Elements.ContainsKey("H") ? molecular.Elements["H"] : 0;
            var N = molecular.Elements.ContainsKey("N") ? molecular.Elements["N"] : 0;
            var X = molecular.Elements.Where(kvp =>
                kvp.Key == "Cl" || kvp.Key == "Br" || kvp.Key == "I" || kvp.Key == "F")
                .Sum(kvp => kvp.Value);

            var DBE = (2 * C + 2 + N - H - X) / 2.0;

            var result = new StringBuilder();
            result.AppendLine($"Molecular formula: {formula}");
            result.AppendLine($"Degree of unsaturation (DBE): {DBE}");
            result.AppendLine();

            // Для простых алканов
            if (DBE == 0 && N == 0 && X == 0)
            {
                result.AppendLine("Possible structural isomers (alkanes):");

                if (C == 4 && H == 10)
                {
                    result.AppendLine("1. n-Butane: CCCC");
                    result.AppendLine("2. Isobutane (2-methylpropane): CC(C)C");
                }
                else if (C == 5 && H == 12)
                {
                    result.AppendLine("1. n-Pentane: CCCCC");
                    result.AppendLine("2. Isopentane (2-methylbutane): CC(C)CC");
                    result.AppendLine("3. Neopentane (2,2-dimethylpropane): CC(C)(C)C");
                }
                else if (C == 6 && H == 14)
                {
                    result.AppendLine("1. n-Hexane: CCCCCC");
                    result.AppendLine("2. 2-Methylpentane: CC(C)CCC");
                    result.AppendLine("3. 3-Methylpentane: CCC(C)CC");
                    result.AppendLine("4. 2,2-Dimethylbutane: CC(C)(C)CC");
                    result.AppendLine("5. 2,3-Dimethylbutane: CC(C)C(C)C");
                }
                else
                {
                    result.AppendLine("(Generation of isomers for this formula not implemented)");
                }
            }
            else
            {
                result.AppendLine("Isomer generation for unsaturated/heteroatom compounds not yet implemented");
            }

            return ChemResult.Ok(result.ToString());
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Isomer generation failed: {ex.Message}");
        }
    }

    public ChemResult IdentifyFunctionalGroups(ParsedCommand cmd)
    {
        try
        {
            var smiles = cmd.Parameters.ContainsKey("smiles")
                ? cmd.Parameters["smiles"]
                : cmd.Parameters["reactant1"];

            var molecule = _smilesParser.ParseSmiles(smiles);
            AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);

            var groups = new List<string>();

            // Простое определение функциональных групп по SMARTS
            var patterns = new Dictionary<string, string>
            {
                ["Hydroxyl (Alcohol)"] = "[OX2H]",
                ["Carbonyl"] = "[CX3]=[OX1]",
                ["Carboxyl"] = "[CX3](=O)[OX2H1]",
                ["Ester"] = "[CX3](=[OX1])[OX2][#6]",
                ["Amine (Primary)"] = "[NX3;H2]",
                ["Amine (Secondary)"] = "[NX3;H1]",
                ["Amine (Tertiary)"] = "[NX3;H0]",
                ["Nitro"] = "[$([NX3](=O)=O),$([NX3+](=O)[O-])]",
                ["Aldehyde"] = "[CX3H1](=O)[#6]",
                ["Ketone"] = "[#6][CX3](=O)[#6]",
                ["Ether"] = "[OD2]([#6])[#6]",
                ["Aromatic"] = "c",
                ["Alkene"] = "[CX3]=[CX3]",
                ["Alkyne"] = "[CX2]#[CX2]"
            };

            foreach (var kvp in patterns)
            {
                try
                {
                    var pattern = SmartsPattern.Create(kvp.Value);
                    if (pattern.Matches(molecule))
                    {
                        groups.Add(kvp.Key);
                    }
                }
                catch
                {
                    // Игнорируем ошибки SMARTS
                }
            }

            var result = new StringBuilder();
            result.AppendLine($"Molecule: {smiles}");
            result.AppendLine("Functional groups found:");

            if (groups.Count > 0)
            {
                foreach (var group in groups)
                    result.AppendLine($"  - {group}");
            }
            else
            {
                result.AppendLine("  No functional groups detected (alkane)");
            }

            var chemResult = ChemResult.Ok(result.ToString());
            chemResult.Data["functional_groups"] = groups;

            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Functional group identification failed: {ex.Message}");
        }
    }

    public ChemResult PredictProduct(ParsedCommand cmd)
    {
        try
        {
            var reactantCount = int.Parse(cmd.Parameters["reactant_count"]);
            var reactant1 = cmd.Parameters["reactant1"];
            string reactant2 = reactantCount > 1 ? cmd.Parameters["reactant2"] : null;

            var result = new StringBuilder();
            result.AppendLine($"Reactants: {reactant1}" +
                (reactant2 != null ? $" + {reactant2}" : ""));

            // Простейшие правила предсказания

            // Присоединение HBr к алкену
            if (reactant1.Contains("=") && reactant2 == "HBr")
            {
                result.AppendLine("\nReaction Type: Electrophilic addition of HBr to alkene");
                result.AppendLine("Mechanism: Markovnikov addition (no peroxides specified)");
                result.AppendLine("\nProduct: Bromoalkane");
                result.AppendLine("H adds to less substituted carbon");
                result.AppendLine("Br adds to more substituted carbon");

                // Пример для пропена
                if (reactant1.Contains("CC=C") || reactant1.Contains("C=CC"))
                {
                    result.AppendLine("\nFor propene (CH₃-CH=CH₂):");
                    result.AppendLine("Product: 2-bromopropane (CH₃-CHBr-CH₃)");
                    result.AppendLine("SMILES: CC(Br)C");
                }
            }
            // Электрофильное замещение в бензоле
            else if ((reactant1.Contains("c1ccccc1") || reactant1 == "C6H6") &&
                     reactant2?.Contains("Br") == true)
            {
                result.AppendLine("\nReaction Type: Electrophilic aromatic substitution");
                result.AppendLine("Mechanism: Bromination of benzene");
                result.AppendLine("\nProduct: Bromobenzene");
                result.AppendLine("SMILES: Brc1ccccc1");
                result.AppendLine("\nConditions required: FeBr₃ catalyst");
            }
            // Дегидратация спирта
            else if (reactant1.Contains("CO") && reactant1.Contains("CC"))
            {
                result.AppendLine("\nPossible Reaction: Dehydration of alcohol");
                result.AppendLine("Conditions: H₂SO₄, heat");
                result.AppendLine("Product: Alkene + H₂O");
                result.AppendLine("Mechanism: E1 or E2 elimination");
            }
            else
            {
                result.AppendLine("\nReaction prediction not available for these reactants");
                result.AppendLine("Please specify reaction conditions or type");
            }

            return ChemResult.Ok(result.ToString());
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Product prediction failed: {ex.Message}");
        }
    }

    public ChemResult Retrosynthesis(ParsedCommand cmd)
    {
        try
        {
            // Извлечение параметров
            string? target = null;
            string? starting = null;

            // Парсинг команды типа "retrosynthesis aspirin from benzene"
            var originalCmd = cmd.OriginalCommand.ToLower();
            var fromIndex = originalCmd.IndexOf(" from ");
            
            if (fromIndex > 0)
            {
                target = originalCmd.Substring(originalCmd.IndexOf("retrosynthesis") + 14, 
                    fromIndex - originalCmd.IndexOf("retrosynthesis") - 14).Trim();
                starting = originalCmd.Substring(fromIndex + 6).Trim();
            }
            else if (cmd.Parameters.ContainsKey("target"))
            {
                target = cmd.Parameters["target"];
                starting = cmd.Parameters.ContainsKey("starting") ? cmd.Parameters["starting"] : null;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                return ChemResult.Error("No target compound specified");
            }

            var result = new StringBuilder();
            result.AppendLine("═══════════════════════════════════════════════════════════════");
            result.AppendLine($"  RETROSYNTHESIS: {target.ToUpper()}");
            if (!string.IsNullOrWhiteSpace(starting))
                result.AppendLine($"  Starting material: {starting}");
            result.AppendLine("═══════════════════════════════════════════════════════════════");
            result.AppendLine();

            // 1. Поиск в базе данных синтезов
            var compound = _synthesisDb.FindCompound(target);

            if (compound == null)
            {
                // 2. Попытка алгоритмического графового ретросинтеза (если target это SMILES)
                bool isSmiles = target.Contains("C") || target.Contains("c") || target.Contains("O") || target.Contains("N");
                
                if (isSmiles)
                {
                    try
                    {
                        result.AppendLine("ВНИМАНИЕ: Соединение не найдено в базе данных. Запуск ГРАФОВОГО РЕТРОСИНТЕЗА...");
                        result.AppendLine();

                        var molecule = _smilesParser.ParseSmiles(target);
                        AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(molecule);
                        CDK.HydrogenAdder.AddImplicitHydrogens(molecule);

                        // Запуск графового решателя
                        var tree = _graphSolver.Solve(molecule);

                        result.AppendLine("ДЕРЕВО РЕТРОСИНТЕЗА (метод дисконнекции):");
                        result.AppendLine("─────────────────────────────────────────────────────────────");
                        result.AppendLine(_graphSolver.FormatTree(tree));
                        result.AppendLine("─────────────────────────────────────────────────────────────");
                        result.AppendLine("Примечание: Это алгоритмическое предсказание на основе правил дисконнекции.");
                        
                        return ChemResult.Ok(result.ToString());
                    }
                    catch
                    {
                        // Если не удалось распарсить как SMILES, падаем ниже к сообщению об ошибке
                    }
                }

                result.AppendLine($"ВНИМАНИЕ: Соединение '{target}' не найдено в базе данных и не является валидным SMILES.");
                result.AppendLine();
                result.AppendLine("Доступные соединения в базе данных:");
                var available = _synthesisDb.GetAvailableCompounds();
                foreach (var comp in available.Take(10))
                {
                    result.AppendLine($"  - {comp}");
                }
                if (available.Count > 10)
                    result.AppendLine($"  ... and {available.Count - 10} more");
                
                return ChemResult.Ok(result.ToString());
            }

            // Показываем информацию о соединении из базы
            result.AppendLine($"TARGET: {compound.Name}");
            if (!string.IsNullOrEmpty(compound.IUPAC))
                result.AppendLine($"IUPAC: {compound.IUPAC}");
            result.AppendLine($"Formula: {compound.Formula}");
            result.AppendLine($"SMILES: {compound.SMILES}");
            if (!string.IsNullOrEmpty(compound.Description))
                result.AppendLine($"Description: {compound.Description}");
            result.AppendLine();

            // Поиск подходящего маршрута
            SynthesisRoute? route = null;
            
            if (!string.IsNullOrWhiteSpace(starting))
            {
                route = _synthesisDb.FindRoute(target, starting);
                if (route == null)
                {
                    result.AppendLine($"ВНИМАНИЕ: Маршрут от '{starting}' не найден");
                    result.AppendLine();
                    result.AppendLine($"Available routes for {compound.Name}:");
                    foreach (var r in compound.Routes)
                    {
                        result.AppendLine($"  - From {r.StartingMaterial} ({r.StepCount} steps, yield: {r.Yield})");
                    }
                    return ChemResult.Ok(result.ToString());
                }
            }
            else
            {
                // Берём первый доступный маршрут
                route = compound.Routes.FirstOrDefault();
                if (route == null)
                {
                    result.AppendLine("ВНИМАНИЕ: Маршруты синтеза для этого соединения отсутствуют");
                    return ChemResult.Ok(result.ToString());
                }
            }

            // Показываем найденный маршрут
            result.Append(FormatSynthesisRoute(route, compound));

            // Если есть альтернативные маршруты, показываем их
            var alternativeRoutes = compound.Routes.Where(r => r != route).ToList();
            if (alternativeRoutes.Any())
            {
                result.AppendLine();
                result.AppendLine("─────────────────────────────────────────────────────────────");
                result.AppendLine("ALTERNATIVE ROUTES:");
                result.AppendLine();
                foreach (var altRoute in alternativeRoutes)
                {
                    result.AppendLine($"• From {altRoute.StartingMaterial}:");
                    result.AppendLine($"  Steps: {altRoute.StepCount}, Yield: {altRoute.Yield}, " +
                        $"Type: {altRoute.RouteType}, Difficulty: {altRoute.Difficulty}");
                }
            }

            var chemResult = ChemResult.Ok(result.ToString());
            chemResult.Data["compound"] = compound;
            chemResult.Data["route"] = route;

            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Retrosynthesis planning failed: {ex.Message}");
        }
    }

    private string FormatSynthesisRoute(SynthesisRoute route, TargetCompound compound)
    {
        var result = new StringBuilder();
        
        result.AppendLine($"ROUTE FROM: {route.StartingMaterial.ToUpper()}");
        result.AppendLine($"Type: {route.RouteType}");
        result.AppendLine($"Difficulty: {route.Difficulty}");
        result.AppendLine($"Overall Yield: {route.Yield}");
        result.AppendLine($"Number of Steps: {route.StepCount}");
        result.AppendLine();

        if (!string.IsNullOrEmpty(route.Notes))
        {
            result.AppendLine($"Notes: {route.Notes}");
            result.AppendLine();
        }

        result.AppendLine("─────────────────────────────────────────────────────────────");
        result.AppendLine("SYNTHESIS STEPS:");
        result.AppendLine("─────────────────────────────────────────────────────────────");

        foreach (var step in route.Steps)
        {
            result.AppendLine();
            result.AppendLine($"Step {step.StepNumber}: {step.ReactionType.ToUpper()}");
            result.AppendLine($"  {step.Description}");
            result.AppendLine();
            result.AppendLine($"  Equation: {step.Equation}");
            
            if (step.Reagents.Any())
                result.AppendLine($"  Reagents: {string.Join(", ", step.Reagents)}");
            
            if (!string.IsNullOrEmpty(step.Catalyst))
                result.AppendLine($"  Catalyst: {step.Catalyst}");
            
            if (step.Conditions.Any())
                result.AppendLine($"  Conditions: {string.Join(", ", step.Conditions)}");
            
            if (!string.IsNullOrEmpty(step.Temperature))
                result.AppendLine($"  Temperature: {step.Temperature}");
            
            if (!string.IsNullOrEmpty(step.Pressure))
                result.AppendLine($"  Pressure: {step.Pressure}");
            
            if (!string.IsNullOrEmpty(step.Time))
                result.AppendLine($"  Time: {step.Time}");
            
            if (!string.IsNullOrEmpty(step.Yield))
                result.AppendLine($"  Yield: {step.Yield}");
            
            if (_verbosity >= VerbosityLevel.Detailed && !string.IsNullOrEmpty(step.Mechanism))
                result.AppendLine($"  Mechanism: {step.Mechanism}");
            
            if (step.Warnings.Any())
            {
                result.AppendLine($"  ⚠ Warnings: {string.Join("; ", step.Warnings)}");
            }
        }

        return result.ToString();
    }


    public ChemResult IUPACName(ParsedCommand cmd)
    {
        try
        {
            var smiles = cmd.Parameters.ContainsKey("smiles") 
                ? cmd.Parameters["smiles"] 
                : cmd.Parameters.ContainsKey("formula") 
                    ? cmd.Parameters["formula"] 
                    : null;

            if (smiles == null)
                return ChemResult.Error("No SMILES or formula provided");

            var result = new StringBuilder();
            result.AppendLine($"Input: {smiles}");
            result.AppendLine();

            // Простые правила именования
            var name = GetSimpleIUPACName(smiles);
            
            if (name != null)
            {
                result.AppendLine($"IUPAC Name: {name}");
                
                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    result.AppendLine();
                    result.AppendLine("NAMING STEPS:");
                    result.AppendLine("1. Identify longest carbon chain (parent)");
                    result.AppendLine("2. Number chain to give substituents lowest numbers");
                    result.AppendLine("3. Name and number substituents");
                    result.AppendLine("4. List substituents alphabetically");
                    result.AppendLine("5. Assign suffix based on functional group");
                }
            }
            else
            {
                result.AppendLine("⚠ IUPAC naming for complex structures requires");
                result.AppendLine("  advanced algorithm. Current implementation supports:");
                result.AppendLine("  - Simple alkanes (C1-C10)");
                result.AppendLine("  - Simple alcohols");
                result.AppendLine("  - Simple carboxylic acids");
                result.AppendLine();
                result.AppendLine("For complex structures, please use external tools like:");
                result.AppendLine("  - ChemDraw");
                result.AppendLine("  - PubChem");
                result.AppendLine("  - ChemSpider");
            }

            return ChemResult.Ok(result.ToString());
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"IUPAC naming failed: {ex.Message}");
        }
    }

    private string GetSimpleIUPACName(string smiles)
    {
        // Простые случаи
        var simple = new Dictionary<string, string>
        {
            ["C"] = "methane",
            ["CC"] = "ethane",
            ["CCC"] = "propane",
            ["CCCC"] = "butane",
            ["CCCCC"] = "pentane",
            ["CCCCCC"] = "hexane",
            ["CCCCCCC"] = "heptane",
            ["CCCCCCCC"] = "octane",
            ["CCCCCCCCC"] = "nonane",
            ["CCCCCCCCCC"] = "decane",
            
            ["CO"] = "methanol",
            ["CCO"] = "ethanol",
            ["CCCO"] = "propan-1-ol",
            ["CC(C)CO"] = "2-methylpropan-1-ol",
            ["CC(C)CCO"] = "3-methylbutan-1-ol",
            
            ["C(=O)O"] = "methanoic acid (formic acid)",
            ["CC(=O)O"] = "ethanoic acid (acetic acid)",
            ["CCC(=O)O"] = "propanoic acid",
            
            ["C=C"] = "ethene (ethylene)",
            ["CC=C"] = "propene",
            ["C#C"] = "ethyne (acetylene)",
            
            ["c1ccccc1"] = "benzene",
            ["Cc1ccccc1"] = "methylbenzene (toluene)",
            ["c1ccccc1O"] = "phenol"
        };

        if (simple.ContainsKey(smiles))
            return simple[smiles];

        // Анализ структуры для более сложных случаев
        try
        {
            var molecule = _smilesParser.ParseSmiles(smiles);
            var atomCount = molecule.Atoms.Count(a => a.Symbol == "C");
            
            // Простой алкан
            if (smiles.All(c => c == 'C') && atomCount > 0 && atomCount <= 20)
            {
                string[] prefixes = { "", "meth", "eth", "prop", "but", "pent", "hex", 
                    "hept", "oct", "non", "dec", "undec", "dodec", "tridec", "tetradec",
                    "pentadec", "hexadec", "heptadec", "octadec", "nonadec", "icos" };
                
                if (atomCount < prefixes.Length)
                    return prefixes[atomCount] + "ane";
            }
        }
        catch { }

        return null;
    }

    public void LoadReactionRules(string path)
    {
        // Загрузка пользовательских правил реакций
    }

    /// <summary>
    /// Загрузка пользовательской базы данных синтезов
    /// </summary>
    public void LoadCustomSynthesisDatabase(string jsonPath)
    {
        _synthesisDb = new SynthesisDatabaseManager(jsonPath);
    }

    /// <summary>
    /// Получить статистику базы данных синтезов
    /// </summary>
    public string GetSynthesisDatabaseStats()
    {
        return _synthesisDb.GetStatistics();
    }
}
