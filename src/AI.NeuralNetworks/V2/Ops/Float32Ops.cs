using System;

namespace AI.ML.NeuralNetworks.V2.Ops;

/// <summary>
/// Каталог поэлементных операций для <see cref="float"/>.
/// </summary>
/// <remarks>
/// Каждая op — <see langword="struct"/>, реализующая <see cref="IUnaryOp{T}"/> или
/// <see cref="IBinaryOp{T}"/>. JIT инлайнит все методы благодаря generic-диспатчу,
/// поэтому overhead на вызов через интерфейс отсутствует.
/// </remarks>
public static class Float32Ops
{
    #region Unary

    /// <summary>y = -x; dy/dx = -1.</summary>
    public struct Neg : IUnaryOp<float>
    {
        public float Forward(float x) => -x;
        public float Backward(float x, float y, float gy) => -gy;
    }

    /// <summary>y = |x|; dy/dx = sign(x).</summary>
    public struct Abs : IUnaryOp<float>
    {
        public float Forward(float x) => Math.Abs(x);
        public float Backward(float x, float y, float gy) => x > 0 ? gy : (x < 0 ? -gy : 0f);
    }

    /// <summary>y = exp(x); dy/dx = exp(x) = y.</summary>
    public struct Exp : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Exp(x);
        public float Backward(float x, float y, float gy) => y * gy;
    }

    /// <summary>y = log(x); dy/dx = 1/x.</summary>
    public struct Log : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Log(x);
        public float Backward(float x, float y, float gy) => gy / x;
    }

    /// <summary>y = sqrt(x); dy/dx = 0.5 / sqrt(x) = 0.5 / y.</summary>
    public struct Sqrt : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Sqrt(x);
        public float Backward(float x, float y, float gy) => 0.5f * gy / (y + 1e-30f);
    }

    /// <summary>y = sin(x); dy/dx = cos(x).</summary>
    public struct Sin : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Sin(x);
        public float Backward(float x, float y, float gy) => MathF.Cos(x) * gy;
    }

    /// <summary>y = cos(x); dy/dx = -sin(x).</summary>
    public struct Cos : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Cos(x);
        public float Backward(float x, float y, float gy) => -MathF.Sin(x) * gy;
    }

    /// <summary>ReLU: y = max(0, x); dy/dx = (x > 0).</summary>
    public struct Relu : IUnaryOp<float>
    {
        public float Forward(float x) => x > 0f ? x : 0f;
        public float Backward(float x, float y, float gy) => x > 0f ? gy : 0f;
    }

    /// <summary>Sigmoid: y = 1/(1+exp(-x)); dy/dx = y*(1-y).</summary>
    public struct Sigmoid : IUnaryOp<float>
    {
        public float Forward(float x) => 1f / (1f + MathF.Exp(-x));
        public float Backward(float x, float y, float gy) => y * (1f - y) * gy;
    }

    /// <summary>tanh; dy/dx = 1 - y^2.</summary>
    public struct Tanh : IUnaryOp<float>
    {
        public float Forward(float x) => MathF.Tanh(x);
        public float Backward(float x, float y, float gy) => (1f - y * y) * gy;
    }

    /// <summary>SiLU/Swish: y = x * sigmoid(x); dy/dx = sigmoid(x) * (1 + x * (1 - sigmoid(x))).</summary>
    public struct Silu : IUnaryOp<float>
    {
        public float Forward(float x)
        {
            float s = 1f / (1f + MathF.Exp(-x));
            return x * s;
        }
        public float Backward(float x, float y, float gy)
        {
            float s = 1f / (1f + MathF.Exp(-x));
            return gy * s * (1f + x * (1f - s));
        }
    }

    /// <summary>
    /// GELU (точная формула через tanh-аппроксимацию, как в torch).
    /// y = 0.5 * x * (1 + tanh(sqrt(2/π) * (x + 0.044715 * x^3))).
    /// </summary>
    public struct Gelu : IUnaryOp<float>
    {
        private const float K = 0.7978845608f; // sqrt(2/π)
        private const float C = 0.044715f;

        public float Forward(float x)
        {
            float x3 = x * x * x;
            float u = K * (x + C * x3);
            return 0.5f * x * (1f + MathF.Tanh(u));
        }

        public float Backward(float x, float y, float gy)
        {
            float x3 = x * x * x;
            float u = K * (x + C * x3);
            float t = MathF.Tanh(u);
            float dudx = K * (1f + 3f * C * x * x);
            float dy = 0.5f * (1f + t) + 0.5f * x * (1f - t * t) * dudx;
            return dy * gy;
        }
    }

    #endregion Unary

    #region Binary

    /// <summary>y = a + b; da = gy, db = gy.</summary>
    public struct Add : IBinaryOp<float>
    {
        public float Forward(float a, float b) => a + b;
        public float BackwardA(float a, float b, float y, float gy) => gy;
        public float BackwardB(float a, float b, float y, float gy) => gy;
    }

    /// <summary>y = a - b; da = gy, db = -gy.</summary>
    public struct Sub : IBinaryOp<float>
    {
        public float Forward(float a, float b) => a - b;
        public float BackwardA(float a, float b, float y, float gy) => gy;
        public float BackwardB(float a, float b, float y, float gy) => -gy;
    }

    /// <summary>y = a * b; da = b*gy, db = a*gy.</summary>
    public struct Mul : IBinaryOp<float>
    {
        public float Forward(float a, float b) => a * b;
        public float BackwardA(float a, float b, float y, float gy) => b * gy;
        public float BackwardB(float a, float b, float y, float gy) => a * gy;
    }

    /// <summary>y = a / b; da = gy/b, db = -a*gy/b^2 = -y*gy/b.</summary>
    public struct Div : IBinaryOp<float>
    {
        public float Forward(float a, float b) => a / b;
        public float BackwardA(float a, float b, float y, float gy) => gy / b;
        public float BackwardB(float a, float b, float y, float gy) => -y * gy / b;
    }

    /// <summary>y = a^b (через exp/log); только для a>0.</summary>
    public struct Pow : IBinaryOp<float>
    {
        public float Forward(float a, float b) => MathF.Pow(a, b);
        public float BackwardA(float a, float b, float y, float gy) => b * MathF.Pow(a, b - 1f) * gy;
        public float BackwardB(float a, float b, float y, float gy) => y * MathF.Log(a) * gy;
    }
    #endregion Binary

}