using AI.DataStructs.Algebraic;
using AI.Fuzzy.Inference;
using System;
using System.Collections.Generic;

namespace AI.Fuzzy.Control;

/// <summary>
/// Режим агрегирования выхода нечёткого регулятора.
/// </summary>
public enum FuzzyPIDOutputMode
{
    /// <summary>Сугено (синглтоны): взвешенное среднее по правилам.</summary>
    Sugeno,
    /// <summary>Мамдани: треугольные следствия на сетке, дефаззификация — центр тяжести.</summary>
    Mamdani
}

/// <summary>
/// Нечёткий регулятор в духе PID: входы — ошибка, её производная и интеграл (все масштабируются в [-1, 1]),
/// база правил 3×3×3 с тройкой термов (N, Z, P). Управление формируется только нечётким выводом (Сугено или Мамдани).
/// </summary>
[Serializable]
public sealed class FuzzyPIDController
{
    /// <summary>Масштаб ошибки e = setpoint − process перед нормализацией в [-1, 1].</summary>
    public double Ke { get; set; } = 1;

    /// <summary>Масштаб производной ошибки.</summary>
    public double Kde { get; set; } = 1;

    /// <summary>Масштаб интеграла ошибки.</summary>
    public double Kie { get; set; } = 1;

    /// <summary>Усиление на выходе нечёткого блока.</summary>
    public double OutputGain { get; set; } = 1;

    /// <summary>Ограничение |интеграла ошибки| (анти wind-up по входу в фаззификатор).</summary>
    public double IntegralLimit { get; set; } = 1e6;

    /// <summary>Сугено или Мамдани для встроенной таблицы следствий.</summary>
    public FuzzyPIDOutputMode Mode { get; set; } = FuzzyPIDOutputMode.Sugeno;

    /// <summary>Число точек универсума выхода для Мамдани.</summary>
    public int MamdaniOutputSamples { get; set; } = 64;

    /// <summary>Полуширина треугольного следствия Мамдани вокруг синглтона Сугено.</summary>
    public double MamdaniConsequentHalfWidth { get; set; } = 0.25;

    /// <summary>
    /// Если true — выход накапливается (приращение управления); если false — выход регулятора равен результату нечёткого вывода (прямое отображение «PID-подобного» закона).
    /// </summary>
    public bool AccumulateOutput { get; set; }

    private double _prevError;
    private double _integral;
    private double _accumulatedControl;

    /// <summary>Ошибка на предыдущем шаге (после последнего вычисления).</summary>
    public double PreviousError => _prevError;

    /// <summary>Накопленный интеграл ошибки.</summary>
    public double Integral => _integral;

    /// <summary>Накопленное управление при <see cref="AccumulateOutput"/> == true.</summary>
    public double AccumulatedControl => _accumulatedControl;

    /// <summary>Сброс состояния интеграла и накопленного выхода.</summary>
    public void Reset()
    {
        _prevError = 0;
        _integral = 0;
        _accumulatedControl = 0;
    }

    /// <summary>
    /// Один шаг: ошибка = задание − измерение. Возвращает сигнал управления (см. <see cref="AccumulateOutput"/>).
    /// </summary>
    public double Compute(double setpoint, double processValue, double dt)
    {
        double du = ComputeFuzzyOutput(setpoint, processValue, dt, updateState: true);
        if (!AccumulateOutput)
            return du;

        _accumulatedControl += du;
        return _accumulatedControl;
    }

    /// <summary>
    /// Только результат нечёткого вывода (без накопления <see cref="AccumulatedControl"/>), состояние интеграла и предыдущей ошибки обновляется.
    /// </summary>
    public double ComputeOutputOnly(double setpoint, double processValue, double dt)
    {
        return ComputeFuzzyOutput(setpoint, processValue, dt, updateState: true);
    }

