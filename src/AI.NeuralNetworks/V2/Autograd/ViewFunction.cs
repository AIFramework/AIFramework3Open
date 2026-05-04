using System;

namespace AI.ML.NeuralNetworks.V2.Autograd;

/// <summary>
/// Универсальный autograd-узел для view-операций (Reshape, Transpose, Permute,
/// Squeeze, Unsqueeze, Expand). Выполняет инверсную view-операцию над градиентом.
/// </summary>
/// <remarks>
/// View — это переинтерпретация той же памяти. Производная — это «обратная»
/// перестановка осей градиента. Для broadcast (Expand) — суммирование по
/// добавленным осям (через <see cref="Ops.Broadcasting.ReduceForBroadcast"/>).
/// </remarks>
public sealed class ViewFunction : Function
{
    private readonly Func<Tensor, Tensor> _inverseView;

    /// <summary>
    /// <paramref name="inverseView"/> — функция, преобразующая градиент output в
    /// градиент input (без копирования, если возможно).
    /// </summary>
    public ViewFunction(Func<Tensor, Tensor> inverseView)
    {
        _inverseView = inverseView ?? throw new ArgumentNullException(nameof(inverseView));
    }

    /// <inheritdoc/>
    public override Tensor[] Backward(Tensor gradOutput)
    {
        // Inverse-view может вернуть strided-вид; Engine ожидает contiguous-grad,
        // поэтому форсируем материализацию.
        var g = _inverseView(gradOutput);
        return new[] { g.IsContiguous ? g : g.Contiguous() };
    }
}
