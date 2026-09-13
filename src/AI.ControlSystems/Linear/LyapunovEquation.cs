using System;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Уравнения Ляпунова: непрерывное <c>AX + XAᵀ + Q = 0</c> и дискретное <c>AXAᵀ − X + Q = 0</c>.
/// </summary>
/// <remarks>
/// <para>
/// Для устойчивой A решение — матрица ковариации установившегося процесса под белым шумом Q,
/// а при Q &gt; 0 положительная определённость решения равносильна устойчивости. На нём стоят
/// законы адаптации MRAC и оценки шума в замкнутой системе.
/// </para>
/// <para>
/// Уравнение переписывается в линейную систему для вектора из столбцов X через произведение
/// Кронекера и решается LU-разложением из <c>AI.ClassicMath</c>. Это O(n⁶), поэтому порядок
/// ограничен тридцатью; для больших систем нужен метод Бартельса — Стюарта.
/// </para>
/// </remarks>
public static class LyapunovEquation
{
    /// <summary>Наибольший порядок системы</summary>
    public const int MaxOrder = 30;

    /// <summary>Решает AX + XAᵀ + Q = 0</summary>
    /// <param name="a">A (n×n); решение единственно, если нет пар собственных значений с λᵢ + λⱼ = 0</param>
    /// <param name="q">Q (n×n)</param>
    public static Matrix SolveContinuous(Matrix a, Matrix q)
    {
        int n = Validate(a, q);
        var system = new Matrix(n * n, n * n);

        // Строка (j', i') соответствует элементу X[i', j'], столбец (j, i) — X[i, j]
        for (int jp = 0; jp < n; jp++)
            for (int ip = 0; ip < n; ip++)
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        double value = 0;
                        if (jp == j)
                            value += a[ip, i];
                        if (ip == i)
                            value += a[jp, j];
                        system[(jp * n) + ip, (j * n) + i] = value;
                    }

        return Solve(system, q, n, "у A есть собственные значения λᵢ + λⱼ = 0");
    }

    /// <summary>Решает AXAᵀ − X + Q = 0</summary>
    /// <param name="a">A (n×n); решение единственно, если нет пар собственных значений с λᵢλⱼ = 1</param>
    /// <param name="q">Q (n×n)</param>
    public static Matrix SolveDiscrete(Matrix a, Matrix q)
    {
        int n = Validate(a, q);
        var system = new Matrix(n * n, n * n);

        for (int jp = 0; jp < n; jp++)
            for (int ip = 0; ip < n; ip++)
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        double value = a[jp, j] * a[ip, i];
                        if (jp == j && ip == i)
                            value -= 1;
                        system[(jp * n) + ip, (j * n) + i] = value;
                    }

        return Solve(system, q, n, "у A есть собственные значения λᵢλⱼ = 1");
    }

    private static Matrix Solve(Matrix system, Matrix q, int n, string reason)
    {
        var rhs = new Vector(n * n);

        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                rhs[(j * n) + i] = -q[i, j];

        Vector solution;

        try
        {
            solution = LU.Solve(system, rhs);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException($"Уравнение Ляпунова не имеет единственного решения: {reason}.");
        }

        var x = new Matrix(n, n);

        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                x[i, j] = solution[(j * n) + i];

        return ControlLinAlg.Symmetrize(x);
    }

    private static int Validate(Matrix a, Matrix q)
    {
        if (a == null || q == null)
            throw new ArgumentNullException();

        int n = a.Height;

        if (!a.IsSquared || q.Height != n || q.Width != n)
            throw new ArgumentException("A и Q должны быть квадратными одного порядка.");

        if (n > MaxOrder)
            throw new ArgumentException($"Порядок {n} больше {MaxOrder}: решение через произведение Кронекера слишком дорого.");

        return n;
    }
}
