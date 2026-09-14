using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространство <c>pde</c> над AI.Solvers.Pde.
/// </summary>
/// <remarks>
/// Задачи с аналитическим решением: затухание синуса в теплопроводности, смена знака стоячей
/// волны, <c>sin·sin</c> для Пуассона, парабола для стационарной диффузии, гармоническая функция
/// на кольце и концентрация напряжений Кирша. Сверка с формулой проверяет, что функции скрипта
/// попали на свои места — источник не спутан с границей, x не спутан с y.
/// </remarks>
public sealed class PdeTests
{
    private static RunResult Run(string source) => Script.RunOk(source);

    private static double Number(RunResult result, string name) => (double)result.Emitted[name]!;

    /// <summary>u(x, 0) = sin πx при нулевых концах: u(½, t) = e^(−απ²t).</summary>
    [Fact]
    public void Heat_SineDecaysExponentially()
    {
        RunResult result = Run("""
            let rod = pde.heat(x => math.sin(pi * x), diffusivity: 0.1, until: 0.5)

            emit middle = rod.u[25]
            emit x = rod.x[25]
            emit stable = rod.stable
            """);

        Assert.Equal(0.5, Number(result, "x"), 12);
        // Допуск, а не число знаков: округление до знаков разводит 0.61050 и 0.61060 по разные
        // стороны границы, и тест проверял бы везение с округлением, а не точность схемы.
        Assert.Equal(Math.Exp(-0.1 * Math.PI * Math.PI * 0.5), Number(result, "middle"), tolerance: 1e-3);
        Assert.Equal(true, result.Emitted["stable"]);
    }

    /// <summary>Явная схема с шагом, в сотни раз больше допустимого, обязана признаться в этом.</summary>
    [Fact]
    public void Heat_ExplicitWithLargeStep_IsReportedUnstable()
    {
        RunResult result = Run("""
            let rod = pde.heat(x => math.sin(pi * x), diffusivity: 1, until: 1, steps: 10, kind: "explicit")

            emit stable = rod.stable
            emit warned = len(rod.warnings) > 0
            """);

        Assert.Equal(false, result.Emitted["stable"]);
        Assert.Equal(true, result.Emitted["warned"]);
    }

    /// <summary>Стоячая волна sin πx при c = 1 через единицу времени меняет знак.</summary>
    [Fact]
    public void Wave_StandingWaveFlipsSign()
    {
        RunResult result = Run("""
            let chord = pde.wave(x => math.sin(pi * x), speed: 1, until: 1)

            emit middle = chord.u[50]
            emit courant = chord.courant
            """);

        Assert.Equal(-1, Number(result, "middle"), tolerance: 0.01);
        Assert.True(Number(result, "courant") <= 1);
    }

    /// <summary>−Δu = 2π² sin πx sin πy с нулём на границе: u = sin πx sin πy, в центре — 1.</summary>
    [Fact]
    public void Poisson_RecoversSineProduct()
    {
        RunResult result = Run("""
            let field = pde.poisson(source: (x, y) => 2 * pi * pi * math.sin(pi * x) * math.sin(pi * y))

            emit centre = field.u[20, 20]
            emit rows = mat.rows(field.u)
            """);

        Assert.Equal(1, Number(result, "centre"), tolerance: 0.01);
        Assert.Equal(41.0, result.Emitted["rows"]);
    }

    /// <summary>Строка матрицы — y, столбец — x: граница u = y обязана расти по строкам.</summary>
    [Fact]
    public void Poisson_RowsAreY()
    {
        RunResult result = Run("""
            let field = pde.poisson(boundary: (x, y) => y, nodes: 11)

            emit bottom = field.u[0, 5]
            emit top = field.u[10, 5]
            """);

        Assert.Equal(0, Number(result, "bottom"), 9);
        Assert.Equal(1, Number(result, "top"), 9);
    }

    [Fact]
    public void Poisson_WithoutSourceAndBoundary_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit f = pde.poisson()");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("source", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>−u″ = 1 при u(0) = u(1) = 0: u = x(1 − x)/2, в середине 1/8 — точно в узлах.</summary>
    [Fact]
    public void Steady_ParabolaIsExactAtNodes()
    {
        RunResult result = Run("emit middle = pde.steady(x => 1).u[25]");

        Assert.Equal(0.125, Number(result, "middle"), 9);
    }

