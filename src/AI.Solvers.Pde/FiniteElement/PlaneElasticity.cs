using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteElement;

/// <summary>Вид плоской задачи теории упругости</summary>
public enum PlaneCondition
{
    /// <summary>Плоское напряжённое состояние: тонкая пластина, σ_zz = 0</summary>
    PlaneStress,

    /// <summary>Плоская деформация: длинное тело, ε_zz = 0, σ_zz = ν(σ_xx + σ_yy)</summary>
    PlaneStrain
}

/// <summary>
/// Плоская задача линейной теории упругости: сетка, материал, закрепления и нагрузки.
/// </summary>
/// <remarks>
/// <para>
/// Решается методом конечных элементов на треугольниках с линейными перемещениями (CST, «треугольник
/// постоянной деформации»): деформации и напряжения в пределах треугольника постоянны. Сетка,
/// разреженная матрица и метод сопряжённых градиентов — те же, что у <see cref="Fem2D"/>.
/// </para>
/// <para>
/// Тело должно быть закреплено от смещения и поворота как целого: хотя бы три перемещения, не все
/// вдоль одной оси. Иначе матрица жёсткости вырождена, и решение не определено.
/// </para>
/// </remarks>
public sealed class ElasticityProblem
{
    private readonly bool[] _fixed;
    private readonly double[] _prescribed;
    private readonly double[] _loads;

    /// <summary>Создаёт задачу</summary>
    /// <param name="mesh">Треугольная сетка</param>
    /// <param name="youngModulus">Модуль Юнга, Па</param>
    /// <param name="poissonRatio">Коэффициент Пуассона</param>
    /// <param name="condition">Плоское напряжённое состояние или плоская деформация</param>
    /// <param name="thickness">Толщина пластины, м; при плоской деформации — расчётная длина</param>
    public ElasticityProblem(
        TriangularMesh mesh, double youngModulus, double poissonRatio,
        PlaneCondition condition = PlaneCondition.PlaneStress, double thickness = 1.0)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (!(youngModulus > 0) || double.IsInfinity(youngModulus))
            throw new ArgumentOutOfRangeException(nameof(youngModulus), "Модуль Юнга должен быть положительным");

        if (!(poissonRatio > -1 && poissonRatio < 0.5))
            throw new ArgumentOutOfRangeException(nameof(poissonRatio), "Коэффициент Пуассона лежит строго между −1 и ½");

        if (!(thickness > 0))
            throw new ArgumentOutOfRangeException(nameof(thickness), "Толщина должна быть положительной");

        Mesh = mesh;
        YoungModulus = youngModulus;
        PoissonRatio = poissonRatio;
        Condition = condition;
        Thickness = thickness;

