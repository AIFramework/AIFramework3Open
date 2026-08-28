using AI.Solvers.Chem.Safety;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Классификация смесей по СГС и паспорт безопасности.</summary>
public class SafetyTests
{
    private static MixtureComponent Component(string name, double percent, params string[] hazards)
        => new()
        {
            Name = name,
            ContentPercent = percent,
            Classifications = hazards.Select(h =>
            {
                Assert.True(HazardCatalog.TryParse(h, out HazardCategory category), $"не разобрана запись '{h}'");
                return category;
            }).ToList()
        };

    private static Mixture Mix(params MixtureComponent[] components)
    {
        var mixture = new Mixture { Name = "смесь" };

        foreach (MixtureComponent component in components)
            mixture.Add(component);

        return mixture;
    }

    [Theory]
    [InlineData("Skin Corr. 1B", HazardClass.SkinCorrosion, "1B")]
    [InlineData("Eye Dam. 1", HazardClass.EyeDamage, "1")]
    [InlineData("Flam. Liq. 2", HazardClass.FlammableLiquid, "2")]
    [InlineData("STOT SE 3", HazardClass.StotSingle, "3")]
    [InlineData("Aquatic Chronic 2", HazardClass.AquaticChronic, "2")]
    [InlineData("Carc. 1B", HazardClass.Carcinogenicity, "1B")]
    [InlineData("Acute Tox. 4 (oral)", HazardClass.AcuteToxicityOral, "4")]
    [InlineData("Acute Tox. 3 (dermal)", HazardClass.AcuteToxicityDermal, "3")]
    public void Notation_IsParsed(string text, HazardClass expectedClass, string expectedCategory)
    {
        Assert.True(HazardCatalog.TryParse(text, out HazardCategory category));
        Assert.Equal(expectedClass, category.Class);
        Assert.Equal(expectedCategory, category.Category);
    }

    [Theory]
    [InlineData("Skin Corr")]
    [InlineData("Что-то опасное 1")]
    [InlineData("")]
    public void UnknownNotation_IsRejected(string text)
        => Assert.False(HazardCatalog.TryParse(text, out _));

    [Fact]
    public void Catalog_LinksCategoryToStatementAndPictogram()
    {
        HazardEntry entry = HazardCatalog.Entry(new HazardCategory(HazardClass.SkinCorrosion, "1A"));

        Assert.Equal("H314", entry.Statement);
        Assert.Equal(Pictogram.Ghs05Corrosion, entry.Pictogram);
        Assert.Equal(SignalWord.Danger, entry.Signal);
        Assert.Contains("ожоги", HazardCatalog.HazardText("H314"), StringComparison.Ordinal);
    }

    /// <summary>Разъедающие компоненты от 5% переводят смесь в тот же класс.</summary>
    [Fact]
    public void SkinCorrosion_AboveFivePercent()
    {
        MixtureClassification result = Mix(Component("щёлочь", 6, "Skin Corr. 1B")).Classify();

        Assert.Contains(new HazardCategory(HazardClass.SkinCorrosion, "1"), result.Hazards);
        Assert.Contains("H314", result.HazardStatements);
        Assert.Equal(SignalWord.Danger, result.Signal);
    }

    /// <summary>От 1% до 5% разъедающего компонента смесь только раздражает кожу.</summary>
    [Fact]
    public void SkinCorrosion_BetweenOneAndFivePercent()
    {
        MixtureClassification result = Mix(Component("щёлочь", 3, "Skin Corr. 1B")).Classify();

        Assert.Contains(new HazardCategory(HazardClass.SkinIrritation, "2"), result.Hazards);
        Assert.DoesNotContain(new HazardCategory(HazardClass.SkinCorrosion, "1"), result.Hazards);

        // При этом 3% разъедающего уже дают серьёзное повреждение глаз
        Assert.Contains(new HazardCategory(HazardClass.EyeDamage, "1"), result.Hazards);
    }

    /// <summary>Правило аддитивности: 10 x разъедающие + раздражающие не менее 10%.</summary>
    [Fact]
    public void SkinIrritation_AdditivityRule()
    {
        MixtureClassification result = Mix(
            Component("разъедающий", 0.5, "Skin Corr. 1B"),
            Component("раздражающий", 6, "Skin Irrit. 2")).Classify();

        Assert.Contains(new HazardCategory(HazardClass.SkinIrritation, "2"), result.Hazards);
        Assert.Contains(result.Reasons, r => r.Rule.Contains("аддитивность", StringComparison.Ordinal));
    }

    [Fact]
    public void SmallAmountOfIrritant_IsNotClassified()
    {
        MixtureClassification result = Mix(Component("раздражающий", 2, "Skin Irrit. 2")).Classify();

        Assert.False(result.IsHazardous);
    }

