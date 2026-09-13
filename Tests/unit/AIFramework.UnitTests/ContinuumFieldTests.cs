using AI.Physics.Continuum;
using AI.Solvers.Pde;
using AI.Solvers.Pde.FiniteDifference;
using AI.Solvers.Pde.FiniteElement;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Поля в сплошной среде проверяются там, где ответ известен точно: однородное растяжение
/// треугольник постоянной деформации обязан воспроизвести до ошибки округления на любой сетке;
/// труба под давлением сходится к решению Ламе со вторым порядком, а реакции опор уравновешивают
/// давление; отверстие даёт концентрацию Кирша. Течение в каверне сравнивается с таблицей
/// Гиа, Гиа и Шина (1982) — общепринятым эталоном для этой задачи.
/// </summary>
public class ContinuumFieldTests
{
    private static Quantity Q(double value, string unit) => Quantity.Of(value, unit);

    #region Упругость методом конечных элементов

    [Fact]
    public void PatchTest_UniformTension_IsReproducedExactlyOnDistortedMesh()
    {
        var grid = new Grid2D(0, 2, 0, 1, 9, 5);
        TriangularMesh regular = TriangularMesh.Rectangle(grid);
        var rng = new Random(1);
        var x = new double[regular.NodeCount];
        var y = new double[regular.NodeCount];

        for (int node = 0; node < regular.NodeCount; node++)
        {
            bool inner = !regular.IsBoundary(node);
            x[node] = regular.X(node) + (inner ? (rng.NextDouble() - 0.5) * 0.3 * grid.StepX : 0);
            y[node] = regular.Y(node) + (inner ? (rng.NextDouble() - 0.5) * 0.3 * grid.StepY : 0);
        }

        TriangularMesh mesh = TriangularMesh.Create(x, y,
            Enumerable.Range(0, regular.TriangleCount).Select(regular.Triangle).ToArray());

        const double E = 200e9, Nu = 0.3, S = 1e6, Thickness = 0.01;
        int origin = Enumerable.Range(0, mesh.NodeCount).Single(n => mesh.X(n) == 0 && mesh.Y(n) == 0);

        ElasticitySolution solution = new ElasticityProblem(mesh, E, Nu, PlaneCondition.PlaneStress, Thickness)
            .FixWhere((px, _) => Math.Abs(px) < 1e-12, x: true, y: false)
            .FixY(origin)
            .AddTraction((px, _) => Math.Abs(px - 2) < 1e-12, (_, _) => (S, 0))
            .Solve();

        for (int e = 0; e < mesh.TriangleCount; e++)
        {
            (double xx, double yy, double xy) = solution.ElementStress(e);
            Assert.Equal(S, xx, 1e-6 * S);
            Assert.Equal(0, yy, 1e-6 * S);
            Assert.Equal(0, xy, 1e-6 * S);
        }

        for (int node = 0; node < mesh.NodeCount; node++)
        {
            Assert.Equal(S * mesh.X(node) / E, solution.DisplacementX(node), 1e-9 * S / E);
            Assert.Equal(-Nu * S * mesh.Y(node) / E, solution.DisplacementY(node), 1e-9 * S / E);
        }

        Assert.Equal(S * 1 * Thickness, solution.TotalLoad.X, 1e-6);
        Assert.Equal(-solution.TotalLoad.X, solution.TotalReaction.X, 1e-6);
        Assert.Equal(0.5 * S * S / E * 2 * 1 * Thickness, solution.StrainEnergy, 1e-9);
    }

    [Fact]
    public void PressurisedTube_ConvergesToLameWithSecondOrder_AndReactionsBalancePressure()
    {
        const double A = 0.1, B = 0.2, P = 100e6;
        ElasticMaterial material = ElasticMaterial.FromYoungAndPoisson(Q(200, "GPa"), 0.3);
        double exact = ThickWalledVessels.CylinderRadialDisplacement(
            Q(A, "m"), Q(B, "m"), Q(P, "Pa"), default, Q(A, "m"), material, AxialCondition.PlaneStrain).SiValue;

        double Error(int radial, int angular, out ElasticitySolution solution)
        {
            TriangularMesh mesh = TriangularMesh.QuarterAnnulus(A, B, radial, angular);

            solution = new ElasticityProblem(mesh, 200e9, 0.3, PlaneCondition.PlaneStrain)
                .FixWhere((_, py) => Math.Abs(py) < 1e-12, x: false, y: true)
                .FixWhere((px, _) => Math.Abs(px) < 1e-12, x: true, y: false)
                .AddPressure((px, py) => Math.Abs(Math.Sqrt((px * px) + (py * py)) - A) < 1e-9, P)
                .Solve();

            double worst = 0;

            for (int node = 0; node < mesh.NodeCount; node++)
            {
                double r = Math.Sqrt((mesh.X(node) * mesh.X(node)) + (mesh.Y(node) * mesh.Y(node)));

                if (Math.Abs(r - A) > 1e-9)
                    continue;

                double radialDisplacement = ((solution.DisplacementX(node) * mesh.X(node)) + (solution.DisplacementY(node) * mesh.Y(node))) / r;
                worst = Math.Max(worst, Math.Abs(radialDisplacement - exact) / exact);
            }

            return worst;
        }

        double coarse = Error(8, 16, out _);
        double fine = Error(16, 32, out ElasticitySolution tube);

        Assert.True(fine < 0.01, $"ошибка перемещения {fine}");
        Assert.True(coarse / fine > 3, $"порядок сходимости: {coarse} → {fine}");

        // Равновесие четверти: реакции на оси симметрии x = 0 уравновешивают давление, p·a на единицу длины
        double reaction = Enumerable.Range(0, tube.Mesh.NodeCount)
            .Where(n => Math.Abs(tube.Mesh.X(n)) < 1e-12)
            .Sum(n => tube.ReactionX(n));

        Assert.Equal(-P * A, reaction, 1e-6 * P * A);
    }

