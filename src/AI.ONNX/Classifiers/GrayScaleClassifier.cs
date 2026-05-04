using AI.DataStructs.Algebraic;

namespace AI.ONNX.Classifiers;

/// <summary>
/// Классификатор изображений в градациях серого
/// </summary>
public class GrayScaleClassifier
{
    private readonly Tensor2Tensor _t2t;

    public GrayScaleClassifier(string path, LibType libType = LibType.Keras)
    {
        _t2t = new Tensor2Tensor(path, libType);
    }

    /// <summary>
    /// Классификация изображения (матрицы яркости)
    /// </summary>
    public Vector Classify(Matrix img)
    {
        Tensor tensor = Tensor.FromMatrices(new[] { img });
        return _t2t.Transform(tensor).Data;
    }
}
