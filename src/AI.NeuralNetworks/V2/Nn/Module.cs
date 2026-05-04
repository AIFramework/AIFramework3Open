using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Базовый класс для всех слоёв и моделей. «Фрактальный» дизайн: модули могут
/// содержать модули рекурсивно, и обход параметров/буферов работает на любую глубину.
/// </summary>
/// <remarks>
/// <para>
/// Аналог <c>torch.nn.Module</c>. Производный класс регистрирует:
/// <list type="bullet">
///   <item><see cref="RegisterParameter"/> — обучаемые параметры (Linear.Weight, Bias).</item>
///   <item><see cref="RegisterBuffer"/> — non-trainable state (BN.RunningMean).</item>
///   <item><see cref="RegisterModule"/> — вложенные модули (Sequential, Transformer).</item>
/// </list>
/// </para>
/// <para>
/// Реализует <see cref="Forward"/> в производном классе — ничего больше не нужно.
/// Параметры/буферы/обход — наследуются автоматически.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> регистрация происходит в конструкторе и обычно
/// не пересекается с inference; чтение через <see cref="Parameters"/> и
/// <see cref="Buffers"/> — read-only и потокобезопасно.
/// </para>
/// </remarks>
public abstract class Module
{
    private readonly Dictionary<string, Parameter> _parameters = new();
    private readonly Dictionary<string, Buffer> _buffers = new();
    private readonly Dictionary<string, Module> _modules = new();

    private bool _training = true;

    /// <summary>
    /// Имя модуля (заполняется родителем при регистрации; для корня — пустая строка).
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>True, если модуль в режиме обучения (Dropout/BN ведут себя по-другому).</summary>
    public bool Training => _training;

    /// <summary>
    /// Прямой проход. Реализуется производным классом.
    /// </summary>
    public abstract Tensor Forward(Tensor input);

    /// <summary>
    /// Универсальный operator() — позволяет вызывать модуль как функцию.
    /// </summary>
    public Tensor Call(Tensor input) => Forward(input);

    #region Регистрация

    /// <summary>
    /// Регистрирует обучаемый параметр под именем <paramref name="name"/>.
    /// </summary>
    protected Parameter RegisterParameter(string name, Tensor tensor)
    {
        EnsureValidName(name);
        var p = new Parameter(tensor) { Name = QualifiedName(name) };
        _parameters[name] = p;
        return p;
    }

    /// <summary>
    /// Регистрирует non-trainable буфер (running mean, masks, ...). 
    /// Не учитывается оптимизатором, но сохраняется в state_dict.
    /// Возвращает обёртку <see cref="Buffer"/>, чей <see cref="Buffer.Tensor"/>
    /// автоматически обновляется при <see cref="To(Device)"/>.
    /// </summary>
    protected Buffer RegisterBuffer(string name, Tensor tensor)
    {
        EnsureValidName(name);
        var buf = new Buffer(tensor) { Name = QualifiedName(name) };
        _buffers[name] = buf;
        return buf;
    }

    /// <summary>
    /// Регистрирует вложенный модуль; имя становится префиксом для его параметров.
    /// </summary>
    protected T RegisterModule<T>(string name, T module) where T : Module
    {
        EnsureValidName(name);
        if (module == null) throw new ArgumentNullException(nameof(module));
        module.Name = QualifiedName(name);
        _modules[name] = module;
        return module;
    }

