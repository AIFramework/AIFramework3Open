using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>ReLU как Module (для использования в Sequential).</summary>
public sealed class ReLU : Module
{
    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => TensorOps.Relu(input);
}

/// <summary>Sigmoid как Module.</summary>
public sealed class Sigmoid : Module
{
    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => TensorOps.Sigmoid(input);
}

/// <summary>Tanh как Module.</summary>
public sealed class Tanh : Module
{
    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => TensorOps.Tanh(input);
}

/// <summary>GELU (точная формула через tanh-аппроксимацию).</summary>
public sealed class GELU : Module
{
    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => TensorOps.Gelu(input);
}

/// <summary>SiLU/Swish: x * sigmoid(x).</summary>
public sealed class SiLU : Module
{
    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => TensorOps.Silu(input);
}