        _fixed = new bool[2 * mesh.NodeCount];
        _prescribed = new double[2 * mesh.NodeCount];
        _loads = new double[2 * mesh.NodeCount];
    }

    /// <summary>Сетка</summary>
    public TriangularMesh Mesh { get; }

    /// <summary>Модуль Юнга, Па</summary>
    public double YoungModulus { get; }

    /// <summary>Коэффициент Пуассона</summary>
    public double PoissonRatio { get; }

    /// <summary>Вид плоской задачи</summary>
    public PlaneCondition Condition { get; }

    /// <summary>Толщина, м</summary>
    public double Thickness { get; }

    /// <summary>Задаёт перемещение узла по x</summary>
    /// <param name="node">Узел</param>
    /// <param name="displacement">Перемещение, м</param>
    public ElasticityProblem FixX(int node, double displacement = 0) => Fix(2 * CheckNode(node), displacement);

    /// <summary>Задаёт перемещение узла по y</summary>
    /// <param name="node">Узел</param>
    /// <param name="displacement">Перемещение, м</param>
    public ElasticityProblem FixY(int node, double displacement = 0) => Fix((2 * CheckNode(node)) + 1, displacement);

    /// <summary>Закрепляет узлы, удовлетворяющие условию</summary>
    /// <param name="where">Условие по координатам узла</param>
    /// <param name="x">Запретить перемещение по x</param>
    /// <param name="y">Запретить перемещение по y</param>
    public ElasticityProblem FixWhere(Func<double, double, bool> where, bool x, bool y)
    {
        ArgumentNullException.ThrowIfNull(where);

        for (int node = 0; node < Mesh.NodeCount; node++)
        {
            if (!where(Mesh.X(node), Mesh.Y(node)))
                continue;

            if (x)
                FixX(node);

            if (y)
                FixY(node);
        }

        return this;
    }

    /// <summary>Сосредоточенная сила в узле, Н</summary>
    /// <param name="node">Узел</param>
    /// <param name="fx">Составляющая по x</param>
    /// <param name="fy">Составляющая по y</param>
    public ElasticityProblem AddForce(int node, double fx, double fy)
    {
        CheckNode(node);
        _loads[2 * node] += fx;
        _loads[(2 * node) + 1] += fy;

        return this;
    }

    /// <summary>
    /// Распределённая нагрузка на участок границы, Па: на каждое граничное ребро, оба конца которого
    /// удовлетворяют условию; нагрузка ребра делится между концами поровну
    /// </summary>
    /// <param name="onBoundary">Условие принадлежности узла нагруженному участку</param>
    /// <param name="traction">Вектор напряжения на границе в точке</param>
    public ElasticityProblem AddTraction(Func<double, double, bool> onBoundary, Func<double, double, (double Tx, double Ty)> traction)
    {
        ArgumentNullException.ThrowIfNull(onBoundary);
        ArgumentNullException.ThrowIfNull(traction);

        foreach ((int a, int b, double length, _, _) in LoadedEdges(onBoundary))
        {
            (double tx, double ty) = traction((Mesh.X(a) + Mesh.X(b)) / 2, (Mesh.Y(a) + Mesh.Y(b)) / 2);
            double share = 0.5 * length * Thickness;

            AddForce(a, tx * share, ty * share);
            AddForce(b, tx * share, ty * share);
        }

        return this;
    }

    /// <summary>Давление на участок границы, Па: действует по нормали внутрь тела</summary>
    /// <param name="onBoundary">Условие принадлежности узла нагруженному участку</param>
    /// <param name="pressure">Давление; отрицательное — отрыв</param>
    public ElasticityProblem AddPressure(Func<double, double, bool> onBoundary, double pressure)
    {
        ArgumentNullException.ThrowIfNull(onBoundary);

        foreach ((int a, int b, double length, double nx, double ny) in LoadedEdges(onBoundary))
        {
            double share = 0.5 * length * Thickness * pressure;

            AddForce(a, -nx * share, -ny * share);
            AddForce(b, -nx * share, -ny * share);
        }

        return this;
    }

    /// <summary>Объёмная сила, Н/м³ — например, вес ρg</summary>
    /// <param name="force">Объёмная сила в точке</param>
    public ElasticityProblem AddBodyForce(Func<double, double, (double Fx, double Fy)> force)
    {
        ArgumentNullException.ThrowIfNull(force);

        for (int e = 0; e < Mesh.TriangleCount; e++)
        {
            IReadOnlyList<int> t = Mesh.Triangle(e);
            double area = Math.Abs(DoubleArea(t)) / 2;
            double cx = (Mesh.X(t[0]) + Mesh.X(t[1]) + Mesh.X(t[2])) / 3;
            double cy = (Mesh.Y(t[0]) + Mesh.Y(t[1]) + Mesh.Y(t[2])) / 3;
            (double fx, double fy) = force(cx, cy);
            double share = area * Thickness / 3;

            foreach (int node in t)
                AddForce(node, fx * share, fy * share);
        }

        return this;
    }

    /// <summary>Решает задачу</summary>
    /// <param name="tolerance">Относительный порог невязки метода сопряжённых градиентов</param>
    public ElasticitySolution Solve(double tolerance = 1e-10)
    {
        int constrained = _fixed.Count(f => f);

        if (constrained < 3 || _fixed.Where((_, dof) => dof % 2 == 0).All(f => !f) || _fixed.Where((_, dof) => dof % 2 == 1).All(f => !f))
            throw new InvalidOperationException(
                "Тело не закреплено: нужно запретить смещение по обеим осям и поворот — хотя бы три перемещения, не все вдоль одной оси");

        int dofs = 2 * Mesh.NodeCount;
        double[,] d = Elasticity();
        var matrix = new SparseMatrix(dofs, dofs);
        var right = new Vector(_loads);
        var stiffness = new double[Mesh.TriangleCount][,];
        var strain = new double[Mesh.TriangleCount][,];

        for (int e = 0; e < Mesh.TriangleCount; e++)
        {
            IReadOnlyList<int> t = Mesh.Triangle(e);
            double[,] b = StrainDisplacement(t);
            double[,] k = Multiply(Transpose(b), Multiply(d, b), Math.Abs(DoubleArea(t)) / 2 * Thickness);

            strain[e] = b;
            stiffness[e] = k;

            for (int p = 0; p < 6; p++)
            {
                for (int q = 0; q < 6; q++)
                    matrix.Add(Dof(t, p), Dof(t, q), k[p, q]);
            }
        }

        for (int dof = 0; dof < dofs; dof++)
        {
            if (_fixed[dof])
                matrix.EliminateKnown(dof, _prescribed[dof], right);
        }

        matrix.Compress();

        IterativeResult result = IterativeSolvers.ConjugateGradient(matrix, right, tolerance);
        Vector u = result.Solution;

        var internalForces = new double[dofs];
        var stresses = new (double Xx, double Yy, double Xy)[Mesh.TriangleCount];

        for (int e = 0; e < Mesh.TriangleCount; e++)
        {
            IReadOnlyList<int> t = Mesh.Triangle(e);
            var local = new double[6];

            for (int p = 0; p < 6; p++)
                local[p] = u[Dof(t, p)];

            for (int p = 0; p < 6; p++)
            {
                double sum = 0;

                for (int q = 0; q < 6; q++)
                    sum += stiffness[e][p, q] * local[q];

                internalForces[Dof(t, p)] += sum;
            }

            var epsilon = new double[3];

            for (int r = 0; r < 3; r++)
            {
                for (int p = 0; p < 6; p++)
                    epsilon[r] += strain[e][r, p] * local[p];
            }

            stresses[e] = (
                (d[0, 0] * epsilon[0]) + (d[0, 1] * epsilon[1]),
                (d[1, 0] * epsilon[0]) + (d[1, 1] * epsilon[1]),
                d[2, 2] * epsilon[2]);
        }

        var reactions = new double[dofs];

        for (int dof = 0; dof < dofs; dof++)
        {
            if (_fixed[dof])
                reactions[dof] = internalForces[dof] - _loads[dof];
        }

        double energy = 0;

        for (int dof = 0; dof < dofs; dof++)
            energy += 0.5 * u[dof] * internalForces[dof];

        return new ElasticitySolution(this, u, stresses, reactions, _loads.ToArray(), energy, result.Iterations, result.Converged);
    }

    private ElasticityProblem Fix(int dof, double displacement)
    {
        if (double.IsNaN(displacement) || double.IsInfinity(displacement))
            throw new ArgumentOutOfRangeException(nameof(displacement), "Перемещение должно быть конечным");

        _fixed[dof] = true;
        _prescribed[dof] = displacement;

        return this;
    }

    private int CheckNode(int node)
        => node >= 0 && node < Mesh.NodeCount
            ? node
            : throw new ArgumentOutOfRangeException(nameof(node), $"Узла {node} в сетке из {Mesh.NodeCount} нет");

    private IEnumerable<(int A, int B, double Length, double Nx, double Ny)> LoadedEdges(Func<double, double, bool> onBoundary)
    {
        foreach ((int a, int b) in Mesh.BoundaryEdges())
        {
            if (!onBoundary(Mesh.X(a), Mesh.Y(a)) || !onBoundary(Mesh.X(b), Mesh.Y(b)))
                continue;

            double dx = Mesh.X(b) - Mesh.X(a);
            double dy = Mesh.Y(b) - Mesh.Y(a);
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            yield return (a, b, length, dy / length, -dx / length);
        }
    }

    private double[,] Elasticity()
    {
        double e = YoungModulus, nu = PoissonRatio;

        if (Condition == PlaneCondition.PlaneStress)
        {
            double c = e / (1 - (nu * nu));

            return new[,] { { c, c * nu, 0 }, { c * nu, c, 0 }, { 0, 0, c * (1 - nu) / 2 } };
        }

        double s = e / ((1 + nu) * (1 - (2 * nu)));

        return new[,] { { s * (1 - nu), s * nu, 0 }, { s * nu, s * (1 - nu), 0 }, { 0, 0, s * (1 - (2 * nu)) / 2 } };
    }

    private double DoubleArea(IReadOnlyList<int> t)
        => ((Mesh.X(t[1]) - Mesh.X(t[0])) * (Mesh.Y(t[2]) - Mesh.Y(t[0])))
            - ((Mesh.X(t[2]) - Mesh.X(t[0])) * (Mesh.Y(t[1]) - Mesh.Y(t[0])));

    // Производные линейных базисных функций: ∂Nᵢ/∂x = bᵢ/(2A), ∂Nᵢ/∂y = cᵢ/(2A) при знаковой площади
    private double[,] StrainDisplacement(IReadOnlyList<int> t)
    {
        double x1 = Mesh.X(t[0]), y1 = Mesh.Y(t[0]);
        double x2 = Mesh.X(t[1]), y2 = Mesh.Y(t[1]);
        double x3 = Mesh.X(t[2]), y3 = Mesh.Y(t[2]);

        double doubleArea = DoubleArea(t);

        if (Math.Abs(doubleArea) < 1e-14)
            throw new InvalidOperationException("В сетке есть вырожденный треугольник нулевой площади");

        double[] b = [y2 - y3, y3 - y1, y1 - y2];
        double[] c = [x3 - x2, x1 - x3, x2 - x1];
        var matrix = new double[3, 6];

        for (int i = 0; i < 3; i++)
        {
            matrix[0, 2 * i] = b[i] / doubleArea;
            matrix[1, (2 * i) + 1] = c[i] / doubleArea;
            matrix[2, 2 * i] = c[i] / doubleArea;
            matrix[2, (2 * i) + 1] = b[i] / doubleArea;
        }

        return matrix;
    }

    private static int Dof(IReadOnlyList<int> t, int local) => (2 * t[local / 2]) + (local % 2);

    private static double[,] Transpose(double[,] a)
    {
        var result = new double[a.GetLength(1), a.GetLength(0)];

        for (int i = 0; i < a.GetLength(0); i++)
            for (int j = 0; j < a.GetLength(1); j++)
                result[j, i] = a[i, j];

        return result;
    }

    private static double[,] Multiply(double[,] a, double[,] b, double scale = 1)
    {
        int rows = a.GetLength(0), inner = a.GetLength(1), columns = b.GetLength(1);
        var result = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                double sum = 0;

                for (int k = 0; k < inner; k++)
                    sum += a[i, k] * b[k, j];

                result[i, j] = sum * scale;
            }
        }

        return result;
    }
}

