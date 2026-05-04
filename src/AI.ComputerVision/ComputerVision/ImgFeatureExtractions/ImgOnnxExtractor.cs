using AI.DataPrepaire.FeatureExtractors;
using AI.DataStructs.Algebraic;
using AI.ONNX;
using SkiaSharp;
using System;

namespace AI.ComputerVision.ImgFeatureExtractions;

/// <summary>
/// Извлечение признаков на базе onnx модели
/// </summary>
[Serializable]
public class ImgOnnxExtractor : FeaturesExtractor<SKBitmap>
{
    private readonly Tensor2Tensor emb;
    private readonly Vector _mean, _std;
    private readonly int _h, _w;

    /// <summary>
    /// Извлечение признаков на базе onnx модели
    /// </summary>
    public ImgOnnxExtractor(string pathToOnnxModel, Vector mean, Vector std, int inpH, int inpW, LibType back)
    {
        emb = new Tensor2Tensor(pathToOnnxModel, back);
        _w = inpW;
        _h = inpH;
        _mean = mean;
        _std = std;
    }

    /// <summary>
    /// Получение признаков из модели
    /// </summary>
    public override Vector GetFeatures(SKBitmap data)
    {
        using var resized = data.Resize(new SKImageInfo(_w, _h), new SKSamplingOptions(SKCubicResampler.Mitchell));
        var inpTensor = ImageMatrixConverter.BmpToTensor(resized) / 255;
        var transformTensor = new Tensor(_h, _w, inpTensor.Depth);

        double m0 = _mean[0], m1 = _mean[1], m2 = _mean[2];
        double s0 = _std[0], s1 = _std[1], s2 = _std[2];

        for (int i = 0; i < inpTensor.Height; i++)
            for (int j = 0; j < inpTensor.Width; j++)
            {
                transformTensor[i, j, 0] = (inpTensor[i, j, 2] - m2) / s2;
                transformTensor[i, j, 1] = (inpTensor[i, j, 1] - m1) / s1;
                transformTensor[i, j, 2] = (inpTensor[i, j, 0] - m0) / s0;
            }

        // Ранее в emb.Transform передавался ненормализованный inpTensor — BGR-перестановка
        // и mean/std фактически не применялись. Передаём подготовленный tensor.
        return emb.Transform(transformTensor).Data;
    }
}
