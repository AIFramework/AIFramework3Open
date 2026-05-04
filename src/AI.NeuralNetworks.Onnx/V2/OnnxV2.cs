using System;
using System.Collections.Generic;
using System.IO;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Nn;
using Google.Protobuf;
using OnnxAttrType = global::Onnx.AttributeProto.Types.AttributeType;
using OnnxDType = global::Onnx.TensorProto.Types.DataType;

namespace AI.ML.NeuralNetworks.Onnx.V2;

/// <summary>
/// Bridge между V2-Tensor/Module и ONNX-форматом.
/// </summary>
/// <remarks>
/// <para>
/// Минимальный, но рабочий уровень: позволяет (1) сохранить веса <see cref="Module"/>
/// в ONNX-файл как набор инициализаторов и (2) загрузить веса из ONNX в Module
/// (по совпадающим именам параметров). Полноценный экспорт графа операций
/// (для inference в onnxruntime) — задача более крупного scope; здесь
/// фокус на checkpoint interop с PyTorch / Hugging Face.
/// </para>
/// <para>
/// <b>Использование:</b>
/// <code>
/// // Сохранить веса.
/// OnnxV2.SaveStateDict(model, "model_weights.onnx");
///
/// // Загрузить веса.
/// OnnxV2.LoadStateDict(model, "model_weights.onnx", strict: true);
/// </code>
/// </para>
/// </remarks>
public static class OnnxV2
{
    private const long IrVersion = 9;
    private const long OpsetVersion = 17;

