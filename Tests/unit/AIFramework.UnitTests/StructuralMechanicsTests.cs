using AI.Geometry.Primitives;
using AI.Physics.Continuum;
using AI.Physics.Continuum.Structures;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Метод жесткостей для ферм и рам сверяется с равновесием узлов и с замкнутыми формулами <see cref="Beams"/>:
/// прогибы консоли и балки на двух опорах, критическая сила Эйлера с приведенной длиной. Изменяемые конструкции
/// должны распознаваться, а не давать бессмысленные перемещения.
/// </summary>
public class StructuralMechanicsTests
{
    private const double E = 2.1e11;

    private static readonly ElasticMaterial Steel = ElasticMaterial.FromYoungAndPoisson(new Quantity(E, Dimension.Pressure), 0.3);

    private static Quantity Length(double meters) => new(meters, Dimension.LengthDim);

    private static Quantity Inertia(double value) => new(value, Beams.SecondMomentDimension);

    #region Ферма

    [Fact]
    public void Truss_Solve_TwoBarsCarryApexLoadInCompression()
    {
        var truss = new Truss();
        int left = truss.AddNode(new Vector3(-1, 0, 0), TrussSupport.All);
        int right = truss.AddNode(new Vector3(1, 0, 0), TrussSupport.All);
        int apex = truss.AddNode(new Vector3(0, 0, 1));
        truss.AddMember(left, apex, Steel, 1e-3);
        truss.AddMember(right, apex, Steel, 1e-3);
        truss.AddLoad(apex, new Vector3(0, 0, -1000));

        TrussSolution solution = truss.Solve();

        // Стержни под 45°: каждый сжат силой P/(2·sin 45°), опоры держат по половине
        Assert.All(solution.Forces, force => Assert.Equal(-1000 / (2 * Math.Sin(Math.PI / 4)), force, 1e-6));
        Assert.Equal(500, solution.Reactions[left].Z, 1e-6);
        Assert.Equal(500, solution.Reactions[right].Z, 1e-6);
        Assert.True(solution.SafetyFactor(2.5e8) > 100);
    }

    [Fact]
    public void Truss_Solve_AsymmetricTwoBarsMatchJointEquilibrium()
    {
        var truss = new Truss();
        int a = truss.AddNode(new Vector3(0, 0, 0), TrussSupport.All);
        int b = truss.AddNode(new Vector3(4, 0, 0), TrussSupport.All);
        int c = truss.AddNode(new Vector3(1, 0, 2));
        truss.AddMember(a, c, Steel, 5e-4);
        truss.AddMember(b, c, Steel, 8e-4);
        const double p = 12_000;
        truss.AddLoad(c, new Vector3(0, 0, -p));

        TrussSolution solution = truss.Solve();

        // Узел C: T_a·u_a + T_b·u_b = (0, 0, P), где u есть единичные векторы от C к опорам
        (double ax, double az) = (-1 / Math.Sqrt(5), -2 / Math.Sqrt(5));
        (double bx, double bz) = (3 / Math.Sqrt(13), -2 / Math.Sqrt(13));
        double det = (ax * bz) - (bx * az);
        double ta = (0 * bz - bx * p) / det;
        double tb = (ax * p - az * 0) / det;

        Assert.Equal(ta, solution.Forces[0], 1e-6);
        Assert.Equal(tb, solution.Forces[1], 1e-6);
        Assert.Equal(ta / 5e-4, solution.Stresses[0], 1e-2);

        // Реакции уравновешивают нагрузку
        Vector3 total = solution.Reactions.Aggregate(Vector3.Zero, (sum, r) => sum + r);
        Assert.Equal(0, total.X, 1e-6);
        Assert.Equal(p, total.Z, 1e-6);

        // Работа нагрузки равна энергии деформации стержней: P·|w| = Σ N²·L/(E·A)
        double strain = (ta * ta * Math.Sqrt(5) / (E * 5e-4)) + (tb * tb * Math.Sqrt(13) / (E * 8e-4));
        Assert.Equal(strain, p * -solution.Displacements[c].Z, strain * 1e-9);
    }

    [Fact]
    public void Truss_Solve_SlenderStrutIsLimitedByEulerBuckling()
    {
        var truss = new Truss();
        int left = truss.AddNode(new Vector3(-3, 0, 0), TrussSupport.All);
        int right = truss.AddNode(new Vector3(3, 0, 0), TrussSupport.All);
        int apex = truss.AddNode(new Vector3(0, 0, 0.5));
        truss.AddMember(left, apex, Steel, 1e-4, secondMoment: 1e-9);
        truss.AddMember(right, apex, Steel, 1e-4, secondMoment: 1e-9);
        truss.AddLoad(apex, new Vector3(0, 0, -1000));

        TrussSolution solution = truss.Solve();

        double length = Math.Sqrt(9.25);
        double euler = Beams.EulerBucklingLoad(Steel, Inertia(1e-9), Length(length), ColumnEnds.PinnedPinned).SiValue;
        Assert.Equal(euler / -solution.Forces[0], solution.BucklingFactors[0], 1e-9);
        Assert.Equal(solution.BucklingFactors.Min(), solution.SafetyFactor(2.5e8));
    }

