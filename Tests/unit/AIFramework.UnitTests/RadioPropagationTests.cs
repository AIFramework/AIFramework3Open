using AI.Microwave.Propagation;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Распространение радиосигнала: классические формулы потерь, насыщение потерь в лесу
/// и вероятность связи при затенении.
/// </summary>
public class RadioPropagationTests
{
    [Fact]
    public void PathLoss_FreeSpace_MatchesClassicFormula()
    {
        // 32.44 + 20·lg f[МГц] + 20·lg d[км]
        Assert.Equal(32.44 + (20 * Math.Log10(900)), PathLoss.FreeSpaceDb(900e6, 1000), 1);

        // Удвоение расстояния — плюс 6 дБ
        Assert.Equal(6.02, PathLoss.FreeSpaceDb(900e6, 2000) - PathLoss.FreeSpaceDb(900e6, 1000), 2);
    }

    [Fact]
    public void PathLoss_Hata_MatchesPublishedFormulaAndOrdersEnvironments()
    {
        // Средний город, 900 МГц, базовая станция 30 м, абонент 1.5 м, 10 км: по формулам Хаты 161.6 дБ
        double urban = PathLoss.HataDb(900e6, 10_000, 30, 1.5, HataEnvironment.MediumCity);
        double suburban = PathLoss.HataDb(900e6, 10_000, 30, 1.5, HataEnvironment.Suburban);
        double open = PathLoss.HataDb(900e6, 10_000, 30, 1.5, HataEnvironment.Open);

        Assert.Equal(161.6, urban, 1);
        Assert.True(open < suburban && suburban < urban);
    }

    [Fact]
    public void PathLoss_Hata_OpenAreaAbove1500MHz_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => PathLoss.HataDb(1800e6, 5000, 30, 1.5, HataEnvironment.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => PathLoss.HataDb(100e6, 5000, 30, 1.5, HataEnvironment.MediumCity));
    }

    [Fact]
    public void VegetationLoss_Weissberger_IsContinuousAt14Metres()
    {
        double below = VegetationLoss.WeissbergerDb(900e6, 14.0);
        double above = VegetationLoss.WeissbergerDb(900e6, 14.0 + 1e-9);

        Assert.Equal(below, above, 1);
    }

    [Fact]
    public void VegetationLoss_Itu833_GrowsLinearlyThenSaturates()
    {
        // Мелкий лес: почти γ·d; глубокий: предел Am, а не сотни децибел
        Assert.Equal(0.3 * 5, VegetationLoss.Itu833Db(5, 0.3, 30), 1);
        Assert.Equal(30.0, VegetationLoss.Itu833Db(10_000, 0.3, 30), 9);
    }

    [Fact]
    public void LinkBudget_CoverageProbability_FollowsShadowing()
    {
        Assert.Equal(-100.0, LinkBudget.ReceivedPowerDbm(60, 160), 12);
        Assert.Equal(0.5, LinkBudget.CoverageProbability(-100, -100, 8), 6);

        // Медиана на σ выше чувствительности: Φ(1)
        Assert.Equal(0.8413, LinkBudget.CoverageProbability(-92, -100, 8), 3);
        Assert.Equal(1.0, LinkBudget.CoverageProbability(-99, -100, 0), 12);
    }
}
