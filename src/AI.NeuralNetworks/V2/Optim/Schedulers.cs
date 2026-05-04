using System;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Optim;

/// <summary>
/// Базовый класс LR-шедулера. Аналог <c>torch.optim.lr_scheduler._LRScheduler</c>.
/// </summary>
/// <remarks>
/// <para>
/// Шедулер запоминает базовую LR при создании и переписывает
/// <see cref="Optimizer.LearningRate"/> на <see cref="Step"/>.
/// </para>
/// </remarks>
public abstract class LRScheduler
{
    /// <summary>Управляемый оптимизатор.</summary>
    public Optimizer Optimizer { get; }
    /// <summary>Базовая LR (заморожена при создании).</summary>
    public float BaseLR { get; }
    /// <summary>Текущая эпоха / число вызовов Step.</summary>
    public int LastEpoch { get; protected set; } = -1;

    /// <summary>Создать шедулер.</summary>
    protected LRScheduler(Optimizer optimizer)
    {
        Optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        BaseLR = optimizer.LearningRate;
    }

    /// <summary>Сделать шаг шедулера: вычислить новую LR и записать в оптимизатор.</summary>
    public virtual void Step()
    {
        LastEpoch++;
        Optimizer.LearningRate = ComputeLR();
    }

    /// <summary>Вычислить LR для текущего <see cref="LastEpoch"/>.</summary>
    protected abstract float ComputeLR();

    /// <summary>Текущая LR.</summary>
    public float CurrentLR => Optimizer.LearningRate;
}

/// <summary>StepLR: lr * γ^(epoch/stepSize).</summary>
public sealed class StepLR : LRScheduler
{
    /// <summary>Шаг (в эпохах).</summary>
    public int StepSize { get; }
    /// <summary>γ.</summary>
    public float Gamma { get; }
    /// <summary>Создать.</summary>
    public StepLR(Optimizer opt, int stepSize, float gamma = 0.1f) : base(opt)
    { StepSize = stepSize; Gamma = gamma; }
    /// <inheritdoc/>
    protected override float ComputeLR() =>
        BaseLR * MathF.Pow(Gamma, LastEpoch / StepSize);
}

/// <summary>MultiStepLR: lr * γ^(num milestones reached).</summary>
public sealed class MultiStepLR : LRScheduler
{
    /// <summary>Эпохи-вехи.</summary>
    public IReadOnlyList<int> Milestones { get; }
    /// <summary>γ.</summary>
    public float Gamma { get; }
    /// <summary>Создать.</summary>
    public MultiStepLR(Optimizer opt, int[] milestones, float gamma = 0.1f) : base(opt)
    { Milestones = milestones; Gamma = gamma; }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        int count = 0;
        for (int i = 0; i < Milestones.Count; i++) if (LastEpoch >= Milestones[i]) count++;
        return BaseLR * MathF.Pow(Gamma, count);
    }
}

/// <summary>ExponentialLR: lr * γ^epoch.</summary>
public sealed class ExponentialLR : LRScheduler
{
    /// <summary>γ.</summary>
    public float Gamma { get; }
    /// <summary>Создать.</summary>
    public ExponentialLR(Optimizer opt, float gamma) : base(opt) { Gamma = gamma; }
    /// <inheritdoc/>
    protected override float ComputeLR() => BaseLR * MathF.Pow(Gamma, LastEpoch);
}

/// <summary>Cosine annealing LR (Loshchilov &amp; Hutter).</summary>
public sealed class CosineAnnealingLR : LRScheduler
{
    /// <summary>Tmax — длина одного цикла косинуса.</summary>
    public int TMax { get; }
    /// <summary>Минимальная LR.</summary>
    public float EtaMin { get; }
    /// <summary>Создать.</summary>
    public CosineAnnealingLR(Optimizer opt, int tMax, float etaMin = 0f) : base(opt)
    { TMax = tMax; EtaMin = etaMin; }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        if (LastEpoch <= 0) return BaseLR;
        float t = MathF.Min(LastEpoch, TMax);
        return EtaMin + 0.5f * (BaseLR - EtaMin) * (1f + MathF.Cos(MathF.PI * t / TMax));
    }
}

