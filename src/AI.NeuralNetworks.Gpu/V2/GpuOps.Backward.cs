using System;
using AI.ML.NeuralNetworks.Gpu.CuBlas;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace AI.ML.NeuralNetworks.Gpu.V2;

internal sealed partial class GpuOps
{
    #region Backward functions

    /// <summary>
    /// Native GPU backward для element-wise unary op'ов.
    /// </summary>
    private sealed class GpuUnaryFn : Function
    {
        private readonly GpuOps _ops;
        private readonly OpCode _op;
        private readonly Tensor _saved;
        private readonly bool _saveOutput;

        public GpuUnaryFn(GpuOps ops, OpCode op, Tensor x, Tensor y)
        {
            _ops = ops; _op = op;
            _saveOutput = op is OpCode.Sigmoid or OpCode.Tanh or OpCode.Exp or OpCode.Sqrt;
            _saved = _saveOutput ? y : x;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var savedC = _saved.IsContiguous ? _saved : _saved.Contiguous();
                var gx = Tensor.Empty(savedC.Shape, savedC.DType, savedC.Device);
                int n = (int)gx.NumElements;
                switch (_op)
                {
                    case OpCode.Sigmoid: _ops._sigmoidBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx)); break;
                    case OpCode.Tanh:    _ops._tanhBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));    break;
                    case OpCode.Exp:     _ops._expBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));     break;
                    case OpCode.Sqrt:    _ops._sqrtBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));    break;
                    case OpCode.Relu:    _ops._reluBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));    break;
                    case OpCode.Neg:     _ops._negBwd(n, ViewOf(gy), ViewOf(gx));                     break;
                    case OpCode.Log:     _ops._logBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));     break;
                    case OpCode.Abs:     _ops._absBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));     break;
                    case OpCode.Sin:     _ops._sinBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));     break;
                    case OpCode.Cos:     _ops._cosBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));     break;
                    case OpCode.Silu:    _ops._siluBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));    break;
                    case OpCode.Gelu:    _ops._geluBwd(n, ViewOf(savedC), ViewOf(gy), ViewOf(gx));    break;
                    default:
                        throw new NotSupportedException($"GpuUnaryFn: backward для {_op} не реализован.");
                }
                return new[] { gx };
            }
        }
    }

    /// <summary>Native GPU backward для same-shape Add: ga=gy, gb=gy.</summary>
    private sealed class GpuAddFn : Function
    {
        private readonly GpuOps _ops;
        public GpuAddFn(GpuOps ops) { _ops = ops; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                int n = (int)gy.NumElements;
                var ga = Tensor.Empty(gy.Shape, gy.DType, gy.Device);
                var gb = Tensor.Empty(gy.Shape, gy.DType, gy.Device);
                _ops._smul(n, ViewOf(gy), 1f, ViewOf(ga));
                _ops._smul(n, ViewOf(gy), 1f, ViewOf(gb));
                return new[] { ga, gb };
            }
        }
    }

    /// <summary>Native GPU backward для same-shape Sub: ga=gy, gb=-gy.</summary>
    private sealed class GpuSubFn : Function
    {
        private readonly GpuOps _ops;
        public GpuSubFn(GpuOps ops) { _ops = ops; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                int n = (int)gy.NumElements;
                var ga = Tensor.Empty(gy.Shape, gy.DType, gy.Device);
                var gb = Tensor.Empty(gy.Shape, gy.DType, gy.Device);
                _ops._smul(n, ViewOf(gy), 1f, ViewOf(ga));
                _ops._negBwd(n, ViewOf(gy), ViewOf(gb));
                return new[] { ga, gb };
            }
        }
    }

    /// <summary>Native GPU backward для same-shape Mul: ga=b·gy, gb=a·gy.</summary>
    private sealed class GpuMulFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _a, _b;
        public GpuMulFn(GpuOps ops, Tensor a, Tensor b) { _ops = ops; _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var ac = _a.IsContiguous ? _a : _a.Contiguous();
                var bc = _b.IsContiguous ? _b : _b.Contiguous();
                var ga = Tensor.Empty(_a.Shape, _a.DType, _a.Device);
                var gb = Tensor.Empty(_b.Shape, _b.DType, _b.Device);
                _ops._mulBwd((int)gy.NumElements, ViewOf(ac), ViewOf(bc), ViewOf(gy), ViewOf(ga), ViewOf(gb));
                return new[]
                {
                    _a.RequiresGrad ? ga : null,
                    _b.RequiresGrad ? gb : null,
                };
            }
        }
    }

    /// <summary>Native GPU backward для same-shape Div: ga=gy/b, gb=-a·gy/b².</summary>
    private sealed class GpuDivFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _a, _b;
        public GpuDivFn(GpuOps ops, Tensor a, Tensor b) { _ops = ops; _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var ac = _a.IsContiguous ? _a : _a.Contiguous();
                var bc = _b.IsContiguous ? _b : _b.Contiguous();
                var ga = Tensor.Empty(_a.Shape, _a.DType, _a.Device);
                var gb = Tensor.Empty(_b.Shape, _b.DType, _b.Device);
                _ops._divBwd((int)gy.NumElements, ViewOf(ac), ViewOf(bc), ViewOf(gy), ViewOf(ga), ViewOf(gb));
                return new[]
                {
                    _a.RequiresGrad ? ga : null,
                    _b.RequiresGrad ? gb : null,
                };
            }
        }
    }

    /// <summary>
    /// Native GPU backward для broadcast-Add.
    /// </summary>
    private sealed class GpuBroadcastAddFn : Function
    {
        private readonly Shape _aShape, _bShape;
        public GpuBroadcastAddFn(Shape aShape, Shape bShape) { _aShape = aShape; _bShape = bShape; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var ga = TensorOps.SumToShape(gradOutput, _aShape);
                var gb = TensorOps.SumToShape(gradOutput, _bShape);
                return new[] { ga, gb };
            }
        }
    }

    /// <summary>CPU-fallback backward для unary GPU op.</summary>
    private sealed class CpuFallbackUnaryFn : Function
    {
        private readonly OpCode _op;
        private readonly Tensor _x;
        public CpuFallbackUnaryFn(OpCode op, Tensor x) { _op = op; _x = x; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.EnableGrad())
            {
                var xCpu = _x.ToCpu().SetRequiresGrad(true);
                Tensor yCpu = _op switch
                {
                    OpCode.Neg => TensorOps.Neg(xCpu),
                    OpCode.Abs => TensorOps.Abs(xCpu),
                    OpCode.Exp => TensorOps.Exp(xCpu),
                    OpCode.Log => TensorOps.Log(xCpu),
                    OpCode.Sqrt => TensorOps.Sqrt(xCpu),
                    OpCode.Sin => TensorOps.Sin(xCpu),
                    OpCode.Cos => TensorOps.Cos(xCpu),
                    OpCode.Relu => TensorOps.Relu(xCpu),
                    OpCode.Sigmoid => TensorOps.Sigmoid(xCpu),
                    OpCode.Tanh => TensorOps.Tanh(xCpu),
                    OpCode.Silu => TensorOps.Silu(xCpu),
                    OpCode.Gelu => TensorOps.Gelu(xCpu),
                    _ => throw new NotSupportedException()
                };
                yCpu.Backward(gradOutput.ToCpu());
                return new[] { xCpu.Grad.To(_x.Device) };
            }
        }
    }

    private sealed class CpuFallbackBinaryFn : Function
    {
        private readonly OpCode _op;
        private readonly Tensor _a, _b;
        public CpuFallbackBinaryFn(OpCode op, Tensor a, Tensor b) { _op = op; _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.EnableGrad())
            {
                var aCpu = _a.ToCpu().SetRequiresGrad(true);
                var bCpu = _b.ToCpu().SetRequiresGrad(true);
                Tensor yCpu = _op switch
                {
                    OpCode.Add => TensorOps.Add(aCpu, bCpu),
                    OpCode.Sub => TensorOps.Sub(aCpu, bCpu),
                    OpCode.Mul => TensorOps.Mul(aCpu, bCpu),
                    OpCode.Div => TensorOps.Div(aCpu, bCpu),
                    OpCode.Pow => TensorOps.Pow(aCpu, bCpu),
                    _ => throw new NotSupportedException()
                };
                yCpu.Backward(gradOutput.ToCpu());
                return new[]
                {
                    _a.RequiresGrad ? aCpu.Grad.To(_a.Device) : null,
                    _b.RequiresGrad ? bCpu.Grad.To(_b.Device) : null,
                };
            }
        }
    }

    private sealed class CpuFallbackBmmFn : Function
    {
        private readonly Tensor _a, _b;
        public CpuFallbackBmmFn(Tensor a, Tensor b) { _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.EnableGrad())
            {
                var aCpu = _a.ToCpu().SetRequiresGrad(true);
                var bCpu = _b.ToCpu().SetRequiresGrad(true);
                var yCpu = TensorOps.MatMul(aCpu, bCpu);
                yCpu.Backward(gradOutput.ToCpu());
                return new[]
                {
                    _a.RequiresGrad ? aCpu.Grad.To(_a.Device) : null,
                    _b.RequiresGrad ? bCpu.Grad.To(_b.Device) : null,
                };
            }
        }
    }

    private sealed class CpuFallbackLinearGeluFn : Function
    {
        private readonly Tensor _x, _w, _b;
        public CpuFallbackLinearGeluFn(Tensor x, Tensor w, Tensor b) { _x = x; _w = w; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.EnableGrad())
            {
                var xCpu = _x.ToCpu().SetRequiresGrad(true);
                var wCpu = _w.ToCpu().SetRequiresGrad(true);
                var bCpu = _b.ToCpu().SetRequiresGrad(true);
                var preact = TensorOps.MatMul(xCpu, wCpu.Transpose(0, 1)) + bCpu;
                var yCpu = TensorOps.Gelu(preact);
                yCpu.Backward(gradOutput.ToCpu());
                return new[]
                {
                    _x.RequiresGrad ? xCpu.Grad.To(_x.Device) : null,
                    _w.RequiresGrad ? wCpu.Grad.To(_w.Device) : null,
                    _b.RequiresGrad ? bCpu.Grad.To(_b.Device) : null,
                };
            }
        }
    }

    private sealed class CpuFallbackAddBiasReluFn : Function
    {
        private readonly Tensor _x, _bias;
        public CpuFallbackAddBiasReluFn(Tensor x, Tensor bias) { _x = x; _bias = bias; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.EnableGrad())
            {
                var xCpu = _x.ToCpu().SetRequiresGrad(true);
                var bCpu = _bias.ToCpu().SetRequiresGrad(true);
                var yCpu = TensorOps.Relu(xCpu + bCpu);
                yCpu.Backward(gradOutput.ToCpu());
                return new[]
                {
                    _x.RequiresGrad ? xCpu.Grad.To(_x.Device) : null,
                    _bias.RequiresGrad ? bCpu.Grad.To(_bias.Device) : null,
                };
            }
        }
    }

    /// <summary>Native GPU backward для batched MatMul.</summary>
    private sealed class GpuBmmFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _a, _b;
        public GpuBmmFn(GpuOps ops, Tensor a, Tensor b) { _ops = ops; _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                var gy = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
                var ac = _a.IsContiguous ? _a : _a.Contiguous();
                var bc = _b.IsContiguous ? _b : _b.Contiguous();

                Tensor da = null, db = null;
                if (_a.RequiresGrad)
                {
                    da = Tensor.Empty(ac.Shape, ac.DType, ac.Device);
                    _ops.BmmRaw(gy, false, bc, true, da);
                }
                if (_b.RequiresGrad)
                {
                    db = Tensor.Empty(bc.Shape, bc.DType, bc.Device);
                    _ops.BmmRaw(ac, true, gy, false, db);
                }
                return new[] { da, db };
            }
        }
    }

    /// <summary>Native GPU backward для 2D MatMul.</summary>
    private sealed class MatMulGpuFn : Function
    {
        private readonly GpuOps _ops;
        private readonly Tensor _a, _b;
        public MatMulGpuFn(GpuOps ops, Tensor a, Tensor b) { _ops = ops; _a = a; _b = b; }
        public override Tensor[] Backward(Tensor gradOutput)
        {
            using (TapeContext.NoGrad())
            {
                Tensor da = null, db = null;
                if (_a.RequiresGrad)
                {
                    var bT = _b.Transpose(0, 1).Contiguous();
                    da = _ops.MatMulRaw(gradOutput, bT);
                }
                if (_b.RequiresGrad)
                {
                    var aT = _a.Transpose(0, 1).Contiguous();
                    db = _ops.MatMulRaw(aT, gradOutput);
                }
                return new[] { da, db };
            }
        }
    }

    #endregion Backward functions
}
