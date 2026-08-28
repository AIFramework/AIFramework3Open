using FractalAgentsAI.Solvers.Chem.Core;
using NCDK;
using NCDK.Default;
using NCDK.Graphs;
using NCDK.SMARTS;
using NCDK.Smiles;
using NCDK.Tools.Manipulator;
using System.Text;

namespace FractalAgentsAI.Solvers.Chem.Processors.Organic.Graph;

/// <summary>
/// Узел в дереве ретросинтеза
/// </summary>
public class RetroNode
{
    public IAtomContainer Molecule { get; set; }
    public string SMILES { get; set; }
    public List<RetroNode> Children { get; set; } = new();
    public string TransformationName { get; set; }
    public int Depth { get; set; }
    public bool IsSimple { get; set; } // Является ли простым прекурсором

    public RetroNode(IAtomContainer mol, string transform, int depth)
    {
        Molecule = mol;
        TransformationName = transform;
        Depth = depth;
        
        // Генерация SMILES без явных водородов
        var sg = new SmilesGenerator(SmiFlavors.Canonical);
        try 
        { 
            // Удаляем явные атомы водорода для красивого вывода
            var molClone = (IAtomContainer)mol.Clone();
            AtomContainerManipulator.SuppressHydrogens(molClone);
            SMILES = sg.Create(molClone); 
        } 
        catch { SMILES = "Ошибка"; }

        // Критерий простоты: мало тяжёлых атомов (без водорода)
        var heavyAtomCount = mol.Atoms.Count(a => a.Symbol != "H");
        IsSimple = heavyAtomCount <= 3; // Только очень маленькие молекулы
    }
}

/// <summary>
/// Интерфейс правила ретросинтетической дисконнекции
/// </summary>
public interface IRetroRule
{
    string Name { get; }
    string PatternString { get; } // Переименовал во избежание конфликта
    /// <summary>
    /// Пытается применить правило к молекуле. Возвращает список прекурсоров (фрагментов).
    /// </summary>
    List<IAtomContainer>? Apply(IAtomContainer target);
}

/// <summary>
/// Правило гидролиза сложного эфира: R-C(=O)-O-R' => R-C(=O)OH + R'-OH
/// </summary>
public class EsterHydrolysisRule : IRetroRule
{
    public string Name => "Гидролиз эфира";
    // Паттерн сложного эфира: C(=O)-O-C
    public string PatternString => "[CX3](=[OX1])[OX2][#6]";

    public List<IAtomContainer>? Apply(IAtomContainer target)
    {
        // Используем полное имя класса NCDK.SMARTS.SmartsPattern
        var pattern = NCDK.SMARTS.SmartsPattern.Create(PatternString);
        
        if (!pattern.Matches(target)) return null;

        // Берем первое совпадение
        var matches = pattern.MatchAll(target);
        if (!matches.Any()) return null;
        
        var mapping = matches.First();
        
        // В паттерне [CX3](=[OX1])[OX2][#6]:
        // Индекс 0: Карбонильный углерод
        // Индекс 2: Эфирный кислород
        
        var carbonylCIndex = mapping[0];
        var etherOIndex = mapping[2];
        
        var carbonylC = target.Atoms[carbonylCIndex];
        var etherO = target.Atoms[etherOIndex];
        
        // Клонируем молекулу
        var precursor = (IAtomContainer)target.Clone();
        // Используем те же индексы для клона
        var cAtom = precursor.Atoms[carbonylCIndex];
        var oAtom = precursor.Atoms[etherOIndex];

        // Находим и удаляем связь
        var bond = precursor.GetBond(cAtom, oAtom);
        if (bond == null) return null;
        
        precursor.Bonds.Remove(bond);

        // Добавляем группу OH к Карбонилу
        var builder = ChemObjectBuilder.Instance;
        var oxygenAcid = builder.NewAtom("O");
        var hydrogenAcid = builder.NewAtom("H");
        precursor.Atoms.Add(oxygenAcid);
        precursor.Atoms.Add(hydrogenAcid);
        // Создаём связи и устанавливаем порядок
        var bond1 = builder.NewBond(cAtom, oxygenAcid, BondOrder.Single);
        var bond2 = builder.NewBond(oxygenAcid, hydrogenAcid, BondOrder.Single);
        precursor.Bonds.Add(bond1);
        precursor.Bonds.Add(bond2);

        // Добавляем H к эфирному кислороду
        var hydrogenAlcohol = builder.NewAtom("H");
        precursor.Atoms.Add(hydrogenAlcohol);
        var bond3 = builder.NewBond(oAtom, hydrogenAlcohol, BondOrder.Single);
        precursor.Bonds.Add(bond3);

        // Разделяем на молекулы
        var parts = ConnectivityChecker.PartitionIntoMolecules(precursor);
        return parts.ToList();
    }
}

