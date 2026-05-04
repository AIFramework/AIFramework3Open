using AI.DataStructs.Algebraic;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.ONNX.Base.LayersModel;

/// <summary>
/// Полносвязный слой (ONNX)
/// </summary>
public class Dense : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    /// <summary>
    /// Тип данных для вычислений
    /// </summary>
    public DataType DType { get; set; } = DataType.Float32;

    /// <summary>
    /// Полносвязный слой
    /// </summary>
    public Dense(string modelPath, DataType dtype = DataType.Float32)
    {
        _session = new InferenceSession(modelPath);
        _inputName = _session.InputMetadata.Keys.First();
        DType = dtype;
    }

    /// <summary>
    /// Прямой проход без батча
    /// </summary>
    public Vector ForwardNoBatch(Vector inputVector)
    {
        RunOptions runOptions = new RunOptions();
        var input = CreateInput(inputVector);

        try
        {
            var resultTensors = _session.Run(runOptions, input, _session.OutputNames);

            switch (DType)
            {
                case DataType.Float32:
                    return resultTensors[0].GetTensorDataAsSpan<float>().ToArray();
                case DataType.Float64:
                    return resultTensors[0].GetTensorDataAsSpan<double>().ToArray();
                case DataType.Int32:
                    return Array.ConvertAll(
                        resultTensors[0].GetTensorDataAsSpan<int>().ToArray(),
                        x => (double)x);
                case DataType.Int64:
                    return Array.ConvertAll(
                        resultTensors[0].GetTensorDataAsSpan<long>().ToArray(),
                        x => (double)x);
                default:
                    throw new NotSupportedException($"Тип данных {DType} не поддерживается.");
            }
        }
        finally
        {
            foreach (var kv in input)
                kv.Value.Dispose();
        }
    }

    private Dictionary<string, OrtValue> CreateInput(Vector inpVect)
    {
        var shape = new long[] { 1, inpVect.Count };
        OrtValue inputOrtValue;

        switch (DType)
        {
            case DataType.Float32:
                inputOrtValue = OrtValue.CreateTensorValueFromMemory((float[])inpVect, shape);
                break;
            case DataType.Float64:
                inputOrtValue = OrtValue.CreateTensorValueFromMemory((double[])inpVect, shape);
                break;
            case DataType.Int32:
                inputOrtValue = OrtValue.CreateTensorValueFromMemory(
                    Array.ConvertAll(inpVect.ToArray(), x => (int)x), shape);
                break;
            case DataType.Int64:
                inputOrtValue = OrtValue.CreateTensorValueFromMemory(
                    Array.ConvertAll(inpVect.ToArray(), x => (long)x), shape);
                break;
            default:
                throw new NotSupportedException($"Тип данных {DType} не поддерживается.");
        }

        return new Dictionary<string, OrtValue>
        {
            { _inputName, inputOrtValue }
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Поддерживаемые типы данных
/// </summary>
public enum DataType
{
    Float32,
    Float64,
    Int32,
    Int64,
}