    /// <summary>Без источника, ноль слева, поток 2 справа: решение линейно, |u(1)| = 2.</summary>
    [Fact]
    public void Steady_FluxCondition_GivesLinearProfile()
    {
        RunResult result = Run("""
            let u = pde.steady(x => 0, left: { value: 0 }, right: { flux: 2 })

            emit last = u.u[50]
            """);

        Assert.Equal(2, Math.Abs(Number(result, "last")), 6);
    }

    [Fact]
    public void Steady_ConditionWithBothKinds_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit u = pde.steady(x => 1, left: { value: 0, flux: 1 })");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("{ value: 0 }", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Domain_NotAnInterval_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit u = pde.steady(x => 1, x: <1, 0>)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("<0, 1>", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Mesh_HasTrianglesOfThreeNodes()
    {
        RunResult result = Run("""
            let plate = pde.mesh(kind: "plate_with_hole", radius: 1, size: 10)

            emit width = mat.cols(plate.triangles)
            emit elements = plate.elements
            emit nodes = len(plate.x)
            """);

        Assert.Equal(3.0, result.Emitted["width"]);
        Assert.True(Number(result, "elements") > 0);
        Assert.True(Number(result, "nodes") > 0);
    }

    /// <summary>x + y гармонична: на кольце с такой границей решение совпадает с ней в каждом узле.</summary>
    [Fact]
    public void PoissonMesh_HarmonicBoundary_IsReproduced()
    {
        RunResult result = Run("""
            let ring = pde.mesh(kind: "annulus", radius: 1, size: 2)
            let field = pde.poisson_mesh(ring, boundary: (x, y) => x + y)

            emit error = vec.norm(field.u - (field.x + field.y))
            """);

        Assert.True(Number(result, "error") < 1e-6);
    }

    /// <summary>
    /// Задача Кирша: пластина с отверстием под растяжением. У края отверстия поперёк нагрузки
    /// напряжение втрое выше приложенного — это и проверяется, как в тесте библиотеки.
    /// </summary>
    [Fact]
    public void Elasticity_Kirsch_StressConcentrationNearThree()
    {
        RunResult result = Run("""
            let plate = pde.mesh(kind: "plate_with_hole", radius: 1, size: 10, radial: 32, angular: 48, grading: 1.12)

            let solved = pde.elasticity(plate, young: 70e9, poisson_ratio: 0.33,
                fixed: [
                    { where: (x, y) => math.approx(y, 0), x: false, y: true },
                    { where: (x, y) => math.approx(x, 0), x: true, y: false }
                ],
                traction: [{ where: (x, y) => math.approx(x, 10), tx: 1e6, ty: 0 }])

            let top = solved.nodes |> table.filter(n => math.approx(n.x, 0) && math.approx(n.y, 1))

            emit concentration = top[0].sxx / 1e6
            emit converged = solved.converged
            """);

        Assert.InRange(Number(result, "concentration"), 2.8, 3.3);
        Assert.Equal(true, result.Emitted["converged"]);
    }

    [Fact]
    public void Elasticity_UnrestrainedBody_IsReported()
    {
        Diagnostic error = Script.FailsWith("""
            let grid = pde.mesh(nodes: 3)
            emit r = pde.elasticity(grid, young: 1e9, poisson_ratio: 0.3, fixed: [])
            """);

        Assert.Contains("pde.elasticity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Elasticity_TypoInLoadField_IsRejected()
    {
        Diagnostic error = Script.FailsWith("""
            let grid = pde.mesh(nodes: 3)
            emit r = pde.elasticity(grid, young: 1e9, poisson_ratio: 0.3,
                fixed: [{ where: (x, y) => math.approx(x, 0), x: true, y: true }],
                traction: [{ where: (x, y) => math.approx(x, 1), tx: 1, tyy: 0 }])
            """);

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("tyy", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Малое число Рейнольдса: течение устанавливается, главный вихрь в середине каверны.</summary>
    [Fact]
    public void Cavity_LowReynolds_ConvergesWithCentralVortex()
    {
        RunResult result = Run("""
            let flow = pde.cavity(10, nodes: 21)

            emit converged = flow.converged
            emit vortex_x = flow.vortex_x
            emit rows = mat.rows(flow.psi)
            """);

        Assert.Equal(true, result.Emitted["converged"]);
        Assert.InRange(Number(result, "vortex_x"), 0.35, 0.65);
        Assert.Equal(21.0, result.Emitted["rows"]);
    }
}