/// <summary>
/// Правило гидролиза амида: R-C(=O)-N-R' => R-C(=O)OH + R'-NH2
/// </summary>
public class AmideHydrolysisRule : IRetroRule
{
    public string Name => "Гидролиз амида";
    public string PatternString => "[CX3](=[OX1])[NX3][#6]";

    public List<IAtomContainer>? Apply(IAtomContainer target)
    {
        var pattern = NCDK.SMARTS.SmartsPattern.Create(PatternString);
        
        if (!pattern.Matches(target)) return null;
        
        var matches = pattern.MatchAll(target);
        if (!matches.Any()) return null;
        
        var mapping = matches.First();
        
        var carbonylCIndex = mapping[0];
        var nitrogenIndex = mapping[2];
        
        var carbonylC = target.Atoms[carbonylCIndex];
        var nitrogen = target.Atoms[nitrogenIndex];

        var precursor = (IAtomContainer)target.Clone();
        var cAtom = precursor.Atoms[carbonylCIndex];
        var nAtom = precursor.Atoms[nitrogenIndex];

        var bond = precursor.GetBond(cAtom, nAtom);
        if (bond == null) return null;
        precursor.Bonds.Remove(bond);

        // R-C(=O) -> R-C(=O)OH
        var builder = ChemObjectBuilder.Instance;
        var oxygenAcid = builder.NewAtom("O");
        var hydrogenAcid = builder.NewAtom("H");
        precursor.Atoms.Add(oxygenAcid);
        precursor.Atoms.Add(hydrogenAcid);
        var bond1 = builder.NewBond(cAtom, oxygenAcid, BondOrder.Single);
        var bond2 = builder.NewBond(oxygenAcid, hydrogenAcid, BondOrder.Single);
        precursor.Bonds.Add(bond1);
        precursor.Bonds.Add(bond2);

        // ...-N-R -> ...-N-H
        var hydrogenAmine = builder.NewAtom("H");
        precursor.Atoms.Add(hydrogenAmine);
        var bond3 = builder.NewBond(nAtom, hydrogenAmine, BondOrder.Single);
        precursor.Bonds.Add(bond3);

        var parts = ConnectivityChecker.PartitionIntoMolecules(precursor);
        return parts.ToList();
    }
}

/// <summary>
/// Правило разрыва C-N связи (Восстановительное аминирование): R-CH2-NH2 => R-CHO + NH3
/// </summary>
public class AlkylAmineCleavageRule : IRetroRule
{
    public string Name => "Разрыв C-N (восстановительное аминирование)";
    public string PatternString => "[CX4][NX3;H2,H1,H0]"; // Алкил-амин

    public List<IAtomContainer>? Apply(IAtomContainer target)
    {
        var pattern = NCDK.SMARTS.SmartsPattern.Create(PatternString);
        
        if (!pattern.Matches(target)) return null;
        
        var matches = pattern.MatchAll(target);
        if (!matches.Any()) return null;
        
        foreach(var mapping in matches)
        {
            var carbonIndex = mapping[0];
            var nitrogenIndex = mapping[1];
            
            var carbon = target.Atoms[carbonIndex];
            var nitrogen = target.Atoms[nitrogenIndex];
            
            // Пропускаем амиды (уже обработаны другим правилом)
            var neighbors = target.GetConnectedAtoms(carbon);
            bool isAmide = false;
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Symbol == "O" && target.GetBond(carbon, neighbor)?.Order == BondOrder.Double)
                {
                    isAmide = true;
                    break;
                }
            }
            if (isAmide) continue;
            
