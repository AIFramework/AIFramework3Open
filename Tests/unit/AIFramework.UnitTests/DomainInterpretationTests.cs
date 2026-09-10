using AI.Biology.Sequences;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Physics.Mechanics;
using AI.Solvers.Constraints.Cp;
using AI.Solvers.Constraints.Sat;
using AI.Solvers.Pde;
using AI.Solvers.Pde.FiniteDifference;
using AI.Solvers.Pde.FiniteElement;
using AI.Solvers.Pde.Numerics;
using AI.Units;
using System.Globalization;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Результаты решателей и доменных расчётов объясняют себя. Проверяется не наличие текста,
/// а то, что объяснение следует из известных фактов: принципа максимума, условия устойчивости,
/// полноты перебора, формы баллистической траектории. Где объяснение утверждает что-то
/// проверяемое, тест проверяет и само утверждение.
/// </summary>
public class DomainInterpretationTests
{
    #region Уравнения в частных производных

    [Fact]
    public void Poisson_LaplaceSolution_HasExtremesOnBoundary()
    {
        // u = x гармонична: по принципу максимума экстремумы только на границе
        PoissonSolution solution = Poisson2D.SolveLaplace(new Grid2D(0, 1, 0, 1, 21, 21), static (x, _) => x);

        Interpretation interpretation = solution.Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("Оба экстремума лежат на границе", StringComparison.Ordinal));
        Assert.Contains(interpretation.Metrics, m => m.Name == "Максимум" && m.Value == "1.0000");
        Assert.Contains(interpretation.Warnings, w => w.Contains("Невязка говорит", StringComparison.Ordinal));
    }

    [Fact]
    public void Poisson_WithPositiveSource_HasInteriorMaximum()
    {
        // −Δu = 1 при нулевой границе: решение положительно внутри, максимум в середине
        PoissonSolution solution = Poisson2D.Solve(
            new Grid2D(0, 1, 0, 1, 21, 21), static (_, _) => 1.0, static (_, _) => 0.0);

        Interpretation interpretation = solution.Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("Экстремум лежит внутри области", StringComparison.Ordinal));
        Assert.Contains(interpretation.Metrics, m => m.Name == "Максимум" && m.Meaning == "достигается внутри области");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Минимум" && m.Meaning == "достигается на границе");
    }

    [Fact]
    public void Heat_ExplicitAboveHalf_IsReportedUnstable()
    {
        // h = 0.1, α = 1, Δt = 0.01: α·Δt/h² = 1 > 1/2
        HeatSolution solution = HeatEquation1D.Solve(
            new Grid1D(0, 1, 11), 1.0, static x => Math.Sin(Math.PI * x), static _ => 0.0, static _ => 0.0,
            finalTime: 0.1, steps: 10, scheme: TimeScheme.Explicit);

        Interpretation interpretation = solution.Interpret();

        Assert.False(solution.IsStable);
        Assert.Contains("неустойчива", interpretation.Summary, StringComparison.Ordinal);
        Assert.Contains(interpretation.Warnings, w => w.Contains("≤ 1/2", StringComparison.Ordinal));
    }

    [Fact]
    public void Heat_CrankNicolsonAboveHalf_WarnsAboutOscillation()
    {
        HeatSolution solution = HeatEquation1D.Solve(
            new Grid1D(0, 1, 11), 1.0, static x => Math.Sin(Math.PI * x), static _ => 0.0, static _ => 0.0,
            finalTime: 0.1, steps: 10, scheme: TimeScheme.CrankNicolson);

        Interpretation interpretation = solution.Interpret();

        // Та же сетка и тот же шаг, что у неустойчивой явной схемы, — но предупреждение другое
        Assert.True(solution.IsStable);
        Assert.Equal(TimeScheme.CrankNicolson, solution.Scheme);
        Assert.Contains(interpretation.Warnings, w => w.Contains("меняют знак", StringComparison.Ordinal));
        Assert.DoesNotContain(interpretation.Warnings, w => w.Contains("≤ 1/2", StringComparison.Ordinal));
    }

    [Fact]
    public void Heat_ExplicitBelowHalf_ExplainsTimeStepCost()
    {
        // Δt = 0.0025: α·Δt/h² = 0.25
        HeatSolution solution = HeatEquation1D.Solve(
            new Grid1D(0, 1, 11), 1.0, static x => Math.Sin(Math.PI * x), static _ => 0.0, static _ => 0.0,
            finalTime: 0.1, steps: 40, scheme: TimeScheme.Explicit);

        Interpretation interpretation = solution.Interpret();

        Assert.True(solution.IsStable);
        Assert.Contains(interpretation.Findings, f => f.Contains("вчетверо", StringComparison.Ordinal));
        Assert.Empty(interpretation.Warnings);
    }

    [Fact]
    public void Wave_CourantOne_IsExact_AndReallyIs()
    {
        // h = 0.01, c = 1, Δt = 0.01: число Куранта ровно единица
        WaveSolution solution = WaveEquation1D.Solve(
            new Grid1D(0, 1, 101), 1.0, static x => Math.Sin(Math.PI * x), null, finalTime: 0.5, steps: 50);

        Assert.Contains(solution.Interpret().Findings, f => f.Contains("точна", StringComparison.Ordinal));

        // Утверждение проверяемо: u = sin(πx)·cos(πt) при t = 0.5 обращается в нуль всюду
        double largest = 0;
        for (int i = 0; i < solution.Values.Count; i++)
            largest = Math.Max(largest, Math.Abs(solution.Values[i]));

        Assert.True(largest < 1e-9, $"Наибольшее отклонение {largest:E2}");
    }

    [Fact]
    public void Wave_SmallCourant_WarnsAboutDispersion()
    {
        // Δt = 0.0025: число Куранта 0.25
        WaveSolution solution = WaveEquation1D.Solve(
            new Grid1D(0, 1, 101), 1.0, static x => Math.Sin(Math.PI * x), null, finalTime: 0.5, steps: 200);

        Assert.Contains(solution.Interpret().Findings, f => f.Contains("дисперсией", StringComparison.Ordinal));
    }

    [Fact]
    public void Fem1D_ReportsKnownMaximum()
    {
        // −u″ = 1, u(0) = u(1) = 0: u = x(1 − x)/2, максимум 1/8 в середине.
        // Линейные элементы в одномерной задаче точны в узлах, поэтому значение точное
        Fem1DSolution solution = Fem1D.Solve(new Grid1D(0, 1, 21),
            static _ => 1.0, static _ => 0.0, static _ => 1.0,
            BoundaryCondition.Fixed(0), BoundaryCondition.Fixed(0));

        Interpretation interpretation = solution.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Максимум" && m.Value == "0.1250");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Сходимость" && m.Value == "достигнута");
        Assert.Contains(interpretation.Warnings, w => w.Contains("производной", StringComparison.Ordinal));
    }

    [Fact]
    public void Fem2D_CountsMeshCorrectly()
    {
        // Сетка 11×11: на границе 4·10 = 40 узлов, треугольников 10·10·2 = 200
        TriangularMesh mesh = TriangularMesh.Rectangle(new Grid2D(0, 1, 0, 1, 11, 11));
        Fem2DSolution solution = Fem2D.SolvePoisson(mesh, static (_, _) => 1.0, static (_, _) => 0.0);

        Interpretation interpretation = solution.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Граничных узлов" && m.Value == "40");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Треугольников" && m.Value == "200");
    }

    [Fact]
    public void IterativeResult_ExplainsNonConvergence()
    {
        var result = new IterativeResult(new Vector(3), Iterations: 5, Residual: 1e-3, Converged: false);

        Interpretation interpretation = result.Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Сходимость" && m.Value == "не достигнута");
        Assert.Contains(interpretation.Findings, f => f.Contains("больше, чем неизвестных", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("несимметрична", StringComparison.Ordinal));
    }

    #endregion

    #region Выполнимость и ограничения

    [Fact]
    public void Sat_Unsatisfiable_IsAnAnswerNotARefusal()
    {
        var formula = new CnfFormula();
        _ = formula.AddClause(1).AddClause(-1);

        Interpretation interpretation = SatSolver.Solve(formula).Interpret();

        Assert.Contains("невыполнима", interpretation.Summary, StringComparison.Ordinal);
        Assert.Contains(interpretation.Findings, f => f.Contains("ответ, а не отказ", StringComparison.Ordinal));
    }

    [Fact]
    public void Sat_Satisfiable_RecommendsIndependentCheck()
    {
        var formula = new CnfFormula();
        _ = formula.ExactlyOne(formula.AddVariables(5));

        Interpretation interpretation = SatSolver.Solve(formula).Interpret();

        Assert.Contains(interpretation.Recommendations, r => r.Contains("Verify", StringComparison.Ordinal));
    }

    [Fact]
    public void Sat_ConflictLimit_GivesNoAnswer()
    {
        // Пять голубей в четырёх клетках: невыполнимо, но за один конфликт этого не доказать
        var formula = new CnfFormula();
        int[] variables = formula.AddVariables(20);

        int Sits(int pigeon, int hole) => variables[(pigeon * 4) + hole];

        for (int pigeon = 0; pigeon < 5; pigeon++)
            _ = formula.AddClause(Sits(pigeon, 0), Sits(pigeon, 1), Sits(pigeon, 2), Sits(pigeon, 3));

        for (int hole = 0; hole < 4; hole++)
            for (int first = 0; first < 5; first++)
                for (int second = first + 1; second < 5; second++)
                    _ = formula.AddClause(-Sits(first, hole), -Sits(second, hole));

        SatSolution solution = SatSolver.Solve(formula, new SatOptions { MaxConflicts = 1 });

        Assert.Equal(SatStatus.Unknown, solution.Status);
        Assert.Contains(solution.Interpret().Warnings, w => w.Contains("скорее всего невыполнима", StringComparison.Ordinal));
    }

    [Fact]
    public void Cp_ThreeQueens_InfeasibilityIsProven()
    {
        // Трёх ферзей на доске 3×3 расставить нельзя — известный факт
        CpSolution solution = CpSolver.Solve(Queens(3));

        Assert.Equal(CpStatus.Infeasible, solution.Status);
        Assert.Contains(solution.Interpret().Findings, f => f.Contains("доказано полным перебором", StringComparison.Ordinal));
    }

    [Fact]
    public void Cp_EightQueens_ReportsFirstSolution()
    {
        CpSolution solution = CpSolver.Solve(Queens(8));
        Interpretation interpretation = solution.Interpret();

        Assert.True(solution.IsSatisfiable);
        Assert.Equal(8, interpretation.Metrics.Count(m => m.Meaning == "в первом решении"));
        Assert.Contains(interpretation.Metrics,
            m => m.Name == "q0" && m.Value == solution["q0"].ToString(CultureInfo.InvariantCulture));
    }

    private static CpModel Queens(int size)
    {
        var model = new CpModel($"{size} ферзей");
        IntVariable[] queens = model.AddVariables("q", size, 0, size - 1);

        _ = model.Add(new AllDifferent(queens));

        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                _ = model.Add(new NotEqual(queens[i], queens[j], j - i));
                _ = model.Add(new NotEqual(queens[i], queens[j], i - j));
            }
        }

        return model;
    }

    #endregion

    #region Физика и биология

    [Fact]
    public void Trajectory_At45Degrees_IsMaximalRange()
    {
        Interpretation interpretation = Projectile.Launch(new Quantity(20, Dimension.Velocity), 45).Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("наибольшая возможная дальность", StringComparison.Ordinal));
        Assert.Contains(interpretation.Metrics, m => m.Name == "Угол броска" && m.Value == "45.0");
    }

    [Fact]
    public void Trajectory_At30Degrees_ReachesSineOfDoubleAngle()
    {
        // Дальность пропорциональна sin 2θ: при 30° это sin 60° ≈ 86.6 % от наибольшей.
        // Угол не передаётся в результат — он восстановлен по форме траектории
        Interpretation interpretation = Projectile.Launch(new Quantity(20, Dimension.Velocity), 30).Interpret();

        Assert.Contains(interpretation.Findings, f => f.Contains("86.6 %", StringComparison.Ordinal));
        Assert.Contains(interpretation.Metrics, m => m.Name == "Угол броска" && m.Value == "30.0");
    }

    [Fact]
    public void Alignment_Identical_StillWarnsAboutSignificance()
    {
        Interpretation interpretation = Alignment.Global("ACGTACGT", "ACGTACGT").Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Идентичность" && m.Value == "100.0 %");
        Assert.DoesNotContain(interpretation.Warnings, w => w.Contains("ниже половины", StringComparison.Ordinal));

        // Значимость не вычислена — об этом сказано всегда, даже при полном совпадении
        Assert.Contains(interpretation.Warnings, w => w.Contains("E-значение", StringComparison.Ordinal));
    }

    [Fact]
    public void Alignment_Insertion_IsOneGapRun()
    {
        // Вставка двух нуклеотидов: аффинный штраф делает её одним участком, а не двумя
        AlignmentResult result = Alignment.Global("ACGTACGT", "ACGTTTACGT");

        Assert.Equal(2, result.Gaps);
        Assert.Equal(1, result.GapRuns);
        Assert.Contains(result.Interpret().Findings, f => f.Contains("их 1,", StringComparison.Ordinal));
    }

    #endregion
}
