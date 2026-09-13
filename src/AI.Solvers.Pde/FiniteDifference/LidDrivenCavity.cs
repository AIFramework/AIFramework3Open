using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteDifference;

/// <summary>Течение в квадратной каверне с подвижной крышкой</summary>
public sealed class CavityFlowSolution : IInterpretable
{
    internal CavityFlowSolution(
        Grid2D grid, Matrix streamFunction, Matrix vorticity, Matrix velocityX, Matrix velocityY,
        double reynolds, int steps, double time, bool converged, double residual)
    {
        Grid = grid;
        StreamFunction = streamFunction;
        Vorticity = vorticity;
        VelocityX = velocityX;
        VelocityY = velocityY;
        Reynolds = reynolds;
        Steps = steps;
        Time = time;
        Converged = converged;
        Residual = residual;
    }

    /// <summary>Сетка на единичном квадрате</summary>
    public Grid2D Grid { get; }

    /// <summary>Функция тока ψ; строка — постоянное y</summary>
    public Matrix StreamFunction { get; }

    /// <summary>Завихренность ω = ∂v/∂x − ∂u/∂y</summary>
    public Matrix Vorticity { get; }

    /// <summary>Горизонтальная скорость u = ∂ψ/∂y</summary>
    public Matrix VelocityX { get; }

    /// <summary>Вертикальная скорость v = −∂ψ/∂x</summary>
    public Matrix VelocityY { get; }

    /// <summary>Число Рейнольдса по крышке и стороне каверны</summary>
    public double Reynolds { get; }

    /// <summary>Сделано шагов по времени</summary>
    public int Steps { get; }

    /// <summary>Безразмерное время установления</summary>
    public double Time { get; }

    /// <summary>Достигнуто ли установившееся течение</summary>
    public bool Converged { get; }

    /// <summary>Относительная скорость изменения завихренности на последнем шаге</summary>
    public double Residual { get; }

    /// <summary>Сеточное число Рейнольдса Re·h: центральные разности надёжны, пока оно меньше двух</summary>
    public double CellReynolds => Reynolds * Grid.StepX;

    /// <summary>Горизонтальная скорость в точке — билинейная интерполяция</summary>
    /// <param name="x">Координата x на [0; 1]</param>
    /// <param name="y">Координата y на [0; 1]</param>
    public double VelocityXAt(double x, double y) => Sample(VelocityX, x, y);

    /// <summary>Вертикальная скорость в точке</summary>
    /// <param name="x">Координата x</param>
    /// <param name="y">Координата y</param>
    public double VelocityYAt(double x, double y) => Sample(VelocityY, x, y);

