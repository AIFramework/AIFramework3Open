using System.Text;

namespace AI.Solvers.Math.Core.Solvers;

// Численный решатель PDE методом конечных разностей
public static partial class NumericalPDESolver
{
    // Решение волнового уравнения u_tt = c²·u_xx
    public static string SolveWaveEquationNumerical(double c, double T = 2.0, int nx = 50, int nt = 200)
    {
        double dx = 1.0 / (nx - 1);
        double dt = T / nt;
        double r  = c * dt / dx;

        if (r > 1.0)
            return $"ОШИБКА: Условие устойчивости нарушено! CFL = {r:F4} > 1.0\nУменьшите dt или увеличьте nx.";

        double[] uOld = new double[nx];
        double[] u    = new double[nx];
        double[] uNew = new double[nx];

        for (int i = 0; i < nx; i++)
            uOld[i] = System.Math.Sin(2 * System.Math.PI * i * dx);
        uOld[0] = uOld[nx - 1] = 0;

        // Первый шаг считается отдельно: при u_t(x,0) = 0 разложение Тейлора даёт
        // u¹ = u⁰ + ½r²·δ²u⁰. Если же стартовать с uOld = u и общей трёхслойной
        // формулы, первое приращение удваивается и схема теряет второй порядок.
        for (int i = 1; i < nx - 1; i++)
            u[i] = uOld[i] + 0.5 * r * r * (uOld[i + 1] - 2 * uOld[i] + uOld[i - 1]);
        u[0] = u[nx - 1] = 0;

        for (int n = 1; n < nt; n++)
        {
            for (int i = 1; i < nx - 1; i++)
                uNew[i] = 2 * u[i] - uOld[i] + r * r * (u[i + 1] - 2 * u[i] + u[i - 1]);

            uNew[0] = 0; uNew[nx - 1] = 0;
            Array.Copy(u, uOld, nx);
            Array.Copy(uNew, u, nx);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ ВОЛНОВОГО УРАВНЕНИЯ ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: u_tt = {c * c}·u_xx  (c = {c})");
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx} точек, {nt} шагов по времени");
        result.AppendLine($"Число Куранта: CFL = {r:F4} < 1.0");
        result.AppendLine();
        result.AppendLine("Начальное условие: u(x, 0) = sin(2π·x), u_t(x, 0) = 0");
        result.AppendLine("Граничные условия: u(0, t) = u(1, t) = 0");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine();
        for (int i = 0; i < nx; i += 5)
            result.AppendLine($"  x = {i * dx:F3}: u = {u[i]:F6}");
        return result.ToString();
    }

    // Решение уравнения теплопроводности u_t = α·u_xx
    public static string SolveHeatEquationNumerical(double alpha, double T = 0.1, int nx = 50, int nt = 1000)
    {
        double dx = 1.0 / (nx - 1);
        double dt = T / nt;
        double r  = alpha * dt / (dx * dx);

        if (r > 0.5)
            return $"ОШИБКА: Условие устойчивости нарушено! r = {r:F4} > 0.5\nУменьшите dt или увеличьте nx.";

        double[] u    = new double[nx];
        double[] uNew = new double[nx];

        for (int i = 0; i < nx; i++)
            u[i] = System.Math.Sin(System.Math.PI * i * dx);
        u[0] = u[nx - 1] = 0;

        for (int n = 0; n < nt; n++)
        {
            for (int i = 1; i < nx - 1; i++)
                uNew[i] = u[i] + r * (u[i - 1] - 2 * u[i] + u[i + 1]);
            uNew[0] = uNew[nx - 1] = 0;
            Array.Copy(uNew, u, nx);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ ТЕПЛОПРОВОДНОСТИ ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: u_t = {alpha}·u_xx");
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx} точек, {nt} шагов по времени");
        result.AppendLine($"Параметр устойчивости: r = {r:F4} < 0.5");
        result.AppendLine();
        result.AppendLine("Начальное условие: u(x, 0) = sin(π·x)");
        result.AppendLine("Граничные условия: u(0, t) = u(1, t) = 0");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine();
        for (int i = 0; i < nx; i += 5)
            result.AppendLine($"  x = {i * dx:F3}: u = {u[i]:F6}");
        return result.ToString();
    }
}
