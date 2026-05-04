using System;

namespace AI.ML.NeuralNetworks.V2.Autograd;

/// <summary>
/// Численная проверка градиентов: сравнивает аналитический градиент,
/// вычисленный движком autograd, с finite-difference приближением.
/// </summary>
/// <remarks>
/// Аналог <c>torch.autograd.gradcheck</c>. Используется в тестах:
/// <code>
/// var x = Tensor.Randn(new Shape(3,4)).SetRequiresGrad();
/// Assert.True(GradCheck.Check(x, t => t.Relu().Sum()));
/// </code>
/// Реализация: тензор <c>x</c> приводится к contiguous-форме,
/// чтобы линейная индексация по элементам была корректна для view-тензоров.
/// </remarks>
public static class GradCheck
{
    /// <summary>
    /// Проверить градиенты функции <paramref name="fn"/> по входу <paramref name="x"/>.
    /// </summary>
    /// <param name="x">Входной тензор (должен иметь requires_grad=true и быть на CPU).</param>
    /// <param name="fn">Функция, возвращающая скалярный тензор.</param>
    /// <param name="eps">Шаг конечных разностей.</param>
    /// <param name="rtol">Относительная погрешность.</param>
    /// <param name="atol">Абсолютная погрешность.</param>
    /// <returns>true, если градиенты совпадают в пределах допуска.</returns>
    public static bool Check(Tensor x, Func<Tensor, Tensor> fn,
        double eps = 1e-3, double rtol = 1e-2, double atol = 1e-3)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (fn == null) throw new ArgumentNullException(nameof(fn));
        if (!x.RequiresGrad)
            throw new ArgumentException("Входной тензор должен иметь requires_grad=true.", nameof(x));
        if (x.DType != DType.Float32)
            throw new NotSupportedException("GradCheck реализован пока для Float32.");
        if (x.Device.Type != DeviceType.Cpu)
            throw new NotSupportedException("GradCheck реализован пока только для CPU-тензоров.");
        long n = x.NumElements;
        if (n > int.MaxValue)
            throw new OverflowException(
                $"GradCheck: NumElements={n} превышает int.MaxValue.");
        if (!x.IsContiguous)
            throw new ArgumentException(
                "GradCheck требует contiguous-входа. Вызовите .Contiguous() перед SetRequiresGrad.",
                nameof(x));

        x.Grad = null;
        var y = fn(x);
        if (y.NumElements != 1)
            throw new ArgumentException("fn должна возвращать скалярный тензор.", nameof(fn));
        y.Backward();
        var analyticGrad = x.Grad
            ?? throw new InvalidOperationException("Autograd не записал градиент в x.Grad.");
        var analyticContiguous = analyticGrad.IsContiguous ? analyticGrad : analyticGrad.Contiguous();

        var xData = x.AsSpan<float>();
        var gA = analyticContiguous.AsReadOnlySpan<float>();
        int total = (int)n;
        for (int i = 0; i < total; i++)
        {
            float orig = xData[i];
            xData[i] = orig + (float)eps;
            using (TapeContext.NoGrad())
            {
                var yPlusT = fn(x);
                var yPlus = (yPlusT.IsContiguous ? yPlusT : yPlusT.Contiguous()).AsReadOnlySpan<float>()[0];
                xData[i] = orig - (float)eps;
                var yMinusT = fn(x);
                var yMinus = (yMinusT.IsContiguous ? yMinusT : yMinusT.Contiguous()).AsReadOnlySpan<float>()[0];
                xData[i] = orig;
                double numerical = (yPlus - yMinus) / (2.0 * eps);
                double analytical = gA[i];
                double diff = Math.Abs(numerical - analytical);
                double tol = atol + rtol * Math.Max(Math.Abs(numerical), Math.Abs(analytical));
                if (diff > tol)
                    return false;
            }
        }
        return true;
    }
}
