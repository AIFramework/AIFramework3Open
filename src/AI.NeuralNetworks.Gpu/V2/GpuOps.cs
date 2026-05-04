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

/// <summary>
/// Реализации V2-операций на GPU: регистрируются в <see cref="OpRegistry"/>
/// и автоматически используются <c>TensorOps</c> для тензоров на CUDA.
/// </summary>
/// <remarks>
/// <para>
/// Содержит ленивый кэш скомпилированных ILGPU kernel-ов (по одному на дельта).
/// При первом обращении из GpuBackend создаётся экземпляр <see cref="GpuOps"/>,
/// который уже хранит делегаты-ядра.
/// </para>
/// <para>
/// Для matmul при доступности cuBLAS используется SGEMM; иначе — naïve ILGPU.
/// </para>
/// </remarks>
internal sealed partial class GpuOps
{
    private readonly GpuContext _gpu;

    // Compiled kernel delegates -- float32
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _neg;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _abs;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _exp;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _log;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _sqrt;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _sin;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _cos;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _relu;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _sigmoid;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _tanh;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _silu;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _gelu;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _add;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _sub;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _mul;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _div;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _pow;
    private readonly Action<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int> _gemm;
    private readonly Action<Index3D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int, int> _bgemm;
    private readonly Action<Index1D, ArrayView<float>, float, ArrayView<float>> _smul;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, V2Kernels.BroadcastArgs> _bcast;
    private readonly Action<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int> _linearGelu;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int> _addBiasRelu;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        float, float, float, float, float, float, float> _adamW;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs> _contig;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs> _scatter;

    // Fused recurrent step kernels (LSTM/GRU).
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, int, int, int> _lstmStepFwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, ArrayView<float>, int, int> _lstmStepBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, int, int, int> _gruStepFwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, ArrayView<float>, int, int> _gruStepBwd;

    // Element-wise backward kernels.
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _sigmoidBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _tanhBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _expBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _sqrtBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _reluBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _negBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _logBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _absBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _sinBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _cosBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _siluBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>> _mulBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>> _divBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _geluBwd;

    // Reductions / softmax / layernorm.
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, int, int> _sumAxis;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs> _bcastFill;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, int, int> _softmaxFwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int> _softmaxBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, int, int> _logSoftmaxFwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int> _logSoftmaxBwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, ArrayView<float>, ArrayView<float>, int, float, int> _layerNormFwd;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int> _layerNormBwdX;
    private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
        ArrayView<float>, ArrayView<float>, ArrayView<float>, int> _layerNormBwdWB;

