using System;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Autograd;

/// <summary>
/// Движок обратного распространения: топологически сортирует Function-узлы
/// от output-тензора назад к leaf-входам и накапливает градиенты.
/// </summary>
/// <remarks>
/// <para>
/// Алгоритм:
/// <list type="number">
///   <item>Обходим граф в обратном порядке от <c>root</c>.GradFn,
///         считая, сколько «детей» (потребителей grad) у каждого узла.</item>
///   <item>Запускаем topological-sweep: узел готов, когда от всех детей пришли
///         накопленные градиенты.</item>
///   <item>Для leaf-входов с requires_grad записываем .Grad (с аккумуляцией).</item>
/// </list>
/// </para>
/// <para>
/// Аккумуляция градиентов идёт <b>in-place</b> в существующий accumulator-тензор,
/// без аллокации нового на каждом merge — это критично для широких графов с fan-in.
/// </para>
/// <para>
/// Поддерживаются все DType (через диспатч по типу элементов в <see cref="AddInplace"/>).
/// </para>
/// <para>
/// Потокобезопасность: метод вызывается на тензоре, граф которого был построен
/// тем же логическим потоком (через AsyncLocal-tape). Параллельные обратные
/// проходы для разных моделей — безопасны (не делят узлы).
/// </para>
/// </remarks>
public static class Engine
{
    /// <summary>
    /// Запустить backward от <paramref name="root"/> с начальным градиентом
    /// <paramref name="gradOutput"/>.
    /// </summary>
    public static void Run(Tensor root, Tensor gradOutput)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (gradOutput == null) throw new ArgumentNullException(nameof(gradOutput));
        if (!root.RequiresGrad && root.GradFn == null)
            throw new InvalidOperationException(
                "Backward вызван на тензоре без grad_fn и без requires_grad.");
        if (gradOutput.Shape != root.Shape)
            throw new ArgumentException(
                $"Engine.Run: gradOutput.Shape={gradOutput.Shape} != root.Shape={root.Shape}.",
                nameof(gradOutput));
        if (gradOutput.DType != root.DType)
            throw new ArgumentException(
                $"Engine.Run: gradOutput.DType={gradOutput.DType} != root.DType={root.DType}.",
                nameof(gradOutput));
        if (gradOutput.Device != root.Device)
            throw new ArgumentException(
                $"Engine.Run: gradOutput.Device={gradOutput.Device} != root.Device={root.Device}.",
                nameof(gradOutput));

        // Если root — leaf с requires_grad: просто записываем gradient.
        if (root.GradFn == null)
        {
            AccumulateGrad(root, gradOutput);
            return;
        }

        var deps = new Dictionary<Function, int>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Function>();
        stack.Push(root.GradFn);
        deps[root.GradFn] = 0;
        var visited = new HashSet<Function>(ReferenceEqualityComparer.Instance);
        visited.Add(root.GradFn);
        while (stack.Count > 0)
        {
            var f = stack.Pop();
            foreach (var next in f.NextFunctions)
            {
                if (next == null) continue;
                if (deps.TryGetValue(next, out int c)) deps[next] = c + 1;
                else deps[next] = 1;
                if (visited.Add(next)) stack.Push(next);
            }
        }

        var grads = new Dictionary<Function, Tensor>(ReferenceEqualityComparer.Instance)
        {
            [root.GradFn] = gradOutput
        };

        var ready = new Queue<Function>();
        ready.Enqueue(root.GradFn);

