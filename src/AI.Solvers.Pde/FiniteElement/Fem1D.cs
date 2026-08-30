using AI.DataStructs.Algebraic;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteElement;

/// <summary>Род краевого условия</summary>
public enum BoundaryKind
{
    /// <summary>Задано значение решения</summary>
    Dirichlet,

    /// <summary>Задан поток через границу</summary>
    Neumann
}

/// <summary>Краевое условие на конце отрезка</summary>
/// <param name="Kind">Род условия</param>
/// <param name="Value">Значение решения либо поток</param>
public readonly record struct BoundaryCondition(BoundaryKind Kind, double Value)
{
    /// <summary>Условие первого рода: задано значение</summary>
    /// <param name="value">Значение решения на границе</param>
    public static BoundaryCondition Fixed(double value) => new(BoundaryKind.Dirichlet, value);

    /// <summary>Условие второго рода: задан поток</summary>
    /// <param name="flux">Поток через границу</param>
    public static BoundaryCondition Flux(double flux) => new(BoundaryKind.Neumann, flux);
}

/// <summary>Решение одномерной краевой задачи методом конечных элементов</summary>
public sealed class Fem1DSolution
{
    internal Fem1DSolution(Grid1D mesh, Vector values, int iterations, bool converged)
    {
        Mesh = mesh;
        Values = values;
        Iterations = iterations;
        Converged = converged;
    }

    /// <summary>Сетка узлов</summary>
    public Grid1D Mesh { get; }

    /// <summary>Значения решения в узлах</summary>
    public Vector Values { get; }

    /// <summary>Число итераций решателя системы</summary>
    public int Iterations { get; }

    /// <summary>Сошёлся ли решатель системы</summary>
    public bool Converged { get; }

    /// <summary>
    /// Значение решения в произвольной точке — линейная интерполяция по элементу,
    /// то есть ровно то представление, в котором задача и решалась
    /// </summary>
    /// <param name="x">Координата</param>
    public double Evaluate(double x)
    {
        double left = Mesh.Left;
        double step = Mesh.Step;

        if (x <= left)
            return Values[0];

        if (x >= Mesh.Right)
            return Values[Mesh.Count - 1];

        int element = Math.Min((int)((x - left) / step), Mesh.Count - 2);
        double local = (x - Mesh.Node(element)) / step;

        return ((1 - local) * Values[element]) + (local * Values[element + 1]);
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => $"МКЭ 1D: узлов {Mesh.Count}, итераций {Iterations}";
}

/// <summary>
/// Метод конечных элементов для задачи <c>−(k·u′)′ + c·u = f</c> на отрезке.
/// </summary>
/// <remarks>
/// <para>
/// Линейные элементы: решение ищется как ломаная, непрерывная в узлах. Матрица жёсткости
/// и матрица масс собираются поэлементно, коэффициенты <c>k</c> и <c>c</c> берутся
/// в середине элемента, нагрузка — по правилу трапеций.
/// </para>
/// <para>
/// Отличие от конечных разностей не в точности на равномерной сетке — там схемы совпадают, —
/// а в том, что метод формулируется через интеграл и потому естественно принимает переменные
/// коэффициенты, условия второго рода и неравномерные сетки.
/// </para>
/// </remarks>
public static class Fem1D
{
    /// <summary>
    /// Решает краевую задачу
    /// </summary>
    /// <param name="mesh">Сетка узлов</param>
    /// <param name="conductivity">Коэффициент <c>k(x)</c> при второй производной</param>
    /// <param name="reaction">Коэффициент <c>c(x)</c> при решении</param>
    /// <param name="source">Правая часть <c>f(x)</c></param>
    /// <param name="left">Условие на левом конце</param>
    /// <param name="right">Условие на правом конце</param>
    public static Fem1DSolution Solve(
        Grid1D mesh,
        Func<double, double> conductivity,
        Func<double, double> reaction,
        Func<double, double> source,
        BoundaryCondition left,
        BoundaryCondition right)
    {
        mesh.Validate();
        ArgumentNullException.ThrowIfNull(conductivity);
        ArgumentNullException.ThrowIfNull(reaction);
        ArgumentNullException.ThrowIfNull(source);

        int nodes = mesh.Count;
        double h = mesh.Step;

        var matrix = new SparseMatrix(nodes, nodes);
        var rightHandSide = new Vector(nodes);

        for (int element = 0; element < nodes - 1; element++)
        {
            double middle = 0.5 * (mesh.Node(element) + mesh.Node(element + 1));
            double k = conductivity(middle);
            double c = reaction(middle);

            // Жёсткость: k/h · [[1, −1], [−1, 1]]
            matrix.Add(element, element, k / h);
            matrix.Add(element, element + 1, -k / h);
            matrix.Add(element + 1, element, -k / h);
            matrix.Add(element + 1, element + 1, k / h);

            // Масса: c·h/6 · [[2, 1], [1, 2]]
            matrix.Add(element, element, c * h / 3.0);
            matrix.Add(element, element + 1, c * h / 6.0);
            matrix.Add(element + 1, element, c * h / 6.0);
            matrix.Add(element + 1, element + 1, c * h / 3.0);

            // Нагрузка по правилу трапеций
            rightHandSide[element] += source(mesh.Node(element)) * h / 2.0;
            rightHandSide[element + 1] += source(mesh.Node(element + 1)) * h / 2.0;
        }

        // Условия второго рода входят в правую часть как поток через границу
        if (left.Kind == BoundaryKind.Neumann)
            rightHandSide[0] += left.Value;

        if (right.Kind == BoundaryKind.Neumann)
            rightHandSide[nodes - 1] += right.Value;

        if (left.Kind == BoundaryKind.Dirichlet)
            matrix.EliminateKnown(0, left.Value, rightHandSide);

        if (right.Kind == BoundaryKind.Dirichlet)
            matrix.EliminateKnown(nodes - 1, right.Value, rightHandSide);

        IterativeResult result = IterativeSolvers.ConjugateGradient(matrix, rightHandSide);

        return new Fem1DSolution(mesh, result.Solution, result.Iterations, result.Converged);
    }
}
