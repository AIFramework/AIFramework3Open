#nullable enable

using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Адаптивный метод Рунге-Кутты Дормана-Принса 5(4) с управлением шагом, плотной выдачей и событиями.
/// </summary>
/// <remarks>
/// Решение продвигается формулой 5-го порядка, ошибка оценивается разностью с вложенной формулой 4-го порядка.
/// Шаг принимается, если взвешенная среднеквадратичная ошибка с весами atol + rtol·|y| не больше 1.
/// Последняя стадия шага совпадает с первой стадией следующего (FSAL), поэтому на шаг уходит 6 вычислений правой части.
/// Между узлами решение восстанавливается плотной выдачей Хайрера 4-го порядка; на ней же ищутся моменты
/// событий методом Иллинойса (модифицированный метод ложного положения).
/// </remarks>
public static class DormandPrince
{
    private const double C2 = 1.0 / 5, C3 = 3.0 / 10, C4 = 4.0 / 5, C5 = 8.0 / 9;
    private const double A21 = 1.0 / 5;
    private const double A31 = 3.0 / 40, A32 = 9.0 / 40;
    private const double A41 = 44.0 / 45, A42 = -56.0 / 15, A43 = 32.0 / 9;
    private const double A51 = 19372.0 / 6561, A52 = -25360.0 / 2187, A53 = 64448.0 / 6561, A54 = -212.0 / 729;
    private const double A61 = 9017.0 / 3168, A62 = -355.0 / 33, A63 = 46732.0 / 5247, A64 = 49.0 / 176, A65 = -5103.0 / 18656;
    private const double B1 = 35.0 / 384, B3 = 500.0 / 1113, B4 = 125.0 / 192, B5 = -2187.0 / 6784, B6 = 11.0 / 84;

    // Разность весов формул 5-го и 4-го порядков
    private const double E1 = 71.0 / 57600, E3 = -71.0 / 16695, E4 = 71.0 / 1920;
    private const double E5 = -17253.0 / 339200, E6 = 22.0 / 525, E7 = -1.0 / 40;

    // Коэффициенты плотной выдачи (Hairer, Norsett, Wanner, программа DOPRI5)
    private const double D1 = -12715105075.0 / 11282082432, D3 = 87487479700.0 / 32700410799;
    private const double D4 = -10690763975.0 / 1880347072, D5 = 701980252875.0 / 199316789632;
    private const double D6 = -1453857185.0 / 822651844, D7 = 69997945.0 / 29380423;

    private const double Safety = 0.9;
    private const double MinFactor = 0.2;
    private const double MaxFactor = 5.0;