/// <summary>Cosine annealing with warm restarts (SGDR).</summary>
public sealed class CosineAnnealingWarmRestarts : LRScheduler
{
    /// <summary>Длина первого цикла.</summary>
    public int T0 { get; }
    /// <summary>Множитель длины следующего цикла.</summary>
    public int TMult { get; }
    /// <summary>Минимальная LR.</summary>
    public float EtaMin { get; }
    /// <summary>Создать.</summary>
    public CosineAnnealingWarmRestarts(Optimizer opt, int t0, int tMult = 1, float etaMin = 0f)
        : base(opt) { T0 = t0; TMult = tMult; EtaMin = etaMin; }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        // Найдём текущий цикл и фазу.
        int epoch = LastEpoch;
        int t = T0;
        int cumStart = 0;
        while (epoch >= cumStart + t)
        {
            cumStart += t;
            t *= TMult;
            if (t == 0) t = T0;
        }
        int phase = epoch - cumStart;
        return EtaMin + 0.5f * (BaseLR - EtaMin) * (1f + MathF.Cos(MathF.PI * phase / t));
    }
}

/// <summary>One Cycle Policy (Smith).</summary>
public sealed class OneCycleLR : LRScheduler
{
    private readonly int _totalSteps;
    private readonly float _maxLR;
    private readonly float _initLR;
    private readonly float _minLR;
    private readonly float _pctStart;
    /// <summary>Создать.</summary>
    public OneCycleLR(Optimizer opt, float maxLR, int totalSteps,
        float pctStart = 0.3f, float divFactor = 25f, float finalDivFactor = 1e4f)
        : base(opt)
    {
        if (totalSteps <= 0) throw new ArgumentOutOfRangeException(nameof(totalSteps));
        _maxLR = maxLR;
        _initLR = maxLR / divFactor;
        _minLR = _initLR / finalDivFactor;
        _pctStart = pctStart;
        _totalSteps = totalSteps;
    }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        if (LastEpoch <= 0) return _initLR;
        int peakStep = (int)(_pctStart * _totalSteps);
        if (LastEpoch <= peakStep)
        {
            // Линейный warmup до maxLR.
            float pct = (float)LastEpoch / peakStep;
            return _initLR + (_maxLR - _initLR) * pct;
        }
        // Косинусный спад до minLR.
        float pctDown = (float)(LastEpoch - peakStep) / (_totalSteps - peakStep);
        pctDown = MathF.Min(pctDown, 1f);
        return _minLR + 0.5f * (_maxLR - _minLR) * (1f + MathF.Cos(MathF.PI * pctDown));
    }
}

/// <summary>Linear warmup до peakLR за N шагов, потом удержание.</summary>
public sealed class LinearWarmupLR : LRScheduler
{
    private readonly int _warmupSteps;
    private readonly float _peakLR;
    /// <summary>Создать.</summary>
    public LinearWarmupLR(Optimizer opt, int warmupSteps, float peakLR) : base(opt)
    { _warmupSteps = warmupSteps; _peakLR = peakLR; }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        if (LastEpoch >= _warmupSteps) return _peakLR;
        float pct = (LastEpoch + 1f) / _warmupSteps;
        return BaseLR + (_peakLR - BaseLR) * pct;
    }
}

/// <summary>Линейный спад LR.</summary>
public sealed class LinearLR : LRScheduler
{
    private readonly float _startFactor;
    private readonly float _endFactor;
    private readonly int _totalIters;
    /// <summary>Создать.</summary>
    public LinearLR(Optimizer opt, float startFactor = 1f / 3f, float endFactor = 1f, int totalIters = 5)
        : base(opt) { _startFactor = startFactor; _endFactor = endFactor; _totalIters = totalIters; }
    /// <inheritdoc/>
    protected override float ComputeLR()
    {
        if (LastEpoch < 0) return BaseLR;
        if (LastEpoch >= _totalIters) return BaseLR * _endFactor;
        float t = (float)LastEpoch / _totalIters;
        return BaseLR * (_startFactor + t * (_endFactor - _startFactor));
    }
}

/// <summary>Lambda LR: пользователь сам задаёт функцию epoch -> multiplier.</summary>
public sealed class LambdaLR : LRScheduler
{
    private readonly Func<int, float> _lr;
    /// <summary>Создать.</summary>
    public LambdaLR(Optimizer opt, Func<int, float> lrLambda) : base(opt)
    { _lr = lrLambda ?? throw new ArgumentNullException(nameof(lrLambda)); }
    /// <inheritdoc/>
    protected override float ComputeLR() => BaseLR * _lr(LastEpoch);
}