    /// <summary>
    /// Сохранить все параметры модели в ONNX-файл (в виде инициализаторов).
    /// </summary>
    public static void SaveStateDict(Module model, string path)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        using var fs = File.Create(path);
        SaveStateDict(model, fs);
    }

    /// <summary>Сохранить все параметры модели в поток.</summary>
    public static void SaveStateDict(Module model, Stream stream)
    {
        var proto = new global::Onnx.ModelProto
        {
            IrVersion = IrVersion,
            ProducerName = "AIFramework3.V2",
            ProducerVersion = "2.0",
            ModelVersion = 1,
            Graph = new global::Onnx.GraphProto { Name = "state_dict" }
        };
        proto.OpsetImport.Add(new global::Onnx.OperatorSetIdProto { Domain = string.Empty, Version = OpsetVersion });

        foreach (var (name, param) in model.NamedParameters())
        {
            var t = param.Tensor.IsContiguous ? param.Tensor : param.Tensor.Contiguous();
            proto.Graph.Initializer.Add(TensorToInitializer(name, t));
        }
        foreach (var (name, buf) in model.NamedBuffers())
        {
            var t = buf.IsContiguous ? buf : buf.Contiguous();
            proto.Graph.Initializer.Add(TensorToInitializer(name, t));
        }
        proto.WriteTo(stream);
    }

    /// <summary>
    /// Загрузить параметры модели из ONNX-файла.
    /// </summary>
    /// <param name="strict">Если true — выбросить, если найдены лишние/пропущенные имена.</param>
    /// <returns>Отчёт о загрузке: сколько параметров загружено / пропущено.</returns>
    public static LoadResult LoadStateDict(Module model, string path, bool strict = true)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        using var fs = File.OpenRead(path);
        return LoadStateDict(model, fs, strict);
    }

    /// <summary>Загрузить веса из потока.</summary>
    public static LoadResult LoadStateDict(Module model, Stream stream, bool strict = true)
    {
        var proto = global::Onnx.ModelProto.Parser.ParseFrom(stream);
        var byName = new Dictionary<string, global::Onnx.TensorProto>(StringComparer.Ordinal);
        if (proto.Graph != null)
            foreach (var init in proto.Graph.Initializer)
                byName[init.Name] = init;

        var report = new LoadResult();
        var missing = new List<string>();
        var loaded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, param) in model.NamedParameters())
        {
            if (!byName.TryGetValue(name, out var init))
            {
                missing.Add(name);
                continue;
            }
            CopyInitializerInto(init, param.Tensor);
            loaded.Add(name);
            report.Loaded++;
        }
        foreach (var (name, buf) in model.NamedBuffers())
        {
            if (!byName.TryGetValue(name, out var init))
            {
                missing.Add(name);
                continue;
            }
            CopyInitializerInto(init, buf);
            loaded.Add(name);
            report.Loaded++;
        }

        report.Missing = missing.ToArray();
        var unexpected = new List<string>();
        foreach (var k in byName.Keys) if (!loaded.Contains(k)) unexpected.Add(k);
        report.Unexpected = unexpected.ToArray();

        if (strict && (missing.Count > 0 || unexpected.Count > 0))
            throw new InvalidOperationException(
                $"State-dict mismatch. Missing: [{string.Join(", ", missing)}]; " +
                $"Unexpected: [{string.Join(", ", unexpected)}]");
        return report;
    }

    /// <summary>Конвертировать V2-Tensor в ONNX-TensorProto (initializer).</summary>
    public static global::Onnx.TensorProto TensorToInitializer(string name, Tensor t)
    {
        if (t == null) throw new ArgumentNullException(nameof(t));
        var contig = t.IsContiguous ? t : t.Contiguous();
        var cpu = contig.Device.Type == DeviceType.Cpu ? contig : contig.ToCpu();
        var proto = new global::Onnx.TensorProto { Name = name };
        foreach (var d in cpu.Shape.AsSpan().ToArray()) proto.Dims.Add(d);
        proto.DataType = (int)DTypeToOnnx(cpu.DType);
        switch (cpu.DType)
        {
            case DType.Float32:
                {
                    var span = cpu.AsReadOnlySpan<float>();
                    var bytes = new byte[span.Length * 4];
                    Buffer.BlockCopy(span.ToArray(), 0, bytes, 0, bytes.Length);
                    proto.RawData = ByteString.CopyFrom(bytes);
                    break;
                }
            case DType.Float64:
                {
                    var span = cpu.AsReadOnlySpan<double>();
                    var bytes = new byte[span.Length * 8];
                    Buffer.BlockCopy(span.ToArray(), 0, bytes, 0, bytes.Length);
                    proto.RawData = ByteString.CopyFrom(bytes);
                    break;
                }
            case DType.Int32:
                {
                    var span = cpu.AsReadOnlySpan<int>();
                    var bytes = new byte[span.Length * 4];
                    Buffer.BlockCopy(span.ToArray(), 0, bytes, 0, bytes.Length);
                    proto.RawData = ByteString.CopyFrom(bytes);
                    break;
                }
            case DType.Int64:
                {
                    var span = cpu.AsReadOnlySpan<long>();
                    var bytes = new byte[span.Length * 8];
                    Buffer.BlockCopy(span.ToArray(), 0, bytes, 0, bytes.Length);
                    proto.RawData = ByteString.CopyFrom(bytes);
                    break;
                }
            default:
                throw new NotSupportedException($"DType {cpu.DType} ещё не поддерживается в ONNX-bridge.");
        }
        return proto;
    }

    /// <summary>Скопировать данные из ONNX-инициализатора в существующий V2-тензор.</summary>
    public static void CopyInitializerInto(global::Onnx.TensorProto init, Tensor dst)
    {
        if (init == null) throw new ArgumentNullException(nameof(init));
        if (dst == null) throw new ArgumentNullException(nameof(dst));
        // Проверка формы.
        if (init.Dims.Count != dst.Rank)
            throw new InvalidOperationException(
                $"Rank mismatch для '{init.Name}': ONNX={init.Dims.Count}, Tensor={dst.Rank}.");
        for (int i = 0; i < init.Dims.Count; i++)
            if ((int)init.Dims[i] != dst.Shape[i])
                throw new InvalidOperationException(
                    $"Shape mismatch для '{init.Name}' на оси {i}: ONNX={init.Dims[i]}, Tensor={dst.Shape[i]}.");

        var dt = OnnxToDType((OnnxDType)init.DataType);
        if (dt != dst.DType)
            throw new InvalidOperationException(
                $"DType mismatch для '{init.Name}': ONNX={dt}, Tensor={dst.DType}.");

        // Загружаем сначала на CPU, затем (если нужно) переносим на исходное устройство.
        var dstCpu = dst.Device.Type == DeviceType.Cpu
            ? dst
            : Tensor.Empty(dst.Shape, dt, AI.ML.NeuralNetworks.V2.Device.Cpu);

        switch (dt)
        {
            case DType.Float32: ExtractFloats(init, dstCpu.AsSpan<float>()); break;
            case DType.Float64: ExtractDoubles(init, dstCpu.AsSpan<double>()); break;
            case DType.Int32: ExtractInts(init, dstCpu.AsSpan<int>()); break;
            case DType.Int64: ExtractLongs(init, dstCpu.AsSpan<long>()); break;
            default: throw new NotSupportedException($"DType {dt} ещё не поддерживается.");
        }

        if (dst.Device.Type != DeviceType.Cpu)
        {
            // Копируем CPU -> device storage по байтам.
            if (dst.Storage is AI.ML.NeuralNetworks.V2.Storage.IHostCopyable hc)
                hc.CopyFromHost(dstCpu.Storage, dst.Offset, dst.NumElements);
            else
                throw new NotSupportedException(
                    $"Storage {dst.Storage.GetType().Name} не поддерживает host-copy.");
        }
    }

    private static void ExtractFloats(global::Onnx.TensorProto t, Span<float> dst)
    {
        if (t.FloatData != null && t.FloatData.Count > 0)
        {
            for (int i = 0; i < dst.Length; i++) dst[i] = t.FloatData[i];
            return;
        }
        if (t.RawData != null && t.RawData.Length > 0)
        {
            var raw = t.RawData.ToByteArray();
            var arr = new float[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
            arr.AsSpan().CopyTo(dst);
            return;
        }
        throw new InvalidOperationException($"Initializer '{t.Name}' не содержит float-данных.");
    }

    private static void ExtractDoubles(global::Onnx.TensorProto t, Span<double> dst)
    {
        if (t.DoubleData != null && t.DoubleData.Count > 0)
        {
            for (int i = 0; i < dst.Length; i++) dst[i] = t.DoubleData[i];
            return;
        }
        if (t.RawData != null && t.RawData.Length > 0)
        {
            var raw = t.RawData.ToByteArray();
            var arr = new double[raw.Length / 8];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
            arr.AsSpan().CopyTo(dst);
            return;
        }
        throw new InvalidOperationException($"Initializer '{t.Name}' не содержит double-данных.");
    }

    private static void ExtractInts(global::Onnx.TensorProto t, Span<int> dst)
    {
        if (t.Int32Data != null && t.Int32Data.Count > 0)
        {
            for (int i = 0; i < dst.Length; i++) dst[i] = t.Int32Data[i];
            return;
        }
        if (t.RawData != null && t.RawData.Length > 0)
        {
            var raw = t.RawData.ToByteArray();
            var arr = new int[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
            arr.AsSpan().CopyTo(dst);
            return;
        }
        throw new InvalidOperationException($"Initializer '{t.Name}' не содержит int32-данных.");
    }

    private static void ExtractLongs(global::Onnx.TensorProto t, Span<long> dst)
    {
        if (t.Int64Data != null && t.Int64Data.Count > 0)
        {
            for (int i = 0; i < dst.Length; i++) dst[i] = t.Int64Data[i];
            return;
        }
        if (t.RawData != null && t.RawData.Length > 0)
        {
            var raw = t.RawData.ToByteArray();
            var arr = new long[raw.Length / 8];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
            arr.AsSpan().CopyTo(dst);
            return;
        }
        throw new InvalidOperationException($"Initializer '{t.Name}' не содержит int64-данных.");
    }

    private static OnnxDType DTypeToOnnx(DType dt) => dt switch
    {
        DType.Float32 => OnnxDType.Float,
        DType.Float64 => OnnxDType.Double,
        DType.Int32 => OnnxDType.Int32,
        DType.Int64 => OnnxDType.Int64,
        _ => throw new NotSupportedException($"DType {dt} -> ONNX не поддерживается.")
    };

    private static DType OnnxToDType(OnnxDType dt) => dt switch
    {
        OnnxDType.Float => DType.Float32,
        OnnxDType.Double => DType.Float64,
        OnnxDType.Int32 => DType.Int32,
        OnnxDType.Int64 => DType.Int64,
        _ => throw new NotSupportedException($"ONNX-DType {dt} не поддерживается.")
    };

    /// <summary>Отчёт о загрузке state-dict.</summary>
    public sealed class LoadResult
    {
        /// <summary>Сколько параметров/буферов реально загружено.</summary>
        public int Loaded { get; set; }
        /// <summary>Параметры, для которых в файле нет данных.</summary>
        public string[] Missing { get; set; } = Array.Empty<string>();
        /// <summary>Имена в файле, которых нет в модели.</summary>
        public string[] Unexpected { get; set; } = Array.Empty<string>();
    }
}