        while (ready.Count > 0)
        {
            var f = ready.Dequeue();
            var gOut = grads[f];
            grads.Remove(f);

            Tensor[] gradInputs = f.Backward(gOut);

            for (int i = 0; i < f.Inputs.Count; i++)
            {
                var input = f.Inputs[i];
                var nextFn = f.NextFunctions[i];
                var gi = gradInputs[i];
                if (gi == null) continue;

                if (nextFn == null)
                {
                    if (input != null && input.IsLeafRequiringGrad)
                        AccumulateGrad(input, gi);
                }
                else
                {
                    if (grads.TryGetValue(nextFn, out var existing))
                    {
                        // In-place: gi прибавляется к existing, который мы держим
                        // эксклюзивно в этом словаре.
                        var merged = AddIntoFirst(existing, gi);
                        if (!ReferenceEquals(merged, existing))
                            grads[nextFn] = merged;
                    }
                    else
                    {
                        grads[nextFn] = gi;
                    }

                    deps[nextFn]--;
                    if (deps[nextFn] == 0)
                        ready.Enqueue(nextFn);
                }
            }
        }
    }

    private static void AccumulateGrad(Tensor leaf, Tensor grad)
    {
        if (!leaf.Shape.Equals(grad.Shape))
            throw new InvalidOperationException(
                $"AccumulateGrad: shape leaf={leaf.Shape} vs grad={grad.Shape}.");
        if (leaf.Grad == null)
            leaf.Grad = Tensor.Zeros(leaf.Shape, leaf.DType, leaf.Device);
        AddInplace(leaf.Grad, grad);
    }

    /// <summary>
    /// Прибавить <paramref name="b"/> в <paramref name="a"/> in-place, если возможно;
    /// иначе вернуть новый аккумулятор того же DType. Возвращаемое значение —
    /// тензор, в котором лежит сумма (может совпадать с <paramref name="a"/>).
    /// </summary>
    private static Tensor AddIntoFirst(Tensor a, Tensor b)
    {
        if (!a.Shape.Equals(b.Shape))
            throw new InvalidOperationException(
                $"Aggregate-grad ожидает совпадение форм: {a.Shape} vs {b.Shape}.");
        if (a.DType != b.DType)
            throw new InvalidOperationException(
                $"Aggregate-grad ожидает совпадение DType: {a.DType} vs {b.DType}.");
        if (a.Device != b.Device)
            throw new InvalidOperationException(
                $"Aggregate-grad ожидает совпадение Device: {a.Device} vs {b.Device}.");
        // Если a контигуозен — складываем in-place. Иначе материализуем contiguous-копию.
        if (!a.IsContiguous)
            a = a.Contiguous();
        AddInplace(a, b);
        return a;
    }

    /// <summary>
    /// In-place: <paramref name="target"/> += <paramref name="add"/>. Диспатч по DType.
    /// </summary>
    private static void AddInplace(Tensor target, Tensor add)
    {
        if (!target.Shape.Equals(add.Shape))
            throw new InvalidOperationException(
                $"Add-inplace ожидает совпадение форм: {target.Shape} vs {add.Shape}.");
        if (target.DType != add.DType)
            throw new InvalidOperationException(
                $"Add-inplace ожидает совпадение DType: {target.DType} vs {add.DType}.");
        if (target.Device != add.Device)
            throw new InvalidOperationException(
                $"Add-inplace ожидает совпадение Device: {target.Device} vs {add.Device}.");

        // Для не-CPU — спускаемся в backend через TensorOps (если зарегистрирован in-place add)
        // либо делаем generic путь через ToCpu() + повторное копирование.
        if (target.Device.Type != DeviceType.Cpu)
        {
            // Универсальный путь: считаем sum через TensorOps и копируем результат обратно.
            // Не in-place, но корректен для всех backend'ов.
            var sum = Ops.TensorOps.Add(target, add);
            CopyTensorContents(sum, target);
            return;
        }

        var src = add.IsContiguous ? add : add.Contiguous();
        switch (target.DType)
        {
            case DType.Float32:
            {
                var t = target.AsSpan<float>();
                var a = src.AsReadOnlySpan<float>();
                for (int i = 0; i < t.Length; i++) t[i] += a[i];
                break;
            }
            case DType.Float64:
            {
                var t = target.AsSpan<double>();
                var a = src.AsReadOnlySpan<double>();
                for (int i = 0; i < t.Length; i++) t[i] += a[i];
                break;
            }
            case DType.Int32:
            {
                var t = target.AsSpan<int>();
                var a = src.AsReadOnlySpan<int>();
                for (int i = 0; i < t.Length; i++) t[i] += a[i];
                break;
            }
            case DType.Int64:
            {
                var t = target.AsSpan<long>();
                var a = src.AsReadOnlySpan<long>();
                for (int i = 0; i < t.Length; i++) t[i] += a[i];
                break;
            }
            default:
                throw new NotSupportedException(
                    $"Engine.AddInplace: DType {target.DType} пока не поддерживается.");
        }
    }

    private static void CopyTensorContents(Tensor src, Tensor dst)
    {
        if (src.Device.Type != DeviceType.Cpu || dst.Device.Type != DeviceType.Cpu)
        {
            // Через CPU: единственный универсальный путь, который мы можем гарантировать
            // на уровне engine. Вызывающие модули могут предоставить более быстрый.
            var srcCpu = src.Device.Type == DeviceType.Cpu ? src : src.ToCpu();
            var dstStaging = Tensor.Empty(dst.Shape, dst.DType, Device.Cpu);
            CopyContiguousCpu(srcCpu.Contiguous(), dstStaging);
            if (dst.Storage is Storage.IHostCopyable hc)
            {
                // ВАЖНО: dst может быть contiguous-view с _offset > 0 (типичный случай —
                // accumulator, попавший в Engine.AddIntoFirst как Narrow(gradOut, 0, ...)
                // .Contiguous() из CatFunction.Backward). Без учёта Offset мы бы записывали
                // в OFFSET 0 хранилища, перетирая чужой срез. Это давало неверные градиенты
                // в RNN/LSTM/GRU при T≥3, когда промежуточный hidden h_t имел два пути:
                // через Stack и через рекуррентную h.MatMul.
                hc.CopyFromHost(dstStaging.Storage, dst.Offset, dst.NumElements);
            }
            else
                throw new NotSupportedException(
                    $"CopyTensorContents: storage {dst.Storage.GetType().Name} не поддерживает host-copy.");
            return;
        }
        // CPU-путь: AsSpan<T>() уже учитывает _offset, поэтому отдельная коррекция
        // здесь не нужна.
        CopyContiguousCpu(src.Contiguous(), dst);
    }

    private static void CopyContiguousCpu(Tensor src, Tensor dst)
    {
        switch (src.DType)
        {
            case DType.Float32: src.AsReadOnlySpan<float>().CopyTo(dst.AsSpan<float>()); break;
            case DType.Float64: src.AsReadOnlySpan<double>().CopyTo(dst.AsSpan<double>()); break;
            case DType.Int32: src.AsReadOnlySpan<int>().CopyTo(dst.AsSpan<int>()); break;
            case DType.Int64: src.AsReadOnlySpan<long>().CopyTo(dst.AsSpan<long>()); break;
            default: throw new NotSupportedException(
                $"CopyContiguousCpu: DType {src.DType} не поддерживается.");
        }
    }
}