/// <summary>Решение плоской задачи теории упругости</summary>
public sealed class ElasticitySolution : IInterpretable
{
    private readonly Vector _displacements;
    private readonly (double Xx, double Yy, double Xy)[] _stresses;
    private readonly double[] _reactions;
    private readonly double[] _loads;
    private readonly (double Xx, double Yy, double Xy)[] _nodal;

    internal ElasticitySolution(
        ElasticityProblem problem, Vector displacements, (double Xx, double Yy, double Xy)[] stresses,
        double[] reactions, double[] loads, double strainEnergy, int iterations, bool converged)
    {
        Problem = problem;
        _displacements = displacements;
        _stresses = stresses;
        _reactions = reactions;
        _loads = loads;
        StrainEnergy = strainEnergy;
        Iterations = iterations;
        Converged = converged;
        _nodal = AverageToNodes();
    }

    /// <summary>Задача</summary>
    public ElasticityProblem Problem { get; }

    /// <summary>Сетка</summary>
    public TriangularMesh Mesh => Problem.Mesh;

    /// <summary>Энергия деформации, Дж — половина работы внешних сил</summary>
    public double StrainEnergy { get; }

    /// <summary>Число итераций метода сопряжённых градиентов</summary>
    public int Iterations { get; }

    /// <summary>Сошёлся ли решатель</summary>
    public bool Converged { get; }

