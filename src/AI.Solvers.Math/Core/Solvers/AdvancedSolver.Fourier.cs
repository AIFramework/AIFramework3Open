using System.Text;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;
using AI.Solvers.Math.Core.Patterns;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    // -- Преобразование Фурье --------------------------------------------

    public static string FourierTransform(string expression, string variable = "x")
    {
        try
        {
            var expr    = AdvancedMathExpression.Parse(expression).Simplify();
            var exprStr = expr.ToString();

            // F{c} = c·2π·δ(ω)
            if (expr is Constant c)
                return System.Math.Abs(c.Value - 1) < 1e-10
                    ? "F{1} = 2π·δ(ω)  (дельта-функция Дирака)"
                    : $"F{{{c.Value}}} = {c.Value}·2π·δ(ω)";

            // F{exp(-ax²)} и F{exp(ax)}
            if (expr is Exp expNode)
            {
                var res = TryFourierExp(expNode, variable);
                if (res != null) return res;
            }

            // F{sin(ω₀x)}
            if (expr is Sin sin)
            {
                var res = TryFourierSin(sin, variable, exprStr);
                if (res != null) return res;
            }

            // F{cos(ω₀x)}
            if (expr is Cos cos)
            {
                var res = TryFourierCos(cos, variable, exprStr);
                if (res != null) return res;
            }

            // F{exp·sin} / F{exp·cos} и смешанные произведения
            if (expr is Multiply multProd)
            {
                var res = TryFourierProduct(multProd, variable, exprStr);
                if (res != null) return res;
            }

            // Специальные функции и сигналы
            var special = TryFourierSpecial(expr, variable, exprStr);
            if (special != null) return special;

            // Линейность F{f+g}
            if (expr is Add addExpr)
            {
                var lRes = FourierTransformInternal(addExpr.Left,  variable);
                var rRes = FourierTransformInternal(addExpr.Right, variable);
                if (lRes != null && rRes != null)
                    return $"F{{{exprStr}}} = F{{{addExpr.Left}}} + F{{{addExpr.Right}}}\n\n" +
                           $"  = ({lRes})\n  + ({rRes})";
            }

            // F{c·f} = c·F{f}
            if (expr is Multiply multConst)
            {
                Constant? cf   = null;
                Expression? fp = null;
                if (multConst.Left  is Constant cl) { cf = cl; fp = multConst.Right; }
                else if (multConst.Right is Constant cr) { cf = cr; fp = multConst.Left; }

                if (cf != null && fp != null)
                {
                    var fRes = FourierTransformInternal(fp, variable);
                    if (fRes != null)
                        return $"F{{{exprStr}}} = {cf.Value}·F{{{fp}}}\n\n  = {cf.Value}·({fRes})";
                }
            }

            return ComputeNumericalFourier(expr, exprStr, variable);
        }
        catch (Exception ex)
        {
            return $"F{{{expression}}} - ошибка: {ex.Message}";
        }
    }

    // -- Частные случаи аналитических формул ----------------------------

    private static string? TryFourierExp(Exp expNode, string variable)
    {
        // F{exp(-ax²)}
        if (ExpressionPatterns.TryMatchGaussian(expNode, variable, out double decay))
        {
            return System.Math.Abs(decay - 1) < 1e-10
                ? "F{exp(-x²)} = √π · exp(-ω²/4)"
                : $"F{{exp(-{decay}x²)}} = √(π/{decay}) · exp(-ω²/{4 * decay})";
        }

        // F{exp(ax)}: e^(ax) = e^(i·(-ia)x), а F{e^(iξx)} = 2π·δ(ω-ξ),
        // поэтому ξ = -ia и полюс стоит в ω = -ia, то есть δ(ω + i·a).
        if (expNode.Argument is Multiply ml &&
            ml.Left is Constant cl &&
            ml.Right is Variable vl && vl.Name == variable)
        {
            double a = cl.Value;
            return a > 0
                ? $"F{{exp({a}x)}} = 2π·δ(ω+i·{a})  (сдвиг в комплексной плоскости)"
                : $"F{{exp({a}x)}} = 2π·δ(ω-i·{-a})  (сдвиг в комплексной плоскости)";
        }

        if (expNode.Argument is Variable ve && ve.Name == variable)
            return "F{exp(x)} = 2π·δ(ω+i)  (сдвиг в комплексной плоскости)";

        return null;
    }

    private static string? TryFourierSin(Sin sin, string variable, string exprStr)
    {
        if (sin.Argument is Multiply ms &&
            ms.Left is Constant cs &&
            ms.Right is Variable vs && vs.Name == variable)
        {
            double omega0 = cs.Value;
            string w1 = omega0 >= 0 ? $"(ω-{omega0})" : $"(ω+{-omega0})";
            string w2 = omega0 >= 0 ? $"(ω+{omega0})" : $"(ω-{-omega0})";
            string arg = omega0 == 1 ? "sin(x)" : omega0 == -1 ? "sin(-x)" : $"sin({omega0}x)";
            return $"F{{{arg}}} = -i·π[δ{w1} - δ{w2}]";
        }
        if (sin.Argument is Variable vs2 && vs2.Name == variable)
            return "F{sin(x)} = -i·π[δ(ω-1) - δ(ω+1)]";
        return null;
    }

    private static string? TryFourierCos(Cos cos, string variable, string exprStr)
    {
        if (cos.Argument is Multiply mc &&
            mc.Left is Constant cc &&
            mc.Right is Variable vc && vc.Name == variable)
        {
            double omega0 = cc.Value;
            string w1 = omega0 >= 0 ? $"(ω-{omega0})" : $"(ω+{-omega0})";
            string w2 = omega0 >= 0 ? $"(ω+{omega0})" : $"(ω-{-omega0})";
            string arg = omega0 == 1 ? "cos(x)" : omega0 == -1 ? "cos(-x)" : $"cos({omega0}x)";
            return $"F{{{arg}}} = π[δ{w1} + δ{w2}]";
        }
        if (cos.Argument is Variable vc2 && vc2.Name == variable)
            return "F{cos(x)} = π[δ(ω-1) + δ(ω+1)]";
        return null;
    }

    private static string? TryFourierProduct(Multiply multProd, string variable, string exprStr)
    {
        Exp? expPart    = null;
        Sin? sinPart    = null;
        Cos? cosPart    = null;
        Constant? cPart = null;

        void Classify(Expression e)
        {
            if (e is Exp ex) expPart = ex;
            else if (e is Sin s) sinPart = s;
            else if (e is Cos co) cosPart = co;
            else if (e is Constant co2) cPart = co2;
        }
        Classify(multProd.Left);
        Classify(multProd.Right);

        if (expPart == null || (sinPart == null && cosPart == null))
            return null;

        bool isSin   = sinPart != null;
        var trigArg  = isSin ? sinPart!.Argument : cosPart!.Argument;

        // Гауссиан × trig
        if (ExpressionPatterns.TryMatchGaussian(expPart, variable, out double gaussA))
        {
            double b = ExpressionPatterns.LinearCoefficient(trigArg, variable);
            string expStr  = System.Math.Abs(gaussA - 1) < 1e-10 ? "exp(-x²)" : $"exp(-{gaussA}x²)";
            string trigStr = b == 1 ? (isSin ? "sin(x)" : "cos(x)") : (isSin ? $"sin({b}x)" : $"cos({b}x)");

            // F{exp(-ax²)·sin(bx)} = (1/2i)[G(ω-b) - G(ω+b)] = -i·√(π/a)·exp(-(ω²+b²)/4a)·sinh(bω/2a)
            // F{exp(-ax²)·cos(bx)} = (1/2) [G(ω-b) + G(ω+b)] =    √(π/a)·exp(-(ω²+b²)/4a)·cosh(bω/2a)
            if (System.Math.Abs(gaussA - 1) < 1e-10 && System.Math.Abs(b - 1) < 1e-10)
                return isSin
                    ? "F{exp(-x²)·sin(x)} = -i·√π·exp(-(ω²+1)/4)·sinh(ω/2)"
                    : "F{exp(-x²)·cos(x)} = √π·exp(-(ω²+1)/4)·cosh(ω/2)";

            return isSin
                ? $"F{{{expStr}·{trigStr}}} = -i·√(π/{gaussA})·exp(-(ω²+{b * b})/(4·{gaussA}))·sinh({b}ω/(2·{gaussA}))"
                : $"F{{{expStr}·{trigStr}}} = √(π/{gaussA})·exp(-(ω²+{b * b})/(4·{gaussA}))·cosh({b}ω/(2·{gaussA}))";
        }

        // Линейная экспонента × trig (теорема сдвига)
        double a = 0;
        bool isLinear = false;
        if (expPart.Argument is Multiply mlExp &&
            mlExp.Left is Constant clExp &&
            mlExp.Right is Variable vlExp && vlExp.Name == variable)
        { a = clExp.Value; isLinear = true; }
        else if (expPart.Argument is Variable vExpVar && vExpVar.Name == variable)
        { a = 1; isLinear = true; }

        if (!isLinear) return null;

        double bTrig = ExpressionPatterns.LinearCoefficient(trigArg, variable);

        // Множитель e^(ax) сдвигает спектр на МНИМУЮ величину: F{e^(ax)g(x)} = G(ω + i·a).
        // Раньше здесь складывались вещественная частота и вещественное a,
        // и получалось δ(ω-b+a) вместо δ(ω-b+i·a).
        string shift = a >= 0 ? $"+i·{a}" : $"-i·{-a}";
        string omega1 = bTrig >= 0 ? $"(ω-{bTrig}{shift})" : $"(ω+{-bTrig}{shift})";
        string omega2 = bTrig >= 0 ? $"(ω+{bTrig}{shift})" : $"(ω-{-bTrig}{shift})";

        string expPStr = a == 0 ? "" : a == 1 ? "exp(x)·" : a == -1 ? "exp(-x)·" : $"exp({a}x)·";
        string trig2   = bTrig == 1 ? (isSin ? "sin(x)" : "cos(x)") :
                         bTrig == -1 ? (isSin ? "sin(-x)" : "cos(-x)") :
                         isSin ? $"sin({bTrig}x)" : $"cos({bTrig}x)";

        if (cPart != null)
        {
            return isSin
                ? $"F{{{exprStr}}} = -{cPart.Value}·i·π[δ{omega1} - δ{omega2}]  (теорема сдвига)"
                : $"F{{{exprStr}}} = {cPart.Value}·π[δ{omega1} + δ{omega2}]  (теорема сдвига)";
        }
        return isSin
            ? $"F{{{expPStr}{trig2}}} = -i·π[δ{omega1} - δ{omega2}]  (теорема сдвига)"
            : $"F{{{expPStr}{trig2}}} = π[δ{omega1} + δ{omega2}]  (теорема сдвига)";
    }

    private static string? TryFourierSpecial(Expression expr, string variable, string exprStr)
    {
        // sgn(x)
        if (expr is Sgn sgn && sgn.Argument is Variable vSgn && vSgn.Name == variable)
            return "F{sgn(x)} = 2/(i·ω) = -2i/ω";

        // H(x)
        if (expr is Heaviside h && h.Argument is Variable vH && vH.Name == variable)
            return "F{H(x)} = π·δ(ω) + 1/(i·ω)";

        // 1/(1+x²)
        if (expr is Power powL && powL.Exponent is Constant ceL && System.Math.Abs(ceL.Value + 1) < 1e-10 &&
            powL.Base is Add addL &&
            addL.Left is Constant c1L && System.Math.Abs(c1L.Value - 1) < 1e-10 &&
            addL.Right is Power p2L &&
            p2L.Base is Variable vL && vL.Name == variable &&
            p2L.Exponent is Constant ce2L && System.Math.Abs(ce2L.Value - 2) < 1e-10)
            return "F{1/(1+x²)} = π·exp(-|ω|)  (Лоренциан/распределение Коши)";

        // |x|
        if (expr is Abs absX && absX.Argument is Variable vAbs && vAbs.Name == variable)
            return "F{|x|} = -2/ω²";

        // sin(x)/x
        if (IsSincPattern(expr, variable))
            return "F{sin(x)/x} = rect(ω/2) = {π для |ω|<1, 0 для |ω|>1}  (прямоугольное окно)";

        // exp(-|x|)
        if (expr is Exp expAbs && expAbs.Argument is Multiply mAbs &&
            mAbs.Left is Constant cAbs && cAbs.Value < 0 &&
            mAbs.Right is Abs absArg && absArg.Argument is Variable vAbs2 && vAbs2.Name == variable)
        {
            double a = -cAbs.Value;
            return System.Math.Abs(a - 1) < 1e-10
                ? "F{exp(-|x|)} = 2/(1+ω²)  (двусторонняя экспонента)"
                : $"F{{exp(-{a}|x|)}} = {2 * a}/({a}²+ω²)";
        }

        // x
        if (expr is Variable vX && vX.Name == variable)
            return "F{x} = 2π·i·δ'(ω)  (производная дельта-функции)";

        // x^n
        if (expr is Power powXN && powXN.Base is Variable vXN && vXN.Name == variable &&
            powXN.Exponent is Constant ceXN && ceXN.Value > 0 &&
            System.Math.Abs(ceXN.Value - System.Math.Round(ceXN.Value)) < 1e-10)
        {
            int n = (int)System.Math.Round(ceXN.Value);
            return $"F{{x^{n}}} = 2π·i^{n}·δ^({n})(ω)  ({n}-я производная дельта-функции)";
        }

        // 1/x
        if (expr is Power powInv && powInv.Exponent is Constant ceInv && System.Math.Abs(ceInv.Value + 1) < 1e-10 &&
            powInv.Base is Variable vInv && vInv.Name == variable)
            return "F{1/x} = -i·π·sgn(ω)  (в смысле обобщенных функций)";

        return null;
    }

    private static bool IsSincPattern(Expression expr, string variable)
    {
        if (expr is not Multiply mult) return false;
        Sin? sinPart    = mult.Left as Sin ?? mult.Right as Sin;
        Power? invXPart = null;
        if (mult.Left  is Power p1 && p1.Exponent is Constant c1 && System.Math.Abs(c1.Value + 1) < 1e-10) invXPart = p1;
        if (mult.Right is Power p2 && p2.Exponent is Constant c2 && System.Math.Abs(c2.Value + 1) < 1e-10) invXPart = p2;
        return sinPart != null && invXPart != null &&
               sinPart.Argument is Variable vs && vs.Name == variable &&
               invXPart.Base is Variable vi && vi.Name == variable;
    }

    // -- Рекурсивное вычисление (для свойства линейности) ---------------

    private static string? FourierTransformInternal(Expression expr, string variable)
    {
        try
        {
            var s = expr.Simplify();
            if (s is Constant c)
                return System.Math.Abs(c.Value - 1) < 1e-10 ? "2π·δ(ω)" : $"{c.Value}·2π·δ(ω)";

            if (s is Exp eg && ExpressionPatterns.TryMatchGaussian(eg, variable, out double a))
                return System.Math.Abs(a - 1) < 1e-10 ? "√π · exp(-ω²/4)" : $"√(π/{a}) · exp(-ω²/{4 * a})";

            if (s is Sin sin)
            {
                if (sin.Argument is Multiply ms && ms.Left is Constant cs && ms.Right is Variable vs && vs.Name == variable)
                {
                    double o = cs.Value;
                    return $"-i·π[δ{(o >= 0 ? $"(ω-{o})" : $"(ω+{-o})")} - δ{(o >= 0 ? $"(ω+{o})" : $"(ω-{-o})")}]";
                }
                if (sin.Argument is Variable vs2 && vs2.Name == variable)
                    return "-i·π[δ(ω-1) - δ(ω+1)]";
            }

            if (s is Cos cos)
            {
                if (cos.Argument is Multiply mc && mc.Left is Constant cc && mc.Right is Variable vc && vc.Name == variable)
                {
                    double o = cc.Value;
                    return $"π[δ{(o >= 0 ? $"(ω-{o})" : $"(ω+{-o})")} + δ{(o >= 0 ? $"(ω+{o})" : $"(ω-{-o})")}]";
                }
                if (cos.Argument is Variable vc2 && vc2.Name == variable)
                    return "π[δ(ω-1) + δ(ω+1)]";
            }

            if (s is Variable vX && vX.Name == variable)
                return "2π·i·δ'(ω)";

            if (s is Power px && px.Base is Variable vxn && vxn.Name == variable &&
                px.Exponent is Constant cxn && cxn.Value > 0 &&
                System.Math.Abs(cxn.Value - System.Math.Round(cxn.Value)) < 1e-10)
            {
                int n = (int)System.Math.Round(cxn.Value);
                return $"2π·i^{n}·δ^({n})(ω)";
            }

            return null;
        }
        catch
        {
            // Шаблоны Фурье-преобразования через жёсткое сопоставление AST: при
            // структурном несовпадении пробрасывается NullReferenceException и др.
            // Возвращаем null, чтобы вызывающий код перешёл к численному FFT.
            return null;
        }
    }

    // -- Численная оценка F(ω) на конечном окне (ДПФ + окно Блэкмана) -----

    /// <summary>
    /// Оценивает F(ω) = ∫f(x)·e^(-iωx)dx на окне [-T/2, T/2].
    /// <para>
    /// Три поправки, без которых числа в выводе не значили ничего:
    /// сетка частот — угловая (ω_k = 2πk/T, а не k/T); множитель dt, без которого
    /// это просто сумма отсчётов, а не интеграл; фаза приводится к центру окна
    /// (сдвиг на -T/2 даёт множитель e^(iω_k·T/2) = (-1)^k). Окно Блэкмана
    /// компенсируется по когерентному усилению, иначе амплитуда занижена в ~2.4 раза.
    /// </para>
    /// </summary>
    private static string ComputeNumericalFourier(Expression expr, string exprStr, string variable)
    {
        try
        {
            const int N    = 512;
            const double T = 10.0;
            double dt = T / N;

            var values = new double[N];
            var vars   = new Dictionary<string, double>();

            for (int n = 0; n < N; n++)
            {
                vars[variable] = (-T / 2) + (n * dt);
                try
                {
                    double value = EvaluateExpression(expr, vars);
                    values[n] = double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
                }
                catch
                {
                    // В точке n выражение неопределено (например, 1/x в нуле):
                    // используем 0 как значение сэмпла. Это вносит небольшую
                    // ошибку в спектр, но позволяет продолжить анализ.
                    values[n] = 0;
                }
            }

            // Окно нужно только тому сигналу, который на краях окна не затух:
            // иначе оно давит уже затухшую функцию, а компенсация усиления
            // (деление на 0.42) завышает амплитуду импульса в центре в 2.4 раза.
            double peakValue = values.Max(System.Math.Abs);
            double edgeValue = System.Math.Max(System.Math.Abs(values[0]), System.Math.Abs(values[N - 1]));
            bool   windowed  = peakValue > 0 && edgeValue > 0.01 * peakValue;

            var samples = new System.Numerics.Complex[N];
            double windowSum = 0;

            for (int n = 0; n < N; n++)
            {
                double window = windowed
                    ? 0.42 - (0.5 * System.Math.Cos(2 * System.Math.PI * n / (N - 1)))
                           + (0.08 * System.Math.Cos(4 * System.Math.PI * n / (N - 1)))
                    : 1.0;
                windowSum += window;
                samples[n] = new System.Numerics.Complex(values[n] * window, 0);
            }

            var spectrum = ComputeDFT(samples);
            double coherentGain = windowSum / N;

            var harmonics = Enumerable.Range(0, N / 2)
                .Select(k => (
                    omega: 2 * System.Math.PI * k / T,
                    value: spectrum[k] * (dt / coherentGain) * (k % 2 == 0 ? 1 : -1)))
                .ToList();

            double peak = harmonics.Max(h => h.value.Magnitude);
            if (peak < 1e-9)
                return $"F{{{exprStr}}} ≈ 0  (численно: значимых компонент нет)";

            var top = harmonics
                .Where(h => h.value.Magnitude > 0.01 * peak)
                .OrderByDescending(h => h.value.Magnitude)
                .Take(5)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"F{{{exprStr}}} ≈ численная оценка (ДПФ по {N} точкам, " +
                          $"{(windowed ? "окно Блэкмана с компенсацией усиления" : "без окна: функция затухает на краях")}):");
            sb.AppendLine($"  Окно: x ∈ [{-T / 2:F1}, {T / 2:F1}], dt={dt:F4}, шаг по частоте Δω={2 * System.Math.PI / T:F4}");
            sb.AppendLine($"  Наибольшие по модулю значения F(ω):");

            foreach (var (omega, value) in top)
                sb.AppendLine($"    ω={omega:F3}: |F|={value.Magnitude:F4}, φ={value.Phase * 180 / System.Math.PI:F1}°");

            sb.Append("  Это оценка непрерывного преобразования на конечном окне: " +
                      "разрывы и медленно убывающие функции дают растекание спектра.");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"F{{{exprStr}}} - не удалось вычислить численно: {ex.Message}";
        }
    }

    /// <summary>
    /// Прямое суммирование ДПФ без нормировки: масштаб задаёт вызывающий код.
    /// O(N²) при N=512 — доли миллисекунды; готовый БПФ живёт в AI.DSP, но тянет
    /// за собой AI.ML и AI.KNN, что для одной трансформации несоразмерно.
    /// </summary>
    private static System.Numerics.Complex[] ComputeDFT(System.Numerics.Complex[] input)
    {
        int N = input.Length;
        var output = new System.Numerics.Complex[N];
        for (int k = 0; k < N; k++)
        {
            System.Numerics.Complex sum = System.Numerics.Complex.Zero;
            for (int n = 0; n < N; n++)
            {
                double angle = -2 * System.Math.PI * k * n / N;
                sum += input[n] * new System.Numerics.Complex(System.Math.Cos(angle), System.Math.Sin(angle));
            }
            output[k] = sum;
        }
        return output;
    }

}
