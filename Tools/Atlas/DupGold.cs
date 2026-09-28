namespace AiFramework.Tools.Atlas;

/// <summary>
/// Эталон дублей: пары, проверенные чтением кода до того, как детектор их увидел.
/// </summary>
/// <remarks>
/// <para>
/// История git здесь почти бесполезна: 62 сжатых коммита, и прошлые дубли в них не
/// разделены на «до» и «после». Поэтому эталон собран из живого кода: сначала выбраны
/// одноимённые нетривиальные методы разных типов, потом каждая пара прочитана глазами.
/// Выбор шёл по коду, а не по выдаче детектора, чтобы эталон не подстраивался под него.
/// </para>
/// <para>
/// Положительные пары — дубли в любом смысле: дословная копия, тот же алгоритм другими
/// словами, та же функция другим алгоритмом. Отрицательные — совпадение имени без общего
/// смысла и обёртка, вызывающая другой метод.
/// </para>
/// </remarks>
public static class DupGold
{
    /// <summary>Пара, ожидание и почему.</summary>
    public sealed record Pair(string A, string B, bool Duplicate, string Note);

    /// <summary>Пары эталона (XML doc ID).</summary>
    public static IReadOnlyList<Pair> Pairs { get; } =
    [
        new("M:AI.Econometrics.Numerics.LinearAlgebra.Transpose(System.Double[0:,0:])",
            "M:AI.Solvers.Pde.FiniteElement.ElasticityProblem.Transpose(System.Double[0:,0:])", true,
            "транспонирование double[,], отличаются имена переменных"),
        new("M:AI.HighLevelFunctions.FunctionsForEachElements.Factorial(System.Int32)",
            "M:AI.BackEnds.DSP.NWaves.Utils.MathUtilsDSP.Factorial(System.Int32)", true,
            "факториал: таблица и цикл"),
        new("M:AI.BackEnds.DSP.NWaves.Utils.MathUtilsDSP.Factorial(System.Int32)",
            "M:AI.Script.Std.MathModule.Factorial(System.Int32)", true,
            "обёртка AIScript написала факториал заново вместо вызова ядра"),
        new("M:AI.HighLevelFunctions.ActivationFunctions.Softmax(AI.DataStructs.Algebraic.Vector)",
            "M:AI.ML.SequenceAnalysis.SeqAnalyze.ClassifierS2V.SoftMax(AI.DataStructs.Algebraic.Vector)", true,
            "softmax; во втором нет вычитания максимума — разойдётся на больших входах"),
        new("M:AI.HighLevelFunctions.ActivationFunctions.Sigmoid(AI.DataStructs.Algebraic.Vector,System.Double)",
            "M:AI.ML.SequenceAnalysis.SeqAnalyze.ClassifierS2V.Sigmoid(AI.DataStructs.Algebraic.Vector)", true,
            "сигмоида по вектору"),
        new("M:AI.HighLevelFunctions.FunctionsForEachElements.LogGamma(System.Double)",
            "M:AI.Statistics.StatInference.LogGamma(System.Double)", true,
            "логарифм гамма-функции дважды в ядре, разный код"),
        new("M:AI.ClassicMath.MatrixUtils.LU.Determinant(AI.DataStructs.Algebraic.Matrix)",
            "M:AI.Econometrics.VectorAutoregression.Determinant(AI.DataStructs.Algebraic.Matrix)", true,
            "определитель через LU и через Холецкого: для не положительно определённой матрицы ответы разные"),

        new("M:AI.HighLevelFunctions.FunctionsForEachElements.Gamma(System.Double)",
            "M:AI.BackEnds.DSP.NWaves.Filters.Fda.Remez.Gamma(System.Int32)", false,
            "гамма-функция и вспомогательный шаг алгоритма Ремеза: совпало только имя"),
        new("M:AI.Economics.Marketing.MarketingMixModel.Median(System.Double[])",
            "M:AI.Script.Vision.CvModule.Median(AI.Script.Binding.IScriptContext,AI.Script.Runtime.ScriptValue,System.Int32)", false,
            "медиана ряда и медианный фильтр изображения"),
        new("M:AI.ML.NeuralNetworks.V2.Tensor.Transpose(System.Int32,System.Int32)",
            "M:AI.Econometrics.Numerics.LinearAlgebra.Transpose(System.Double[0:,0:])", false,
            "перестановка осей тензора без копирования и транспонирование матрицы"),
        new("M:AI.HighLevelFunctions.FunctionsForEachElements.Erf(AI.DataStructs.Algebraic.Vector)",
            "M:AI.HighLevelFunctions.FunctionsForEachElements.Erf(System.Double)", false,
            "перегрузка по вектору вызывает скалярную: обёртка, а не дубль"),
    ];
}