    [Fact]
    public void PlateWithHole_ConcentratesStressNearlyThreefold()
    {
        const double S = 1e6;
        TriangularMesh mesh = TriangularMesh.QuarterPlateWithHole(1, 10, 32, 48, grading: 1.12);

        ElasticitySolution solution = new ElasticityProblem(mesh, 70e9, 0.33)
            .FixWhere((_, py) => Math.Abs(py) < 1e-9, x: false, y: true)
            .FixWhere((px, _) => Math.Abs(px) < 1e-9, x: true, y: false)
            .AddTraction((px, _) => Math.Abs(px - 10) < 1e-9, (_, _) => (S, 0))
            .Solve();

        int top = Enumerable.Range(0, mesh.NodeCount).Single(n => Math.Abs(mesh.X(n)) < 1e-9 && Math.Abs(mesh.Y(n) - 1) < 1e-9);
        int side = Enumerable.Range(0, mesh.NodeCount).Single(n => Math.Abs(mesh.X(n) - 1) < 1e-9 && Math.Abs(mesh.Y(n)) < 1e-9);

        double concentration = solution.NodalStress(top).Xx / S;
        double compression = solution.NodalStress(side).Yy / S;

        Assert.InRange(concentration, 2.8, 3.3);
        Assert.InRange(compression, -1.2, -0.8);
        Assert.Contains(solution.Interpret().Warnings, w => w.Contains("концентраторах", StringComparison.Ordinal));
    }

    [Fact]
    public void Elasticity_RefusesAnUnrestrainedBody()
    {
        TriangularMesh mesh = TriangularMesh.Rectangle(new Grid2D(0, 1, 0, 1, 3, 3));

        _ = Assert.Throws<InvalidOperationException>(() => new ElasticityProblem(mesh, 1e9, 0.3).FixX(0).FixX(1).FixX(2).Solve());
    }

    #endregion

    #region Навье — Стокс

    // Гиа, Гиа, Шин (1982), Re = 100: u на вертикали x = 0,5 и v на горизонтали y = 0,5
    private static readonly (double Y, double U)[] GhiaU =
    [
        (0.9766, 0.84123), (0.9688, 0.78871), (0.9609, 0.73722), (0.9531, 0.68717), (0.8516, 0.23151),
        (0.7344, 0.00332), (0.6172, -0.13641), (0.5000, -0.20581), (0.4531, -0.21090), (0.2813, -0.15662),
        (0.1719, -0.10150), (0.1016, -0.06434), (0.0703, -0.04775), (0.0625, -0.04192), (0.0547, -0.03717)
    ];

    private static readonly (double X, double V)[] GhiaV =
    [
        (0.9688, -0.05906), (0.9609, -0.07391), (0.9531, -0.08864), (0.9453, -0.10313), (0.9063, -0.16914),
        (0.8594, -0.22445), (0.8047, -0.24533), (0.5000, 0.05454), (0.2344, 0.17527), (0.2266, 0.17507),
        (0.1563, 0.16077), (0.0938, 0.12317), (0.0781, 0.10890), (0.0703, 0.10091), (0.0625, 0.09233)
    ];

    [Fact]
    public void Cavity_AtRe100_MatchesGhiaGhiaShin()
    {
        CavityFlowSolution flow = LidDrivenCavity.Solve(100, nodes: 41, tolerance: 1e-5);

        Assert.True(flow.Converged);

        double worstU = GhiaU.Max(p => Math.Abs(flow.VelocityXAt(0.5, p.Y) - p.U));
        double worstV = GhiaV.Max(p => Math.Abs(flow.VelocityYAt(p.X, 0.5) - p.V));

        Assert.True(worstU < 0.02, $"u: {worstU}");
        Assert.True(worstV < 0.02, $"v: {worstV}");

        (double vx, double vy, double psi) = flow.PrimaryVortex;
        Assert.Equal(0.6172, vx, 0.05);
        Assert.Equal(0.7344, vy, 0.05);
        Assert.Equal(-0.103423, psi, 0.005);

        // Через вертикальное сечение жидкость не уходит: поток туда и обратно равен
        const int Points = 400;
        double flux = 0;

        for (int k = 0; k <= Points; k++)
            flux += (k == 0 || k == Points ? 0.5 : 1) * flow.VelocityXAt(0.5, (double)k / Points) / Points;

        Assert.True(Math.Abs(flux) < 2e-3, $"поток через сечение {flux}");
    }

    [Fact]
    public void Cavity_StokesLimit_IsMirrorSymmetric()
    {
        // При Re → 0 инерции нет, и течение симметрично относительно вертикали x = 0,5;
        // при Re = 1 асимметрия ещё около процента — инерция не пренебрежима
        CavityFlowSolution flow = LidDrivenCavity.Solve(0.01, nodes: 25, tolerance: 1e-6);

        Assert.True(flow.Converged);

        foreach ((double x, double y) in new[] { (0.2, 0.5), (0.3, 0.8), (0.1, 0.3) })
        {
            Assert.Equal(flow.VelocityXAt(x, y), flow.VelocityXAt(1 - x, y), 1e-3);
            Assert.Equal(flow.VelocityYAt(x, y), -flow.VelocityYAt(1 - x, y), 1e-3);
        }

        (double vx, _, _) = flow.PrimaryVortex;
        Assert.Equal(0.5, vx, 0.05);
    }

    #endregion
}