    /// <summary>Перемещение узла по x, м</summary>
    /// <param name="node">Узел</param>
    public double DisplacementX(int node) => _displacements[2 * node];

    /// <summary>Перемещение узла по y, м</summary>
    /// <param name="node">Узел</param>
    public double DisplacementY(int node) => _displacements[(2 * node) + 1];

    /// <summary>Напряжения в треугольнике, Па — постоянные в его пределах</summary>
    /// <param name="element">Номер треугольника</param>
    public (double Xx, double Yy, double Xy) ElementStress(int element) => _stresses[element];

    /// <summary>Напряжения в узле, усреднённые по прилегающим треугольникам с весом площади, Па</summary>
    /// <param name="node">Узел</param>
    public (double Xx, double Yy, double Xy) NodalStress(int node) => _nodal[node];

    /// <summary>Нормальное напряжение σ_zz в треугольнике: ν(σ_xx + σ_yy) при плоской деформации, иначе нуль</summary>
    /// <param name="element">Номер треугольника</param>
    public double ElementStressZz(int element)
        => Problem.Condition == PlaneCondition.PlaneStrain
            ? Problem.PoissonRatio * (_stresses[element].Xx + _stresses[element].Yy)
            : 0;

    /// <summary>Эквивалентное напряжение по Мизесу в треугольнике, Па</summary>
    /// <param name="element">Номер треугольника</param>
    public double ElementVonMises(int element)
    {
        (double xx, double yy, double xy) = _stresses[element];
        double zz = ElementStressZz(element);

        return Math.Sqrt((0.5 * (((xx - yy) * (xx - yy)) + ((yy - zz) * (yy - zz)) + ((zz - xx) * (zz - xx)))) + (3 * xy * xy));
    }

