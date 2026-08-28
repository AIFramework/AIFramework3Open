using AI.Solvers.Chem.Parsing;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Определение типа команды и извлечение параметров.</summary>
public class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Theory]
    [InlineData("balance Cu + HNO3 = Cu(NO3)2 + NO + H2O", CommandType.Balance)]
    [InlineData("Cu + HNO3 = Cu(NO3)2 + NO + H2O", CommandType.Balance)]
    [InlineData("pH of 0.01M HCl", CommandType.PhCalculation)]
    [InlineData("solubility of AgCl", CommandType.Solubility)]
    [InlineData("common ion compound=AgCl ion=Cl concentration=0.1M", CommandType.SolubilityCommonIon)]
    [InlineData("fractional precipitation compound1=AgCl compound2=AgBr anion=0.001", CommandType.FractionalPrecipitation)]
    [InlineData("stepwise complex metal=Ag ligand=NH3 [ligand]=0.1M", CommandType.StepwiseComplexation)]
    [InlineData("integrated rate law order=1 A0=1.0 k=0.05 time=10", CommandType.IntegratedRateLaw)]
    [InlineData("half-life k=0.05 order=1", CommandType.HalfLife)]
    [InlineData("mixture analysis A1=0.5 A2=0.7", CommandType.MixtureAnalysis)]
    [InlineData("blood gas pH=7.25 pCO2=55mmHg", CommandType.BloodGasAnalysis)]
    [InlineData("Faraday I=2 time=3600 substance=Cu", CommandType.FaradayLaw)]
    [InlineData("analyze CCO", CommandType.Properties)]
    [InlineData("properties of element Fe", CommandType.ElementInfo)]
    public void CommandType_IsDetected(string command, CommandType expected)
        => Assert.Equal(expected, _parser.Parse(command).CommandType);

    /// <summary>
    /// Ключевые слова ищутся по границам слов, иначе «phenol» опознаётся как команда «pH».
    /// </summary>
    [Theory]
    [InlineData("lookup phenol", CommandType.CompoundLookup)]
    [InlineData("retrosynthesis phenol", CommandType.Retrosynthesis)]
    public void ShortKeywords_DoNotMatchInsideWords(string command, CommandType expected)
        => Assert.Equal(expected, _parser.Parse(command).CommandType);

    /// <summary>Тип уточняется по фактически заданным параметрам.</summary>
    [Theory]
    [InlineData("solubility of AgCl ion=Cl concentration=0.1", CommandType.SolubilityCommonIon)]
    [InlineData("complex metal=Ca ligand=EDTA pH=10 [metal]=0.01M [EDTA]=0.01M", CommandType.ComplexationAtPH)]
    [InlineData("calculate buffer capacity pKa=4.76 acid=0.1M base=0.1M", CommandType.BufferPH)]
    public void CommandType_IsRefinedByParameters(string command, CommandType expected)
        => Assert.Equal(expected, _parser.Parse(command).CommandType);

    /// <summary>
    /// Концентрация в скобках хранится отдельным ключом и не затирает название вещества.
    /// </summary>
    [Fact]
    public void BracketKeys_DoNotOverwriteNames()
    {
        ParsedCommand command = _parser.Parse("complex metal=Cu ligand=NH3 [metal]=0.1M [ligand]=1.0M");

        Assert.Equal("Cu", command.Parameters["metal"]);
        Assert.Equal("0.1", command.Parameters["[metal]"]);
        Assert.Equal("NH3", command.Parameters["ligand"]);
        Assert.Equal("1.0", command.Parameters["[ligand]"]);
    }

    /// <summary>Единицы измерения отделяются от числа.</summary>
    [Fact]
    public void Units_AreSeparatedFromValues()
    {
        ParsedCommand command = _parser.Parse("blood gas pH=7.25 pCO2=55mmHg HCO3=24mEq/L");

        Assert.Equal("7.25", command.Parameters["pH"]);
        Assert.Equal("55", command.Parameters["pCO2"]);
        Assert.Equal("mmHg", command.Parameters["pCO2_unit"]);
    }

    /// <summary>Соседний параметр не должен попадать в единицы измерения предыдущего.</summary>
    [Fact]
    public void GasLawParameters_DoNotSwallowNeighbours()
    {
        ParsedCommand command = _parser.Parse("ideal gas P=2 V=10 T=300 find n");

        Assert.Equal("2", command.Parameters["P"]);
        Assert.Equal("10", command.Parameters["V"]);
        Assert.Equal("300", command.Parameters["T"]);
        Assert.Equal("N", command.Parameters["find"]);
    }

    /// <summary>Формула не обрезается на первой прописной букве.</summary>
    [Theory]
    [InlineData("molar mass of H2SO4", "formula", "H2SO4")]
    [InlineData("molar mass of CuSO4·5H2O", "formula", "CuSO4·5H2O")]
    [InlineData("isomers of C4H10", "formula", "C4H10")]
    public void FormulaParameter_IsCaptured(string command, string key, string expected)
        => Assert.Equal(expected, _parser.Parse(command).Parameters[key]);

    /// <summary>Регистр SMILES сохраняется: «C» и «c» - разные атомы.</summary>
    [Fact]
    public void Retrosynthesis_KeepsCase()
    {
        ParsedCommand command = _parser.Parse("retrosynthesis CC(=O)Oc1ccccc1C(=O)O from benzene");

        Assert.Equal("CC(=O)Oc1ccccc1C(=O)O", command.Parameters["target"]);
        Assert.Equal("benzene", command.Parameters["starting"]);
    }

    [Fact]
    public void UnknownCommand_IsRejected()
    {
        ParsedCommand command = _parser.Parse("сделай красиво");

        Assert.False(command.Success);
        Assert.Contains("Unknown", command.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Числа читаются инвариантной культурой независимо от локали машины.</summary>
    [Fact]
    public void Numbers_AreCultureInvariant()
    {
        ParsedCommand command = _parser.Parse("buffer pKa=4.76 acid=0.1M base=0.15M");

        Assert.Equal(4.76, command.GetDouble("pKa"), 1e-12);
        Assert.Equal(0.1, command.GetDouble("acid"), 1e-12);
    }

    /// <summary>Алиасы имён: документированный синтаксис и внутреннее имя - одно и то же.</summary>
    [Fact]
    public void Aliases_AreResolved()
    {
        ParsedCommand command = _parser.Parse("titration acid=0.1M base=0.1M V_acid=25ml");

        Assert.Equal(0.1, command.GetDouble("Ca", "acid"), 1e-12);
        Assert.Equal(25, command.GetDouble("Va", "V_acid"), 1e-12);
        Assert.True(command.Has("V_acid"));
        Assert.False(command.Has("V_base"));
    }

    [Fact]
    public void MissingParameter_NamesExpectedAliases()
    {
        ParsedCommand command = _parser.Parse("titration acid=0.1M base=0.1M V_acid=25ml");
        var error = Assert.Throws<MissingParameterException>(() => command.GetDouble("Vb", "V_base"));

        Assert.Contains("V_base", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arrays_AreParsed()
    {
        ParsedCommand command = _parser.Parse("calibration concentrations=1,2,3,4,5 absorbance=0.1,0.2,0.3,0.4,0.5");

        Assert.Equal(new[] { 1.0, 2, 3, 4, 5 }, command.GetArray("concentrations"));
        Assert.Equal(5, command.GetArray("absorbance").Length);
    }
}