    private void EnsureValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Имя не должно быть пустым.", nameof(name));
        if (_parameters.ContainsKey(name) || _buffers.ContainsKey(name) || _modules.ContainsKey(name))
            throw new ArgumentException($"Имя '{name}' уже зарегистрировано в модуле {GetType().Name}.");
    }

    private string QualifiedName(string local) =>
        string.IsNullOrEmpty(Name) ? local : $"{Name}.{local}";

    #endregion Регистрация

    #region Доступ

    /// <summary>
    /// Перечислить все параметры (рекурсивно по вложенным модулям).
    /// </summary>
    public IEnumerable<Parameter> Parameters()
    {
        foreach (var p in _parameters.Values) yield return p;
        foreach (var m in _modules.Values)
            foreach (var p in m.Parameters()) yield return p;
    }

    /// <summary>
    /// Перечислить (имя, параметр) (рекурсивно).
    /// </summary>
    public IEnumerable<(string name, Parameter param)> NamedParameters()
    {
        foreach (var kv in _parameters)
            yield return (QualifiedName(kv.Key), kv.Value);
        foreach (var (childName, child) in _modules)
            foreach (var (name, p) in child.NamedParameters())
                yield return (name, p);
    }

    /// <summary>
    /// Перечислить буферы (рекурсивно).
    /// </summary>
    public IEnumerable<Tensor> Buffers()
    {
        foreach (var b in _buffers.Values) yield return b.Tensor;
        foreach (var m in _modules.Values)
            foreach (var b in m.Buffers()) yield return b;
    }

    /// <summary>(имя, буфер) рекурсивно.</summary>
    public IEnumerable<(string name, Tensor buffer)> NamedBuffers()
    {
        foreach (var kv in _buffers)
            yield return (QualifiedName(kv.Key), kv.Value.Tensor);
        foreach (var (childName, child) in _modules)
            foreach (var (name, b) in child.NamedBuffers())
                yield return (name, b);
    }

    /// <summary>Перечислить дочерние модули (только прямые потомки).</summary>
    public IEnumerable<Module> Children() => _modules.Values;

    /// <summary>Перечислить все модули рекурсивно (включая self).</summary>
    public IEnumerable<Module> Modules()
    {
        yield return this;
        foreach (var m in _modules.Values)
            foreach (var sub in m.Modules()) yield return sub;
    }

    #endregion Доступ

    #region Режимы

    /// <summary>Перейти в train-режим (рекурсивно).</summary>
    public Module Train(bool mode = true)
    {
        _training = mode;
        foreach (var m in _modules.Values) m.Train(mode);
        return this;
    }

    /// <summary>Перейти в eval-режим (= Train(false)).</summary>
    public Module Eval() => Train(false);

    #endregion Режимы

    #region Перенос на устройство

    /// <summary>
    /// Переместить все параметры и буферы модуля (рекурсивно) на указанное устройство.
    /// Аналог <c>torch.nn.Module.to(device)</c>.
    /// </summary>
    public Module To(Device device)
    {
        foreach (var p in _parameters.Values) p.MoveTo(device);
        foreach (var b in _buffers.Values) b.MoveTo(device);
        foreach (var m in _modules.Values) m.To(device);
        return this;
    }

    /// <summary>Алиас <see cref="To(Device)"/>: model.Cuda(0).</summary>
    public Module Cuda(int index = 0) => To(V2.Device.Cuda(index));

    /// <summary>Алиас <see cref="To(Device)"/>: model.Cpu().</summary>
    public Module Cpu() => To(V2.Device.Cpu);

    #endregion Перенос на устройство

    #region State dict

    /// <summary>
    /// Сериализовать все параметры и буферы в плоский словарь
    /// "qualified.name" -> <see cref="Tensor"/>.
    /// </summary>
    public Dictionary<string, Tensor> StateDict()
    {
        var dict = new Dictionary<string, Tensor>();
        foreach (var (name, p) in NamedParameters())
            dict[name] = p.Tensor;
        foreach (var (name, b) in NamedBuffers())
            dict[name] = b;
        return dict;
    }

    /// <summary>
    /// Загрузить состояние из словаря. Имена должны совпадать с теми, что
    /// возвращает <see cref="StateDict"/>. Несовпадающие формы — ошибка.
    /// </summary>
    public void LoadStateDict(Dictionary<string, Tensor> state, bool strict = true)
    {
        var current = new Dictionary<string, (Tensor target, bool isParam)>();
        foreach (var (n, p) in NamedParameters()) current[n] = (p.Tensor, true);
        foreach (var (n, b) in NamedBuffers()) current[n] = (b, false);

        foreach (var (k, src) in state)
        {
            if (!current.TryGetValue(k, out var entry))
            {
                if (strict)
                    throw new ArgumentException($"Неизвестный ключ '{k}' при загрузке state_dict.");
                continue;
            }
            if (!entry.target.Shape.Equals(src.Shape))
                throw new InvalidOperationException(
                    $"Несовпадение формы для '{k}': ожидалось {entry.target.Shape}, получено {src.Shape}.");
            // copy data
            CopyTensor(src, entry.target);
        }

        if (strict)
        {
            foreach (var k in current.Keys)
                if (!state.ContainsKey(k))
                    throw new ArgumentException($"Отсутствует ключ '{k}' в state_dict.");
        }
    }

    private static void CopyTensor(Tensor src, Tensor dst)
    {
        if (src.DType != dst.DType)
            throw new InvalidOperationException(
                $"DType mismatch при copy ({src.DType} vs {dst.DType}).");
        if (src.Device != dst.Device)
        {
            // Копируем через CPU-промежуток.
            var srcCpu = src.Device.Type == DeviceType.Cpu ? src.Contiguous() : src.ToCpu();
            if (dst.Device.Type == DeviceType.Cpu)
            {
                CopyContiguous(srcCpu, dst);
                return;
            }
            // dst на GPU: используем IHostCopyable.
            if (dst.Storage is Storage.IHostCopyable hc)
            {
                hc.CopyFromHost(srcCpu.Storage, 0, dst.NumElements);
                return;
            }
            throw new NotSupportedException(
                $"CopyTensor: storage {dst.Storage.GetType().Name} не поддерживает host-copy.");
        }
        var srcSame = src.Contiguous();
        CopyContiguous(srcSame, dst);
    }

    private static void CopyContiguous(Tensor src, Tensor dst)
    {
        switch (src.DType)
        {
            case DType.Float32: src.AsReadOnlySpan<float>().CopyTo(dst.AsSpan<float>()); break;
            case DType.Float64: src.AsReadOnlySpan<double>().CopyTo(dst.AsSpan<double>()); break;
            case DType.Int32: src.AsReadOnlySpan<int>().CopyTo(dst.AsSpan<int>()); break;
            case DType.Int64: src.AsReadOnlySpan<long>().CopyTo(dst.AsSpan<long>()); break;
            case DType.Int16: src.AsReadOnlySpan<short>().CopyTo(dst.AsSpan<short>()); break;
            case DType.Int8: src.AsReadOnlySpan<sbyte>().CopyTo(dst.AsSpan<sbyte>()); break;
            case DType.UInt8: src.AsReadOnlySpan<byte>().CopyTo(dst.AsSpan<byte>()); break;
            case DType.Bool: src.AsReadOnlySpan<byte>().CopyTo(dst.AsSpan<byte>()); break;
            default:
                throw new NotSupportedException(
                    $"CopyTensor для {src.DType} ещё не реализован.");
        }
    }

    #endregion State dict

    #region Применение и обход

    /// <summary>
    /// Применить функцию к каждому параметру (in-place transform).
    /// Полезно для инициализации, к которой потом легко применить нюансы.
    /// </summary>
    public Module Apply(Action<Parameter> fn)
    {
        foreach (var p in Parameters()) fn(p);
        return this;
    }

    /// <summary>
    /// Обнулить .Grad всех параметров (рекурсивно). Аналог <c>optimizer.zero_grad()</c>.
    /// </summary>
    public void ZeroGrad()
    {
        foreach (var p in Parameters())
            p.Tensor.ZeroGrad();
    }

    /// <summary>Подсчёт общего числа обучаемых параметров.</summary>
    public long NumParameters() => Parameters().Sum(p => p.Tensor.NumElements);

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(GetType().Name).Append('(');
        bool first = true;
        foreach (var (n, c) in _modules)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(n).Append('=').Append(c.GetType().Name);
        }
        sb.Append(')');
        return sb.ToString();
    }
    #endregion Применение и обход

}