    /// <summary>Реакция опоры узла по x, Н; нуль у свободного направления</summary>
    /// <param name="node">Узел</param>
    public double ReactionX(int node) => _reactions[2 * node];

    /// <summary>Реакция опоры узла по y, Н</summary>
    /// <param name="node">Узел</param>
    public double ReactionY(int node) => _reactions[(2 * node) + 1];

    /// <summary>Сумма реакций опор — по равновесию равна сумме внешних сил с обратным знаком</summary>
    public (double X, double Y) TotalReaction
        => (_reactions.Where((_, dof) => dof % 2 == 0).Sum(), _reactions.Where((_, dof) => dof % 2 == 1).Sum());

    /// <summary>Сумма приложенных сил</summary>
    public (double X, double Y) TotalLoad
        => (_loads.Where((_, dof) => dof % 2 == 0).Sum(), _loads.Where((_, dof) => dof % 2 == 1).Sum());

    /// <summary>Наибольшее перемещение узла, м</summary>
    public double MaxDisplacement
        => Enumerable.Range(0, Mesh.NodeCount).Max(n => Math.Sqrt((DisplacementX(n) * DisplacementX(n)) + (DisplacementY(n) * DisplacementY(n))));

    /// <summary>Наибольшее эквивалентное напряжение среди треугольников, Па</summary>
    public double MaxVonMises => Enumerable.Range(0, Mesh.TriangleCount).Max(ElementVonMises);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (double rx, double ry) = TotalReaction;
        (double lx, double ly) = TotalLoad;
        double imbalance = Math.Sqrt(((rx + lx) * (rx + lx)) + ((ry + ly) * (ry + ly)));
        double scale = Math.Max(1e-30, Math.Sqrt((lx * lx) + (ly * ly)) + Math.Sqrt((rx * rx) + (ry * ry)));