    /// <summary>
    /// Нечёткий вывод без изменения состояния (анализ и отладка).
    /// </summary>
    public double PreviewOutput(double error, double dError, double integralError)
    {
        double ne = Normalize(error);
        double nde = Normalize(dError);
        double nie = Normalize(integralError);
        double[] muE = Membership3(ne);
        double[] muDe = Membership3(nde);
        double[] muIe = Membership3(nie);
        return Mode == FuzzyPIDOutputMode.Sugeno
            ? InferSugeno(muE, muDe, muIe)
            : InferMamdani(muE, muDe, muIe);
    }

    private double ComputeFuzzyOutput(double setpoint, double processValue, double dt, bool updateState)
    {
        if (dt <= 0)
            dt = 1e-6;

        double e = setpoint - processValue;
        double de = (e - _prevError) / dt;
        double nextIntegral = _integral + e * dt;
        if (nextIntegral > IntegralLimit)
            nextIntegral = IntegralLimit;
        else if (nextIntegral < -IntegralLimit)
            nextIntegral = -IntegralLimit;

        double ne = Normalize(e * Ke);
        double nde = Normalize(de * Kde);
        double nie = Normalize(nextIntegral * Kie);

        double[] muE = Membership3(ne);
        double[] muDe = Membership3(nde);
        double[] muIe = Membership3(nie);

        double du = Mode == FuzzyPIDOutputMode.Sugeno
            ? InferSugeno(muE, muDe, muIe)
            : InferMamdani(muE, muDe, muIe);

        if (updateState)
        {
            _prevError = e;
            _integral = nextIntegral;
        }

        return du * OutputGain;
    }

    private static double Normalize(double x)
    {
        if (x > 1)
            return 1;
        if (x < -1)
            return -1;
        return x;
    }

    /// <summary>Три терма N, Z, P на универсуме [-1, 1].</summary>
    private static double[] Membership3(double x)
    {
        return new[]
        {
            FuzzyMembershipShapes.Triangular(x, -1.5, -1, 0),
            FuzzyMembershipShapes.Triangular(x, -1, 0, 1),
            FuzzyMembershipShapes.Triangular(x, 0, 1, 1.5)
        };
    }

    private double InferSugeno(double[] muE, double[] muDe, double[] muIe)
    {
        var weights = new List<double>(27);
        var cons = new List<double>(27);
        for (int ie = 0; ie < 3; ie++)
        for (int de = 0; de < 3; de++)
        for (int e = 0; e < 3; e++)
        {
            double w = muE[e] * muDe[de] * muIe[ie];
            weights.Add(w);
            cons.Add(ConsequentSingleton(e, de, ie));
        }

        return FuzzySugenoInference.WeightedAverageSingletons(weights, cons);
    }

    private double InferMamdani(double[] muE, double[] muDe, double[] muIe)
    {
        int n = Math.Max(16, MamdaniOutputSamples);
        Vector u = new Vector(n);
        for (int k = 0; k < n; k++)
            u[k] = -1 + 2.0 * k / Math.Max(1, n - 1);

        var weights = new List<double>();
        var terms = new List<Vector>();
        double hw = MamdaniConsequentHalfWidth;

        for (int ie = 0; ie < 3; ie++)
        for (int de = 0; de < 3; de++)
        for (int e = 0; e < 3; e++)
        {
            double w = muE[e] * muDe[de] * muIe[ie];
            if (w < AI.AISettings.GlobalEps)
                continue;

            double center = ConsequentSingleton(e, de, ie);
            Vector term = new Vector(n);
            for (int k = 0; k < n; k++)
                term[k] = FuzzyMembershipShapes.Triangular(u[k], center - hw, center, center + hw);

            weights.Add(w);
            terms.Add(term);
        }

        if (weights.Count == 0)
            return 0;

        return FuzzyMamdaniInference.InferCentroid(weights, terms, u);
    }

    /// <summary>Встроенная таблица синглтонов следствий в [-1, 1] (эвристика стабилизации).</summary>
    private static double ConsequentSingleton(int e, int de, int ie)
    {
        double ce = e - 1;
        double cde = de - 1;
        double cie = ie - 1;
        double c = 0.45 * ce + 0.35 * cde + 0.2 * cie;
        if (c > 1)
            return 1;
        if (c < -1)
            return -1;
        return c;
    }
}
