using System.Text;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class NumericalPDESolver
{
    // Решение уравнения диффузии-адвекции: u_t + c·u_x = D·u_xx
    public static string SolveDiffusionAdvectionNumerical(double c, double D, double T = 1.0, int nx = 50, int nt = 500)
    {
        if (nx < 2) return "ОШИБКА: nx должен быть >= 2.";
        if (nt < 1) return "ОШИБКА: nt должен быть >= 1.";
        if (System.Math.Abs(D) < 1e-15)
            return "ОШИБКА: D = 0. Для чистой адвекции (без диффузии) используйте solveAdvection.";
        double dx = 1.0 / (nx - 1);
        double dt = T / nt;
        double r   = D * dt / (dx * dx);
        double Pe  = System.Math.Abs(c) * dx / D;

        if (r > 0.5)
            return $"ОШИБКА: Условие устойчивости нарушено! r = {r:F4} > 0.5\nУменьшите dt или увеличьте nx.";

        double[] u    = new double[nx];
        double[] uNew = new double[nx];

        for (int i = 0; i < nx; i++)
        {
            double x = i * dx - 0.5;
            u[i] = System.Math.Exp(-50 * x * x);
        }
        u[0] = 0; u[nx - 1] = 0;

        for (int n = 0; n < nt; n++)
        {
            for (int i = 1; i < nx - 1; i++)
            {
                double diffusion = D * (u[i - 1] - 2 * u[i] + u[i + 1]) / (dx * dx);
                double advection = c > 0
                    ? -c * (u[i] - u[i - 1]) / dx
                    : -c * (u[i + 1] - u[i]) / dx;
                uNew[i] = u[i] + dt * (diffusion + advection);
            }
            uNew[0] = 0; uNew[nx - 1] = 0;
            Array.Copy(uNew, u, nx);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ ДИФФУЗИИ-АДВЕКЦИИ ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: u_t + {c}·u_x = {D}·u_xx");
        result.AppendLine($"Скорость переноса: c = {c}");
        result.AppendLine($"Коэффициент диффузии: D = {D}");
        result.AppendLine($"Число Пекле (локальное): Pe = {Pe:F2}");
        result.AppendLine(Pe < 1 ? "  -> Доминирует ДИФФУЗИЯ" : Pe > 10 ? "  -> Доминирует АДВЕКЦИЯ" : "  -> Смешанный режим");
        result.AppendLine();
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx} точек, {nt} шагов по времени");
        result.AppendLine($"Параметр устойчивости: r = {r:F4} < 0.5");
        result.AppendLine();
        result.AppendLine("Начальное условие: Гауссов импульс в центре");
        result.AppendLine("Граничные условия: u(0, t) = u(1, t) = 0");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine("(Пик смещается вправо с размытием)");
        result.AppendLine();

        double maxU = System.Math.Max(u.Max(), 1e-10);
        for (int i = 0; i < nx; i += 10)
        {
            double x = i * dx;
            int barLength = System.Math.Max(0, System.Math.Min(20, (int)(20 * u[i] / maxU)));
            int halfBar   = System.Math.Max(0, barLength / 2);
            int remBar    = System.Math.Max(0, barLength % 2 * 5);
            result.AppendLine($"  x = {x:F3}: u = {u[i]:F6}  {new string('#', halfBar)}{new string('#', remBar)}");
        }

        return result.ToString();
    }

    // Решение уравнения адвекции: u_t + c·u_x = 0
    public static string SolveAdvectionNumerical(double c, double T = 1.0, int nx = 100, int nt = 100)
    {
        if (nx < 2) return "ОШИБКА: nx должен быть >= 2.";
        if (nt < 1) return "ОШИБКА: nt должен быть >= 1.";
        double dx  = 1.0 / (nx - 1);
        double dt  = T / nt;
        double CFL = System.Math.Abs(c) * dt / dx;

        if (CFL > 1.0)
            return $"ОШИБКА: Условие CFL нарушено! CFL = {CFL:F4} > 1.0\nУменьшите dt или увеличьте nx.";

        double[] u    = new double[nx];
        double[] uNew = new double[nx];

        for (int i = 0; i < nx; i++)
        {
            double x = i * dx;
            u[i] = x > 0.3 && x < 0.5 ? 1.0 : 0.0;
        }

        for (int n = 0; n < nt; n++)
        {
            for (int i = 0; i < nx; i++)
            {
                int iPrev = (i - 1 + nx) % nx;
                int iNext = (i + 1) % nx;
                uNew[i] = c > 0
                    ? u[i] - CFL * (u[i] - u[iPrev])
                    : u[i] - CFL * (u[iNext] - u[i]);
            }
            Array.Copy(uNew, u, nx);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ АДВЕКЦИИ ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: u_t + {c}·u_x = 0");
        result.AppendLine($"Скорость переноса: c = {c}");
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx} точек, {nt} шагов");
        result.AppendLine($"Число Куранта: CFL = {CFL:F4} < 1.0");
        result.AppendLine();
        result.AppendLine("Начальное условие: Прямоугольный импульс [0.3, 0.5]");
        result.AppendLine("Граничные условия: Периодические");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine($"(Импульс сдвинулся на расстояние {c * T:F3})");
        result.AppendLine();

        for (int i = 0; i < nx; i += 10)
        {
            double x = i * dx;
            int barLength = System.Math.Max(0, (int)(20 * u[i]));
            result.AppendLine($"  x = {x:F3}: u = {u[i]:F6}  {new string('#', barLength)}");
        }

        return result.ToString();
    }

    // Решение уравнения диффузии 2D: u_t = D·(u_xx + u_yy)
    public static string SolveDiffusion2DNumerical(double D, double T = 0.1, int nx = 20, int ny = 20, int nt = 500)
    {
        if (nx < 2 || ny < 2) return "ОШИБКА: nx и ny должны быть >= 2.";
        if (nt < 1) return "ОШИБКА: nt должен быть >= 1.";
        double dx = 1.0 / (nx - 1);
        double dy = 1.0 / (ny - 1);
        double dt = T / nt;
        double rx  = D * dt / (dx * dx);
        double ry  = D * dt / (dy * dy);

        if (rx > 0.25 || ry > 0.25)
            return $"ОШИБКА: Условие устойчивости нарушено! rx = {rx:F4}, ry = {ry:F4} (должны быть < 0.25)\nУменьшите dt или увеличьте nx, ny.";

        double[,] u    = new double[nx, ny];
        double[,] uNew = new double[nx, ny];

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                double x = i * dx - 0.5;
                double y = j * dy - 0.5;
                u[i, j] = System.Math.Exp(-100 * (x * x + y * y));
            }

        for (int i = 0; i < nx; i++) { u[i, 0] = 0; u[i, ny - 1] = 0; }
        for (int j = 0; j < ny; j++) { u[0, j] = 0; u[nx - 1, j] = 0; }

        for (int n = 0; n < nt; n++)
        {
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < ny - 1; j++)
                    uNew[i, j] = u[i, j]
                        + rx * (u[i - 1, j] - 2 * u[i, j] + u[i + 1, j])
                        + ry * (u[i, j - 1] - 2 * u[i, j] + u[i, j + 1]);

            for (int i = 0; i < nx; i++) { uNew[i, 0] = 0; uNew[i, ny - 1] = 0; }
            for (int j = 0; j < ny; j++) { uNew[0, j] = 0; uNew[nx - 1, j] = 0; }
            Array.Copy(uNew, u, nx * ny);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ ДИФФУЗИИ 2D ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: u_t = {D}·(u_xx + u_yy)");
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx}×{ny} точек, {nt} шагов по времени");
        result.AppendLine($"Параметры устойчивости: rx = {rx:F4}, ry = {ry:F4} < 0.25");
        result.AppendLine();
        result.AppendLine("Начальное условие: Гауссов импульс в центре");
        result.AppendLine("Граничные условия: u = 0 на всех границах");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine("(Диффузия радиально от центра)");
        result.AppendLine();
        result.AppendLine("Сечение y = 0.5:");
        for (int i = 0; i < nx; i += 4)
            result.AppendLine($"  x = {i * dx:F2}: u = {u[i, ny / 2]:F6}");
        result.AppendLine();
        result.AppendLine("Ключевые точки:");
        result.AppendLine($"  u(0.5, 0.5) = {u[nx / 2, ny / 2]:F6}  (центр)");
        result.AppendLine($"  u(0.25, 0.5) = {u[nx / 4, ny / 2]:F6}");
        result.AppendLine($"  u(0.75, 0.5) = {u[3 * nx / 4, ny / 2]:F6}");
        return result.ToString();
    }

    // Решение уравнения Бюргерса: u_t + α·u·u_x = ν·u_xx
    public static string SolveBurgersNumerical(double alpha, double nu, double T = 0.5, int nx = 100, int nt = 500)
    {
        if (nx < 2) return "ОШИБКА: nx должен быть >= 2.";
        if (nt < 1) return "ОШИБКА: nt должен быть >= 1.";
        double dx = 1.0 / (nx - 1);
        double dt = T / nt;

        double[] u    = new double[nx];
        double[] uNew = new double[nx];

        for (int i = 0; i < nx; i++)
            u[i] = i * dx < 0.5 ? 1.0 : 0.2;

        for (int n = 0; n < nt; n++)
        {
            for (int i = 1; i < nx - 1; i++)
            {
                double advection = alpha * u[i] * (u[i] - u[i - 1]) / dx;
                double diffusion = nu * (u[i - 1] - 2 * u[i] + u[i + 1]) / (dx * dx);
                uNew[i] = u[i] - dt * advection + dt * diffusion;
            }
            uNew[0]       = uNew[1];
            uNew[nx - 1]  = uNew[nx - 2];
            Array.Copy(uNew, u, nx);
        }

        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ УРАВНЕНИЯ БЮРГЕРСА ===");
        result.AppendLine();
        string alphaStr = System.Math.Abs(alpha - 1.0) < 0.01 ? "" : $"{alpha}·";
        result.AppendLine($"Уравнение: u_t + {alphaStr}u·u_x = {nu}·u_xx");
        result.AppendLine($"Коэффициент нелинейности: α = {alpha}");
        result.AppendLine($"Коэффициент вязкости: ν = {nu}");
        result.AppendLine(System.Math.Abs(nu) > 1e-15
            ? $"Отношение α/ν = {alpha / nu:F2}"
            : "Отношение α/ν = ∞ (ν = 0, чистая нелинейная адвекция)");
        result.AppendLine($"Время: t = {T:F2}");
        result.AppendLine($"Сетка: {nx} точек, {nt} шагов");
        result.AppendLine();
        result.AppendLine("Начальное условие: Ступенька (формирует ударную волну)");
        result.AppendLine();
        result.AppendLine($"РЕШЕНИЕ в момент времени t = {T:F2}:");
        result.AppendLine();
        for (int i = 0; i < nx; i += 10)
        {
            double x = i * dx;
            int barLength = System.Math.Max(0, (int)(30 * u[i]));
            result.AppendLine($"  x = {x:F2}: u = {u[i]:F4}  {new string('#', barLength)}");
        }
        return result.ToString();
    }
}