    /// <summary>
    /// Интегрирует систему dy/dt = f(t, y) от t0 до tEnd (допускается tEnd &lt; t0).
    /// </summary>
    /// <param name="function">Правая часть f(t, y).</param>
    /// <param name="t0">Начальный момент.</param>
    /// <param name="y0">Начальное состояние.</param>
    /// <param name="tEnd">Конечный момент.</param>
    /// <param name="options">Настройки; если null, используются значения по умолчанию.</param>
    /// <param name="events">События; останавливающее событие завершает интегрирование в найденный момент.</param>
    /// <returns>Траектория по принятым шагам, события и плотная выдача.</returns>
    public static OdeSolution Integrate(
        Func<double, Vector, Vector> function,
        double t0,
        Vector y0,
        double tEnd,
        DormandPrinceOptions? options = null,
        IReadOnlyList<OdeEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(y0);

        options ??= new DormandPrinceOptions();
        events ??= Array.Empty<OdeEvent>();

        int n = y0.Count;
        int evaluations = 0;

        double[] Rhs(double t, double[] y)
        {
            evaluations++;
            Vector value = function(t, new Vector(y)) ?? throw new InvalidOperationException("Правая часть вернула null.");

            if (value.Count != n)
                throw new InvalidOperationException($"Правая часть вернула {value.Count} значений вместо {n}.");

            return value.ToArray();
        }

        double time = t0;
        double[] state = y0.ToArray();
        var times = new List<double> { time };
        var states = new List<Vector> { new Vector(state) };
        var steps = new List<double>();
        var dense = new List<double[]>();
        var hits = new List<OdeEventHit>();

        var previousEvents = new double[events.Count];

        for (int e = 0; e < events.Count; e++)
            previousEvents[e] = events[e].Function(time, new Vector(state));

        double direction = Math.Sign(tEnd - t0);
        int accepted = 0;
        int rejected = 0;
        bool success = true;
        bool stopped = false;

        if (direction == 0)
            return Build(times, states, hits, steps, dense, success, stopped, accepted, rejected, evaluations);

        double[] k1 = Rhs(time, state);
        double step = options.InitialStep > 0
            ? options.InitialStep
            : InitialStep(Rhs, time, state, k1, direction, options);
        step = direction * Math.Min(Math.Min(step, options.MaxStep), Math.Abs(tEnd - t0));

        bool lastRejected = false;
        var stage = new double[n];
        var next = new double[n];

        while (direction * (tEnd - time) > 0)
        {
            if (accepted + rejected >= options.MaxSteps)
            {
                success = false;
                break;
            }

            bool reachesEnd = direction * (time + step - tEnd) >= 0;

            if (reachesEnd)
                step = tEnd - time;

            if (Math.Abs(step) < 16 * double.Epsilon + (16 * 2.220446049250313e-16 * Math.Abs(time)))
            {
                success = false;
                break;
            }

            double h = step;

            for (int i = 0; i < n; i++) stage[i] = state[i] + (h * A21 * k1[i]);
            double[] k2 = Rhs(time + (C2 * h), stage);

            for (int i = 0; i < n; i++) stage[i] = state[i] + (h * ((A31 * k1[i]) + (A32 * k2[i])));
            double[] k3 = Rhs(time + (C3 * h), stage);

            for (int i = 0; i < n; i++) stage[i] = state[i] + (h * ((A41 * k1[i]) + (A42 * k2[i]) + (A43 * k3[i])));
            double[] k4 = Rhs(time + (C4 * h), stage);

            for (int i = 0; i < n; i++)
                stage[i] = state[i] + (h * ((A51 * k1[i]) + (A52 * k2[i]) + (A53 * k3[i]) + (A54 * k4[i])));
            double[] k5 = Rhs(time + (C5 * h), stage);

            for (int i = 0; i < n; i++)
                stage[i] = state[i] + (h * ((A61 * k1[i]) + (A62 * k2[i]) + (A63 * k3[i]) + (A64 * k4[i]) + (A65 * k5[i])));
            double[] k6 = Rhs(time + h, stage);

            for (int i = 0; i < n; i++)
                next[i] = state[i] + (h * ((B1 * k1[i]) + (B3 * k3[i]) + (B4 * k4[i]) + (B5 * k5[i]) + (B6 * k6[i])));

            double newTime = reachesEnd ? tEnd : time + h;
            double[] k7 = Rhs(newTime, next);

            double errorSum = 0;

            for (int i = 0; i < n; i++)
            {
                double error = h * ((E1 * k1[i]) + (E3 * k3[i]) + (E4 * k4[i]) + (E5 * k5[i]) + (E6 * k6[i]) + (E7 * k7[i]));
                double tolerance = options.AbsoluteTolerance
                    + (options.RelativeTolerance * Math.Max(Math.Abs(state[i]), Math.Abs(next[i])));
                double scaled = error / tolerance;
                errorSum += scaled * scaled;
            }

            double errorNorm = n > 0 ? Math.Sqrt(errorSum / n) : 0;

            if (!(errorNorm <= 1))
            {
                // Отвергнутый шаг (в том числе при переполнении): уменьшаем и повторяем
                rejected++;
                double shrink = double.IsFinite(errorNorm) ? Math.Max(MinFactor, Safety * Math.Pow(errorNorm, -0.2)) : MinFactor;
                step *= shrink;
                lastRejected = true;
                continue;
            }

            accepted++;
            double[] coefficients = DenseCoefficients(state, next, k1, k3, k4, k5, k6, k7, h);
            double[] accepted7 = (double[])next.Clone();

            OdeEventHit? terminal = FindEvents(
                events, previousEvents, coefficients, time, h, newTime, accepted7, direction, options.EventTolerance, hits);

            steps.Add(h);
            dense.Add(coefficients);

            if (terminal != null)
            {
                times.Add(terminal.Time);
                states.Add(terminal.State.Clone());
                stopped = true;
                break;
            }

            times.Add(newTime);
            states.Add(new Vector(accepted7));

            time = newTime;
            state = accepted7;
            k1 = k7;

            double factor = errorNorm == 0 ? MaxFactor : Math.Min(MaxFactor, Math.Max(MinFactor, Safety * Math.Pow(errorNorm, -0.2)));

            if (lastRejected)
                factor = Math.Min(factor, 1.0);

            step = direction * Math.Min(Math.Abs(h * factor), options.MaxStep);
            lastRejected = false;
        }

        return Build(times, states, hits, steps, dense, success, stopped, accepted, rejected, evaluations);
    }

    private static OdeSolution Build(
        List<double> times,
        List<Vector> states,
        List<OdeEventHit> hits,
        List<double> steps,
        List<double[]> dense,
        bool success,
        bool stopped,
        int accepted,
        int rejected,
        int evaluations)
    {
        return new OdeSolution(new Vector(times), states, hits, steps, dense)
        {
            Success = success,
            StoppedByEvent = stopped,
            AcceptedSteps = accepted,
            RejectedSteps = rejected,
            FunctionEvaluations = evaluations
        };
    }

