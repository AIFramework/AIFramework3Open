using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.DataSets;
using AI.ML.Regression;
using Xunit;

namespace AI.ML.Integration;

/// <summary>
/// Интеграционные проверки после рефакторинга 4.x: сборка доменов и базовых типов.
/// </summary>
public class RefactorSmokeTests
{
    [Fact]
    public void VectorDataset_and_IClassifier_contract_compile_path()
    {
        var ds = new VectorDataset();
        ds.Add(new VectorDatasetItem(new Vector(1, 2, 3), 0));
        Assert.Single(ds);
        Assert.Equal(0, ds[0].ClassMark);
    }

    [Fact]
    public void LinearRegression_constructible()
    {
        var x = new Vector(1.0, 2.0, 3.0);
        var y = new Vector(2.0, 4.0, 6.0);
        var lr = new LinearRegression(x, y);
        Assert.NotNull(lr.Lrm);
    }
}
