using AI.DataStructs.Algebraic;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.ONNX.NLP.Bert;

/// <summary>
/// Работа с моделью Bert
/// </summary>
public class BertInfer : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputIdsName;
    private readonly string _attentionMaskName;
    private readonly string _tokenTypeIdsName;

    /// <summary>
    /// Работа с моделью Bert
    /// </summary>
    public BertInfer(string modelPath)
    {
        _session = new InferenceSession(modelPath);

        var inputKeys = _session.InputMetadata.Keys.ToArray();
        _inputIdsName = inputKeys.Length > 0 ? inputKeys[0] : "input_ids";
        _attentionMaskName = inputKeys.Length > 1 ? inputKeys[1] : "input_mask";
        _tokenTypeIdsName = inputKeys.Length > 2 ? inputKeys[2] : "segment_ids";
    }

    /// <summary>
    /// Прямой проход Bert
    /// </summary>
    public Vector[] Forward(IEnumerable<int> inpIds, IEnumerable<int> attentionMask, IEnumerable<int> types)
    {
        RunOptions runOptions = new RunOptions();
        var input = CreateInput(ToLongArray(inpIds), ToLongArray(attentionMask), ToLongArray(types));

        try
        {
            using var resultTensors = _session.Run(runOptions, input, _session.OutputNames);

            var output = new Vector[resultTensors.Count];

            for (int i = 0; i < resultTensors.Count; i++)
                output[i] = resultTensors[i].GetTensorDataAsSpan<float>().ToArray();

            return output;
        }
        finally
        {
            foreach (var kv in input)
                kv.Value.Dispose();
        }
    }

    /// <summary>
    /// Создание входных данных для сессии
    /// </summary>
    public Dictionary<string, OrtValue> CreateInput(long[] inpIds, long[] attentionMask, long[] types)
    {
        var shape = new long[] { 1, inpIds.Length };

        return new Dictionary<string, OrtValue>
        {
            { _inputIdsName, OrtValue.CreateTensorValueFromMemory(inpIds, shape) },
            { _attentionMaskName, OrtValue.CreateTensorValueFromMemory(attentionMask, shape) },
            { _tokenTypeIdsName, OrtValue.CreateTensorValueFromMemory(types, shape) }
        };
    }

    private static long[] ToLongArray(IEnumerable<int> ints)
    {
        var data = ints.ToArray();
        long[] ret = new long[data.Length];

        for (int i = 0; i < data.Length; i++)
            ret[i] = data[i];

        return ret;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