    /// <summary>Центр главного вихря — узел с наименьшей функцией тока</summary>
    public (double X, double Y, double StreamFunction) PrimaryVortex
    {
        get
        {
            (double X, double Y, double Psi) best = (0, 0, double.PositiveInfinity);

            for (int j = 0; j < Grid.CountY; j++)
            {
                for (int i = 0; i < Grid.CountX; i++)
                {
                    if (StreamFunction[j, i] < best.Psi)
                        best = (Grid.X(i), Grid.Y(j), StreamFunction[j, i]);
                }
            }

            return best;
        }
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (double vx, double vy, double psi) = PrimaryVortex;

        return new InterpretationBuilder("Течение в каверне с подвижной крышкой")
            .Summary($"Re = {Fmt.Num(Reynolds, 0)}, сетка {Grid.CountX}×{Grid.CountY}. "
                + (Converged
                    ? $"Установившееся течение достигнуто к t = {Fmt.Num(Time, 2)} за {Steps} шагов."
                    : $"За {Steps} шагов (t = {Fmt.Num(Time, 2)}) течение не установилось.")
                + $" Центр главного вихря в ({Fmt.Num(vx, 3)}; {Fmt.Num(vy, 3)}), ψ = {Fmt.Num(psi, 4)}.")
            .Metric("Число Рейнольдса", Fmt.Num(Reynolds, 0), null, "по скорости крышки и стороне")
            .Metric("Сеточное число Рейнольдса", Fmt.Num(CellReynolds, 3), null, "Re·h; центральные разности — при меньше двух",
                CellReynolds < 2 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Шагов по времени", Steps, null, "явная схема для завихренности", MetricQuality.Unknown, 0)
            .Metric("Установление", Converged ? "достигнуто" : "не достигнуто", null, null,
                Converged ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("ψ в центре вихря", Fmt.Num(psi, 5), null, "расход между вихрем и стенкой")
            .Finding("Крышка увлекает жидкость вязкостью, и в каверне устанавливается один главный вихрь; "
                + "с ростом Re его центр смещается к середине, а в нижних углах растут вторичные вихри обратного вращения.")
            .WarningIf(CellReynolds >= 2,
                "Сеточное число Рейнольдса не меньше двух: центральные разности дают нефизичные осцилляции. "
                + "Сетку нужно мельчить пропорционально Re.")
            .WarningIf(Reynolds > 5000,
                "При Re выше нескольких тысяч течение в каверне теряет устойчивость и перестаёт быть стационарным; "
                + "установившееся решение, даже если найдено, может не наблюдаться в эксперименте.")
            .Warning("В углах у крышки скорость скачком меняется от U до нуля, и завихренность там бесконечна. "
                + "Это особенность постановки, а не ошибка счёта; сравнивать решения нужно вдали от углов.")
            .Build();
    }

    private double Sample(Matrix field, double x, double y)
    {
        if (!(x >= 0 && x <= 1 && y >= 0 && y <= 1))
            throw new ArgumentOutOfRangeException(nameof(x), "Точка должна лежать в каверне");

        double fi = x / Grid.StepX, fj = y / Grid.StepY;
        int i = Math.Min((int)fi, Grid.CountX - 2), j = Math.Min((int)fj, Grid.CountY - 2);
        double s = fi - i, t = fj - j;

        return ((1 - s) * (1 - t) * field[j, i]) + (s * (1 - t) * field[j, i + 1])
            + ((1 - s) * t * field[j + 1, i]) + (s * t * field[j + 1, i + 1]);
    }
}

/// <summary>
/// Уравнения Навье — Стокса для несжимаемой жидкости в квадратной каверне с подвижной крышкой —
/// классическая задача сравнения численных методов.
/// </summary>
/// <remarks>
/// <para>
/// Двумерное течение описывается функцией тока ψ и завихренностью ω: <c>u = ∂ψ/∂y</c>, <c>v = −∂ψ/∂x</c>,
/// <c>−Δψ = ω</c>. Давление из уравнений исключено, и условие несжимаемости выполняется тождественно.
/// Завихренность переносится потоком и диффундирует: <c>∂ω/∂t + u·∂ω/∂x + v·∂ω/∂y = Δω/Re</c>.
/// </para>
/// <para>
/// Перенос считается явной схемой с центральными разностями до установления; шаг ограничен
/// устойчивостью диффузии <c>h²Re/4</c> и переноса <c>2/(Re·U²)</c>. Функция тока на каждом шаге
/// находится из уравнения Пуассона той же пятиточечной матрицей, что в <see cref="Poisson2D"/>.
/// Матрица от шага к шагу не меняется, поэтому раскладывается по Холецкому один раз
/// (<see cref="BandedCholesky"/>), и шаг стоит двух подстановок в ленте шириной в сторону сетки;
/// метод сопряжённых градиентов даже с тёплым стартом тратил на то же десятки итераций за шаг.
/// Завихренность на стенках — по формуле Тома из условия прилипания.
/// </para>
/// <para>
/// Это решатель одной постановки, а не общая вычислительная гидродинамика: область — квадрат,
/// граничные условия — неподвижные стенки и крышка, течение двумерное и ламинарное.
/// </para>
/// </remarks>
public static class LidDrivenCavity
{
    /// <summary>Находит установившееся течение</summary>
    /// <param name="reynolds">Число Рейнольдса по скорости крышки и стороне каверны</param>
    /// <param name="nodes">Узлов на сторону</param>
    /// <param name="tolerance">Порог относительной скорости изменения завихренности</param>
    /// <param name="maxTime">Предел безразмерного времени</param>
    public static CavityFlowSolution Solve(double reynolds, int nodes = 65, double tolerance = 1e-6, double maxTime = 200)
    {
        if (!(reynolds > 0) || double.IsInfinity(reynolds))
            throw new ArgumentOutOfRangeException(nameof(reynolds), "Число Рейнольдса должно быть положительным");

        if (nodes < 9)
            throw new ArgumentOutOfRangeException(nameof(nodes), "Нужно не меньше девяти узлов на сторону");

        if (!(tolerance > 0) || !(maxTime > 0))
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Порог и предел времени должны быть положительными");

        const double Lid = 1.0;
        var grid = new Grid2D(0, 1, 0, 1, nodes, nodes);
        int n = nodes;
        double h = grid.StepX;
        double nu = 1 / reynolds;

        (SparseMatrix laplacian, _) = Poisson2D.Assemble(grid, static (_, _) => 0.0, static (_, _) => 0.0);
        BandedCholesky poisson = BandedCholesky.Factor(laplacian);

        var psi = new double[n, n];
        var omega = new double[n, n];
        var next = new double[n, n];
        var right = new double[n * n];
        var solution = new double[n * n];

        double dt = 0.8 * Math.Min(Math.Min(h * h / (4 * nu), 2 * nu / (Lid * Lid)), h / Lid);
        double time = 0, residual = double.PositiveInfinity;
        int steps = 0;
        bool converged = false;

        while (time < maxTime)
        {
            WallVorticity(psi, omega, n, h, Lid);

            double change = 0, scale = 1;

            for (int j = 1; j < n - 1; j++)
            {
                for (int i = 1; i < n - 1; i++)
                {
                    double u = (psi[i, j + 1] - psi[i, j - 1]) / (2 * h);
                    double v = -(psi[i + 1, j] - psi[i - 1, j]) / (2 * h);
                    double dx = (omega[i + 1, j] - omega[i - 1, j]) / (2 * h);
                    double dy = (omega[i, j + 1] - omega[i, j - 1]) / (2 * h);
                    double laplace = (omega[i + 1, j] + omega[i - 1, j] + omega[i, j + 1] + omega[i, j - 1] - (4 * omega[i, j])) / (h * h);

                    next[i, j] = omega[i, j] + (dt * ((-u * dx) - (v * dy) + (nu * laplace)));
                    change = Math.Max(change, Math.Abs(next[i, j] - omega[i, j]));
                    scale = Math.Max(scale, Math.Abs(omega[i, j]));
                }
            }

            for (int j = 1; j < n - 1; j++)
                for (int i = 1; i < n - 1; i++)
                    omega[i, j] = next[i, j];

            time += dt;
            steps++;

            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    right[grid.Index(i, j)] = i > 0 && j > 0 && i < n - 1 && j < n - 1 ? omega[i, j] : 0;

            poisson.Solve(right, solution);

            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    psi[i, j] = solution[grid.Index(i, j)];

            residual = change / (dt * scale);

            if (residual < tolerance)
            {
                converged = true;
                break;
            }
        }

        WallVorticity(psi, omega, n, h, Lid);

        var streamFunction = new Matrix(n, n);
        var vorticity = new Matrix(n, n);
        var velocityX = new Matrix(n, n);
        var velocityY = new Matrix(n, n);

        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                streamFunction[j, i] = psi[i, j];
                vorticity[j, i] = omega[i, j];

                if (i > 0 && j > 0 && i < n - 1 && j < n - 1)
                {
                    velocityX[j, i] = (psi[i, j + 1] - psi[i, j - 1]) / (2 * h);
                    velocityY[j, i] = -(psi[i + 1, j] - psi[i - 1, j]) / (2 * h);
                }
                else if (j == n - 1 && i > 0 && i < n - 1)
                {
                    velocityX[j, i] = Lid;
                }
            }
        }

        return new CavityFlowSolution(grid, streamFunction, vorticity, velocityX, velocityY,
            reynolds, steps, time, converged, residual);
    }

    // Формула Тома: ψ на стенке нулевое, а касательная скорость задана условием прилипания
    private static void WallVorticity(double[,] psi, double[,] omega, int n, double h, double lid)
    {
        double h2 = h * h;

        for (int i = 0; i < n; i++)
        {
            omega[i, 0] = -2 * psi[i, 1] / h2;
            omega[i, n - 1] = (-2 * psi[i, n - 2] / h2) - (2 * lid / h);
        }

        for (int j = 1; j < n - 1; j++)
        {
            omega[0, j] = -2 * psi[1, j] / h2;
            omega[n - 1, j] = -2 * psi[n - 2, j] / h2;
        }
    }
}