    // Коэффициенты плотной выдачи: y(θ) = r1 + θ(r2 + (1-θ)(r3 + θ(r4 + (1-θ)r5)))
    private static double[] DenseCoefficients(
        double[] y, double[] yNext, double[] k1, double[] k3, double[] k4, double[] k5, double[] k6, double[] k7, double h)
    {
        int n = y.Length;
        var result = new double[5 * n];

        for (int i = 0; i < n; i++)
        {
            double r2 = yNext[i] - y[i];
            double r3 = (h * k1[i]) - r2;
            double r4 = r2 - (h * k7[i]) - r3;
            double r5 = h * ((D1 * k1[i]) + (D3 * k3[i]) + (D4 * k4[i]) + (D5 * k5[i]) + (D6 * k6[i]) + (D7 * k7[i]));

            result[i] = y[i];
            result[n + i] = r2;
            result[(2 * n) + i] = r3;
            result[(3 * n) + i] = r4;
            result[(4 * n) + i] = r5;
        }

        return result;
    }

    // Ищет смены знака функций событий на принятом шаге, записывает их по порядку времени
    // и возвращает первое останавливающее событие (или null)
    private static OdeEventHit? FindEvents(
        IReadOnlyList<OdeEvent> events,
        double[] previous,
        double[] coefficients,
        double start,
        double h,
        double end,
        double[] endState,
        double direction,
        double tolerance,
        List<OdeEventHit> hits)
    {
        if (events.Count == 0)
            return null;

        var found = new List<(double Time, int Index)>();
        var endValues = new double[events.Count];
        var endVector = new Vector(endState);

        for (int e = 0; e < events.Count; e++)
        {
            OdeEvent current = events[e];
            double before = previous[e];
            double after = current.Function(end, endVector);
            endValues[e] = after;

            // Ноль в начале шага уже учтен на предыдущем шаге
            bool crosses = (before < 0 && after >= 0) || (before > 0 && after <= 0);

            if (!crosses)
                continue;

            bool rising = before < 0;

            if ((current.Direction > 0 && !rising) || (current.Direction < 0 && rising))
                continue;

            double Value(double t) => current.Function(t, new Vector(OdeSolution.DenseState(coefficients, start, h, t)));

            double root = LocateRoot(Value, start, before, end, after, tolerance * Math.Max(1, Math.Abs(end)));
            found.Add((root, e));
        }

        Array.Copy(endValues, previous, endValues.Length);
        found.Sort((left, right) => (direction * left.Time).CompareTo(direction * right.Time));

        foreach (var (eventTime, index) in found)
        {
            var hit = new OdeEventHit(index, eventTime, new Vector(OdeSolution.DenseState(coefficients, start, h, eventTime)));
            hits.Add(hit);

            if (events[index].IsTerminal)
                return hit;
        }

        return null;
    }

    // Метод Иллинойса: ложное положение с уполовиниванием значения на "застрявшем" конце
    private static double LocateRoot(Func<double, double> function, double a, double fa, double b, double fb, double tolerance)
    {
        if (fb == 0)
            return b;

        int side = 0;
        double root = b;

        for (int iteration = 0; iteration < 200 && Math.Abs(b - a) > tolerance; iteration++)
        {
            root = ((a * fb) - (b * fa)) / (fb - fa);

            // Выход за отрезок из-за округления заменяем серединой
            if (!(root > Math.Min(a, b) && root < Math.Max(a, b)))
                root = 0.5 * (a + b);

            double value = function(root);

            if (value == 0)
                return root;

            if ((value > 0) == (fb > 0))
            {
                b = root;
                fb = value;

                if (side == -1)
                    fa *= 0.5;

                side = -1;
            }
            else
            {
                a = root;
                fa = value;

                if (side == 1)
                    fb *= 0.5;

                side = 1;
            }
        }

        return root;
    }

    // Начальный шаг по Хайреру: из оценок первой и второй производных решения
    private static double InitialStep(
        Func<double, double[], double[]> rhs, double t0, double[] y0, double[] f0, double direction, DormandPrinceOptions options)
    {
        int n = y0.Length;
        double d0 = 0, d1 = 0;

        for (int i = 0; i < n; i++)
        {
            double scale = options.AbsoluteTolerance + (options.RelativeTolerance * Math.Abs(y0[i]));
            d0 += (y0[i] / scale) * (y0[i] / scale);
            d1 += (f0[i] / scale) * (f0[i] / scale);
        }

        d0 = Math.Sqrt(d0 / Math.Max(n, 1));
        d1 = Math.Sqrt(d1 / Math.Max(n, 1));

        double h0 = d0 < 1e-5 || d1 < 1e-5 ? 1e-6 : 0.01 * d0 / d1;
        var y1 = new double[n];

        for (int i = 0; i < n; i++)
            y1[i] = y0[i] + (direction * h0 * f0[i]);

        double[] f1 = rhs(t0 + (direction * h0), y1);
        double d2 = 0;

        for (int i = 0; i < n; i++)
        {
            double scale = options.AbsoluteTolerance + (options.RelativeTolerance * Math.Abs(y0[i]));
            double value = (f1[i] - f0[i]) / scale;
            d2 += value * value;
        }

        d2 = Math.Sqrt(d2 / Math.Max(n, 1)) / h0;

        double largest = Math.Max(d1, d2);
        double h1 = largest <= 1e-15 ? Math.Max(1e-6, h0 * 1e-3) : Math.Pow(0.01 / largest, 0.2);

        return Math.Min(100 * h0, h1);
    }
}
