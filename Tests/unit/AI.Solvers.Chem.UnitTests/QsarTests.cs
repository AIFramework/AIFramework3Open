using AI.Solvers.Chem.Qsar;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Дескрипторы структуры и линейная модель свойства с проверкой области применимости.</summary>
public class QsarTests
{
    private static readonly string[] Alkanes = { "C", "CC", "CCC", "CCCC", "CCCCC", "CCCCCC" };

    /// <summary>Состав бензола и его топологические индексы известны точно.</summary>
    [Fact]
    public void Descriptors_Benzene()
    {
        DescriptorSet set = MolecularDescriptors.Compute("c1ccccc1");

        Assert.Equal(78.11, set["MolarMass"], 1);
        Assert.Equal(6, set["HeavyAtoms"], 9);
        Assert.Equal(6, set["Carbons"], 9);
        Assert.Equal(6, set["Hydrogens"], 9);
        Assert.Equal(1, set["Rings"], 9);
        Assert.Equal(1, set["AromaticRings"], 9);
        Assert.Equal(4, set["Unsaturation"], 9);
        Assert.Equal(0, set["Fsp3"], 9);

        // Индекс Винера цикла C6 равен 27, индекс Рандича 3, индекс Балабана 2
        Assert.Equal(27, set["Wiener"], 9);
        Assert.Equal(3, set["Randic"], 9);
        Assert.Equal(24, set["ZagrebM1"], 9);
        Assert.Equal(2.0, set["BalabanJ"], 6);
        Assert.Equal(3, set["Diameter"], 9);
    }

    /// <summary>Топологические индексы нормального бутана совпадают с табличными.</summary>
    [Fact]
    public void Descriptors_Butane()
    {
        DescriptorSet set = MolecularDescriptors.Compute("CCCC");

        Assert.Equal(10, set["Wiener"], 9);
        Assert.Equal(1.914214, set["Randic"], 6);
        // J = 3·(1/sqrt(24) + 1/4 + 1/sqrt(24)) при суммах расстояний 6, 4, 4, 6
        Assert.Equal(1.974745, set["BalabanJ"], 6);
        Assert.Equal(1, set["Fsp3"], 9);
        Assert.Equal(0, set["Rings"], 9);
        Assert.Equal(1, set["RotatableBonds"], 9);
    }

    /// <summary>Циклогексан отличается от бензола насыщенностью и отсутствием ароматики.</summary>
    [Fact]
    public void Descriptors_CyclohexaneDiffersFromBenzene()
    {
        DescriptorSet cyclohexane = MolecularDescriptors.Compute("C1CCCCC1");

        Assert.Equal(1, cyclohexane["Fsp3"], 9);
        Assert.Equal(0, cyclohexane["AromaticRings"], 9);
        Assert.Equal(1, cyclohexane["Rings"], 9);
        Assert.Equal(12, cyclohexane["Hydrogens"], 9);

        // Топология кольца та же, поэтому индекс Винера совпадает с бензольным
        Assert.Equal(27, cyclohexane["Wiener"], 9);
    }

    /// <summary>Доноры и акцепторы водородной связи считаются по правилу Липинского.</summary>
    [Fact]
    public void Descriptors_CountsHydrogenBondPartners()
    {
        DescriptorSet ethanol = MolecularDescriptors.Compute("CCO");
        DescriptorSet acetone = MolecularDescriptors.Compute("CC(=O)C");
        DescriptorSet glycine = MolecularDescriptors.Compute("NCC(=O)O");

        Assert.Equal(1, ethanol["HBondDonors"], 9);
        Assert.Equal(1, ethanol["HBondAcceptors"], 9);

        Assert.Equal(0, acetone["HBondDonors"], 9);
        Assert.Equal(1, acetone["HBondAcceptors"], 9);

        Assert.Equal(2, glycine["HBondDonors"], 9);
        Assert.Equal(3, glycine["HBondAcceptors"], 9);
    }

    /// <summary>Кратные связи и степень ненасыщенности.</summary>
    [Fact]
    public void Descriptors_CountsMultipleBonds()
    {
        DescriptorSet ethylene = MolecularDescriptors.Compute("C=C");
        DescriptorSet acetylene = MolecularDescriptors.Compute("C#C");

        Assert.Equal(1, ethylene["DoubleBonds"], 9);
        Assert.Equal(1, ethylene["Unsaturation"], 9);
        Assert.Equal(1, acetylene["TripleBonds"], 9);
        Assert.Equal(2, acetylene["Unsaturation"], 9);
    }

