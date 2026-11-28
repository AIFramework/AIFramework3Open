using System;
using System.Collections.Generic;

namespace AI.HighLevelFunctions;

/// <summary>Итог поиска корня.</summary>
/// <param name="Root">Найденный корень (лучшая точка, если поиск не сошёлся).</param>
/// <param name="Converged">Достигнута ли заданная точность по аргументу.</param>
/// <param name="Iterations">Число шагов.</param>
/// <param name="Residual">Значение функции в найденной точке.</param>
public readonly record struct RootResult(double Root, bool Converged, int Iterations, double Residual);

/// <summary>
/// Корни скалярного уравнения f(x) = 0 — единственный решатель фреймворка.
/// </summary>
/// <remarks>
/// <para>
/// Метод Брента: обратная квадратичная интерполяция и секущие, подстрахованные делением пополам.
/// Сходится всегда, когда корень заключён в скобку, и обычно за 6–10 вычислений функции — против
/// пятидесяти у чистого деления пополам. Совпадает со <c>scipy.optimize.brentq</c>.
/// </para>
/// <para>
/// Критерий остановки — по аргументу: ширина скобки не больше <c>xTolerance + relativeTolerance·|x|</c>.
/// Критерий по значению функции (<c>|f(x)| &lt; ε</c>) не используется: у функции с малым масштабом,
/// например <c>1e-12·(x − 0.3)</c>, он выполняется в любой точке, и «корнем» оказывалась середина отрезка.
/// </para>
/// <para>
/// Лежит в ядре, а не в AI.ClassicMath, потому что корни ищут и сами функции ядра (квантили
/// распределений, смесь гауссиан); остальные сборки берут решатель отсюда.
/// </para>
/// </remarks>
public static class RootFinder
{
    /// <summary>Абсолютная точность по аргументу по умолчанию — как <c>xtol</c> в scipy.</summary>
    public const double DefaultXTolerance = 2e-12;

    /// <summary>Относительная точность по аргументу по умолчанию: четыре единицы младшего разряда.</summary>
    public const double DefaultRelativeTolerance = 8.881784197001252e-16;

    /// <summary>
    /// Ищет корень на отрезке, на концах которого функция имеет разные знаки.
    /// </summary>
    /// <param name="function">Непрерывная функция.</param>
    /// <param name="a">Один конец отрезка.</param>
    /// <param name="b">Другой конец отрезка.</param>
    /// <param name="xTolerance">Абсолютная точность по аргументу.</param>
    /// <param name="relativeTolerance">Относительная точность по аргументу.</param>
    /// <param name="maxIterations">Предел числа шагов.</param>
    /// <returns>Корень и признак сходимости.</returns>
    /// <exception cref="ArgumentException">
    /// На концах отрезка функция одного знака (корень не заключён в скобку) либо не число.
    /// </exception>
    public static RootResult Brent(
        Func<double, double> function, double a, double b,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance, int maxIterations = 200)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (!(xTolerance >= 0) || !(relativeTolerance >= 0))
            throw new ArgumentOutOfRangeException(nameof(xTolerance), "Точность должна быть неотрицательной.");

        double fa = function(a), fb = function(b);

        if (double.IsNaN(fa) || double.IsNaN(fb))
            throw new ArgumentException($"Функция не определена на конце отрезка: f({a:R}) = {fa:R}, f({b:R}) = {fb:R}.", nameof(function));

        if (fa == 0) return new RootResult(a, true, 0, 0);
        if (fb == 0) return new RootResult(b, true, 0, 0);

        if (Math.Sign(fa) == Math.Sign(fb))
        {
            throw new ArgumentException(
                $"Корень не заключён в отрезок [{a:R}; {b:R}]: на концах функция одного знака (f = {fa:R} и {fb:R}).", nameof(b));
        }

        // Обозначения классические: b — текущее приближение, a — предыдущее, c — другой конец скобки
        double c = a, fc = fa, d = b - a, e = d;

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            if (Math.Sign(fb) == Math.Sign(fc))
            {
                c = a;
                fc = fa;
                d = b - a;
                e = d;
            }

            if (Math.Abs(fc) < Math.Abs(fb))
            {
                a = b;
                b = c;
                c = a;
                fa = fb;
                fb = fc;
                fc = fa;
            }

