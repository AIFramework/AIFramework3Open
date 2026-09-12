namespace AI.Physics.Nuclear;

/// <summary>
/// Разделённая разность экспоненты по произвольным узлам — ядро решения уравнений Бейтмана.
/// </summary>
/// <remarks>
/// <para>
/// Классическая формула Бейтмана — сумма экспонент, делённых на произведения разностей постоянных
/// распада. Она делит на ноль при совпадающих постоянных и теряет все знаки при близких, а также
/// в самом начале, пока все экспоненты почти равны единице: огромные слагаемые разных знаков
/// гасят друг друга. Та же сумма — разделённая разность функции <c>e^x</c>, и её можно
/// вычислять устойчиво.
/// </para>
/// <para>
/// Узлы сортируются. Если разброс узлов в группе не меньше единицы, работает обычная
/// рекуррентность: делитель велик, и вычитание безопасно. Если меньше — ряд Тейлора вокруг
/// середины группы через полные однородные симметрические многочлены: он сходится быстро
/// и не вычитает близкие числа. Совпадающие узлы — частный случай малого разброса.
/// </para>
/// <para>
/// Результат возвращается со сдвигом на наибольший узел, чтобы жёсткие цепочки, где времена
/// жизни различаются в 10¹⁵ раз, не уходили в переполнение.
/// </para>
/// </remarks>
internal static class ExponentialDividedDifference
{
    private const double SeriesSpread = 1.0;
    private const int SeriesTerms = 30;

    /// <summary>
    /// Разделённая разность <c>e^x</c> по узлам: <c>e[x₀…x_m] = e^shift · результат</c>
    /// </summary>
    /// <param name="nodes">Узлы в любом порядке</param>
    /// <param name="shift">Наибольший узел</param>
    internal static double Compute(ReadOnlySpan<double> nodes, out double shift)
    {
        if (nodes.IsEmpty)
            throw new ArgumentException("Нужен хотя бы один узел", nameof(nodes));

        double[] y = nodes.ToArray();
        Array.Sort(y);
        shift = y[^1];

        for (int i = 0; i < y.Length; i++)
            y[i] -= shift;

        // level[i] после шага length хранит разность по узлам y[i]…y[i + length]
        var level = new double[y.Length];

        for (int i = 0; i < y.Length; i++)
            level[i] = Math.Exp(y[i]);

        for (int length = 1; length < y.Length; length++)
        {
            for (int i = 0; i + length < y.Length; i++)
            {
                int j = i + length;
                double spread = y[j] - y[i];

                level[i] = spread < SeriesSpread
                    ? Series(y, i, j)
                    : (level[i + 1] - level[i]) / spread;
            }
        }

        return level[0];
    }

    // e[z₀…z_m] = Σ_p h_p(z)/(p + m)!, где h_p — полные однородные симметрические многочлены
    private static double Series(double[] y, int first, int last)
    {
        int order = last - first;
        double centre = 0.5 * (y[first] + y[last]);
        Span<double> h = stackalloc double[SeriesTerms + 1];
        h.Clear();
        h[0] = 1;

        for (int k = first; k <= last; k++)
        {
            double z = y[k] - centre;

            for (int p = 1; p <= SeriesTerms; p++)
                h[p] += z * h[p - 1];
        }

        double coefficient = 1;

        for (int k = 2; k <= order; k++)
            coefficient /= k;

        double sum = 0;

        for (int p = 0; p <= SeriesTerms; p++)
        {
            sum += h[p] * coefficient;
            coefficient /= order + p + 1;
        }

        return Math.Exp(centre) * sum;
    }
}