    /// <summary>Обращение к нерассчитываемому дескриптору отвергается.</summary>
    [Fact]
    public void Descriptors_RejectUnknownName()
    {
        DescriptorSet set = MolecularDescriptors.Compute("CCO");

        Assert.Throws<KeyNotFoundException>(() => set["LogP"]);
        Assert.Throws<ArgumentException>(() => MolecularDescriptors.Compute(" "));
    }

    /// <summary>Модель восстанавливает заложенную линейную зависимость свойства от числа атомов.</summary>
    [Fact]
    public void Model_RecoversLinearProperty()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();
        var options = new QsarOptions { Features = new[] { "HeavyAtoms" } };

        QsarModel model = QsarModel.Train(Alkanes, property, options);

        Assert.Equal(1.0, model.Quality.R2, 6);
        Assert.True(model.Quality.Rmse < 1e-6, $"ошибка обучения {model.Quality.Rmse:E2} слишком велика");
        Assert.Equal(15.0, model.Predict("CCCCCCC"), 4);
        Assert.Single(model.DescriptorNames);
    }

    /// <summary>Перекрёстная проверка на точной зависимости даёт Q2, близкий к единице.</summary>
    [Fact]
    public void Model_CrossValidationConfirmsExactModel()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();
        var options = new QsarOptions { Features = new[] { "HeavyAtoms", "Wiener" }, CrossValidationFolds = 3 };

        QsarModel model = QsarModel.Train(Alkanes, property, options);

        Assert.True(model.Quality.Q2 > 0.99, $"Q2 = {model.Quality.Q2:F4}");
        Assert.True(model.Quality.RmseCv < 1e-4);
    }

    /// <summary>Далёкая от обучающей выборки структура выходит из области применимости.</summary>
    [Fact]
    public void Model_DetectsStructureOutsideDomain()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();
        var options = new QsarOptions { Features = new[] { "HeavyAtoms" } };

        QsarModel model = QsarModel.Train(Alkanes, property, options);

        Assert.True(model.InDomain("CCCC"), "бутан лежит внутри обучающего диапазона");
        Assert.False(model.InDomain(new string('C', 20)), "двадцать атомов углерода - это экстраполяция");
        Assert.True(model.Leverage(MolecularDescriptors.Compute(new string('C', 20))) > model.LeverageThreshold);
    }

    /// <summary>Признаков не должно быть больше, чем структур: иначе модель описывает шум.</summary>
    [Fact]
    public void Model_RejectsOverfittedSetup()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();

        Assert.Throws<ArgumentException>(() => QsarModel.Train(Alkanes, property));

        Assert.Throws<ArgumentException>(() => QsarModel.Train(
            Alkanes.Take(3).ToArray(), property.Take(3).ToArray(),
            new QsarOptions { Features = new[] { "HeavyAtoms" } }));
    }

    /// <summary>Постоянный на выборке признак отбрасывается, неизвестный - отвергается.</summary>
    [Fact]
    public void Model_HandlesConstantAndUnknownFeatures()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();

        // Доля sp3-углерода у всех алканов равна единице и ничего не объясняет
        Assert.Throws<ArgumentException>(() => QsarModel.Train(Alkanes, property,
            new QsarOptions { Features = new[] { "Fsp3" } }));

        Assert.Throws<ArgumentException>(() => QsarModel.Train(Alkanes, property,
            new QsarOptions { Features = new[] { "LogP" } }));
    }

    /// <summary>Отчёт по модели перечисляет признаки и показатели качества.</summary>
    [Fact]
    public void Model_ReportMentionsQuality()
    {
        var property = Alkanes.Select(s => (2.0 * s.Length) + 1).ToArray();
        QsarModel model = QsarModel.Train(Alkanes, property,
            new QsarOptions { Features = new[] { "HeavyAtoms" } });

        string report = model.Report();

        Assert.Contains("HeavyAtoms", report);
        Assert.Contains("R2", report);
        Assert.Contains("рычага", report);
    }
}
