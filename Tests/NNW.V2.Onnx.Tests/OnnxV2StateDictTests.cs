using System.IO;
using AI.ML.NeuralNetworks.Onnx.V2;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using Xunit;

namespace NNW.V2.Onnx.Tests;

/// <summary>
/// Round-trip тесты сохранения/загрузки <see cref="Module"/> state-dict через ONNX.
/// </summary>
public class OnnxV2StateDictTests
{
    private const float Tol = 1e-6f;

    private static Sequential MakeMlp(int? seed = 42)
    {
        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        return new Sequential(
            new Linear(8, 16, bias: true, rng: rng),
            new ReLU(),
            new Linear(16, 4, bias: true, rng: rng));
    }

    [Fact]
    public void SaveLoad_RoundTrip_Sequential_PreservesAllWeights()
    {
        var src = MakeMlp(seed: 123);
        using var ms = new MemoryStream();
        OnnxV2.SaveStateDict(src, ms);

        Assert.True(ms.Length > 0, "ONNX-blob must not be empty.");

        ms.Position = 0;
        var dst = MakeMlp(seed: null);

        var report = OnnxV2.LoadStateDict(dst, ms, strict: true);
        Assert.True(report.Loaded > 0);
        Assert.Empty(report.Missing);
        Assert.Empty(report.Unexpected);

        AssertParametersEqual(src, dst);
    }

    [Fact]
    public void Load_NonStrict_AllowsMissingNames()
    {
        var rng = new System.Random(7);
        var src = new Sequential(new Linear(4, 4, bias: true, rng: rng));
        using var ms = new MemoryStream();
        OnnxV2.SaveStateDict(src, ms);

        var dst = new Sequential(new Linear(4, 4), new ReLU(), new Linear(4, 2));
        ms.Position = 0;
        var report = OnnxV2.LoadStateDict(dst, ms, strict: false);

        Assert.True(report.Loaded >= 2, "Должны загрузиться weight + bias первого Linear.");
        Assert.NotEmpty(report.Missing);
    }

    [Fact]
    public void Forward_AfterLoad_MatchesOriginal()
    {
        var src = MakeMlp(seed: 999);
        using var ms = new MemoryStream();
        OnnxV2.SaveStateDict(src, ms);

        ms.Position = 0;
        var dst = MakeMlp(seed: null);
        OnnxV2.LoadStateDict(dst, ms, strict: true);

        var x = Tensor.From(new float[]
        {
            0.10f, 0.20f, 0.30f, 0.40f, 0.50f, 0.60f, 0.70f, 0.80f
        }, new Shape(1, 8));

        var ySrc = src.Forward(x);
        var yDst = dst.Forward(x);

        var s = ySrc.AsReadOnlySpan<float>();
        var d = yDst.AsReadOnlySpan<float>();
        Assert.Equal(s.Length, d.Length);
        for (int i = 0; i < s.Length; i++)
            Assert.True(System.MathF.Abs(s[i] - d[i]) < Tol,
                $"Output mismatch at {i}: {s[i]} vs {d[i]}");
    }

    [Fact]
    public void Save_FilePath_IsReadable()
    {
        var src = MakeMlp(seed: 1);
        var path = Path.Combine(Path.GetTempPath(), $"onnxv2_state_{System.Guid.NewGuid():N}.onnx");
        try
        {
            OnnxV2.SaveStateDict(src, path);
            var dst = MakeMlp(seed: null);
            OnnxV2.LoadStateDict(dst, path, strict: true);
            AssertParametersEqual(src, dst);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertParametersEqual(Module a, Module b)
    {
        var aParams = new System.Collections.Generic.Dictionary<string, Parameter>();
        foreach (var (n, p) in a.NamedParameters()) aParams[n] = p;
        foreach (var (n, p) in b.NamedParameters())
        {
            Assert.True(aParams.TryGetValue(n, out var ap), $"Param '{n}' missing in A.");
            Assert.Equal(ap.Tensor.Shape, p.Tensor.Shape);
            var s1 = ap.Tensor.AsReadOnlySpan<float>();
            var s2 = p.Tensor.AsReadOnlySpan<float>();
            Assert.Equal(s1.Length, s2.Length);
            for (int i = 0; i < s1.Length; i++)
                Assert.True(System.MathF.Abs(s1[i] - s2[i]) < Tol,
                    $"Param '{n}' differs at {i}: {s1[i]} vs {s2[i]}");
        }
    }
}