            double tolerance = (xTolerance + (relativeTolerance * Math.Abs(b))) / 2;
            double half = (c - b) / 2;

            if (Math.Abs(half) <= tolerance || fb == 0) return new RootResult(b, true, iteration - 1, fb);

            if (Math.Abs(e) >= tolerance && Math.Abs(fa) > Math.Abs(fb))
            {
                // Секущая либо обратная квадратичная интерполяция
                double s = fb / fa, p, q;

                if (a == c)
                {
                    p = 2 * half * s;
                    q = 1 - s;
                }
                else
                {
                    double r = fb / fc;

                    q = fa / fc;
                    p = s * ((2 * half * q * (q - r)) - ((b - a) * (r - 1)));
                    q = (q - 1) * (r - 1) * (s - 1);
                }

                if (p > 0) q = -q;
                else p = -p;

                // Шаг интерполяции принимается, только если он остаётся в скобке и сужает её быстрее половины
                if (2 * p < Math.Min((3 * half * q) - Math.Abs(tolerance * q), Math.Abs(e * q)))
                {
                    e = d;
                    d = p / q;
                }
                else
                {
                    d = half;
                    e = d;
                }
            }
            else
            {
                d = half;
                e = d;
            }

            a = b;
            fa = fb;
            b += Math.Abs(d) > tolerance ? d : (half > 0 ? tolerance : -tolerance);
            fb = function(b);

