using System;

namespace AI.ML.NeuralNetworks.Gpu.CudaGraphs;

/// <summary>
/// Отслеживает «форму» тренировочного шага (число backward-узлов, размер батча и
/// длину последовательности) и сообщает, стабилизирована ли она N шагов подряд.
/// </summary>
/// <remarks>
/// <para>
/// Изначально класс назывался <c>TrainStepRecorder</c> и подразумевался как стартовая
/// инфраструктура для CUDA Graph capture. В ILGPU 1.5.x настоящего capture/replay нет,
/// поэтому он переименован в <see cref="StepShapeMonitor"/> — слово «graph» вводило в
/// заблуждение. Старое имя сохранено как <c>obsolete</c>-алиас для обратной совместимости.
/// </para>
/// <para>
/// <b>Потокобезопасность:</b> все обращения к внутреннему состоянию защищены
/// <c>lock</c>; <see cref="RecordStep"/> может вызываться из любого потока.
/// </para>
/// </remarks>
public sealed class StepShapeMonitor
{
    private readonly object _gate = new();
    private int _lastBackpropCount;
    private int _lastBatchSize;
    private int _lastSeqLen;
    private int _warmSteps;
    private bool _isWarm;

    /// <summary>
    /// true когда форма шага стабильна N шагов подряд (по умолчанию N=3).
    /// </summary>
    public bool IsWarm
    {
        get { lock (_gate) return _isWarm; }
    }

    /// <summary>Число подряд идущих шагов с одинаковой формой.</summary>
    public int WarmSteps
    {
        get { lock (_gate) return _warmSteps; }
    }

    /// <summary>
    /// Зафиксировать конец тренировочного шага. Вызывать после backward+optimizer.
    /// </summary>
    public void RecordStep(int backpropCount, int batchSize, int seqLen)
    {
        lock (_gate)
        {
            if (backpropCount == _lastBackpropCount && batchSize == _lastBatchSize && seqLen == _lastSeqLen)
            {
                _warmSteps++;
                if (_warmSteps >= 3) _isWarm = true;
            }
            else
            {
                _warmSteps = 0;
                _isWarm = false;
            }
            _lastBackpropCount = backpropCount;
            _lastBatchSize = batchSize;
            _lastSeqLen = seqLen;
        }
    }

    /// <summary>Сбросить состояние (например, при смене эпохи / размера батча).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _lastBackpropCount = 0;
            _lastBatchSize = 0;
            _lastSeqLen = 0;
            _warmSteps = 0;
            _isWarm = false;
        }
    }
}

/// <summary>
/// Старое имя класса; оставлено для обратной совместимости.
/// </summary>
[Obsolete("Переименовано в StepShapeMonitor: класс не делает CUDA Graph capture, а лишь отслеживает форму шага.", error: false)]
public sealed class TrainStepRecorder
{
    private readonly StepShapeMonitor _impl = new();
    /// <inheritdoc cref="StepShapeMonitor.IsWarm"/>
    public bool IsWarm => _impl.IsWarm;
    /// <inheritdoc cref="StepShapeMonitor.WarmSteps"/>
    public int WarmSteps => _impl.WarmSteps;
    /// <inheritdoc cref="StepShapeMonitor.RecordStep"/>
    public void RecordStep(int backpropCount, int batchSize, int seqLen)
        => _impl.RecordStep(backpropCount, batchSize, seqLen);
    /// <inheritdoc cref="StepShapeMonitor.Reset"/>
    public void Reset() => _impl.Reset();
}
