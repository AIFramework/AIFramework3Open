using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.Gpu.V2;
using Xunit;

namespace NNW.V2.Gpu.Tests;

/// <summary>
/// Parity-тесты V2 CPU vs CUDA: сверяем результаты element-wise и matmul-операций.
/// Skipped на машинах без CUDA.
/// </summary>
public partial class GpuV2ParityTests : IClassFixture<GpuV2ParityTests.GpuFixture>
{
    private readonly GpuFixture _fx;
    public GpuV2ParityTests(GpuFixture fx) { _fx = fx; }

    public sealed class GpuFixture : IDisposable
    {
        public bool HasCuda { get; }
        public GpuFixture()
        {
            try { GpuBackend.Initialize(0); HasCuda = true; }
            catch { HasCuda = false; }
        }
        public void Dispose()
        {
            if (HasCuda)
            {
                try { GpuBackend.Shutdown(); } catch { /* best-effort */ }
            }
        }
    }

    private void SkipIfNoCuda() => Skip.IfNot(_fx.HasCuda, "CUDA device not available");

    private static void AssertClose(Tensor expected, Tensor actual, float tol = 1e-4f)
    {
        Assert.Equal(expected.Shape, actual.Shape);
        var e = expected.AsReadOnlySpan<float>();
        var a = actual.AsReadOnlySpan<float>();
        for (int i = 0; i < e.Length; i++)
            Assert.True(MathF.Abs(e[i] - a[i]) < tol,
                $"Index {i}: expected={e[i]}, actual={a[i]}, diff={MathF.Abs(e[i] - a[i])}");
    }

    [SkippableFact]
    public void ToDevice_RoundTrip_PreservesData()
    {
        SkipIfNoCuda();
        var cpu = Tensor.From(new float[] { 1, 2, 3, 4, 5 }, new Shape(5));
        var gpu = cpu.To(Device.Cuda(0));
        var back = gpu.ToCpu();
        AssertClose(cpu, back);
    }

    [SkippableFact]
    public void Add_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(0);
        var a = Tensor.Randn(new Shape(64, 64), rng);
        var b = Tensor.Randn(new Shape(64, 64), rng);
        var cpu = TensorOps.Add(a, b);
        var gpu = TensorOps.Add(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Mul_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(1);
        var a = Tensor.Randn(new Shape(32, 16), rng);
        var b = Tensor.Randn(new Shape(32, 16), rng);
        var cpu = TensorOps.Mul(a, b);
        var gpu = TensorOps.Mul(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Relu_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(2);
        var x = Tensor.Randn(new Shape(128), rng);
        var cpu = TensorOps.Relu(x);
        var gpu = TensorOps.Relu(x.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu);
    }

    [SkippableFact]
    public void Sigmoid_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(64), new Random(3));
        var cpu = TensorOps.Sigmoid(x);
        var gpu = TensorOps.Sigmoid(x.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu, 1e-3f);
    }

    [SkippableFact]
    public void Tanh_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(64), new Random(4));
        var cpu = TensorOps.Tanh(x);
        var gpu = TensorOps.Tanh(x.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu, 1e-3f);
    }

    [SkippableFact]
    public void Exp_Log_Sqrt_Parity()
    {
        SkipIfNoCuda();
        var x = Tensor.Randn(new Shape(64), new Random(5));
        var xPos = TensorOps.Add(x, Tensor.Full(x.Shape, 3.0f)); // > 0
        var dev = xPos.To(Device.Cuda(0));
        AssertClose(TensorOps.Exp(xPos), TensorOps.Exp(dev).ToCpu(), 1e-2f);
        AssertClose(TensorOps.Log(xPos), TensorOps.Log(dev).ToCpu(), 1e-3f);
        AssertClose(TensorOps.Sqrt(xPos), TensorOps.Sqrt(dev).ToCpu(), 1e-3f);
    }

    [SkippableFact]
    public void MatMul_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(6);
        var a = Tensor.Randn(new Shape(8, 16), rng);
        var b = Tensor.Randn(new Shape(16, 12), rng);
        var cpu = TensorOps.MatMul(a, b);
        var gpu = TensorOps.MatMul(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu, 1e-3f);
    }

    [SkippableFact]
    public void BatchedMatMul_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(7);
        var a = Tensor.Randn(new Shape(4, 8, 16), rng);
        var b = Tensor.Randn(new Shape(4, 16, 12), rng);
        var cpu = TensorOps.MatMul(a, b);
        var gpu = TensorOps.MatMul(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu();
        AssertClose(cpu, gpu, 1e-3f);
    }

    [SkippableFact]
    public void Sub_Div_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(8);
        var a = Tensor.Randn(new Shape(64), rng);
        var b = TensorOps.Add(Tensor.Randn(new Shape(64), rng), Tensor.Full(new Shape(64), 2.0f));
        AssertClose(TensorOps.Sub(a, b),
            TensorOps.Sub(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu());
        AssertClose(TensorOps.Div(a, b),
            TensorOps.Div(a.To(Device.Cuda(0)), b.To(Device.Cuda(0))).ToCpu(), 1e-3f);
    }
}