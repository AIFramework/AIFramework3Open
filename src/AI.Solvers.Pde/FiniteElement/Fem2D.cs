using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteElement;

/// <summary>
/// Треугольная сетка: узлы, треугольники и признак принадлежности узла границе
/// </summary>
public sealed class TriangularMesh
{
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly int[][] _triangles;
    private readonly bool[] _boundary;

    internal TriangularMesh(double[] x, double[] y, int[][] triangles, bool[] boundary)
    {
        _x = x;
        _y = y;
        _triangles = triangles;
        _boundary = boundary;
    }

    /// <summary>Число узлов</summary>
    public int NodeCount => _x.Length;

    /// <summary>Число треугольников</summary>
    public int TriangleCount => _triangles.Length;

    /// <summary>Координата x узла</summary>
    /// <param name="node">Номер узла</param>
    public double X(int node) => _x[node];

    /// <summary>Координата y узла</summary>
    /// <param name="node">Номер узла</param>
    public double Y(int node) => _y[node];

    /// <summary>Узлы треугольника</summary>
    /// <param name="triangle">Номер треугольника</param>
    public IReadOnlyList<int> Triangle(int triangle) => _triangles[triangle];

    /// <summary>Лежит ли узел на границе</summary>
    /// <param name="node">Номер узла</param>
    public bool IsBoundary(int node) => _boundary[node];

    /// <summary>
    /// Разбивает прямоугольник на треугольники: каждая ячейка структурированной
    /// сетки делится диагональю надвое
    /// </summary>
    /// <param name="grid">Прямоугольная сетка</param>
    public static TriangularMesh Rectangle(Grid2D grid)
    {
        grid.Validate();

        int count = grid.NodeCount;
        var x = new double[count];
        var y = new double[count];
        var boundary = new bool[count];

        for (int j = 0; j < grid.CountY; j++)
        {
            for (int i = 0; i < grid.CountX; i++)
            {
                int index = grid.Index(i, j);

                x[index] = grid.X(i);
                y[index] = grid.Y(j);
                boundary[index] = grid.IsBoundary(i, j);
            }
        }

        var triangles = new List<int[]>((grid.CountX - 1) * (grid.CountY - 1) * 2);

        for (int j = 0; j < grid.CountY - 1; j++)
        {
            for (int i = 0; i < grid.CountX - 1; i++)
            {
                int bottomLeft = grid.Index(i, j);
                int bottomRight = grid.Index(i + 1, j);
                int topLeft = grid.Index(i, j + 1);
                int topRight = grid.Index(i + 1, j + 1);

                triangles.Add([bottomLeft, bottomRight, topRight]);
                triangles.Add([bottomLeft, topRight, topLeft]);
            }
        }

        return new TriangularMesh(x, y, triangles.ToArray(), boundary);
    }

    /// <summary>
    /// Сетка из произвольных узлов и треугольников; граничные узлы определяются по рёбрам,
    /// принадлежащим ровно одному треугольнику
    /// </summary>
    /// <param name="x">Координаты x узлов</param>
    /// <param name="y">Координаты y узлов</param>
    /// <param name="triangles">Треугольники — тройки номеров узлов</param>
    public static TriangularMesh Create(IReadOnlyList<double> x, IReadOnlyList<double> y, IReadOnlyList<IReadOnlyList<int>> triangles)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(triangles);

        if (x.Count == 0 || x.Count != y.Count)
            throw new ArgumentException("Координат x и y должно быть поровну, хотя бы по одной", nameof(y));

        var elements = new int[triangles.Count][];

        for (int e = 0; e < triangles.Count; e++)
        {
            IReadOnlyList<int> t = triangles[e];

            if (t is null || t.Count != 3 || t.Any(n => n < 0 || n >= x.Count) || t.Distinct().Count() != 3)
                throw new ArgumentException($"Треугольник {e} должен ссылаться на три разных существующих узла", nameof(triangles));

            double doubleArea = ((x[t[1]] - x[t[0]]) * (y[t[2]] - y[t[0]])) - ((x[t[2]] - x[t[0]]) * (y[t[1]] - y[t[0]]));

            if (Math.Abs(doubleArea) < 1e-14)
                throw new ArgumentException($"Треугольник {e} вырожден: его площадь нулевая", nameof(triangles));

            elements[e] = [t[0], t[1], t[2]];
        }

