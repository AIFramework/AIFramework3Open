namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Поэлементная унарная операция, реализуемая как <see langword="struct"/> для
/// zero-overhead generic-диспатча. Один интерфейс описывает forward+backward,
/// что позволяет завести новую операцию в 5 строках кода.
/// </summary>
/// <typeparam name="T">Тип элемента (float/double/Half/...).</typeparam>
/// <example>
/// <code>
/// public struct ReluOp : IUnaryOp&lt;float&gt; {
///     public float Forward(float x) => x &gt; 0 ? x : 0;
///     public float Backward(float x, float y, float gy) =&gt; x &gt; 0 ? gy : 0;
/// }
/// </code>
/// </example>
public interface IUnaryOp<T> where T : unmanaged
{
    /// <summary>Forward: y = f(x).</summary>
    T Forward(T x);
    /// <summary>Backward: dx = df/dx * gy. Доступны x (вход), y (выход), gy (грэд по y).</summary>
    T Backward(T x, T y, T gy);
}

/// <summary>
/// Поэлементная бинарная операция: y = f(a, b), и её градиенты по a и b.
/// </summary>
/// <typeparam name="T">Тип элемента.</typeparam>
public interface IBinaryOp<T> where T : unmanaged
{
    /// <summary>Forward: y = f(a, b).</summary>
    T Forward(T a, T b);
    /// <summary>Backward по a: da = df/da * gy.</summary>
    T BackwardA(T a, T b, T y, T gy);
    /// <summary>Backward по b: db = df/db * gy.</summary>
    T BackwardB(T a, T b, T y, T gy);
}
