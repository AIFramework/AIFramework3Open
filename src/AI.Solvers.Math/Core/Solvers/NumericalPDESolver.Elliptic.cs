using System.Text;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class NumericalPDESolver
{
    // Решение уравнения Лапласа ∇²u = a·u_xx + b·u_yy = 0
    // Метод Либмана (итерационный метод релаксации)
    public static string SolveLaplaceNumerical(double a, double b, int nx = 20, int ny = 20, int maxIter = 1000, double tol = 1e-6)
    {
        double dx = 1.0 / (nx - 1);
        double dy = 1.0 / (ny - 1);

        double[,] u    = new double[nx, ny];
        double[,] uNew = new double[nx, ny];

        for (int i = 0; i < nx; i++) { u[i, 0] = 0; u[i, ny - 1] = 100; }
        for (int j = 0; j < ny; j++) { u[0, j] = 0; u[nx - 1, j] = 0; }

        int iter = 0;
        double error = double.MaxValue;

        while (iter < maxIter && error > tol)
        {
            error = 0;
            for (int i = 1; i < nx - 1; i++)
            {
                for (int j = 1; j < ny - 1; j++)
                {
                    double alpha2 = a / (dx * dx);
                    double beta   = b / (dy * dy);
                    uNew[i, j] = (alpha2 * (u[i - 1, j] + u[i + 1, j]) +
                                  beta   * (u[i, j - 1] + u[i, j + 1])) /
                                 (2 * (alpha2 + beta));
                    error = System.Math.Max(error, System.Math.Abs(uNew[i, j] - u[i, j]));
                }
            }
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < ny - 1; j++)
                    u[i, j] = uNew[i, j];
            iter++;
        }

        var result = new StringBuilder();
        result.AppendLine($"=== ЧИСЛЕННОЕ РЕШЕНИЕ {(System.Math.Abs(a - b) < 1e-6 ? "УРАВНЕНИЯ ЛАПЛАСА" : "ЭЛЛИПТИЧЕСКОГО УРАВНЕНИЯ")} ===");
        result.AppendLine();
        result.AppendLine($"Уравнение: {a}·u_xx + {b}·u_yy = 0");
        result.AppendLine($"Область: [0, 1] × [0, 1]");
        result.AppendLine($"Сетка: {nx}×{ny} точек");
        result.AppendLine($"Метод: Либмана (итерационная релаксация)");
        result.AppendLine($"Сходимость: {iter} итераций, погрешность = {error:E3}");
        result.AppendLine();
        result.AppendLine("Граничные условия:");
        result.AppendLine("  u(x, 0) = 0      (нижняя граница)");
        result.AppendLine("  u(x, 1) = 100    (верхняя граница)");
        result.AppendLine("  u(0, y) = 0      (левая граница)");
        result.AppendLine("  u(1, y) = 0      (правая граница)");
        result.AppendLine();
        result.AppendLine("ЧИСЛЕННОЕ РЕШЕНИЕ (выборка точек):");
        result.AppendLine();
        result.AppendLine("    y ->");
        result.Append("x v ");
        for (int j = 0; j < ny; j += 4) result.Append($"  {j * dy,6:F2}");
        result.AppendLine();
        for (int i = 0; i < nx; i += 4)
        {
            result.Append($"{i * dx,4:F2}");
            for (int j = 0; j < ny; j += 4) result.Append($"  {u[i, j],6:F2}");
            result.AppendLine();
        }
        result.AppendLine();
        result.AppendLine("Ключевые точки:");
        result.AppendLine($"  u(0.5, 0.5) = {u[nx / 2, ny / 2]:F4}  (центр области)");
        result.AppendLine($"  u(0.25, 0.5) = {u[nx / 4, ny / 2]:F4}");
        result.AppendLine($"  u(0.75, 0.5) = {u[3 * nx / 4, ny / 2]:F4}");
        result.AppendLine($"  u(0.5, 0.25) = {u[nx / 2, ny / 4]:F4}");
        result.AppendLine($"  u(0.5, 0.75) = {u[nx / 2, 3 * ny / 4]:F4}");
        return result.ToString();
    }

    // Решение уравнения Гельмгольца (собственные значения): a·u_xx + b·u_yy + k²u = 0
    public static string SolveHelmholtzNumerical(double a, double b, double k2, int nx = 20, int ny = 20)
    {
        var result = new StringBuilder();
        result.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ (СОБСТВЕННЫЕ ЗНАЧЕНИЯ) ===");
        result.AppendLine();
        bool isIsotropic = System.Math.Abs(a - b) < 0.001 && System.Math.Abs(a - 1.0) < 0.001;
        if (isIsotropic)
            result.AppendLine($"Уравнение: u_xx + u_yy + {k2}·u = 0");
        else
        {
            result.AppendLine($"Уравнение: {a}·u_xx + {b}·u_yy + {k2}·u = 0");
            result.AppendLine($"АНИЗОТРОПНАЯ среда: a = {a}, b = {b}");
        }

        result.AppendLine();
        result.AppendLine("Для прямоугольника [0,1]×[0,1] ищем собственные функции:");
        result.AppendLine("  u_mn(x,y) = sin(mπx)·sin(nπy)");
        result.AppendLine();
        result.AppendLine(isIsotropic
            ? "Собственные значения λ_mn = (mπ)² + (nπ)²"
            : $"Собственные значения λ_mn = {a}·(mπ)² + {b}·(nπ)²");
        result.AppendLine($"Условие резонанса: λ_mn = {k2:F4}");
        result.AppendLine();
        result.AppendLine("Резонансные моды (первые 10):");

        int count = 0;
        for (int m = 1; m <= 10 && count < 10; m++)
        {
            for (int n = 1; n <= 10 && count < 10; n++)
            {
                double lambda = a * m * System.Math.PI * m * System.Math.PI +
                                b * n * System.Math.PI * n * System.Math.PI;
                if (System.Math.Abs(lambda - k2) < 5.0)
                {
                    result.AppendLine($"  ({m},{n}): λ = {lambda:F4}, отклонение = {System.Math.Abs(lambda - k2):F4}");
                    count++;
                }
            }
        }

        if (count == 0)
        {
            result.AppendLine($"  Нет резонансных мод вблизи k² = {k2:F4}");
            double lambda11 = a * System.Math.PI * System.Math.PI + b * System.Math.PI * System.Math.PI;
            result.AppendLine($"  Ближайшая мода: (1,1) с λ = {lambda11:F4}");
        }

        return result.ToString();
    }
}