/// <summary>
/// SequentialLR: переключается между шедулерами по milestones.
/// </summary>
/// <remarks>
/// <para>
/// Семантика: на каждом <see cref="Step"/> делается ровно один <c>Step()</c>
/// активного подшедулера. Активный подшедулер определяется по
/// <see cref="LRScheduler.LastEpoch"/>: подшедулер с индексом 0 работает на эпохах
/// <c>[0, milestones[0])</c>, индексом <c>i</c> — на <c>[milestones[i-1], milestones[i])</c>
/// и т.д.
/// </para>
/// <para>
/// При первом активировании каждого подшедулера его собственный счётчик
/// <see cref="LRScheduler.LastEpoch"/> «накручивается» до позиции в его фазе,
/// чтобы он стартовал свой график с нуля (PyTorch-совместимая семантика). Это
/// предполагает, что сами подшедулеры передаются в SequentialLR в свежем виде
/// (с <c>LastEpoch = -1</c>).
/// </para>
/// </remarks>
public sealed class SequentialLR : LRScheduler
{
    private readonly LRScheduler[] _schedulers;
    private readonly int[] _milestones;
    private readonly int[] _localStartEpoch;
    private int _activeIdx = -1;
    /// <summary>Создать.</summary>
    public SequentialLR(Optimizer opt, LRScheduler[] schedulers, int[] milestones) : base(opt)
    {
        if (schedulers == null) throw new ArgumentNullException(nameof(schedulers));
        if (milestones == null) throw new ArgumentNullException(nameof(milestones));
        if (schedulers.Length != milestones.Length + 1)
            throw new ArgumentException("schedulers.Length должно быть milestones.Length + 1.");
        for (int i = 1; i < milestones.Length; i++)
            if (milestones[i] <= milestones[i - 1])
                throw new ArgumentException("milestones должны строго возрастать.");
        _schedulers = schedulers; _milestones = milestones;
        _localStartEpoch = new int[schedulers.Length];
        _localStartEpoch[0] = 0;
        for (int i = 0; i < milestones.Length; i++)
            _localStartEpoch[i + 1] = milestones[i];
    }
    /// <inheritdoc/>
    public override void Step()
    {
        LastEpoch++;
        int desiredIdx = 0;
        for (int i = 0; i < _milestones.Length; i++)
            if (LastEpoch >= _milestones[i]) desiredIdx = i + 1;

        if (desiredIdx != _activeIdx)
        {
            // Активируем подшедулер: «подмотать» его внутренний счётчик до
            // позиции в фазе, чтобы он стартовал с нуля и сам устанавливал LR
            // согласно своему графику.
            var sub = _schedulers[desiredIdx];
            int stepsIntoPhase = LastEpoch - _localStartEpoch[desiredIdx];
            for (int k = 0; k <= stepsIntoPhase; k++) sub.Step();
            _activeIdx = desiredIdx;
        }
        else
        {
            _schedulers[_activeIdx].Step();
        }
    }
    /// <inheritdoc/>
    protected override float ComputeLR() => Optimizer.LearningRate;
}

/// <summary>
/// ReduceLROnPlateau: уменьшает LR, если метрика не улучшается N эпох.
/// </summary>
public sealed class ReduceLROnPlateau
{
    /// <summary>Управляемый оптимизатор.</summary>
    public Optimizer Optimizer { get; }
    /// <summary>Множитель уменьшения.</summary>
    public float Factor { get; }
    /// <summary>Сколько эпох ждать.</summary>
    public int Patience { get; }
    /// <summary>Минимальная LR.</summary>
    public float MinLR { get; }
    /// <summary>Режим: минимизация или максимизация.</summary>
    public bool Minimize { get; }
    /// <summary>Tolerance для «улучшения».</summary>
    public float Threshold { get; }

    private float _best;
    private int _badEpochs;

    /// <summary>Создать.</summary>
    public ReduceLROnPlateau(Optimizer optimizer, float factor = 0.1f, int patience = 10,
        float threshold = 1e-4f, float minLR = 0f, bool minimize = true)
    {
        Optimizer = optimizer; Factor = factor; Patience = patience;
        Threshold = threshold; MinLR = minLR; Minimize = minimize;
        _best = minimize ? float.PositiveInfinity : float.NegativeInfinity;
    }

    /// <summary>Шаг с метрикой; возвращает true, если LR была уменьшена.</summary>
    public bool Step(float metric)
    {
        bool improved = Minimize ? metric < _best - Threshold : metric > _best + Threshold;
        if (improved)
        {
            _best = metric;
            _badEpochs = 0;
            return false;
        }
        _badEpochs++;
        // Семантика PyTorch: уменьшаем LR, когда количество «плохих» эпох
        // достигло Patience (а не строго больше). Бывший `>` приводил к off-by-one
        // (срабатывал на (Patience+1)-й шаг), что искажало планирование.
        if (_badEpochs >= Patience)
        {
            float newLR = MathF.Max(MinLR, Optimizer.LearningRate * Factor);
            if (newLR < Optimizer.LearningRate)
            {
                Optimizer.LearningRate = newLR;
                _badEpochs = 0;
                return true;
            }
        }
        return false;
    }
}