        return new TriangularMesh(x.ToArray(), y.ToArray(), elements, BoundaryNodes(x.Count, elements));
    }

    /// <summary>Четверть кольца a ≤ r ≤ b, 0 ≤ θ ≤ 90° — для задач Ламе о трубе под давлением</summary>
    /// <param name="innerRadius">Внутренний радиус</param>
    /// <param name="outerRadius">Наружный радиус</param>
    /// <param name="radialDivisions">Число элементов по радиусу</param>
    /// <param name="angularDivisions">Число элементов по углу</param>
    public static TriangularMesh QuarterAnnulus(double innerRadius, double outerRadius, int radialDivisions, int angularDivisions)
    {
        if (!(innerRadius > 0 && outerRadius > innerRadius))
            throw new ArgumentOutOfRangeException(nameof(outerRadius), "Нужны 0 < a < b");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radialDivisions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(angularDivisions);

        return Structured(radialDivisions + 1, angularDivisions + 1, (i, j) =>
        {
            double r = innerRadius + ((outerRadius - innerRadius) * i / radialDivisions);
            double angle = Math.PI / 2 * j / angularDivisions;

            return j == angularDivisions ? (0, r) : (r * Math.Cos(angle), r * Math.Sin(angle));
        });
    }

    /// <summary>
    /// Четверть квадратной пластины со стороной 2W и круглым отверстием радиуса a в центре —
    /// для задачи Кирша; узлы сгущаются к отверстию с заданным знаменателем прогрессии
    /// </summary>
    /// <param name="holeRadius">Радиус отверстия a</param>
    /// <param name="halfWidth">Половина стороны пластины W</param>
    /// <param name="radialDivisions">Число элементов от отверстия до края</param>
    /// <param name="angularDivisions">Число элементов по углу; чётное, чтобы угол пластины был узлом</param>
    /// <param name="grading">Отношение размеров соседних элементов по радиусу; 1 — равномерно</param>
    public static TriangularMesh QuarterPlateWithHole(
        double holeRadius, double halfWidth, int radialDivisions, int angularDivisions, double grading = 1.0)
    {
        if (!(holeRadius > 0 && halfWidth > holeRadius))
            throw new ArgumentOutOfRangeException(nameof(halfWidth), "Нужны 0 < a < W");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radialDivisions);

        if (angularDivisions < 2 || angularDivisions % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(angularDivisions), "Число элементов по углу — чётное, не меньше двух");

        if (!(grading >= 1))
            throw new ArgumentOutOfRangeException(nameof(grading), "Сгущение задаётся знаменателем не меньше единицы");

        return Structured(radialDivisions + 1, angularDivisions + 1, (i, j) =>
        {
            double angle = Math.PI / 2 * j / angularDivisions;
            (double X, double Y) inner = j == angularDivisions ? (0, holeRadius) : (holeRadius * Math.Cos(angle), holeRadius * Math.Sin(angle));
            (double X, double Y) outer = j * 2 == angularDivisions ? (halfWidth, halfWidth)
                : j == angularDivisions ? (0, halfWidth)
                : j * 2 < angularDivisions ? (halfWidth, halfWidth * Math.Tan(angle))
                : (halfWidth / Math.Tan(angle), halfWidth);

            double s = grading == 1
                ? (double)i / radialDivisions
                : (Math.Pow(grading, i) - 1) / (Math.Pow(grading, radialDivisions) - 1);

            return (inner.X + (s * (outer.X - inner.X)), inner.Y + (s * (outer.Y - inner.Y)));
        });
    }

    /// <summary>
    /// Рёбра границы — принадлежащие ровно одному треугольнику, — ориентированные так, что область
    /// лежит слева: внешняя нормаль ребра из (x₁, y₁) в (x₂, y₂) направлена по (y₂ − y₁, x₁ − x₂)
    /// </summary>
    public IReadOnlyList<(int From, int To)> BoundaryEdges()
    {
        if (_boundaryEdges is not null)
            return _boundaryEdges;

        var owners = new Dictionary<(int, int), (int From, int To, int Count)>();

        foreach (int[] t in _triangles)
        {
            double doubleArea = ((_x[t[1]] - _x[t[0]]) * (_y[t[2]] - _y[t[0]])) - ((_x[t[2]] - _x[t[0]]) * (_y[t[1]] - _y[t[0]]));

            for (int k = 0; k < 3; k++)
            {
                int a = t[k], b = t[(k + 1) % 3];

                if (doubleArea < 0)
                    (a, b) = (b, a);

                var key = a < b ? (a, b) : (b, a);
                owners[key] = owners.TryGetValue(key, out var seen) ? (seen.From, seen.To, seen.Count + 1) : (a, b, 1);
            }
        }

        _boundaryEdges = owners.Values.Where(e => e.Count == 1).Select(e => (e.From, e.To)).ToList();

        return _boundaryEdges;
    }

    private IReadOnlyList<(int From, int To)>? _boundaryEdges;

    private static TriangularMesh Structured(int countI, int countJ, Func<int, int, (double X, double Y)> point)
    {
        var x = new double[countI * countJ];
        var y = new double[countI * countJ];

        for (int j = 0; j < countJ; j++)
        {
            for (int i = 0; i < countI; i++)
                (x[(j * countI) + i], y[(j * countI) + i]) = point(i, j);
        }

        var triangles = new List<int[]>((countI - 1) * (countJ - 1) * 2);

        for (int j = 0; j < countJ - 1; j++)
        {
            for (int i = 0; i < countI - 1; i++)
            {
                int p00 = (j * countI) + i, p10 = p00 + 1, p01 = p00 + countI, p11 = p01 + 1;

                foreach (int[] t in new[] { new[] { p00, p10, p11 }, new[] { p00, p11, p01 } })
                {
                    double doubleArea = ((x[t[1]] - x[t[0]]) * (y[t[2]] - y[t[0]])) - ((x[t[2]] - x[t[0]]) * (y[t[1]] - y[t[0]]));
                    triangles.Add(doubleArea >= 0 ? t : [t[0], t[2], t[1]]);
                }
            }
        }

        int[][] elements = triangles.ToArray();

        return new TriangularMesh(x, y, elements, BoundaryNodes(x.Length, elements));
    }

    private static bool[] BoundaryNodes(int count, int[][] triangles)
    {
        var edges = new Dictionary<(int, int), int>();

        foreach (int[] t in triangles)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = t[k], b = t[(k + 1) % 3];
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.GetValueOrDefault(key) + 1;
            }
        }

        var boundary = new bool[count];

        foreach (((int a, int b), int uses) in edges)
        {
            if (uses != 1)
                continue;

            boundary[a] = true;
            boundary[b] = true;
        }

        return boundary;
    }

    /// <summary>Краткое описание сетки</summary>
    public override string ToString() => $"треугольная сетка: узлов {NodeCount}, треугольников {TriangleCount}";
}

