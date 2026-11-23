using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Engines;
using Xunit;

namespace AI.Agents.Interop.Tests.Engines;

/// <summary>
/// Эталонный бюджет хода, сессия С2: каждый предел дает свой код, исчерпание отменяет <see cref="IRunBudget.Exhausted"/>,
/// вызов без цены виден, а не принят за бесплатный.
/// </summary>
public sealed class LimitedRunBudgetTests
{
    private static ModelUsage Usage(long tokens, decimal? cost = null) =>
        new() { InputTokens = tokens, OutputTokens = 0, Cost = cost };

    [Fact]
    public void Steps_AreCounted_AndTheLimitStopsWithStepLimit()
    {
        using var budget = new LimitedRunBudget(new RunLimits { Steps = 2 });

        Assert.True(budget.TryStep(out _));
        Assert.True(budget.TryStep(out _));
        Assert.False(budget.TryStep(out StopCode stop));

        Assert.Equal(StopCode.StepLimit, stop);
        Assert.True(budget.Exhausted.IsCancellationRequested);
    }

    [Theory]
    [InlineData("tokens")]
    [InlineData("cost")]
    [InlineData("calls")]
    public void Spending_OverALimit_CancelsAtOnce_AndNextStepGetsTheCode(string limit)
    {
        RunLimits limits = limit switch
        {
            "tokens" => new RunLimits { Tokens = 100 },
            "cost" => new RunLimits { Cost = 1m },
            _ => new RunLimits { ModelCalls = 1 },
        };
        StopCode expected = limit switch
        {
            "tokens" => StopCode.TokenLimit,
            "cost" => StopCode.CostLimit,
            _ => StopCode.CallLimit,
        };
        using var budget = new LimitedRunBudget(limits);

        budget.Spend(Usage(150, 2m));

        Assert.True(budget.Exhausted.IsCancellationRequested);
        Assert.False(budget.TryStep(out StopCode stop));
        Assert.Equal(expected, stop);
        Assert.Equal(expected, budget.Stop);
    }

    [Fact]
    public void FirstExhaustedLimit_Wins_AndStays()
    {
        using var budget = new LimitedRunBudget(new RunLimits { Tokens = 100, ModelCalls = 10 });

        budget.Spend(Usage(200));
        for (int i = 0; i < 20; i++)
            budget.Spend(Usage(1));

        Assert.False(budget.TryStep(out StopCode stop));
        Assert.Equal(StopCode.TokenLimit, stop);
    }

    [Fact]
    public void CallWithoutPrice_IsCountedAsUnpriced_NotAsFree()
    {
        using var budget = new LimitedRunBudget(new RunLimits { Cost = 1m });

        budget.Spend(Usage(10, cost: null));
        budget.Spend(Usage(10, cost: 0.25m));

        Assert.Equal(1, budget.UnpricedCalls);
        Assert.Equal(0.25m, budget.Cost);
        Assert.Equal(2, budget.ModelCalls);
        Assert.Null(budget.Stop);
    }

    [Fact]
    public async Task Duration_SetsDeadline_AndCancelsWhenTimeIsOut()
    {
        using var budget = new LimitedRunBudget(new RunLimits { Duration = TimeSpan.FromMilliseconds(50) });

        Assert.NotNull(budget.Deadline);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(TimeSpan.FromSeconds(10), budget.Exhausted));

        Assert.False(budget.TryStep(out StopCode stop));
        Assert.Equal(StopCode.TimeLimit, stop);
        Assert.Equal(TimeSpan.Zero, budget.Remaining);
    }

    [Fact]
    public void NoLimits_NeverStops_AndHasNoDeadline()
    {
        using var budget = new LimitedRunBudget(new RunLimits());

        for (int i = 0; i < 1000; i++)
            Assert.True(budget.TryStep(out _));
        budget.Spend(Usage(1_000_000, 100m));

        Assert.Null(budget.Deadline);
        Assert.Null(budget.Remaining);
        Assert.False(budget.Exhausted.IsCancellationRequested);
    }

    [Fact]
    public void SpendAfterDispose_IsCounted_WithoutThrowing()
    {
        var budget = new LimitedRunBudget(new RunLimits { Tokens = 10 });
        budget.Dispose();

        budget.Spend(Usage(100));

        Assert.Equal(100, budget.Tokens);
        Assert.Equal(StopCode.TokenLimit, budget.Stop);
    }

    [Fact]
    public void HostLimits_NarrowedByAgent_SmallerWins()
    {
        var host = new RunLimits { Steps = 30, Cost = 5m, Tokens = 1000 };

        RunLimits run = host.Narrow(new AgentLimits { Steps = 10, Cost = 8m, Duration = TimeSpan.FromMinutes(2) });

        Assert.Equal(10, run.Steps);
        Assert.Equal(5m, run.Cost);
        Assert.Equal(1000, run.Tokens);
        Assert.Equal(TimeSpan.FromMinutes(2), run.Duration);
        Assert.Same(host, host.Narrow(null));
    }

    [Fact]
    public void BudgetWithoutSignal_DefaultsToNone()
    {
        IRunBudget plain = new Effects.TestBudget();

        Assert.Equal(CancellationToken.None, plain.Exhausted);
    }
}
