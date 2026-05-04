using System;
using System.Collections.Generic;
using System.Linq;
using AI.ML.NeuralNetworks.V2.Nn;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// Базовый класс оптимизаторов. Аналог <c>torch.optim.Optimizer</c>.
/// </summary>
/// <remarks>
/// <para>
/// Хранит группу параметров и per-parameter state. Реализация
/// <see cref="Step"/> объявляется в наследнике; общая инфраструктура
/// (zero_grad, learning rate, веса) — здесь.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> Step не безопасен из нескольких потоков
/// над одним параметром; внешний цикл обучения должен синхронизировать
/// optimizer.Step() и обратное распространение. State хранится по ссылке
/// на <see cref="Parameter"/> (через RuntimeHelpers).
/// </para>
/// </remarks>
public abstract class Optimizer
{
    /// <summary>Параметры, которыми управляет оптимизатор.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>Текущая глобальная learning rate.</summary>
    public float LearningRate { get; set; }

    /// <summary>Число выполненных шагов (для bias-correction в Adam-style).</summary>
    public int StepCount { get; protected set; }

    /// <summary>Per-parameter state-словарь (lazy).</summary>
    protected readonly Dictionary<Parameter, Dictionary<string, Tensor>> State = new();

    /// <summary>Создать оптимизатор для указанных параметров.</summary>
    protected Optimizer(IEnumerable<Parameter> parameters, float lr)
    {
        Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
        if (Parameters.Count == 0)
            throw new ArgumentException("Список параметров пуст.");
        if (lr <= 0) throw new ArgumentOutOfRangeException(nameof(lr));
        LearningRate = lr;
    }

    /// <summary>Сделать один шаг оптимизатора.</summary>
    public abstract void Step();

    /// <summary>Обнулить градиенты всех параметров.</summary>
    public void ZeroGrad()
    {
        foreach (var p in Parameters) p.Tensor.ZeroGrad();
    }

    /// <summary>Получить или создать state-тензор для параметра под именем <paramref name="key"/>.</summary>
    protected Tensor GetOrCreateState(Parameter p, string key, Func<Tensor> factory)
    {
        if (!State.TryGetValue(p, out var dict))
        {
            dict = new Dictionary<string, Tensor>();
            State[p] = dict;
        }
        if (!dict.TryGetValue(key, out var t))
        {
            t = factory();
            dict[key] = t;
        }
        return t;
    }
}

/// <summary>
/// Утилита: ожидание значения градиента для параметра. Если grad ещё не накоплен
/// (например, параметр не участвовал в forward), возвращает null.
/// </summary>
internal static class GradHelpers
{
    public static bool TryGetGrad(Parameter p, out Tensor grad)
    {
        grad = p.Tensor.Grad;
        return grad != null;
    }
}