        return new InterpretationBuilder("Плоская задача теории упругости")
            .Summary($"{(Problem.Condition == PlaneCondition.PlaneStress ? "Плоское напряжённое состояние" : "Плоская деформация")}, "
                + $"треугольников {Mesh.TriangleCount}. Наибольшее перемещение {Fmt.Num(MaxDisplacement * 1e3, 4)} мм, "
                + $"наибольшее эквивалентное напряжение {Fmt.Num(MaxVonMises / 1e6, 4)} МПа, энергия деформации "
                + $"{Fmt.Num(StrainEnergy, 4)} Дж." + (Converged ? string.Empty : " Решатель системы не сошёлся."))
            .Metric("Треугольников", Mesh.TriangleCount, null, "с постоянной деформацией", MetricQuality.Unknown, 0)
            .Metric("Наибольшее перемещение", Fmt.Num(MaxDisplacement * 1e3, 4), "мм", null)
            .Metric("Наибольшее по Мизесу", Fmt.Num(MaxVonMises / 1e6, 4), "МПа", "среди треугольников")
            .Metric("Энергия деформации", Fmt.Num(StrainEnergy, 4), "Дж", "половина работы нагрузок")
            .Metric("Невязка равновесия", Fmt.Pct(imbalance / scale), null, "сумма реакций плюс сумма нагрузок",
                imbalance / scale < 1e-6 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Итераций", Iterations, null, "метода сопряжённых градиентов", MetricQuality.Unknown, 0)
            .Warning("Треугольник постоянной деформации: напряжения постоянны в элементе и скачут на рёбрах. "
                + "В концентраторах — у отверстий, углов, трещин — пик занижен, пока сетка там не измельчена.")
            .Warning("При изгибе треугольник слишком жёсток: тонкая балка из одного-двух слоёв элементов прогибается "
                + "в разы меньше, чем должна. При ν около ½ и плоской деформации жёсткость также завышается — это запирание.")
            .WarningIf(!Converged, PdeFacts.NotConverged)
            .Recommendation(PdeFacts.RefineGrid)
            .Build();
    }

    private (double Xx, double Yy, double Xy)[] AverageToNodes()
    {
        int nodes = Mesh.NodeCount;
        var sum = new (double Xx, double Yy, double Xy)[nodes];
        var weight = new double[nodes];

        for (int e = 0; e < Mesh.TriangleCount; e++)
        {
            IReadOnlyList<int> t = Mesh.Triangle(e);
            double area = Math.Abs(((Mesh.X(t[1]) - Mesh.X(t[0])) * (Mesh.Y(t[2]) - Mesh.Y(t[0])))
                - ((Mesh.X(t[2]) - Mesh.X(t[0])) * (Mesh.Y(t[1]) - Mesh.Y(t[0])))) / 2;

            foreach (int node in t)
            {
                sum[node] = (sum[node].Xx + (area * _stresses[e].Xx), sum[node].Yy + (area * _stresses[e].Yy), sum[node].Xy + (area * _stresses[e].Xy));
                weight[node] += area;
            }
        }

        for (int node = 0; node < nodes; node++)
        {
            if (weight[node] > 0)
                sum[node] = (sum[node].Xx / weight[node], sum[node].Yy / weight[node], sum[node].Xy / weight[node]);
        }

        return sum;
    }
}
