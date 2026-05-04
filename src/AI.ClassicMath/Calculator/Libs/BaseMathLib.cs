// BaseMathLib.cs

using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Distances;
using AI.HighLevelFunctions;
using AI.MathUtils.Combinatorics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Complex = System.Numerics.Complex;

namespace AI.ClassicMath.Calculator.Libs;

/// <summary>
/// Предоставляет базовые математические и векторные функции.
/// </summary>
[Serializable]
public partial class BaseMathLib : IMathLib
{
    /// <summary>
    /// Имя библиотеки.
    /// </summary>
    public string Name
    {
        get;
        set;
    } = "Базовые математические функции";

    /// <summary>
    /// Описание библиотеки.
    /// </summary>
    public string Description
    {
        get;
        set;
    } = "Библиотека содержит тригонометрические, логарифмические, статистические и другие базовые функции, работающие с вещественными и комплексными числами.";

    /// <summary>
    /// Собирает и возвращает словарь доступных функций.
    /// </summary>
    public Dictionary<string, FunctionDefinition> GetFunctions()
    {
        var functions = new List<FunctionDefinition> {
    // Стандартная математика
    CreateRoundFunction(),
    CreateFloorFunction(),
    CreateCeilFunction(),
    CreateAbsFunction(),
    CreateSqrtFunction(),
    CreateCbrtFunction(),
    CreatePowFunction(),

    // Работа с датами
    CreateDateTimeFunction(),
    CreateDateDiffFunction(),

    // Тригонометрия
    CreateSinFunction(),
    CreateCosFunction(),
    CreateTanFunction(),
    CreateAsinFunction(),
    CreateAcosFunction(),
    CreateAtanFunction(),
    CreateTanhFunction(),

    // Логарифмы и экспонента
    CreateLnFunction(),
    CreateLog10Function(),
    CreateLogFunction(),
    CreateExpFunction(),

    // Угловые меры
    CreateRadFunction(),
    CreateDegFunction(),

    // Комбинаторика и специальные функции
    CreateFactFunction(),
    CreateGammaFunction(),
    CreateCombFunction(),
    CreateCombPFunction(),

    // Теория чисел
    CreateGCDFunction(),
    CreateLCMFunction(),

    // Векторные операции
    CreateMagFunction(),
    CreateSumFunction(),
    CreateDotFunction(),
    CreateCrossFunction(),
    CreateIndexFunction(),

    // Статистика
    CreateMeanFunction(),
    CreateMinFunction(),
    CreateMaxFunction(),

    // Bitwise операции
    CreateXorFunction(),
    CreateBitNotFunction(),

    // Строковые операции
    CreateLenFunction(),
    CreateConcatFunction(),
    CreateSubstrFunction(),
    CreateJoinFunction()
  };

        return functions.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
    }

    #region Helper Factory Methods

    /// <summary>Вспомогательный метод для создания функций вида F(x), работающих с комплексными числами.</summary>
    private static FunctionDefinition CreateUnaryComplexFunction(string name, Func<Complex, Complex> function, DescriptionFunction description)
    {
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => function(CastsVar.CastToComplex(args[0], name)),
            Description = description
        };
    }

    /// <summary>Вспомогательный метод для создания функций вида F(x, y), работающих с комплексными числами.</summary>
    private static FunctionDefinition CreateBinaryComplexFunction(string name, Func<Complex, Complex, Complex> function, DescriptionFunction description)
    {
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args => function(CastsVar.CastToComplex(args[0], name), CastsVar.CastToComplex(args[1], name)),
            Description = description
        };
    }

    #endregion
}
