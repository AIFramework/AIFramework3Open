#nullable enable

using System;
using System.Collections.Generic;

namespace AI.MathUtils.Integration;

/// <summary>
/// Адаптивная квадратура Гаусса-Кронрода 7-15 с глобальным управлением погрешностью.
/// </summary>
/// <remarks>
/// На каждом отрезке интеграл считается по 15 узлам Кронрода, а разность с правилом Гаусса по 7 из тех же узлов
/// служит оценкой погрешности. Делится пополам отрезок с наибольшей оценкой, пока сумма оценок не станет меньше
/// заданной доли модуля интеграла или абсолютного порога. Узлы лежат внутри отрезка, поэтому интегрируемая
/// особенность на конце (1/√x в нуле) не дает бесконечного значения. Результат не зависит от случая и порядка потоков.
/// </remarks>
public static class GaussKronrod
{
    /// <summary>Наибольшее число отрезков разбиения: предел работы на плохой функции</summary>
    private const int MaxSegments = 2000;

    /// <summary>Узлы правила Кронрода на [-1, 1] по неотрицательной половине; нечетные по счету это узлы Гаусса</summary>
    private static readonly double[] Nodes =
    [
        0.991455371120812639206854697526329, 0.949107912342758524526189684047851, 0.864864423359769072789712788640926,
        0.741531185599394439863864773280788, 0.586087235467691130294144845693013, 0.405845151377397166906606412076961,
        0.207784955007898467600689403773245, 0.0,
    ];

    /// <summary>Веса правила Кронрода при узлах <see cref="Nodes"/></summary>
    private static readonly double[] KronrodWeights =
    [
        0.022935322010529224963732008058970, 0.063092092629978553290700663189204, 0.104790010322250183839876322541518,
        0.140653259715525918745189590510238, 0.169004726639267902826583426598550, 0.190350578064785409913256402421014,
        0.204432940075298892414161999234649, 0.209482141084727828012999174891714,
    ];

    /// <summary>Веса правила Гаусса при узлах Nodes[1], Nodes[3], Nodes[5] и нуле</summary>
    private static readonly double[] GaussWeights =
    [
        0.129484966168869693270611432679082, 0.279705391489276667901467771423780, 0.381830050505118944950369775488975,
        0.417959183673469387755102040816327,
    ];

    /// <summary>
    /// Определенный интеграл функции на отрезке.
    /// </summary>
    /// <param name="f">Подынтегральная функция</param>
    /// <param name="a">Нижний предел</param>
    /// <param name="b">Верхний предел; при b &lt; a интеграл меняет знак</param>
    /// <param name="relativeTolerance">Допустимая погрешность в долях модуля интеграла</param>
    /// <param name="absoluteTolerance">Допустимая абсолютная погрешность: порог для интеграла, близкого к нулю</param>
    /// <returns>Значение интеграла; NaN, если функция не определена в каком-либо узле</returns>
    /// <exception cref="ArgumentNullException">Функция не задана</exception>
    public static double Integrate(
        Func<double, double> f, double a, double b, double relativeTolerance = 1e-10, double absoluteTolerance = 1e-14)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a == b)
            return 0;

        var segments = new PriorityQueue<(double A, double B, double Value, double Error), double>();
        var (value, error) = Rule(f, a, b);
        segments.Enqueue((a, b, value, error), -error);
        double total = value, totalError = error;

        while (segments.Count < MaxSegments && double.IsFinite(total)
            && totalError > Math.Max(absoluteTolerance, relativeTolerance * Math.Abs(total)))
        {
            var worst = segments.Dequeue();
            double middle = (worst.A + worst.B) / 2;
            if (middle == worst.A || middle == worst.B)
            {
                // Отрезок дальше не делится в двойной точности: точнее не станет
                segments.Enqueue(worst, double.PositiveInfinity);
                break;
            }

            var (left, leftError) = Rule(f, worst.A, middle);
            var (right, rightError) = Rule(f, middle, worst.B);
            segments.Enqueue((worst.A, middle, left, leftError), -leftError);
            segments.Enqueue((middle, worst.B, right, rightError), -rightError);
            total += left + right - worst.Value;
            totalError += leftError + rightError - worst.Error;
        }

        // Итог пересчитывается суммой по отрезкам: накопленные поправки теряют младшие разряды
        double sum = 0;
        foreach (var (segment, _) in segments.UnorderedItems)
            sum += segment.Value;
        return sum;
    }

    /// <summary>Интеграл на отрезке по правилу Кронрода и оценка погрешности разностью с правилом Гаусса</summary>
    private static (double Value, double Error) Rule(Func<double, double> f, double a, double b)
    {
        double center = (a + b) / 2, half = (b - a) / 2;
        double centerValue = f(center);
        double kronrod = KronrodWeights[7] * centerValue, gauss = GaussWeights[3] * centerValue;
        for (int i = 0; i < 7; i++)
        {
            double offset = half * Nodes[i];
            double pair = f(center - offset) + f(center + offset);
            kronrod += KronrodWeights[i] * pair;
            if (i % 2 == 1)
                gauss += GaussWeights[i / 2] * pair;
        }

        return (kronrod * half, Math.Abs((kronrod - gauss) * half));
    }
}