            var bondCheck = target.GetBond(carbon, nitrogen);
            if (bondCheck == null) continue;

            var precursor = (IAtomContainer)target.Clone();
            var c = precursor.Atoms[carbonIndex];
            var n = precursor.Atoms[nitrogenIndex];
            var bond = precursor.GetBond(c, n);
            
            precursor.Bonds.Remove(bond);

            var builder = ChemObjectBuilder.Instance;
            
            // C-... → C=O (карбонильная группа)
            var oxygen = builder.NewAtom("O");
            precursor.Atoms.Add(oxygen);
            var bondCO = builder.NewBond(c, oxygen, BondOrder.Double);
            precursor.Bonds.Add(bondCO);

            // -N → -NH (добавляем H к азоту)
            var hydrogen = builder.NewAtom("H");
            precursor.Atoms.Add(hydrogen);
            var bondNH = builder.NewBond(n, hydrogen, BondOrder.Single);
            precursor.Bonds.Add(bondNH);

            var parts = ConnectivityChecker.PartitionIntoMolecules(precursor);
            return parts.ToList();
        }
        return null;
    }
}

/// <summary>
/// Правило разрыва C-C связи у ароматики: Ar-CH3 => Ar-H + "CH3" (упрощённо)
/// Применимо только для простых алкильных заместителей (метил, этил)
/// </summary>
public class AromaticAlkylCleavageRule : IRetroRule
{
    public string Name => "Отщепление алкильной цепи от ароматики";
    public string PatternString => "c[CX4H3,CX4H2]"; // Ароматика с метил/метилен группой

    public List<IAtomContainer>? Apply(IAtomContainer target)
    {
        var pattern = NCDK.SMARTS.SmartsPattern.Create(PatternString);
        
        if (!pattern.Matches(target)) return null;
        
        var matches = pattern.MatchAll(target);
        if (!matches.Any()) return null;
        
        foreach(var mapping in matches)
        {
            var aromaticCIndex = mapping[0];
            var alkylCIndex = mapping[1];
            
            var aromaticC = target.Atoms[aromaticCIndex];
            var alkylC = target.Atoms[alkylCIndex];
            
            // Проверяем, что это действительно простая группа
            var alkylNeighbors = target.GetConnectedAtoms(alkylC).Count();
            if (alkylNeighbors > 2) continue; // Пропускаем сложные случаи
            
            var bondCheck = target.GetBond(aromaticC, alkylC);
            if (bondCheck == null) continue;

            var precursor = (IAtomContainer)target.Clone();
            var arC = precursor.Atoms[aromaticCIndex];
            var alC = precursor.Atoms[alkylCIndex];
            var bond = precursor.GetBond(arC, alC);
            
            precursor.Bonds.Remove(bond);

            var builder = ChemObjectBuilder.Instance;
            
            // Ar-... → Ar-H
            var h1 = builder.NewAtom("H");
            precursor.Atoms.Add(h1);
            var bondArH = builder.NewBond(arC, h1, BondOrder.Single);
            precursor.Bonds.Add(bondArH);

            // Для алкильной части просто оставляем как есть
            // (она станет отдельным фрагментом)

            var parts = ConnectivityChecker.PartitionIntoMolecules(precursor);
            return parts.ToList();
        }
        return null;
    }
}

/// <summary>
/// Правило расщепления алкена (Озонолиз): R=R' => R=O + O=R'
/// </summary>
public class AlkeneCleavageRule : IRetroRule
{
    public string Name => "Расщепление алкена (озонолиз)";
    public string PatternString => "[CX3]=[CX3]";

