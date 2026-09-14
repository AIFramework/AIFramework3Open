#nullable enable
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Units;

namespace AI.Physics.Continuum.Structures;

/// <summary>
/// Плоская рама в плоскости xz (z вверх) методом жесткостей: элементы Эйлера-Бернулли работают на растяжение,
/// сжатие и изгиб, по три степени свободы на узел. Балка есть рама из элементов на одной линии.
/// </summary>
/// <remarks>
/// <para>
/// Все величины в СИ. Равномерная нагрузка на элемент переводится в эквивалентные узловые силы, а усилия
/// в элементе считаются с ее учетом, поэтому эпюра моментов точна внутри элемента, а не только в узлах.
/// </para>
/// <para>
/// Устойчивость в плоскости считается по всей раме: продольные силы дают согласованную геометрическую жесткость
/// K_G, и наименьший множитель нагрузки λ, при котором det(K + λ·K_G) = 0, находится из обобщенной симметричной
/// задачи (<see cref="Eigen.GeneralizedSymmetric"/>). Перемещения элемента аппроксимируются кубическим многочленом,
/// поэтому для точности сжатый стержень стоит разбить на несколько элементов: один элемент консоли завышает
/// критическую силу на 0,8 %, а четыре элемента меньше чем на 0,01 %.
/// </para>
/// <para>
/// Из плоскости каждый сжатый элемент проверяется по Эйлеру (<see cref="Beams.EulerBucklingLoad"/>) с приведенной
/// длиной по закреплению его концов: заделка держит поворот, опора или соседний элемент держат сдвиг, свободный
/// конец консоли не держит ничего. Сдвиговой податливости, пластичности и больших перемещений нет.
/// </para>
/// </remarks>
public sealed class PlaneFrame
{
    private readonly List<(double X, double Z, FrameRestraint Restraint)> _nodes = [];
    private readonly List<Element> _elements = [];
    private readonly Dictionary<int, FrameNodeValues> _loads = [];

    private enum End
    {
        Free,
        Pinned,
        Fixed,
    }

    /// <summary>Число узлов</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>Число элементов</summary>
    public int ElementCount => _elements.Count;

    /// <summary>Добавляет узел</summary>
    /// <param name="x">Координата по горизонтали, м</param>
    /// <param name="z">Координата по вертикали, м</param>
    /// <param name="restraint">Закрепления</param>
    /// <returns>Номер узла</returns>
    public int AddNode(double x, double z, FrameRestraint restraint = FrameRestraint.None)
    {
        _nodes.Add((x, z, restraint));
        return _nodes.Count - 1;
    }

    /// <summary>Добавляет элемент</summary>
    /// <param name="from">Узел начала</param>
    /// <param name="to">Узел конца</param>
    /// <param name="section">Сечение</param>
    /// <param name="material">Материал: берется модуль Юнга</param>
    /// <param name="load">Равномерная нагрузка вниз (против оси z), Н/м</param>
    /// <param name="projected">Нагрузка на метр горизонтальной проекции, как снег на наклонной кровле; иначе на метр элемента</param>
    /// <returns>Номер элемента</returns>
    public int AddElement(int from, int to, FrameSection section, ElasticMaterial material, double load = 0, bool projected = false)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(material);
        CheckNode(from, nameof(from));
        CheckNode(to, nameof(to));

        if (!(section.Area > 0 && section.SecondMoment > 0 && section.SectionModulus > 0))
            throw new ArgumentOutOfRangeException(nameof(section), "Площадь, момент инерции и момент сопротивления должны быть положительными");

