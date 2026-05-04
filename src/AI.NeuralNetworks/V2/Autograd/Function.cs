using System;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Autograd;

/// <summary>
/// Узел автоматического дифференцирования: захватывает входы и порождает один выход
/// с привязкой к этому узлу через <see cref="Tensor.GradFn"/>.
/// </summary>
/// <remarks>
/// <para>
/// Аналог <c>torch.autograd.Function</c>. Каждая операция, требующая backward,
/// порождает наследника Function, в котором:
/// <list type="bullet">
///   <item><see cref="SavedTensors"/> — входы/промежутки, нужные для backward.</item>
///   <item><see cref="NextFunctions"/> — ссылки на узлы, породившие input-тензоры
///         (или null для leaf'ов с <c>requires_grad=true</c>).</item>
///   <item><see cref="Inputs"/> — сильные ссылки на input-тензоры (для записи gradient
///         в их <c>.Grad</c>, если они leaf и requires_grad). Память графа удерживается
///         до завершения backward; затем граф становится мусором GC.</item>
///   <item><see cref="Backward"/> — реализация: принимает grad выхода, возвращает grad
///         каждого входа (в том же порядке, что и Inputs).</item>
/// </list>
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> Function-узел не разделяется между потоками — каждый
/// поток строит свой граф через свою <see cref="TapeContext"/>.
/// </para>
/// </remarks>
public abstract class Function
{
    /// <summary>Тензоры, сохранённые на forward для использования в backward.</summary>
    public List<Tensor> SavedTensors { get; } = new();

    /// <summary>Скаляры/параметры, сохранённые на forward (произвольный type-erased багаж).</summary>
    public object SavedContext { get; protected set; }

    /// <summary>
    /// Узлы родителей в графе (в порядке Inputs). null для leaf-входов без grad_fn.
    /// </summary>
    public List<Function> NextFunctions { get; } = new();

    /// <summary>Входные тензоры (для записи .Grad в leaf-параметры).</summary>
    public List<Tensor> Inputs { get; } = new();

    /// <summary>
    /// Реализация обратного прохода. <paramref name="gradOutput"/> — грэд по выходу
    /// этой операции; возвращаемый массив — грэды по каждому из <see cref="Inputs"/>
    /// в том же порядке (null для входов без requires_grad).
    /// </summary>
    public abstract Tensor[] Backward(Tensor gradOutput);

    /// <summary>
    /// Записать вход в граф. Запоминает узел-родитель и сам тензор.
    /// Public — чтобы пользовательские/расширенные Function (в т.ч. в адаптерах,
    /// например GPU-backend) могли регистрировать свои входы.
    /// </summary>
    public void RegisterInput(Tensor t)
    {
        Inputs.Add(t);
        NextFunctions.Add(t?.GradFn);
    }
}
