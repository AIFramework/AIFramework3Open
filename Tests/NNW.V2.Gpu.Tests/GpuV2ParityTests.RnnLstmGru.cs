using System;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.Gpu.V2;
using Xunit;

namespace NNW.V2.Gpu.Tests;

public partial class GpuV2ParityTests
{
    #region RNN / LSTM / GRU GPU parity

    /// <summary>Direct kernel test: compares fused GPU LSTM step kernel
    /// against composed CPU formula (see <c>RunLstmStepCpu</c>).</summary>
    [SkippableTheory]
    [InlineData(2, 3)]
    [InlineData(3, 8)]
    [InlineData(8, 16)]
    public void LstmStep_Direct_Parity(int B, int H)
    {
        SkipIfNoCuda();
        int H4 = 4 * H;
        var rng = new Random(99);
        var preact = Tensor.Randn(new Shape(B, H4), rng);
        var c0 = Tensor.Randn(new Shape(B, H), rng);

        var packedCpu = NnInternals.RunLstmStepCpu(preact, c0);
        var k = OpRegistry.Get(OpCode.LstmStep, DType.Float32, Device.Cuda(0));
        var packedGpu = k(new[] { preact.To(Device.Cuda(0)), c0.To(Device.Cuda(0)) },
                          new LstmStepAttrs(B, H))[0].ToCpu();
        AssertClose(packedCpu, packedGpu, 1e-4f);
    }

    /// <summary>Reference CPU implementation of one LSTM step (used by parity tests).</summary>
    internal static class NnInternals
    {
        public static Tensor RunLstmStepCpu(Tensor preact, Tensor c0)
        {
            int B = preact.Shape[0], H4 = preact.Shape[1], H = H4 / 4;
            var pre = preact.Contiguous().AsReadOnlySpan<float>();
            var cP = c0.Contiguous().AsReadOnlySpan<float>();
            var packed = Tensor.Empty(new Shape(2, B, H));
            var sp = packed.AsSpan<float>();
            int planeBH = B * H;
            for (int b = 0; b < B; b++)
            {
                int preBase = b * H4;
                int sIdx = b * H;
                for (int hi = 0; hi < H; hi++)
                {
                    float xI = pre[preBase + 0 * H + hi];
                    float xF = pre[preBase + 1 * H + hi];
                    float xG = pre[preBase + 2 * H + hi];
                    float xO = pre[preBase + 3 * H + hi];
                    float gi = 1f / (1f + MathF.Exp(-xI));
                    float gf = 1f / (1f + MathF.Exp(-xF));
                    float gg = MathF.Tanh(xG);
                    float go = 1f / (1f + MathF.Exp(-xO));
                    float cNew = gf * cP[sIdx + hi] + gi * gg;
                    float tanhC = MathF.Tanh(cNew);
                    float hNew = go * tanhC;
                    sp[sIdx + hi] = hNew;
                    sp[planeBH + sIdx + hi] = cNew;
                }
            }
            return packed;
        }
    }

    [SkippableFact]
    public void RNN_Forward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(40);
        const int B = 2, T = 5, I = 4, H = 6;
        var x = Tensor.Randn(new Shape(B, T, I), rng);
        var h0 = Tensor.Randn(new Shape(B, H), rng);