            if (double.IsNaN(fb))
                throw new ArgumentException($"Функция не определена внутри отрезка: f({b:R}) не число.", nameof(function));
        }

        return new RootResult(b, false, maxIterations, fb);
    }

    /// <summary>
    /// Корень на отрезке; отказ исключением, если скобки нет или точность не достигнута.
    /// </summary>
    /// <param name="function">Непрерывная функция.</param>
    /// <param name="a">Один конец отрезка.</param>
    /// <param name="b">Другой конец отрезка.</param>
    /// <param name="xTolerance">Абсолютная точность по аргументу.</param>
    /// <param name="relativeTolerance">Относительная точность по аргументу.</param>
    /// <exception cref="ArgumentException">Корень не заключён в отрезок.</exception>
    /// <exception cref="InvalidOperationException">Точность не достигнута за отведённое число шагов.</exception>
    public static double Solve(
        Func<double, double> function, double a, double b,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance)
    {
        RootResult result = Brent(function, a, b, xTolerance, relativeTolerance);

        return result.Converged
            ? result.Root
            : throw new InvalidOperationException($"Корень на [{a:R}; {b:R}] не найден с заданной точностью за {result.Iterations} шагов.");
    }

    /// <summary>
    /// Корень на отрезке, если он заключён в скобку; иначе <c>false</c> без исключения.
    /// </summary>
    /// <remarks>
    /// Для мест, где отсутствие корня — штатный исход (пороговая величина не достигается ни при каком
    /// значении параметра): вызывающий получает явный отрицательный ответ и решает сам.
    /// </remarks>
    public static bool TrySolve(
        Func<double, double> function, double a, double b, out double root,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(function);

        double fa = function(a), fb = function(b);

        root = double.NaN;

        if (double.IsNaN(fa) || double.IsNaN(fb) || (fa != 0 && fb != 0 && Math.Sign(fa) == Math.Sign(fb))) return false;

        RootResult result = Brent(function, a, b, xTolerance, relativeTolerance);

        root = result.Root;

        return result.Converged;
    }

    /// <summary>
    /// Граница монотонного признака на отрезке: точка, левее которой признак истинен, правее — ложен.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Деление пополам по признаку, а не по значению функции. Нужно там, где ищут порог: «при какой цене
    /// доходность ещё выше целевой», «с какой дальности уровень поля ниже нормы». В отличие от
    /// <see cref="Brent"/> не требует непрерывности: функция может быть ступенчатой или иметь площадку,
    /// и тогда возвращается её левый край — первая точка, где признак перестал выполняться.
    /// </para>
    /// <para>
    /// Скобка сужается до ширины <c>xTolerance + relativeTolerance·|x|</c>. Если признак на всём отрезке
    /// одинаков, скобка стягивается к соответствующему концу: к <paramref name="high"/>, когда он везде
    /// истинен, и к <paramref name="low"/>, когда везде ложен.
    /// </para>
    /// </remarks>
    /// <param name="isLeft">Признак «точка левее границы».</param>
    /// <param name="low">Левый конец отрезка.</param>
    /// <param name="high">Правый конец отрезка.</param>
    /// <param name="xTolerance">Абсолютная точность по аргументу; 0 — до последнего разряда.</param>
    /// <param name="relativeTolerance">Относительная точность по аргументу.</param>
    /// <returns>Концы итоговой скобки: признак истинен в <c>Low</c> и ложен в <c>High</c>.</returns>
    public static (double Low, double High) BoundaryBracket(
        Func<double, bool> isLeft, double low, double high,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(isLeft);

        if (double.IsNaN(low) || double.IsNaN(high))
            throw new ArgumentException("Концы отрезка должны быть числами.", nameof(low));

        // 2200 шагов хватает, чтобы пройти все двоичные порядки double
        for (int i = 0; i < 2200; i++)
        {
            double middle = low + ((high - low) / 2);

            if (middle == low || middle == high) break;
            if (Math.Abs(high - low) <= xTolerance + (relativeTolerance * Math.Abs(middle))) break;

            if (isLeft(middle)) low = middle;
            else high = middle;
        }

        return (low, high);
    }

    /// <summary>Граница монотонного признака — середина итоговой скобки <see cref="BoundaryBracket"/>.</summary>
    /// <param name="isLeft">Признак «точка левее границы».</param>
    /// <param name="low">Левый конец отрезка.</param>
    /// <param name="high">Правый конец отрезка.</param>
    /// <param name="xTolerance">Абсолютная точность по аргументу; 0 — до последнего разряда.</param>
    /// <param name="relativeTolerance">Относительная точность по аргументу.</param>
    public static double Boundary(
        Func<double, bool> isLeft, double low, double high,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance)
    {
        (double left, double right) = BoundaryBracket(isLeft, low, high, xTolerance, relativeTolerance);

        return left + ((right - left) / 2);
    }

    /// <summary>
    /// Все корни на отрезке: отрезок делится на равные части, на каждой со сменой знака ищется корень.
    /// </summary>
    /// <remarks>
    /// Находит корни, разделённые хотя бы одним узлом сетки. Корень чётной кратности (функция касается
    /// нуля, не меняя знака) сменой знака не обнаруживается — так ведёт себя любой метод скобок.
    /// </remarks>
    /// <param name="function">Непрерывная функция.</param>
    /// <param name="a">Левый конец отрезка.</param>
    /// <param name="b">Правый конец отрезка.</param>
    /// <param name="samples">Число частей сетки.</param>
    /// <param name="xTolerance">Абсолютная точность по аргументу.</param>
    /// <param name="relativeTolerance">Относительная точность по аргументу.</param>
    /// <returns>Корни по возрастанию.</returns>
    public static IReadOnlyList<double> FindAll(
        Func<double, double> function, double a, double b, int samples = 200,
        double xTolerance = DefaultXTolerance, double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);

        var roots = new List<double>();
        double left = a, fLeft = function(a);

        for (int i = 1; i <= samples; i++)
        {
            double right = i == samples ? b : a + ((b - a) * i / samples);
            double fRight = function(right);

            if (fLeft == 0)
            {
                Add(roots, left, xTolerance);
            }
            else if (!double.IsNaN(fLeft) && !double.IsNaN(fRight) && fRight != 0 && Math.Sign(fLeft) != Math.Sign(fRight))
            {
                RootResult result = Brent(function, left, right, xTolerance, relativeTolerance);

                if (result.Converged) Add(roots, result.Root, xTolerance);
            }

            left = right;
            fLeft = fRight;
        }

        if (fLeft == 0) Add(roots, left, xTolerance);

        return roots;
    }

    private static void Add(List<double> roots, double root, double xTolerance)
    {
        // Корень на общем узле двух частей сетки находится дважды
        if (roots.Count == 0 || Math.Abs(roots[^1] - root) > Math.Max(10 * xTolerance, 1e-12 * Math.Abs(root))) roots.Add(root);
    }
}