    public GpuOps(GpuContext gpu)
    {
        _gpu = gpu ?? throw new ArgumentNullException(nameof(gpu));
        var acc = gpu.Accelerator;
        _neg = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.NegFwd);
        _abs = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.AbsFwd);
        _exp = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.ExpFwd);
        _log = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.LogFwd);
        _sqrt = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.SqrtFwd);
        _sin = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.SinFwd);
        _cos = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.CosFwd);
        _relu = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.ReluFwd);
        _sigmoid = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.SigmoidFwd);
        _tanh = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.TanhFwd);
        _silu = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.SiluFwd);
        _gelu = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.GeluFwd);
        _add = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.AddFwd);
        _sub = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.SubFwd);
        _mul = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.MulFwd);
        _div = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.DivFwd);
        _pow = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.PowFwd);
        _gemm = acc.LoadAutoGroupedStreamKernel<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int>(V2Kernels.GemmNaive);
        _bgemm = acc.LoadAutoGroupedStreamKernel<Index3D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int, int>(V2Kernels.BatchedGemmNaive);
        _smul = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, float, ArrayView<float>>(V2Kernels.ScalarMul);
        _bcast = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, V2Kernels.BroadcastArgs>(V2Kernels.BinaryBroadcast6D);
        _linearGelu = acc.LoadAutoGroupedStreamKernel<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int, int>(FusedKernels.LinearGeluFwd);
        _addBiasRelu = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(FusedKernels.AddBiasReluFwd);
        _adamW = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            float, float, float, float, float, float, float>(FusedKernels.AdamWStep);
        _contig = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs>(V2Kernels.ContiguousCopy6D);
        _scatter = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs>(V2Kernels.ScatterContiguous6D);

        _lstmStepFwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, int, int, int>(V2Kernels.LstmStepFwd);
        _lstmStepBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.LstmStepBwd);
        _gruStepFwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, int, int, int>(V2Kernels.GruStepFwd);
        _gruStepBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.GruStepBwd);

        _sigmoidBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.SigmoidBwdY);
        _tanhBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.TanhBwdY);
        _expBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.ExpBwdY);
        _sqrtBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.SqrtBwdY);
        _reluBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.ReluBwdX);
        _negBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(V2Kernels.NegBwd);
        _logBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.LogBwdX);
        _absBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.AbsBwdX);
        _sinBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.SinBwdX);
        _cosBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.CosBwdX);
        _siluBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.SiluBwdX);
        _mulBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.MulBwd);
        _divBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.DivBwd);
        _geluBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(V2Kernels.GeluBwdX);

        _sumAxis = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.SumAxis);
        _bcastFill = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, V2Kernels.StridedCopyArgs>(V2Kernels.BroadcastFill6D);
        _softmaxFwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.SoftmaxFwd);
        _softmaxBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.SoftmaxBwd);
        _logSoftmaxFwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.LogSoftmaxFwd);
        _logSoftmaxBwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.LogSoftmaxBwd);
        _layerNormFwd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, ArrayView<float>, int, float, int>(V2Kernels.LayerNormFwd);
        _layerNormBwdX = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, ArrayView<float>, int, int>(V2Kernels.LayerNormBwdX);
        _layerNormBwdWB = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>,
            ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(V2Kernels.LayerNormBwdWB);
    }

    #region Helpers

    private static ArrayView<float> ViewOf(Tensor t)
    {
        if (t.Storage is not CudaStorage cs)
            throw new InvalidOperationException("Tensor должен лежать на GPU (CudaStorage).");
        return cs.AsView<float>().SubView(t.Offset, (int)t.NumElements);
    }

    private Tensor Empty(Tensor like)
        => Tensor.Empty(like.Shape, like.DType, like.Device);

    #endregion Helpers

    #region Регистрация

    public void Register()
    {
        var dev = AI.ML.NeuralNetworks.V2.Device.Cuda(_gpu.DeviceIndex);
        const DType ft = DType.Float32;

        OpRegistry.Register(OpCode.Neg, ft, dev, (ins, _) => UnaryOp(ins, _neg, OpCode.Neg));
        OpRegistry.Register(OpCode.Abs, ft, dev, (ins, _) => UnaryOp(ins, _abs, OpCode.Abs));
        OpRegistry.Register(OpCode.Exp, ft, dev, (ins, _) => UnaryOp(ins, _exp, OpCode.Exp));
        OpRegistry.Register(OpCode.Log, ft, dev, (ins, _) => UnaryOp(ins, _log, OpCode.Log));
        OpRegistry.Register(OpCode.Sqrt, ft, dev, (ins, _) => UnaryOp(ins, _sqrt, OpCode.Sqrt));
        OpRegistry.Register(OpCode.Sin, ft, dev, (ins, _) => UnaryOp(ins, _sin, OpCode.Sin));
        OpRegistry.Register(OpCode.Cos, ft, dev, (ins, _) => UnaryOp(ins, _cos, OpCode.Cos));
        OpRegistry.Register(OpCode.Relu, ft, dev, (ins, _) => UnaryOp(ins, _relu, OpCode.Relu));
        OpRegistry.Register(OpCode.Sigmoid, ft, dev, (ins, _) => UnaryOp(ins, _sigmoid, OpCode.Sigmoid));
        OpRegistry.Register(OpCode.Tanh, ft, dev, (ins, _) => UnaryOp(ins, _tanh, OpCode.Tanh));
        OpRegistry.Register(OpCode.Silu, ft, dev, (ins, _) => UnaryOp(ins, _silu, OpCode.Silu));
        OpRegistry.Register(OpCode.Gelu, ft, dev, (ins, _) => UnaryOp(ins, _gelu, OpCode.Gelu));

        OpRegistry.Register(OpCode.Add, ft, dev, (ins, _) => BinaryOp(ins, _add, OpCode.Add));
        OpRegistry.Register(OpCode.Sub, ft, dev, (ins, _) => BinaryOp(ins, _sub, OpCode.Sub));
        OpRegistry.Register(OpCode.Mul, ft, dev, (ins, _) => BinaryOp(ins, _mul, OpCode.Mul));
        OpRegistry.Register(OpCode.Div, ft, dev, (ins, _) => BinaryOp(ins, _div, OpCode.Div));
        OpRegistry.Register(OpCode.Pow, ft, dev, (ins, _) => BinaryOp(ins, _pow, OpCode.Pow));

        OpRegistry.Register(OpCode.MatMul, ft, dev, (ins, _) => MatMulOp(ins[0], ins[1]));
        OpRegistry.Register(OpCode.BatchedMatMul, ft, dev, (ins, _) => BatchedMatMulOp(ins[0], ins[1]));

        OpRegistry.Register(OpCode.FusedAdamW, ft, dev, (ins, attrs) => FusedAdamWOp(ins, (FusedAdamWAttrs)attrs));
        OpRegistry.Register(OpCode.FusedLinearGelu, ft, dev, (ins, _) => FusedLinearGeluOp(ins));
        OpRegistry.Register(OpCode.FusedAddBiasRelu, ft, dev, (ins, _) => FusedAddBiasReluOp(ins));
        OpRegistry.Register(OpCode.Contiguous, ft, dev, (ins, _) => ContiguousOp(ins[0]));
        OpRegistry.Register(OpCode.Sum, ft, dev, (ins, attrs) => SumOp(ins[0], (ReduceAttrs)attrs));
        OpRegistry.Register(OpCode.Softmax, ft, dev, (ins, attrs) => SoftmaxOp(ins[0], (SoftmaxAttrs)attrs));
        OpRegistry.Register(OpCode.LogSoftmax, ft, dev, (ins, attrs) => LogSoftmaxOp(ins[0], (SoftmaxAttrs)attrs));
        OpRegistry.Register(OpCode.LayerNorm, ft, dev, (ins, attrs) => LayerNormOp(ins, (LayerNorm.LayerNormAttrs)attrs));
        OpRegistry.Register(OpCode.MulScalar, ft, dev, (ins, attrs) => MulScalarOp(ins[0], (ScalarAttrs)attrs));
        OpRegistry.Register(OpCode.Cat, ft, dev, (ins, attrs) => CatOp(ins, (CatAttrs)attrs));
        OpRegistry.Register(OpCode.ScatterSlice, ft, dev, (ins, attrs) => ScatterSliceOp(ins[0], ins[1], (ScatterAttrs)attrs));
        OpRegistry.Register(OpCode.LstmStep, ft, dev, (ins, attrs) => LstmStepOp(ins[0], ins[1], (LstmStepAttrs)attrs));
        OpRegistry.Register(OpCode.GruStep, ft, dev, (ins, attrs) => GruStepOp(ins[0], ins[1], ins[2], (GruStepAttrs)attrs));
        OpRegistry.Register(OpCode.LstmSeq, ft, dev, (ins, attrs) => LstmSeqOp(ins, (LstmSeqAttrs)attrs));
        OpRegistry.Register(OpCode.GruSeq, ft, dev, (ins, attrs) => GruSeqOp(ins, (GruSeqAttrs)attrs));
        OpRegistry.Register(OpCode.RnnSeq, ft, dev, (ins, attrs) => RnnSeqOp(ins, (RnnSeqAttrs)attrs));
    }

    #endregion Регистрация
}