        var rnn = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(41));
        var (yc, hNc) = rnn.ForwardSeq(x, h0);

        rnn.To(Device.Cuda(0));
        var (yg, hNg) = rnn.ForwardSeq(x.To(Device.Cuda(0)), h0.To(Device.Cuda(0)));
        AssertClose(yc, yg.ToCpu(), 1e-4f);
        AssertClose(hNc, hNg.ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void LSTM_Forward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(50);
        const int B = 3, T = 7, I = 5, H = 8;
        var x = Tensor.Randn(new Shape(B, T, I), rng);
        var h0 = Tensor.Randn(new Shape(B, H), rng);
        var c0 = Tensor.Randn(new Shape(B, H), rng);

        var lstm = new LSTM(I, H, bias: true, batchFirst: true, rng: new Random(51));
        var (yc, hNc, cNc) = lstm.ForwardSeq(x, h0, c0);

        lstm.To(Device.Cuda(0));
        var (yg, hNg, cNg) = lstm.ForwardSeq(x.To(Device.Cuda(0)),
            h0.To(Device.Cuda(0)), c0.To(Device.Cuda(0)));
        AssertClose(yc, yg.ToCpu(), 1e-4f);
        AssertClose(hNc, hNg.ToCpu(), 1e-4f);
        AssertClose(cNc, cNg.ToCpu(), 1e-4f);
    }

    [SkippableFact]
    public void GRU_Forward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(60);
        const int B = 2, T = 6, I = 3, H = 5;
        var x = Tensor.Randn(new Shape(B, T, I), rng);
        var h0 = Tensor.Randn(new Shape(B, H), rng);

        var gru = new GRU(I, H, bias: true, batchFirst: true, rng: new Random(61));
        var (yc, hNc) = gru.ForwardSeq(x, h0);

        gru.To(Device.Cuda(0));
        var (yg, hNg) = gru.ForwardSeq(x.To(Device.Cuda(0)), h0.To(Device.Cuda(0)));
        AssertClose(yc, yg.ToCpu(), 1e-4f);
        AssertClose(hNc, hNg.ToCpu(), 1e-4f);
    }

    // Backward parity для RNN/LSTM/GRU: tolerance 5e-3 (абс.) для коротких
    // последовательностей (T=3..4). Расхождение возникает из-за разной точности
    // FP32-mantissa между CPU naive-MatMul и cuBLAS Sgemm (~1e-7 на multiply),
    // которое накапливается по T шагам через tanh'/sigmoid'-факторы.
    // Раньше tolerance был 5e-2 из-за бага в Engine.CopyTensorContents
    // (offset не учитывался при H2D-копии аккумулятора градиента — см.
    // комментарий там); сейчас поведение приведено в норму.
    [SkippableFact]
    public void LSTM_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(70);
        const int B = 2, T = 4, I = 3, H = 5;
        var x = Tensor.Randn(new Shape(B, T, I), rng);

        var lstmC = new LSTM(I, H, bias: true, batchFirst: true, rng: new Random(71));
        var xc = x.SetRequiresGrad(true);
        var (yc, _, _) = lstmC.ForwardSeq(xc);
        TensorOps.Sum(yc).Backward();

        var lstmG = new LSTM(I, H, bias: true, batchFirst: true, rng: new Random(71));
        lstmG.To(Device.Cuda(0));
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var (yg, _, _) = lstmG.ForwardSeq(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 5e-3f);
        AssertClose(lstmC.Cell.WeightIH.Tensor.Grad,
            lstmG.Cell.WeightIH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(lstmC.Cell.WeightHH.Tensor.Grad,
            lstmG.Cell.WeightHH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(lstmC.Cell.BiasIH.Tensor.Grad,
            lstmG.Cell.BiasIH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(lstmC.Cell.BiasHH.Tensor.Grad,
            lstmG.Cell.BiasHH.Tensor.Grad.ToCpu(), 5e-3f);
    }

    [SkippableFact]
    public void GRU_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(80);
        const int B = 2, T = 3, I = 4, H = 5;
        var x = Tensor.Randn(new Shape(B, T, I), rng);

        var gruC = new GRU(I, H, bias: true, batchFirst: true, rng: new Random(81));
        var xc = x.SetRequiresGrad(true);
        var (yc, _) = gruC.ForwardSeq(xc);
        TensorOps.Sum(yc).Backward();

        var gruG = new GRU(I, H, bias: true, batchFirst: true, rng: new Random(81));
        gruG.To(Device.Cuda(0));
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var (yg, _) = gruG.ForwardSeq(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 5e-3f);
        AssertClose(gruC.Cell.WeightIH.Tensor.Grad,
            gruG.Cell.WeightIH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(gruC.Cell.WeightHH.Tensor.Grad,
            gruG.Cell.WeightHH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(gruC.Cell.BiasIH.Tensor.Grad,
            gruG.Cell.BiasIH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(gruC.Cell.BiasHH.Tensor.Grad,
            gruG.Cell.BiasHH.Tensor.Grad.ToCpu(), 5e-3f);
    }

    [SkippableFact]
    public void RNN_Backward_Parity()
    {
        SkipIfNoCuda();
        var rng = new Random(90);
        const int B = 2, T = 4, I = 3, H = 5;
        var x = Tensor.Randn(new Shape(B, T, I), rng);

        var rnnC = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        var xc = x.SetRequiresGrad(true);
        var (yc, _) = rnnC.ForwardSeq(xc);
        TensorOps.Sum(yc).Backward();

        var rnnG = new RNN(I, H, "tanh", bias: true, batchFirst: true, rng: new Random(91));
        rnnG.To(Device.Cuda(0));
        var xg = x.To(Device.Cuda(0)).SetRequiresGrad(true);
        var (yg, _) = rnnG.ForwardSeq(xg);
        TensorOps.Sum(yg).Backward();

        AssertClose(xc.Grad, xg.Grad.ToCpu(), 5e-3f);
        AssertClose(rnnC.Cell.WeightIH.Tensor.Grad,
            rnnG.Cell.WeightIH.Tensor.Grad.ToCpu(), 5e-3f);
        AssertClose(rnnC.Cell.WeightHH.Tensor.Grad,
            rnnG.Cell.WeightHH.Tensor.Grad.ToCpu(), 5e-3f);
    }

    #endregion RNN / LSTM / GRU GPU parity
}
