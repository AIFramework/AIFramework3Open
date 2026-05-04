using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Data;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

namespace AI.ML.NeuralNetworks.V2.Train;

/// <summary>
/// Универсальный Trainer 2.0. Управляет циклом обучения с поддержкой:
/// gradient accumulation, gradient clipping, EMA, hooks, метрик и LR-шедулера.
/// </summary>
/// <remarks>
/// <para>
/// Минимально-связный API: пользователь предоставляет
/// <see cref="ITrainStep{TBatch}"/>, который описывает forward-backward для одного батча,
/// а Trainer оркеструет всё остальное.
/// </para>
/// </remarks>
public sealed class Trainer<TBatch>
{
    /// <summary>Модуль (для вызова Train/Eval и параметров).</summary>
    public Module Model { get; }
    /// <summary>Оптимизатор.</summary>
    public Optimizer Optimizer { get; }
    /// <summary>Шедулер (опционально).</summary>
    public LRScheduler Scheduler { get; set; }
    /// <summary>Шаг тренировки.</summary>
    public ITrainStep<TBatch> StepFn { get; }

    /// <summary>Число микро-батчей до optimizer.Step (1 = без аккумуляции).</summary>
    public int GradAccumSteps { get; set; } = 1;
    /// <summary>Если &gt; 0, применять clip_grad_norm с этим значением.</summary>
    public float MaxGradNorm { get; set; } = 0f;
    /// <summary>Если не null — применять EMA параметров.</summary>
    public ParameterEMA EMA { get; set; }
    /// <summary>Hooks: вызываются на разных стадиях.</summary>
    public TrainerHooks Hooks { get; } = new();

    /// <summary>Создать тренер.</summary>
    public Trainer(Module model, Optimizer optimizer, ITrainStep<TBatch> stepFn)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        StepFn = stepFn ?? throw new ArgumentNullException(nameof(stepFn));
    }

    /// <summary>Один проход по DataLoader (одна эпоха).</summary>
    public TrainEpochResult TrainEpoch(IEnumerable<TBatch> dataLoader)
    {
        Model.Train();
        var result = new TrainEpochResult();
        Hooks.OnEpochBegin?.Invoke(result);

        int microStep = 0;
        Optimizer.ZeroGrad();
        foreach (var batch in dataLoader)
        {
            Hooks.OnBatchBegin?.Invoke(result);
            float loss = StepFn.Step(Model, batch);
            // Loss усредняется по grad-accum шагам.
            // Caller обычно умножает loss на 1/GradAccumSteps в StepFn.
            result.TotalLoss += loss;
            result.NumBatches++;
            microStep++;

            if (microStep >= GradAccumSteps)
            {
                if (MaxGradNorm > 0f)
                    result.LastGradNorm = GradUtils.ClipGradNorm(Model.Parameters(), MaxGradNorm);
                Hooks.OnBeforeOptimizerStep?.Invoke(result);
                Optimizer.Step();
                EMA?.Update();
                Optimizer.ZeroGrad();
                microStep = 0;
                result.OptimizerSteps++;
            }
            Hooks.OnBatchEnd?.Invoke(result);
        }

        // Если что-то осталось не списано — финальный шаг (опционально).
        if (microStep > 0)
        {
            if (MaxGradNorm > 0f)
                result.LastGradNorm = GradUtils.ClipGradNorm(Model.Parameters(), MaxGradNorm);
            Optimizer.Step();
            EMA?.Update();
            Optimizer.ZeroGrad();
            result.OptimizerSteps++;
        }

        Scheduler?.Step();
        result.AverageLoss = result.NumBatches > 0 ? result.TotalLoss / result.NumBatches : 0f;
        Hooks.OnEpochEnd?.Invoke(result);
        return result;
    }

    /// <summary>Прогнать N эпох.</summary>
    public List<TrainEpochResult> Fit(IEnumerable<TBatch> dataLoader, int epochs)
    {
        var all = new List<TrainEpochResult>(epochs);
        for (int e = 0; e < epochs; e++) all.Add(TrainEpoch(dataLoader));
        return all;
    }
}

/// <summary>
/// Шаг обучения для конкретного типа батча. Реализует forward + loss + backward.
/// </summary>
public interface ITrainStep<TBatch>
{
    /// <summary>Выполнить forward + backward по батчу; вернуть loss-значение.</summary>
    float Step(Module model, TBatch batch);
}

/// <summary>Лямбда-обёртка для <see cref="ITrainStep{TBatch}"/>.</summary>
public sealed class LambdaTrainStep<TBatch> : ITrainStep<TBatch>
{
    private readonly Func<Module, TBatch, float> _step;
    /// <summary>Создать.</summary>
    public LambdaTrainStep(Func<Module, TBatch, float> step) { _step = step; }
    /// <inheritdoc/>
    public float Step(Module model, TBatch batch) => _step(model, batch);
}

/// <summary>
/// Накопленный результат одной эпохи.
/// </summary>
public sealed class TrainEpochResult
{
    /// <summary>Сумма loss по всем батчам.</summary>
    public float TotalLoss { get; set; }
    /// <summary>Среднее loss.</summary>
    public float AverageLoss { get; set; }
    /// <summary>Число обработанных батчей.</summary>
    public int NumBatches { get; set; }
    /// <summary>Сколько раз вызвался Optimizer.Step.</summary>
    public int OptimizerSteps { get; set; }
    /// <summary>Последняя grad-норма перед clip (0 если clip не применялся).</summary>
    public float LastGradNorm { get; set; }
    /// <summary>Произвольные пользовательские метрики.</summary>
    public Dictionary<string, float> Metrics { get; } = new();
}

/// <summary>Hooks-точки расширения тренера.</summary>
public sealed class TrainerHooks
{
    /// <summary>Перед эпохой.</summary>
    public Action<TrainEpochResult> OnEpochBegin { get; set; }
    /// <summary>После эпохи.</summary>
    public Action<TrainEpochResult> OnEpochEnd { get; set; }
    /// <summary>Перед батчем.</summary>
    public Action<TrainEpochResult> OnBatchBegin { get; set; }
    /// <summary>После батча.</summary>
    public Action<TrainEpochResult> OnBatchEnd { get; set; }
    /// <summary>Перед optimizer.Step.</summary>
    public Action<TrainEpochResult> OnBeforeOptimizerStep { get; set; }
}
