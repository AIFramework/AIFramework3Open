using AI.Script.Hosting;

namespace AI.Script.UnitTests;

/// <summary>
/// Согласие двух оценщиков: <c>stat.spearman</c> и <c>stat.kappa</c>.
/// </summary>
public sealed class AgreementTests
{
    /// <summary>Судья на балл щедрее человека: порядок тот же, а согласие по шкале уже не полное.</summary>
    [Fact]
    public void GenerousJudge_SameOrder_LowerKappa()
    {
        RunResult result = Script.RunOk("""
            let human = <1, 2, 3, 4, 5>
            let judge = <2, 3, 4, 5, 5>
            emit rank = stat.spearman(human, judge)
            emit same = stat.kappa(human, human)
            emit shifted = stat.kappa(human, judge)
            """);

        Assert.True((double)result.Emitted["rank"]! > 0.95);
        Assert.Equal(1.0, result.Emitted["same"]);
        Assert.InRange((double)result.Emitted["shifted"]!, 0.5, 0.99);
    }

    /// <summary>Равные значения получают средний ранг: обратный порядок дает минус единицу.</summary>
    [Fact]
    public void Spearman_Reversed_IsMinusOne()
    {
        RunResult result = Script.RunOk("emit r = stat.spearman(<1, 2, 2, 3>, <3, 2, 2, 1>)");

        Assert.Equal(-1.0, (double)result.Emitted["r"]!, 6);
    }

    /// <summary>Без весов «4 против 5» стоит столько же, сколько «1 против 5».</summary>
    [Fact]
    public void Kappa_Weights_PunishFarDisagreementMore()
    {
        RunResult result = Script.RunOk("""
            let a = <1, 2, 3, 4, 5, 3>
            let near = <1, 2, 3, 4, 4, 3>
            let far = <1, 2, 3, 4, 1, 3>
            emit quadratic_near = stat.kappa(a, near)
            emit quadratic_far = stat.kappa(a, far)
            emit plain_near = stat.kappa(a, near, weights: "none")
            emit plain_far = stat.kappa(a, far, weights: "none")
            """);

        Assert.True((double)result.Emitted["quadratic_near"]! > (double)result.Emitted["quadratic_far"]!);
        Assert.Equal((double)result.Emitted["plain_near"]!, (double)result.Emitted["plain_far"]!, 1);
    }

    [Fact]
    public void Kappa_UnknownWeights_ListsKnown()
    {
        RunResult result = Script.RunWith(AI.Script.Std.StandardLibrary.CreateHost(), "emit k = stat.kappa(<1, 2>, <1, 2>, weights: \"cubic\")", new RunOptions());

        Assert.False(result.Success);
        Assert.Contains("quadratic", Script.Report(result), StringComparison.Ordinal);
    }

    // --- найденное при проверке ---

    /// <summary>Шкала это встреченные значения: широкий диапазон не требует огромной матрицы.</summary>
    [Fact]
    public void Kappa_WideRange_IsFastAndBounded()
    {
        RunResult result = Script.RunOk("emit k = stat.kappa(<0, 50000, 100000>, <0, 50000, 100000>)");

        Assert.Equal(1.0, result.Emitted["k"]);
    }

    [Fact]
    public void Kappa_NaN_IsRejected()
    {
        Assert.NotEmpty(Script.FailsWith("emit k = stat.kappa(<1, 0/0>, <1, 2>)").Message);
    }
}
