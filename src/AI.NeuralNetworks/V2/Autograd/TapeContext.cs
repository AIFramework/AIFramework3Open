using System;
using System.Threading;

namespace AI.ML.NeuralNetworks.V2.Autograd;

/// <summary>
/// Per-thread контекст autograd-tape: контролирует, нужно ли записывать
/// градиентные узлы при выполнении операций.
/// </summary>
/// <remarks>
/// <para>
/// Аналог <c>torch.no_grad()</c>: внутри блока <c>using (TapeContext.NoGrad())</c>
/// все операции выполняются без построения графа — это даёт скорость и экономит
/// память при инференсе.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> хранится в <see cref="AsyncLocal{T}"/>, что даёт
/// каждому потоку (или async-потоку) свой независимый стек состояний.
/// </para>
/// </remarks>
public static class TapeContext
{
    private static readonly AsyncLocal<bool> _gradEnabled = new();

    /// <summary>True, если autograd активен на текущем потоке (по умолчанию — да).</summary>
    public static bool IsGradEnabled => !_gradEnabled.Value; // инвертируем: значение по умолчанию false=enabled

    /// <summary>
    /// Выключить autograd на время блока. Возвращает scope-disposable.
    /// </summary>
    public static IDisposable NoGrad()
    {
        bool prev = _gradEnabled.Value;
        _gradEnabled.Value = true; // true = disabled
        return new Scope(() => _gradEnabled.Value = prev);
    }

    /// <summary>
    /// Принудительно включить autograd (полезно во вложенных блоках).
    /// </summary>
    public static IDisposable EnableGrad()
    {
        bool prev = _gradEnabled.Value;
        _gradEnabled.Value = false; // false = enabled
        return new Scope(() => _gradEnabled.Value = prev);
    }

    private sealed class Scope : IDisposable
    {
        private Action _onDispose;
        public Scope(Action onDispose) { _onDispose = onDispose; }
        public void Dispose()
        {
            var a = Interlocked.Exchange(ref _onDispose, null);
            a?.Invoke();
        }
    }
}