/// <summary>Решение двумерной задачи методом конечных элементов</summary>
public sealed class Fem2DSolution : IInterpretable
{
    internal Fem2DSolution(TriangularMesh mesh, Vector values, int iterations, bool converged)
    {
        Mesh = mesh;
        Values = values;
        Iterations = iterations;
        Converged = converged;
    }

    /// <summary>Сетка</summary>
    public TriangularMesh Mesh { get; }

    /// <summary>Значения решения в узлах</summary>
    public Vector Values { get; }

    /// <summary>Число итераций решателя системы</summary>
    public int Iterations { get; }

    /// <summary>Сошёлся ли решатель системы</summary>
    public bool Converged { get; }

    /// <summary>
    /// Решение в виде матрицы для структурированной сетки: строка — постоянное y
    /// </summary>
    /// <param name="grid">Сетка, по которой построена триангуляция</param>
    public Matrix ToMatrix(Grid2D grid)
    {
        var values = new Matrix(grid.CountY, grid.CountX);

        for (int j = 0; j < grid.CountY; j++)
            for (int i = 0; i < grid.CountX; i++)
                values[j, i] = Values[grid.Index(i, j)];

        return values;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (double min, double max) = PdeFacts.Range(Values);
        int boundary = 0;

        for (int node = 0; node < Mesh.NodeCount; node++)
            if (Mesh.IsBoundary(node))
                boundary++;

        return new InterpretationBuilder("Метод конечных элементов на плоской области")
            .Summary($"Решение на линейных треугольниках: треугольников {Mesh.TriangleCount}, узлов {Mesh.NodeCount}, "
                + $"из них на границе {boundary}; значения от {Fmt.Num(min, 4)} до {Fmt.Num(max, 4)}. "
                + (Converged
                    ? $"Система решена, итераций {Iterations}."
                    : $"Система не решена: итераций {Iterations}, порог не достигнут."))
            .Metric("Треугольников", Mesh.TriangleCount, null, "линейных элементов", MetricQuality.Unknown, 0)
            .Metric("Узлов", Mesh.NodeCount, null, null, MetricQuality.Unknown, 0)
            .Metric("Граничных узлов", boundary, null, "значения заданы условием Дирихле", MetricQuality.Unknown, 0)
            .Metric("Итераций", Iterations, null, "шагов метода сопряжённых градиентов", MetricQuality.Unknown, 0)
            .Metric("Сходимость", Converged ? "достигнута" : "не достигнута", null, "решатель системы достиг порога",
                Converged ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Минимум", min, null, null, MetricQuality.Unknown, 4)
            .Metric("Максимум", max, null, null, MetricQuality.Unknown, 4)
            .Warning("Линейные треугольники: решение непрерывно и линейно на каждом треугольнике, а его градиент "
                + "постоянен на треугольнике и скачет на рёбрах. Точность градиента — первого порядка по размеру элемента.")
            .Warning("Нагрузка берётся по значению правой части в центре тяжести треугольника: там, где источник "
                + "резко меняется, сетку нужно мельчить именно в этом месте.")
            .WarningIf(!Converged, PdeFacts.NotConverged)
            .Recommendation(PdeFacts.RefineGrid)
            .Build();
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => $"МКЭ 2D: узлов {Mesh.NodeCount}, итераций {Iterations}";
}

/// <summary>
/// Метод конечных элементов для уравнения Пуассона <c>−Δu = f</c> на плоской области
/// с условием Дирихле на границе.
/// </summary>
/// <remarks>
/// <para>
/// Линейные элементы на треугольниках: решение — кусочно-линейная поверхность, непрерывная
/// на рёбрах. Локальная матрица жёсткости треугольника выражается через координаты вершин
/// в замкнутом виде, численное интегрирование не требуется; нагрузка берётся по значению
/// правой части в центре тяжести.
/// </para>
/// <para>
/// Треугольники, в отличие от прямоугольной сетки конечных разностей, покрывают область
/// произвольной формы. Здесь генератор строит только разбиение прямоугольника, но сборка
/// работает с любой сеткой, которую передадут в <see cref="TriangularMesh"/>.
/// </para>
/// </remarks>
public static class Fem2D
{
    /// <summary>
    /// Решает задачу Пуассона
    /// </summary>
    /// <param name="mesh">Треугольная сетка</param>
    /// <param name="source">Правая часть <c>f(x, y)</c></param>
    /// <param name="boundary">Значение решения на границе <c>g(x, y)</c></param>
    /// <param name="tolerance">Относительный порог по невязке</param>
    public static Fem2DSolution SolvePoisson(
        TriangularMesh mesh,
        Func<double, double, double> source,
        Func<double, double, double> boundary,
        double tolerance = 1e-12)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(boundary);

        int nodes = mesh.NodeCount;
        var matrix = new SparseMatrix(nodes, nodes);
        var rightHandSide = new Vector(nodes);

        for (int element = 0; element < mesh.TriangleCount; element++)
        {
            IReadOnlyList<int> triangle = mesh.Triangle(element);

            double x1 = mesh.X(triangle[0]), y1 = mesh.Y(triangle[0]);
            double x2 = mesh.X(triangle[1]), y2 = mesh.Y(triangle[1]);
            double x3 = mesh.X(triangle[2]), y3 = mesh.Y(triangle[2]);

            double doubleArea = ((x2 - x1) * (y3 - y1)) - ((x3 - x1) * (y2 - y1));
            double area = 0.5 * Math.Abs(doubleArea);

            if (area < 1e-14)
                continue;

            // Градиенты базисных функций выражаются через координаты вершин
            double[] b = [y2 - y3, y3 - y1, y1 - y2];
            double[] c = [x3 - x2, x1 - x3, x2 - x1];

            double scale = 1.0 / (4.0 * area);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                    matrix.Add(triangle[i], triangle[j], scale * ((b[i] * b[j]) + (c[i] * c[j])));
            }

            double centroidX = (x1 + x2 + x3) / 3.0;
            double centroidY = (y1 + y2 + y3) / 3.0;
            double load = source(centroidX, centroidY) * area / 3.0;

            for (int i = 0; i < 3; i++)
                rightHandSide[triangle[i]] += load;
        }

        for (int node = 0; node < nodes; node++)
        {
            if (mesh.IsBoundary(node))
                matrix.EliminateKnown(node, boundary(mesh.X(node), mesh.Y(node)), rightHandSide);
        }

        IterativeResult result = IterativeSolvers.ConjugateGradient(matrix, rightHandSide, tolerance);

        return new Fem2DSolution(mesh, result.Solution, result.Iterations, result.Converged);
    }
}
