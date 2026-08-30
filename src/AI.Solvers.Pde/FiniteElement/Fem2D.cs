using AI.DataStructs.Algebraic;
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

    /// <summary>Краткое описание сетки</summary>
    public override string ToString() => $"треугольная сетка: узлов {NodeCount}, треугольников {TriangleCount}";
}

/// <summary>Решение двумерной задачи методом конечных элементов</summary>
public sealed class Fem2DSolution
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
