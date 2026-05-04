using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.DataPrepaire.DataNormalizers;

/// <summary>
/// Нормализатор данных
/// </summary>
[Serializable]
public abstract class Normalizer
{
    /// <summary>
    /// Обучение преобразователя
    /// </summary>
    /// <param name="data">Набор данных</param>
    public abstract void Train(IEnumerable<IAlgebraicStructure<double>> data);

    /// <summary>
    /// Использование преобразователя (Перезапись значений алгебраической структуры)
    /// </summary>
    public abstract IAlgebraicStructure<double> Transform(IAlgebraicStructure<double> data);


    /// <summary>
    /// Использование преобразователя (Перезапись значений алгебраической структуры)
    /// </summary>
    public virtual IAlgebraicStructure<double>[] Transform(IEnumerable<IAlgebraicStructure<double>> data)
    {
        var source = (data is IAlgebraicStructure<double>[]) ? data as IAlgebraicStructure<double>[] : data.ToArray();
        var result = new IAlgebraicStructure<double>[source.Length];

        for (int i = 0; i < source.Length; i++)
            result[i] = Transform(source[i]);

        return result;
    }

    /// <summary>
    /// Восстановление нормализованных данных (Перезапись значений алгебраической структуры)
    /// </summary>
    /// <param name="normalizeData">Нормализованные данные</param>
    public abstract IAlgebraicStructure<double> Denormalize(IAlgebraicStructure<double> normalizeData);

    /// <summary>
    /// Восстановление нормализованных данных (Перезапись значений алгебраической структуры)
    /// </summary>
    /// <param name="normalizeData">Нормализованные данные</param>
    public virtual IAlgebraicStructure<double>[] Denormalize(IEnumerable<IAlgebraicStructure<double>> normalizeData)
    {
        var source = (normalizeData is IAlgebraicStructure<double>[]) ? normalizeData as IAlgebraicStructure<double>[] : normalizeData.ToArray();
        var result = new IAlgebraicStructure<double>[source.Length];

        for (int i = 0; i < source.Length; i++)
            result[i] = Denormalize(source[i]);

        return result;
    }
}
