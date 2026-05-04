using System;
using System.Collections;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Последовательный контейнер: applies modules один за другим.
/// </summary>
/// <remarks>
/// Аналог <c>torch.nn.Sequential</c>. Имена дочерних модулей — порядковые номера ("0","1",...).
/// </remarks>
public sealed class Sequential : Module, IEnumerable<Module>
{
    private readonly List<Module> _layers = new();

    /// <summary>Создать пустой Sequential (потом Add).</summary>
    public Sequential() { }

    /// <summary>Создать Sequential из набора модулей.</summary>
    public Sequential(params Module[] layers)
    {
        if (layers == null) throw new ArgumentNullException(nameof(layers));
        for (int i = 0; i < layers.Length; i++) Add(layers[i]);
    }

    /// <summary>Добавить модуль; возвращает self для chaining.</summary>
    public Sequential Add(Module module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        int idx = _layers.Count;
        _layers.Add(RegisterModule(idx.ToString(), module));
        return this;
    }

    /// <summary>Число модулей.</summary>
    public int Count => _layers.Count;

    /// <summary>Доступ к модулю по индексу.</summary>
    public Module this[int index] => _layers[index];

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        var x = input;
        for (int i = 0; i < _layers.Count; i++)
            x = _layers[i].Forward(x);
        return x;
    }

    /// <inheritdoc/>
    public IEnumerator<Module> GetEnumerator() => _layers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Список модулей с индексным доступом, без неявного forward (как PyTorch ModuleList).
/// </summary>
public sealed class ModuleList : Module, IEnumerable<Module>
{
    private readonly List<Module> _modules = new();

    /// <summary>Создать пустой ModuleList.</summary>
    public ModuleList() { }

    /// <summary>Создать ModuleList из набора модулей.</summary>
    public ModuleList(params Module[] modules)
    {
        if (modules == null) throw new ArgumentNullException(nameof(modules));
        for (int i = 0; i < modules.Length; i++) Add(modules[i]);
    }

    /// <summary>Добавить модуль (автоматически регистрируется по индексу).</summary>
    public ModuleList Add(Module module)
    {
        int idx = _modules.Count;
        _modules.Add(RegisterModule(idx.ToString(), module));
        return this;
    }

    /// <summary>Число модулей.</summary>
    public int Count => _modules.Count;

    /// <summary>Доступ по индексу.</summary>
    public Module this[int index] => _modules[index];

    /// <inheritdoc/>
    public IEnumerator<Module> GetEnumerator() => _modules.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// ModuleList не имеет default forward — это контейнер. Вызовите Forward
    /// нужного элемента вручную в родительском модуле.
    /// </summary>
    public override Tensor Forward(Tensor input) =>
        throw new NotSupportedException(
            "ModuleList — контейнер без forward; используйте индексирование в родительском Forward.");
}

/// <summary>
/// Словарь модулей по имени (как PyTorch ModuleDict).
/// </summary>
public sealed class ModuleDict : Module
{
    private readonly Dictionary<string, Module> _modules = new();

    /// <summary>Добавить модуль под именем <paramref name="name"/>.</summary>
    public ModuleDict Add(string name, Module module)
    {
        _modules[name] = RegisterModule(name, module);
        return this;
    }

    /// <summary>Доступ по имени.</summary>
    public Module this[string name] => _modules[name];

    /// <summary>Имена модулей.</summary>
    public IEnumerable<string> Keys => _modules.Keys;

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) =>
        throw new NotSupportedException("ModuleDict — контейнер без forward.");
}
