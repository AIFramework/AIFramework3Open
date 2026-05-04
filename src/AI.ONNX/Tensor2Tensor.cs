using AI.DataStructs.Shapes;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using Tensor = AI.DataStructs.Algebraic.Tensor;

namespace AI.ONNX;

/// <summary>
/// Нейронная сеть преобразующая тензор входа в тензор выхода
/// </summary>
public class Tensor2Tensor : IDisposable
{
    /// <summary>
    /// Вычислительный граф
    /// </summary>
    public InferenceSession Session { get; protected set; }
    /// <summary>
    /// Имя входа
    /// </summary>
    public string InputName { get; private set; }
    /// <summary>
    /// Имя выхода
    /// </summary>
    public string OutputName { get; private set; }
    /// <summary>
    /// Высота входного тензора
    /// </summary>
    public int InputH { get; private set; }
    /// <summary>
    /// Ширина входного тензора
    /// </summary>
    public int InputW { get; private set; }
    /// <summary>
    /// Глубина входного тензора
    /// </summary>
    public int InputD { get; private set; }

    /// <summary>
    /// Высота выходного тензора
    /// </summary>
    public int OutpH { get; private set; }
    /// <summary>
    /// Ширина выходного тензора
    /// </summary>
    public int OutpW { get; private set; }
    /// <summary>
    /// Глубина выходного тензора
    /// </summary>
    public int OutpD { get; private set; }
    /// <summary>
    /// Библиотека в которой была создана нейронка
    /// </summary>
    public LibType LibType { get; private set; }
    /// <summary>
    /// Размерность выхода
    /// </summary>
    public int DimOut { get; private set; }

    private readonly int _iH, _iW, _iD, _iHO, _iWO, _iDO;
    private readonly int[] _inputShape;
    private readonly int[] _outpShape;

    /// <summary>
    /// Нейронная сеть преобразующая тензор входа в тензор выхода
    /// </summary>
    public Tensor2Tensor(string path, LibType libType = LibType.Keras, LibType libTypeOut = LibType.Keras)
    {
        Session = new InferenceSession(path);
        InputName = Session.InputMetadata.Keys.First();
        OutputName = Session.OutputMetadata.Keys.First();
        _inputShape = Session.InputMetadata[InputName].Dimensions;
        _outpShape = Session.OutputMetadata[OutputName].Dimensions;
        LibType = libType;

        switch (libType)
        {
            case LibType.Keras:
                _iD = 3;
                _iH = 1;
                _iW = 2;
                break;
            case LibType.PyTorch:
                _iD = 1;
                _iH = 2;
                _iW = 3;
                break;
            case LibType.InverseCh:
                _iD = 1;
                _iH = 2;
                _iW = 3;
                break;
        }

        switch (libTypeOut)
        {
            case LibType.Keras:
                _iDO = 3;
                _iHO = 1;
                _iWO = 2;
                break;
            case LibType.PyTorch:
                _iDO = 1;
                _iHO = 2;
                _iWO = 3;
                break;
            case LibType.InverseCh:
                _iDO = 1;
                _iHO = 2;
                _iWO = 3;
                break;
        }

        InputH = GetDimSafe(_inputShape, _iH, 1);
        InputW = GetDimSafe(_inputShape, _iW, 1);
        InputD = GetDimSafe(_inputShape, _iD, 1);
        DimOut = _outpShape.Length - 1;

        OutpH = GetDimSafe(_outpShape, _iHO, 1);
        OutpW = GetDimSafe(_outpShape, _iWO, 1);
        OutpD = GetDimSafe(_outpShape, _iDO, 1);
    }

    /// <summary>
    /// Преобразование из тензора в тензор
    /// </summary>
    public Tensor Transform(Tensor img)
    {
        int[] shape = (int[])_inputShape.Clone();
        shape[0] = 1;

        DenseTensor<float> tensorF = new DenseTensor<float>(shape);

        bool channelsLast = _iH < _iD;

        if (channelsLast)
        {
            for (int i = 0; i < InputH; i++)
                for (int j = 0; j < InputW; j++)
                    for (int k = 0; k < InputD; k++)
                        tensorF[0, i, j, k] = (float)img[i, j, k];
        }
        else
        {
            for (int i = 0; i < InputH; i++)
                for (int j = 0; j < InputW; j++)
                    for (int k = 0; k < InputD; k++)
                        tensorF[0, k, i, j] = (float)img[i, j, k];
        }

        List<NamedOnnxValue> input = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor<float>(InputName, tensorF)
        };

        using (var results = Session.Run(input))
        {
            var dat = results.First().AsTensor<float>();

            switch (DimOut)
            {
                case 1: return OneD(dat);
                case 2: return TwoD(dat);
                case 3: return TreeD(dat);
            }
        }

        return new Tensor(1, 1, 1);
    }

    /// <summary>
    /// Одномерный тензор
    /// </summary>
    private Tensor OneD(Tensor<float> dat)
    {
        Tensor outTensor = new Tensor(OutpH, OutpW, OutpD);

        for (int i = 0; i < OutpH; i++)
            outTensor[i, 0, 0] = dat[0, i];

        return outTensor;
    }

    /// <summary>
    /// Двумерный тензор
    /// </summary>
    private Tensor TwoD(Tensor<float> dat)
    {
        Tensor outTensor = new Tensor(OutpH, OutpW, OutpD);

        switch (LibType)
        {
            case LibType.Keras:
                for (int i = 0; i < OutpH; i++)
                    for (int j = 0; j < OutpW; j++)
                        outTensor[i, j, 0] = dat[0, i, j];
                break;

            case LibType.PyTorch:
            case LibType.InverseCh:
                for (int i = 0; i < OutpH; i++)
                    for (int j = 0; j < OutpW; j++)
                        outTensor[i, j, 0] = dat[0, 0, i, j];
                break;
        }

        return outTensor;
    }

    /// <summary>
    /// Трехмерный тензор
    /// </summary>
    private Tensor TreeD(Tensor<float> dat)
    {
        Tensor outTensor = new Tensor(OutpH, OutpW, OutpD);

        switch (LibType)
        {
            case LibType.Keras:
                for (int i = 0; i < OutpH; i++)
                    for (int j = 0; j < OutpW; j++)
                        for (int k = 0; k < OutpD; k++)
                            outTensor[i, j, k] = dat[0, i, j, k];
                break;

            case LibType.PyTorch:
            case LibType.InverseCh:
                for (int i = 0; i < OutpH; i++)
                    for (int j = 0; j < OutpW; j++)
                        for (int k = 0; k < OutpD; k++)
                            outTensor[i, j, k] = dat[0, k, i, j];
                break;
        }

        return outTensor;
    }

    private static int GetDimSafe(int[] shape, int index, int defaultValue)
        => index >= 0 && index < shape.Length ? shape[index] : defaultValue;

    /// <inheritdoc/>
    public void Dispose()
    {
        Session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Библиотека в которой была создана нейронка
/// </summary>
public enum LibType
{
    /// <summary>
    /// Кирас (channels last: batch, H, W, D)
    /// </summary>
    Keras,
    /// <summary>
    /// PyTorch (channels first: batch, D, H, W)
    /// </summary>
    PyTorch,
    /// <summary>
    /// Вначале глубина (аналогично PyTorch)
    /// </summary>
    InverseCh
}