    [Fact]
    public void Truss_Solve_SkewedQuadIsRecognizedAsMechanism()
    {
        var truss = new Truss();
        int a = truss.AddNode(new Vector3(0, 0, 0), TrussSupport.All);
        int b = truss.AddNode(new Vector3(2, 0, 0), TrussSupport.Y | TrussSupport.Z);
        int c = truss.AddNode(new Vector3(2.5, 0, 1), TrussSupport.Y);
        int d = truss.AddNode(new Vector3(0.3, 0, 1.2), TrussSupport.Y);
        truss.AddMember(a, b, Steel, 1e-3);
        truss.AddMember(b, c, Steel, 1e-3);
        truss.AddMember(c, d, Steel, 1e-3);
        truss.AddMember(d, a, Steel, 1e-3);
        truss.AddLoad(c, new Vector3(1000, 0, 0));

        Assert.Throws<InvalidOperationException>(() => truss.Solve());
    }

    [Fact]
    public void Truss_Solve_LoadAcrossPlanarTrussIsNotSwallowed()
    {
        var truss = new Truss();
        int left = truss.AddNode(new Vector3(-1, 0, 0), TrussSupport.All);
        int right = truss.AddNode(new Vector3(1, 0, 0), TrussSupport.All);
        int apex = truss.AddNode(new Vector3(0, 0, 1));
        truss.AddMember(left, apex, Steel, 1e-3);
        truss.AddMember(right, apex, Steel, 1e-3);
        truss.AddLoad(apex, new Vector3(0, 500, 0));

        Assert.Throws<InvalidOperationException>(() => truss.Solve());
    }

    #endregion

    #region Рама

    [Fact]
    public void PlaneFrame_Solve_CantileverTipLoadGivesTextbookDeflectionAndMoment()
    {
        var frame = new PlaneFrame();
        var section = new FrameSection(1e-3, 8e-6, 8e-5);
        int root = frame.AddNode(0, 0, FrameRestraint.Clamp);
        int tip = frame.AddNode(2, 0);
        frame.AddElement(root, tip, section, Steel);
        frame.AddLoad(tip, 0, -1000);

        FrameSolution solution = frame.Solve();

        double deflection = Beams.CantileverTipDeflection(new Quantity(1000, Dimension.Force), Length(2), Steel, Inertia(8e-6)).SiValue;
        Assert.Equal(-deflection, solution.Displacements[tip].Z, 1e-12);
        Assert.Equal(-2000, solution.Elements[0].MomentStart, 1e-6);
        Assert.Equal(2000, solution.Elements[0].MaxMoment, 1e-6);
        Assert.Equal(1000, solution.Reactions[root].Z, 1e-6);
        Assert.Equal(2000, solution.Reactions[root].Rotational, 1e-6);
        Assert.Equal(2000 / 8e-5, solution.Elements[0].MaxStress, 1e-3);
    }

    [Fact]
    public void PlaneFrame_Solve_SimplySupportedUniformLoadGivesTextbookMomentAndSag()
    {
        var frame = new PlaneFrame();
        var section = new FrameSection(1e-3, 8e-6, 8e-5);
        int left = frame.AddNode(0, 0, FrameRestraint.Pin);
        int middle = frame.AddNode(2, 0);
        int right = frame.AddNode(4, 0, FrameRestraint.Z);
        frame.AddElement(left, middle, section, Steel, load: 1000);
        frame.AddElement(middle, right, section, Steel, load: 1000);

        FrameSolution solution = frame.Solve();

        double sag = Beams.SimplySupportedUniformDeflection(new Quantity(1000, Dimension.Force / Dimension.LengthDim), Length(4), Steel, Inertia(8e-6)).SiValue;
        Assert.Equal(-sag, solution.Displacements[middle].Z, 1e-12);
        Assert.Equal(1000 * 16 / 8.0, solution.Elements.Max(element => element.MaxMoment), 1e-6);
        Assert.Equal(2000, solution.Reactions[left].Z, 1e-6);
        Assert.Equal(2000, solution.Reactions[right].Z, 1e-6);
        Assert.True(solution.SafetyFactor(2.5e8) > 1);
    }

    [Fact]
    public void PlaneFrame_Solve_MomentInsideSingleElementIsExact()
    {
        // Один элемент на пролет: узлов в середине нет, но эпюра внутри элемента учитывает нагрузку
        var frame = new PlaneFrame();
        int left = frame.AddNode(0, 0, FrameRestraint.Pin);
        int right = frame.AddNode(6, 0, FrameRestraint.Z);
        frame.AddElement(left, right, FrameSection.Rectangle(0.1, 0.3), Steel, load: 2000);

        FrameElementForces element = frame.Solve().Elements[0];

        Assert.Equal(2000 * 36 / 8.0, element.MomentAt(3), 1e-6);
        Assert.Equal(2000 * 36 / 8.0, element.MaxMoment, 1e-6);
        Assert.Equal(0, element.MomentStart, 1e-6);
        Assert.Equal(0, element.MomentEnd, 1e-6);

        var diagram = element.MomentDiagram(5);
        Assert.Equal(2000 * 1.5 * 4.5 / 2, diagram[1], 1e-6);
    }

