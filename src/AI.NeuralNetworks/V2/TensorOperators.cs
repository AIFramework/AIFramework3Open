using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// Операторные перегрузки и instance-методы-обёртки над <see cref="TensorOps"/>.
/// Сделано отдельным partial-классом, чтобы Tensor.cs остался компактным.
/// </summary>
public sealed partial class Tensor
{
    /// <summary>+ для тензоров (с broadcasting).</summary>
    public static Tensor operator +(Tensor a, Tensor b) => TensorOps.Add(a, b);
    /// <summary>- (бинарный).</summary>
    public static Tensor operator -(Tensor a, Tensor b) => TensorOps.Sub(a, b);
    /// <summary>* поэлементное.</summary>
    public static Tensor operator *(Tensor a, Tensor b) => TensorOps.Mul(a, b);
    /// <summary>/ поэлементное.</summary>
    public static Tensor operator /(Tensor a, Tensor b) => TensorOps.Div(a, b);
    /// <summary>Унарный минус.</summary>
    public static Tensor operator -(Tensor x) => TensorOps.Neg(x);

    /// <summary>+ скаляр.</summary>
    public static Tensor operator +(Tensor a, float s) => TensorOps.AddScalar(a, s);
    /// <summary>+ скаляр (слева).</summary>
    public static Tensor operator +(float s, Tensor a) => TensorOps.AddScalar(a, s);
    /// <summary>* скаляр.</summary>
    public static Tensor operator *(Tensor a, float s) => TensorOps.MulScalar(a, s);
    /// <summary>* скаляр (слева).</summary>
    public static Tensor operator *(float s, Tensor a) => TensorOps.MulScalar(a, s);

    /// <summary>Матричное умножение.</summary>
    public Tensor MatMul(Tensor other) => TensorOps.MatMul(this, other);

    /// <summary>Сумма по тензору / оси.</summary>
    public Tensor Sum(int? axis = null, bool keepDim = false) => TensorOps.Sum(this, axis, keepDim);

    /// <summary>Среднее по тензору / оси.</summary>
    public Tensor Mean(int? axis = null, bool keepDim = false) => TensorOps.Mean(this, axis, keepDim);

    /// <summary>ReLU.</summary>
    public Tensor Relu() => TensorOps.Relu(this);
    /// <summary>Sigmoid.</summary>
    public Tensor Sigmoid() => TensorOps.Sigmoid(this);
    /// <summary>tanh.</summary>
    public Tensor Tanh() => TensorOps.Tanh(this);
    /// <summary>GELU.</summary>
    public Tensor Gelu() => TensorOps.Gelu(this);
    /// <summary>SiLU/Swish.</summary>
    public Tensor Silu() => TensorOps.Silu(this);
    /// <summary>exp.</summary>
    public Tensor Exp() => TensorOps.Exp(this);
    /// <summary>log.</summary>
    public Tensor Log() => TensorOps.Log(this);
    /// <summary>sqrt.</summary>
    public Tensor Sqrt() => TensorOps.Sqrt(this);
    /// <summary>abs.</summary>
    public Tensor Abs() => TensorOps.Abs(this);

    /// <summary>Softmax по оси (по умолчанию последняя).</summary>
    public Tensor Softmax(int axis = -1) => SoftmaxOps.Softmax(this, axis);

    /// <summary>LogSoftmax по оси.</summary>
    public Tensor LogSoftmax(int axis = -1) => SoftmaxOps.LogSoftmax(this, axis);
}
