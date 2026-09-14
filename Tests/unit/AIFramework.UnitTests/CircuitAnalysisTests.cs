using AI.Physics.Electromagnetism;
using AI.Physics.Electromagnetism.CircuitAnalysis;
using AI.Units;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Узловой анализ цепи постоянного тока сверяется с законом Ома (<see cref="Circuits"/>), с формулой Тевенена
/// для моста Уитстона и с балансом мощности. Конденсатор в установившемся режиме есть разрыв, катушка есть перемычка.
/// </summary>
public class CircuitAnalysisTests
{
    [Fact]
    public void DcCircuit_Solve_SeriesDividerSplitsVoltageAndCarriesOhmsCurrent()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int middle = circuit.AddNode();
        int source = circuit.AddVoltageSource(top, DcCircuit.Ground, 12);
        int upper = circuit.AddResistor(top, middle, 4);
        circuit.AddResistor(middle, DcCircuit.Ground, 2);

        DcCircuitSolution solution = circuit.Solve();
        double ohm = Circuits.Current(Quantity.Of(12, "V"), Quantity.Of(6, "Ohm")).SiValue;

        Assert.Equal(4, solution.NodeVoltages[middle], 1e-12);
        Assert.Equal(ohm, solution.BranchCurrents[upper], 1e-12);