    [Fact]
    public void PlaneFrame_Solve_ClampedColumnBucklesAtEulerLoad()
    {
        // Консольная колонна 3 м: критическая сила π²·E·I/(2L)²
        FrameSection section = FrameSection.Rectangle(0.1, 0.2);
        double euler = Beams.EulerBucklingLoad(Steel, Inertia(section.SecondMoment), Length(3), ColumnEnds.FixedFree).SiValue;

        foreach ((int pieces, double tolerance) in new[] { (1, 0.01), (4, 1e-4) })
        {
            var frame = new PlaneFrame();
            int previous = frame.AddNode(0, 0, FrameRestraint.Clamp);

            for (int i = 1; i <= pieces; i++)
            {
                int next = frame.AddNode(0, 3.0 * i / pieces);
                frame.AddElement(previous, next, section, Steel);
                previous = next;
            }

            frame.AddLoad(previous, 0, -1e5);
            FrameSolution solution = frame.Solve();

            Assert.Equal(1, solution.InPlaneBuckling * 1e5 / euler, tolerance);
        }
    }

    [Fact]
    public void PlaneFrame_Solve_ColumnUnderMeganewtonIsUnsafeByWeakAxis()
    {
        // По прочности запас около 5, а по слабой оси критическая сила 0,96 МН меньше действующей
        var frame = new PlaneFrame();
        int root = frame.AddNode(0, 0, FrameRestraint.Clamp);
        int top = frame.AddNode(0, 3);
        frame.AddElement(root, top, FrameSection.Rectangle(0.1, 0.2), Steel);
        frame.AddLoad(top, 0, -1e6);

        FrameSolution solution = frame.Solve();

        Assert.Equal(ColumnEnds.FixedFree, solution.Elements[0].Ends);
        Assert.Equal(2.0, solution.Elements[0].EffectiveLengthFactor);
        Assert.True(solution.Elements[0].OutOfPlaneBuckling < 1);
        Assert.True(solution.SafetyFactor(2.5e8) < 1, $"запас {solution.SafetyFactor(2.5e8):0.##}");
    }

    [Theory]
    [InlineData(FrameRestraint.Clamp, ColumnEnds.PinnedPinned)]
    [InlineData(FrameRestraint.Pin, ColumnEnds.FixedFree)]
    public void PlaneFrame_Solve_PortalWithStiffBeamBucklesWithSwayEffectiveLength(FrameRestraint bases, ColumnEnds equivalent)
    {
        // Портальная рама с очень жестким ригелем теряет устойчивость со сдвигом: у защемленных стоек верх не
        // поворачивается, и приведенная длина равна длине (K = 1, как у шарнирного стержня); у шарнирных
        // стоек K = 2, как у консоли
        const double height = 4;
        const double span = 4;
        var column = new FrameSection(5e-3, 2e-5, 2e-4);
        var beam = new FrameSection(5e-3, 2e-2, 2e-1);
        var frame = new PlaneFrame();

        int Column(double x)
        {
            int previous = frame.AddNode(x, 0, bases);
            for (int i = 1; i <= 4; i++)
            {
                int next = frame.AddNode(x, height * i / 4);
                frame.AddElement(previous, next, column, Steel);
                previous = next;
            }

            return previous;
        }

        int leftTop = Column(0);
        int rightTop = Column(span);
        frame.AddElement(leftTop, rightTop, beam, Steel);
        frame.AddLoad(leftTop, 0, -1e5);
        frame.AddLoad(rightTop, 0, -1e5);

        FrameSolution solution = frame.Solve();

        double euler = Beams.EulerBucklingLoad(Steel, Inertia(column.SecondMoment), Length(height), equivalent).SiValue;
        Assert.Equal(1, solution.InPlaneBuckling * 1e5 / euler, 0.01);
    }

    [Fact]
    public void PlaneFrame_Solve_BeamOnSinglePinIsRecognizedAsMechanism()
    {
        var frame = new PlaneFrame();
        int left = frame.AddNode(0, 0, FrameRestraint.Pin);
        int right = frame.AddNode(3, 0);
        frame.AddElement(left, right, FrameSection.Rectangle(0.1, 0.2), Steel);
        frame.AddLoad(right, 0, -1000);

        Assert.Throws<InvalidOperationException>(() => frame.Solve());
    }

    [Fact]
    public void PlaneFrame_Solve_TensionOnlyHasNoInPlaneBuckling()
    {
        var frame = new PlaneFrame();
        int root = frame.AddNode(0, 3, FrameRestraint.Clamp);
        int bottom = frame.AddNode(0, 0);
        frame.AddElement(root, bottom, FrameSection.Rectangle(0.05, 0.05), Steel);
        frame.AddLoad(bottom, 0, -5000);

        FrameSolution solution = frame.Solve();

        Assert.True(double.IsPositiveInfinity(solution.InPlaneBuckling));
        Assert.Equal(5000, solution.Elements[0].AxialStart, 1e-6);
    }

    #endregion
}