    [Theory]
    [InlineData(0.2, true)]
    [InlineData(0.05, false)]
    public void Carcinogen_HasGenericLimitOfOneTenthPercent(double content, bool expected)
    {
        MixtureClassification result = Mix(Component("канцероген", content, "Carc. 1B")).Classify();

        Assert.Equal(expected, result.Hazards.Any(h => h.Class == HazardClass.Carcinogenicity));

        if (expected)
            Assert.Contains("H350", result.HazardStatements);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.2, false)]
    public void ReproductiveToxicant_HasLimitOfThreeTenths(double content, bool expected)
    {
        MixtureClassification result = Mix(Component("вещество", content, "Repr. 1B")).Classify();

        Assert.Equal(expected, result.Hazards.Any(h => h.Class == HazardClass.ReproductiveToxicity));
    }

    /// <summary>ATE смеси: 100/ATE = сумма долей, делённых на ATE компонентов.</summary>
    [Fact]
    public void AcuteToxicity_UsesAdditiveFormula()
    {
        var component = new MixtureComponent
        {
            Name = "токсикант",
            ContentPercent = 50,
            AcuteToxicityEstimates = new Dictionary<ExposureRoute, double> { [ExposureRoute.Oral] = 500 }
        };

        Mixture mixture = Mix(component);

        Assert.Equal(1000, MixtureClassifier.AcuteToxicityEstimate(mixture, ExposureRoute.Oral) ?? 0, 1e-9);

        MixtureClassification result = mixture.Classify();
        Assert.Contains(new HazardCategory(HazardClass.AcuteToxicityOral, "4"), result.Hazards);
        Assert.Contains("H302", result.HazardStatements);
    }

    /// <summary>Если численной ATE нет, берётся пересчёт из категории компонента.</summary>
    [Fact]
    public void AcuteToxicity_UsesConvertedEstimates()
    {
        Mixture mixture = Mix(Component("токсикант", 100, "Acute Tox. 3 (oral)"));

        Assert.Equal(100, MixtureClassifier.AcuteToxicityEstimate(mixture, ExposureRoute.Oral) ?? 0, 1e-9);
        Assert.Contains(new HazardCategory(HazardClass.AcuteToxicityOral, "3"), mixture.Classify().Hazards);
    }

    [Fact]
    public void AcuteToxicity_IgnoresTraceComponents()
    {
        var component = new MixtureComponent
        {
            Name = "следовой токсикант",
            ContentPercent = 0.5,
            AcuteToxicityEstimates = new Dictionary<ExposureRoute, double> { [ExposureRoute.Oral] = 1 }
        };

        Assert.Null(MixtureClassifier.AcuteToxicityEstimate(Mix(component), ExposureRoute.Oral));
    }

    [Fact]
    public void SkinSensitiser_StrongCategoryHasLowerLimit()
    {
        Assert.Contains(new HazardCategory(HazardClass.SkinSensitisation, "1"),
            Mix(Component("сенсибилизатор", 0.2, "Skin Sens. 1A")).Classify().Hazards);

        Assert.DoesNotContain(new HazardCategory(HazardClass.SkinSensitisation, "1"),
            Mix(Component("сенсибилизатор", 0.2, "Skin Sens. 1B")).Classify().Hazards);
    }

    [Fact]
    public void SystemicToxicity_DowngradesBetweenOneAndTenPercent()
    {
        MixtureClassification result = Mix(Component("вещество", 5, "STOT SE 1")).Classify();

        Assert.Contains(new HazardCategory(HazardClass.StotSingle, "2"), result.Hazards);
        Assert.Contains("H371", result.HazardStatements);
    }

    /// <summary>Коэффициент M умножает содержание особо токсичного для среды компонента.</summary>
    [Fact]
    public void Aquatic_AppliesMultiplyingFactor()
    {
        var strong = new MixtureComponent
        {
            Name = "пестицид",
            ContentPercent = 5,
            Classifications = new[] { new HazardCategory(HazardClass.AquaticChronic, "1") },
            ChronicMFactor = 10
        };

        MixtureClassification result = Mix(strong).Classify();

        Assert.Contains(new HazardCategory(HazardClass.AquaticChronic, "1"), result.Hazards);
        Assert.Contains("H410", result.HazardStatements);
        Assert.Contains(Pictogram.Ghs09Environment, result.Pictograms);
    }

    [Fact]
    public void Aquatic_UsesAdditivityForLowerCategories()
    {
        MixtureClassification result = Mix(
            Component("вещество 1", 1, "Aquatic Chronic 1"),
            Component("вещество 2", 20, "Aquatic Chronic 2")).Classify();

        Assert.Contains(new HazardCategory(HazardClass.AquaticChronic, "2"), result.Hazards);
        Assert.Contains("H411", result.HazardStatements);
    }

    [Fact]
    public void PhysicalHazards_ComeFromTesting()
    {
        var mixture = new Mixture
        {
            Name = "растворитель",
            PhysicalHazards = new[] { new HazardCategory(HazardClass.FlammableLiquid, "2") }
        };

        mixture.Add(Component("ацетон", 100, "Eye Irrit. 2"));

        MixtureClassification result = mixture.Classify();

        Assert.Contains("H225", result.HazardStatements);
        Assert.Contains(Pictogram.Ghs02Flame, result.Pictograms);
    }

    [Fact]
    public void SignalWord_TakesTheMostSevere()
    {
        MixtureClassification result = Mix(
            Component("раздражающий", 20, "Skin Irrit. 2"),
            Component("разъедающий", 10, "Skin Corr. 1B")).Classify();

        Assert.Equal(SignalWord.Danger, result.Signal);
    }

    [Fact]
    public void Classification_ExplainsEveryDecision()
    {
        MixtureClassification result = Mix(Component("щёлочь", 6, "Skin Corr. 1B")).Classify();

        Assert.NotEmpty(result.Reasons);
        Assert.All(result.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason.Rule)));
        Assert.Contains("6", result.Report(), StringComparison.Ordinal);
    }

    [Fact]
    public void NonHazardousMixture_IsReportedAsSuch()
    {
        MixtureClassification result = Mix(Component("вода", 100)).Classify();

        Assert.False(result.IsHazardous);
        Assert.Empty(result.HazardStatements);
        Assert.Contains("не классифицируется", result.Report(), StringComparison.Ordinal);
    }

    private static Mixture Solvent()
    {
        var mixture = new Mixture
        {
            Name = "Растворитель технический",
            Use = "разбавление лакокрасочных материалов",
            State = PhysicalState.Liquid,
            PhysicalHazards = new[] { new HazardCategory(HazardClass.FlammableLiquid, "2") }
        };

        mixture.Add(new MixtureComponent
        {
            Name = "метанол",
            CasNumber = "67-56-1",
            ContentPercent = 60,
            Classifications = new[]
            {
                new HazardCategory(HazardClass.AcuteToxicityOral, "3"),
                new HazardCategory(HazardClass.StotSingle, "1")
            },
            UnNumber = "UN1230",
            TransportClass = "3",
            PackingGroup = "II",
            ShippingName = "МЕТАНОЛ"
        });

        mixture.Add(Component("толуол", 30, "Skin Irrit. 2", "Repr. 2", "Asp. Tox. 1"));
        mixture.Add(Component("вода", 10));

        return mixture;
    }

    [Fact]
    public void SafetyDataSheet_HasAllSixteenSections()
    {
        string text = Solvent().CreateSafetyDataSheet().Render();

        for (int section = 1; section <= 16; section++)
            Assert.Contains($"РАЗДЕЛ {section}.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyDataSheet_ContainsClassificationAndComposition()
    {
        string text = Solvent().CreateSafetyDataSheet().Render();

        Assert.Contains("H301", text, StringComparison.Ordinal);
        Assert.Contains("метанол", text, StringComparison.Ordinal);
        Assert.Contains("67-56-1", text, StringComparison.Ordinal);
        Assert.Contains("GHS", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyDataSheet_TakesTransportFromComponent()
    {
        SafetyDataSheet sheet = Solvent().CreateSafetyDataSheet();

        Assert.True(sheet.Transport.IsDangerousGoods);
        Assert.Equal("UN1230", sheet.Transport.UnNumber);
        Assert.Equal("3", sheet.Transport.TransportClass);
        Assert.Contains("UN1230", sheet.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyDataSheet_ListsSectionsAwaitingText()
    {
        SafetyDataSheet sheet = Solvent().CreateSafetyDataSheet();

        Assert.Contains(4, sheet.MissingNarrativeSections);

        sheet.SetNarrative(4, "Промыть кожу водой с мылом.");

        Assert.DoesNotContain(4, sheet.MissingNarrativeSections);
        Assert.Contains("Промыть кожу", sheet.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyDataSheet_RejectsUnknownSection()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Solvent().CreateSafetyDataSheet().SetNarrative(17, "текст"));

    [Fact]
    public void SafetyDataSheet_ReportsAcuteToxicityEstimate()
    {
        string text = Solvent().CreateSafetyDataSheet().Render();

        // 60% метанола с пересчитанной ATE 100 мг/кг дают ATE смеси около 167 мг/кг
        Assert.Contains("оценка острой токсичности", text, StringComparison.OrdinalIgnoreCase);
    }
}