        // Ток источника течет через него от минуса к плюсу: в соглашении ветвей он отрицателен, мощность отдается
        Assert.Equal(-ohm, solution.BranchCurrents[source], 1e-12);
        Assert.Equal(16, solution.BranchPowers[upper], 1e-12);
        Assert.Equal(24, solution.SuppliedPower, 1e-12);
        Assert.Equal(24, solution.DissipatedPower, 1e-12);
    }

    [Fact]
    public void DcCircuit_Solve_ParallelResistorsDrawSumOfCurrents()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int source = circuit.AddVoltageSource(top, DcCircuit.Ground, 12);
        circuit.AddResistor(top, DcCircuit.Ground, 6);
        circuit.AddResistor(top, DcCircuit.Ground, 6);

        Assert.Equal(-4, circuit.Solve().BranchCurrents[source], 1e-12);
    }

    [Fact]
    public void DcCircuit_Solve_CurrentSourceDrivesVoltageAcrossLoad()
    {
        var circuit = new DcCircuit();
        int node = circuit.AddNode();
        int source = circuit.AddCurrentSource(DcCircuit.Ground, node, 1);
        circuit.AddResistor(node, DcCircuit.Ground, 10);

        DcCircuitSolution solution = circuit.Solve();

        Assert.Equal(10, solution.NodeVoltages[node], 1e-12);
        Assert.Equal(-10, solution.BranchPowers[source], 1e-12);
        Assert.Equal(10, solution.DissipatedPower, 1e-12);
    }

    [Fact]
    public void DcCircuit_Solve_BalancedWheatstoneBridgeCarriesNoCurrent()
    {
        (DcCircuitSolution solution, int bridge) = Wheatstone(100, 200, 150, 300, 50, 10);

        Assert.Equal(0, solution.BranchCurrents[bridge], 1e-12);
    }

    [Fact]
    public void DcCircuit_Solve_UnbalancedWheatstoneBridgeMatchesThevenin()
    {
        const double v = 10, r1 = 100, r2 = 220, r3 = 330, r4 = 150, r5 = 47;
        (DcCircuitSolution solution, int bridge) = Wheatstone(r1, r2, r3, r4, r5, v);

        // Тевенин относительно диагонали моста: U = V·(R2/(R1 + R2) − R4/(R3 + R4)), R = R1‖R2 + R3‖R4
        double open = v * ((r2 / (r1 + r2)) - (r4 / (r3 + r4)));
        double inner = (r1 * r2 / (r1 + r2)) + (r3 * r4 / (r3 + r4));
        Assert.Equal(open / (inner + r5), solution.BranchCurrents[bridge], 1e-12);
        Assert.Equal(solution.SuppliedPower, solution.DissipatedPower, 1e-12);
    }

    [Fact]
    public void DcCircuit_Solve_InductorIsShortAndCarriesLoopCurrent()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int middle = circuit.AddNode();
        circuit.AddVoltageSource(top, DcCircuit.Ground, 5);
        circuit.AddResistor(top, middle, 10);
        int coil = circuit.AddInductor(middle, DcCircuit.Ground, 0.1);
        int shunt = circuit.AddResistor(middle, DcCircuit.Ground, 1000);

        DcCircuitSolution solution = circuit.Solve();

        Assert.Equal(0.5, solution.BranchCurrents[coil], 1e-12);
        Assert.Equal(0, solution.BranchVoltages[coil]);
        Assert.Equal(0, solution.BranchCurrents[shunt], 1e-12);
        Assert.Equal(0, solution.BranchPowers[coil]);
    }

    [Fact]
    public void DcCircuit_Solve_CapacitorIsOpenAndHoldsDividerVoltage()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int middle = circuit.AddNode();
        int end = circuit.AddNode();
        circuit.AddVoltageSource(top, DcCircuit.Ground, 9);
        circuit.AddResistor(top, middle, 1000);
        circuit.AddResistor(middle, DcCircuit.Ground, 2000);
        int parallel = circuit.AddCapacitor(middle, DcCircuit.Ground, 1e-6);

        // Последовательная цепочка с конденсатором тока не пропускает: за ней полный потенциал узла
        circuit.AddResistor(middle, end, 500);
        int series = circuit.AddCapacitor(end, DcCircuit.Ground, 1e-6);
        circuit.AddResistor(end, DcCircuit.Ground, 1e9);

        DcCircuitSolution solution = circuit.Solve();

        double divider = 9 * 2000 / 3000.0;
        Assert.Equal(divider, solution.BranchVoltages[parallel], 1e-5);
        Assert.Equal(0, solution.BranchCurrents[parallel]);
        Assert.Equal(solution.NodeVoltages[end], solution.BranchVoltages[series], 1e-12);
        Assert.Equal(DcElementKind.Capacitor, circuit.KindOf(series));
    }

    [Fact]
    public void DcCircuit_Solve_NodeBehindCapacitorIsFloating()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int isolated = circuit.AddNode();
        int fed = circuit.AddNode();
        circuit.AddVoltageSource(top, DcCircuit.Ground, 5);
        circuit.AddResistor(top, DcCircuit.Ground, 10);
        circuit.AddCapacitor(top, isolated, 1e-6);
        circuit.AddCurrentSource(DcCircuit.Ground, fed, 1e-3);

        Assert.Equal(new[] { isolated, fed }, circuit.FloatingNodes());
        var error = Assert.Throws<InvalidOperationException>(() => circuit.Solve());
        Assert.Contains(isolated.ToString(), error.Message);
    }

    [Fact]
    public void DcCircuit_Solve_ConflictingVoltageSourcesThrow()
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        circuit.AddVoltageSource(top, DcCircuit.Ground, 5);
        circuit.AddVoltageSource(top, DcCircuit.Ground, 6);
        circuit.AddResistor(top, DcCircuit.Ground, 10);

        Assert.Throws<InvalidOperationException>(() => circuit.Solve());
    }

    private static (DcCircuitSolution Solution, int Bridge) Wheatstone(double r1, double r2, double r3, double r4, double r5, double v)
    {
        var circuit = new DcCircuit();
        int top = circuit.AddNode();
        int a = circuit.AddNode();
        int b = circuit.AddNode();
        circuit.AddVoltageSource(top, DcCircuit.Ground, v);
        circuit.AddResistor(top, a, r1);
        circuit.AddResistor(a, DcCircuit.Ground, r2);
        circuit.AddResistor(top, b, r3);
        circuit.AddResistor(b, DcCircuit.Ground, r4);
        int bridge = circuit.AddResistor(a, b, r5);

        return (circuit.Solve(), bridge);
    }
}