    public List<IAtomContainer>? Apply(IAtomContainer target)
    {
        var pattern = NCDK.SMARTS.SmartsPattern.Create(PatternString);
        
        if (!pattern.Matches(target)) return null;
        
        var matches = pattern.MatchAll(target);
        if (!matches.Any()) return null;
        
        foreach(var mapping in matches)
        {
            var atom1Index = mapping[0];
            var atom2Index = mapping[1];
            
            var a1 = target.Atoms[atom1Index];
            var a2 = target.Atoms[atom2Index];
            var bondCheck = target.GetBond(a1, a2);
            if (bondCheck.Order != BondOrder.Double) continue;

            var precursor = (IAtomContainer)target.Clone();
            var c1 = precursor.Atoms[atom1Index];
            var c2 = precursor.Atoms[atom2Index];
            var bond = precursor.GetBond(c1, c2);
            
            precursor.Bonds.Remove(bond);

            // C= -> C=O
            var builder = ChemObjectBuilder.Instance;
            var o1 = builder.NewAtom("O");
            precursor.Atoms.Add(o1);
            var bond1 = builder.NewBond(c1, o1, BondOrder.Double);
            precursor.Bonds.Add(bond1);

            // =C -> O=C
            var o2 = builder.NewAtom("O");
            precursor.Atoms.Add(o2);
            var bond2 = builder.NewBond(c2, o2, BondOrder.Double);
            precursor.Bonds.Add(bond2);

            var parts = ConnectivityChecker.PartitionIntoMolecules(precursor);
            return parts.ToList();
        }
        return null;
    }
}

/// <summary>
/// Основной движок графового ретросинтеза
/// </summary>
public class GraphRetrosynthesisSolver
{
    private readonly List<IRetroRule> _rules;
    
    public GraphRetrosynthesisSolver()
    {
        _rules = new List<IRetroRule>
        {
            new EsterHydrolysisRule(),
            new AmideHydrolysisRule(),
            new AlkeneCleavageRule(),
            new AlkylAmineCleavageRule(),
            new AromaticAlkylCleavageRule()
        };
    }

    public RetroNode Solve(IAtomContainer target, int maxDepth = 3)
    {
        var root = new RetroNode(target, "Целевая молекула", 0);
        Expand(root, maxDepth);
        return root;
    }

    private void Expand(RetroNode node, int maxDepth)
    {
        if (node.Depth >= maxDepth) return;
        // Не останавливаемся на "простых" - пусть попробует разрезать

        bool appliedAnyRule = false;
        
        foreach (var rule in _rules)
        {
            try
            {
                var precursors = rule.Apply(node.Molecule);

                if (precursors != null && precursors.Count > 0)
                {
                    appliedAnyRule = true;
                    
                    foreach (var precMol in precursors)
                    {
                        AtomContainerManipulator.PercieveAtomTypesAndConfigureAtoms(precMol);
                        
                        var childNode = new RetroNode(precMol, rule.Name, node.Depth + 1);
                        node.Children.Add(childNode);
                        
                        // Рекурсивно только если не достигли простоты
                        if (!childNode.IsSimple)
                        {
                            Expand(childNode, maxDepth);
                        }
                    }
                    if (node.Children.Count > 0) break; 
                }
            }
            catch
            {
                // Игнорируем ошибки применения правил для стабильности
            }
        }
        
        // Если не применили ни одного правила - это тоже "простой" прекурсор
        if (!appliedAnyRule && node.Children.Count == 0)
        {
            node.IsSimple = true;
        }
    }

    public string FormatTree(RetroNode node, string indent = "")
    {
        var sb = new StringBuilder();
        var simpleMarker = node.IsSimple ? " [Простой прекурсор]" : "";
        sb.AppendLine($"{indent}└── [{node.SMILES}] ({node.TransformationName}){simpleMarker}");
        
        foreach (var child in node.Children)
        {
            sb.Append(FormatTree(child, indent + "    "));
        }
        return sb.ToString();
    }
}