        _elements.Add(new Element(from, to, section, material, load, projected));
        return _elements.Count - 1;
    }

    /// <summary>Добавляет сосредоточенную нагрузку в узле; нагрузки в одном узле складываются</summary>
    /// <param name="node">Узел</param>
    /// <param name="x">Сила по x, Н</param>
    /// <param name="z">Сила по z, Н</param>
    /// <param name="moment">Момент против часовой стрелки, Н·м</param>
    public void AddLoad(int node, double x, double z, double moment = 0)
    {
        CheckNode(node, nameof(node));
        FrameNodeValues current = _loads.GetValueOrDefault(node);
        _loads[node] = new FrameNodeValues(current.X + x, current.Z + z, current.Rotational + moment);
    }

    /// <summary>Решает раму</summary>
    /// <exception cref="InvalidOperationException">Рама изменяема или перемещения слишком велики для линейного расчета</exception>
    public FrameSolution Solve()
    {
        int size = _nodes.Count * 3;
        var stiffness = new Matrix(size, size);
        var loads = new double[size];

        foreach ((int node, FrameNodeValues load) in _loads)
        {
            loads[3 * node] += load.X;
            loads[(3 * node) + 1] += load.Z;
            loads[(3 * node) + 2] += load.Rotational;
        }

        var geometry = _elements.Select(Geometry).ToList();
        for (int e = 0; e < _elements.Count; e++)
            Assemble(stiffness, geometry[e].Local, geometry[e], _elements[e].From, _elements[e].To, loads, geometry[e].Equivalent);

        (Vector displacement, HashSet<int> fixedDofs) = StiffnessSystem.Solve(
            stiffness,
            loads,
            dof => _nodes[dof / 3].Restraint.HasFlag((FrameRestraint)(1 << (dof % 3))),
            dof => $"в узле {dof / 3} {(dof % 3) switch { 0 => "по x", 1 => "по z", _ => "по повороту" }}",
            Span(),
            dof => dof % 3 != 2,
            "рама");

        // Реакция опоры: то, что элементы не уравновесили в закрепленной степени свободы
        double Reaction(int dof) => fixedDofs.Contains(dof)
            ? Enumerable.Range(0, size).Sum(c => stiffness[dof, c] * displacement[c]) - loads[dof]
            : 0;

        var elements = _elements.Select((element, e) => Forces(element, geometry[e], displacement, EndsOf(element))).ToList();
        var reactions = Enumerable.Range(0, _nodes.Count)
            .Select(node => new FrameNodeValues(Reaction(3 * node), Reaction((3 * node) + 1), Reaction((3 * node) + 2)))
            .ToList();
        var moved = Enumerable.Range(0, _nodes.Count)
            .Select(node => new FrameNodeValues(displacement[3 * node], displacement[(3 * node) + 1], displacement[(3 * node) + 2]))
            .ToList();

        return new FrameSolution(moved, reactions, elements, InPlaneBuckling(stiffness, geometry, elements, fixedDofs));
    }

    /// <summary>
    /// Устойчивость рамы в ее плоскости: −K_G·φ = μ·K·φ по свободным степеням свободы, масштабированным по диагонали K;
    /// λ = 1/μ наибольшего положительного μ. Если сжатых элементов или положительных μ нет, бесконечность
    /// </summary>
    private double InPlaneBuckling(Matrix stiffness, List<ElementGeometry> geometry, List<FrameElementForces> forces, HashSet<int> fixedDofs)
    {
        if (forces.All(force => force.AxialStart + force.AxialEnd >= 0))
            return double.PositiveInfinity;

        int size = _nodes.Count * 3;
        var geometric = new Matrix(size, size);

        for (int e = 0; e < _elements.Count; e++)
        {
            double axial = (forces[e].AxialStart + forces[e].AxialEnd) / 2;
            Assemble(geometric, GeometricStiffness(axial, geometry[e].Length), geometry[e], _elements[e].From, _elements[e].To, null, null);
        }

        int[] free = Enumerable.Range(0, size).Where(dof => !fixedDofs.Contains(dof)).ToArray();
        if (free.Length == 0)
            return double.PositiveInfinity;

        double[] scale = free.Select(dof => 1 / Math.Sqrt(stiffness[dof, dof])).ToArray();
        var demand = new Matrix(free.Length, free.Length);
        var capacity = new Matrix(free.Length, free.Length);

        for (int row = 0; row < free.Length; row++)
        {
            for (int column = 0; column < free.Length; column++)
            {
                demand[row, column] = -geometric[free[row], free[column]] * scale[row] * scale[column];
                capacity[row, column] = stiffness[free[row], free[column]] * scale[row] * scale[column];
            }
        }

        (Vector values, _) = Eigen.GeneralizedSymmetric(demand, capacity, EigenOrder.Descending);
        return values[0] > 1e-12 ? 1 / values[0] : double.PositiveInfinity;
    }

    /// <summary>
    /// Геометрическая жесткость балочного элемента в его осях от продольной силы N (растяжение плюс): согласованная
    /// матрица N/(30L)·[36, 3L, 4L², ...] по поперечным сдвигам и поворотам
    /// </summary>
    private static double[,] GeometricStiffness(double axial, double length)
    {
        double f = axial / (30 * length);
        (double a, double b, double c, double d) = (36 * f, 3 * length * f, 4 * length * length * f, length * length * f);

        return new double[6, 6]
        {
            { 0, 0, 0, 0, 0, 0 },
            { 0, a, b, 0, -a, b },
            { 0, b, c, 0, -b, -d },
            { 0, 0, 0, 0, 0, 0 },
            { 0, -a, -b, 0, a, -b },
            { 0, b, -d, 0, -b, c },
        };
    }

    /// <summary>Матрица элемента в глобальных осях добавляется в общую, а эквивалентные силы нагрузки в правую часть</summary>
    private static void Assemble(Matrix target, double[,] local, ElementGeometry geometry, int from, int to, double[]? loads, double[]? equivalent)
    {
        double[,] global = Global(local, geometry.Cos, geometry.Sin);
        double[]? turned = equivalent is null ? null : Rotate(equivalent, geometry.Cos, geometry.Sin, toGlobal: true);
        int[] dofs = Dofs(from, to);

        for (int r = 0; r < 6; r++)
        {
            if (loads is not null && turned is not null)
                loads[dofs[r]] += turned[r];

            for (int c = 0; c < 6; c++)
                target[dofs[r], dofs[c]] += global[r, c];
        }
    }

    /// <summary>
    /// Усилия элемента: в его осях f = k·d − эквивалентные силы нагрузки; момент вдоль него M(x) = −f₃ + f₂·x + q·x²/2
    /// квадратичен, и его наибольший модуль берется на концах или в вершине параболы
    /// </summary>
    private static FrameElementForces Forces(Element element, ElementGeometry geometry, Vector displacement, ColumnEnds ends)
    {
        int[] dofs = Dofs(element.From, element.To);
        double[] local = Rotate(dofs.Select(dof => displacement[dof]).ToArray(), geometry.Cos, geometry.Sin, toGlobal: false);
        var end = new double[6];

        for (int r = 0; r < 6; r++)
            end[r] = Enumerable.Range(0, 6).Sum(c => geometry.Local[r, c] * local[c]) - geometry.Equivalent[r];

        double length = geometry.Length;
        double q = geometry.Across;
        (double momentStart, double shear) = (-end[2], end[1]);
        double Moment(double x) => momentStart + (shear * x) + (q * x * x / 2);

        double maxMoment = Math.Max(Math.Abs(Moment(0)), Math.Abs(Moment(length)));
        if (q != 0 && -shear / q is double vertex && vertex > 0 && vertex < length)
            maxMoment = Math.Max(maxMoment, Math.Abs(Moment(vertex)));

        (double axialStart, double axialEnd) = (-end[0], end[3]);
        double maxAxial = Math.Max(Math.Abs(axialStart), Math.Abs(axialEnd));
        double compression = Math.Max(Math.Max(-axialStart, -axialEnd), 0);
        double buckling = element.Section.WeakSecondMoment is { } weak && weak > 0 && compression > 0
            ? Beams.EulerBucklingLoad(
                element.Material,
                new Quantity(weak, Beams.SecondMomentDimension),
                new Quantity(length, Dimension.LengthDim),
                ends).SiValue / compression
            : double.PositiveInfinity;

        return new FrameElementForces(
            length,
            axialStart,
            axialEnd,
            shear,
            momentStart,
            end[5],
            q,
            maxMoment,
            (maxAxial / element.Section.Area) + (maxMoment / element.Section.SectionModulus),
            ends,
            buckling);
    }

    private ElementGeometry Geometry(Element element)
    {
        (double dx, double dz) = (_nodes[element.To].X - _nodes[element.From].X, _nodes[element.To].Z - _nodes[element.From].Z);
        double length = Math.Sqrt((dx * dx) + (dz * dz));

        if (!(length > 0))
            throw new InvalidOperationException($"Элемент {element.From}-{element.To} нулевой длины");

        (double cos, double sin) = (dx / length, dz / length);
        double modulus = element.Material.YoungModulus.SiValue;
        (double ea, double ei) = (modulus * element.Section.Area, modulus * element.Section.SecondMoment);
        (double a, double b, double c, double d) = (ea / length, 12 * ei / (length * length * length), 6 * ei / (length * length), ei / length);
        var local = new double[6, 6]
        {
            { a, 0, 0, -a, 0, 0 },
            { 0, b, c, 0, -b, c },
            { 0, c, 4 * d, 0, -c, 2 * d },
            { -a, 0, 0, a, 0, 0 },
            { 0, -b, -c, 0, b, -c },
            { 0, c, 2 * d, 0, -c, 4 * d },
        };

        // Нагрузка вниз по z в осях элемента: вдоль него и поперек; на проекцию она реже на длине наклонного элемента
        double intensity = element.Projected ? element.Load * Math.Abs(cos) : element.Load;
        (double along, double across) = (-intensity * sin, -intensity * cos);
        double[] equivalent =
        [
            along * length / 2, across * length / 2, across * length * length / 12,
            along * length / 2, across * length / 2, -across * length * length / 12,
        ];

        return new ElementGeometry(length, cos, sin, local, equivalent, across);
    }

    /// <summary>Закрепление концов элемента для устойчивости из плоскости</summary>
    private ColumnEnds EndsOf(Element element)
    {
        End Kind(int node) => _nodes[node].Restraint.HasFlag(FrameRestraint.Rotation) ? End.Fixed
            : _nodes[node].Restraint != FrameRestraint.None || _elements.Count(other => other.From == node || other.To == node) > 1 ? End.Pinned
            : End.Free;

        return (Kind(element.From), Kind(element.To)) switch
        {
            (End.Fixed, End.Fixed) => ColumnEnds.FixedFixed,
            (End.Fixed, End.Free) or (End.Free, End.Fixed) => ColumnEnds.FixedFree,
            (End.Fixed, End.Pinned) or (End.Pinned, End.Fixed) => ColumnEnds.FixedPinned,
            _ => ColumnEnds.PinnedPinned,
        };
    }

    /// <summary>Размер рамы: диагональ охватывающего узлы прямоугольника, м</summary>
    private double Span() => _nodes.Count == 0
        ? 0
        : Math.Sqrt(Math.Pow(_nodes.Max(n => n.X) - _nodes.Min(n => n.X), 2) + Math.Pow(_nodes.Max(n => n.Z) - _nodes.Min(n => n.Z), 2));

    private static int[] Dofs(int from, int to) => [3 * from, (3 * from) + 1, (3 * from) + 2, 3 * to, (3 * to) + 1, (3 * to) + 2];

    /// <summary>Матрица элемента в глобальных осях: Tᵀ·k·T</summary>
    private static double[,] Global(double[,] local, double cos, double sin)
    {
        var global = new double[6, 6];

        for (int c = 0; c < 6; c++)
        {
            double[] column = Rotate(Enumerable.Range(0, 6).Select(r => local[r, c]).ToArray(), cos, sin, toGlobal: true);
            for (int r = 0; r < 6; r++)
                global[r, c] = column[r];
        }

        for (int r = 0; r < 6; r++)
        {
            double[] row = Rotate(Enumerable.Range(0, 6).Select(c => global[r, c]).ToArray(), cos, sin, toGlobal: true);
            for (int c = 0; c < 6; c++)
                global[r, c] = row[c];
        }

        return global;
    }

    /// <summary>Поворот вектора из шести компонент между осями элемента и глобальными: силы и сдвиги поворачиваются, момент нет</summary>
    private static double[] Rotate(double[] vector, double cos, double sin, bool toGlobal)
    {
        double s = toGlobal ? -sin : sin;

        return
        [
            (cos * vector[0]) + (s * vector[1]), (-s * vector[0]) + (cos * vector[1]), vector[2],
            (cos * vector[3]) + (s * vector[4]), (-s * vector[3]) + (cos * vector[4]), vector[5],
        ];
    }

    private void CheckNode(int node, string name)
    {
        if (node < 0 || node >= _nodes.Count)
            throw new ArgumentOutOfRangeException(name, $"Узла {node} нет");
    }

    private sealed record Element(int From, int To, FrameSection Section, ElasticMaterial Material, double Load, bool Projected);

    /// <summary>Геометрия и жесткость элемента; Across есть поперечная составляющая нагрузки в осях элемента, Н/м</summary>
    private sealed record ElementGeometry(double Length, double Cos, double Sin, double[,] Local, double[] Equivalent, double Across);
}
