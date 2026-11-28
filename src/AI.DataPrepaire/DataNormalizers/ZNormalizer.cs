using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Statistics;

namespace AI.DataPrepaire.DataNormalizers;

/// <summary>
/// Z - нормализация (Мат. ожидание = 0, СКО = 1)
/// </summary>
[Serializable]
public class ZNormalizer : Normalizer
{
    /// <summary>
    /// Среднее
    /// </summary>
    public double[] Mean { get; set; }
    /// <summary>
    /// СКО
    /// </summary>
    public double[] Std { get; set; }



    /// <summary>
    /// Запоминает среднее и выборочное отклонение (делитель n − 1) каждой компоненты.
    /// </summary>
    /// <remarks>
    /// Считает <see cref="StatUtils.MeanAndVariance"/> — ту же дисперсию, что и остальной фреймворк.
    /// У компоненты без разброса отклонение равно нулю, и <see cref="Transform"/> переводит её в ноль.
    /// </remarks>
    public override void Train(IEnumerable<IAlgebraicStructure<double>> data)
    {
        IAlgebraicStructure<double>[] algebraicStructures = (data is IAlgebraicStructure<double>[]) ? data as IAlgebraicStructure<double>[] : data.ToArray();

        if (algebraicStructures.Length == 0)
            throw new ArgumentException("Нормировщику нужен хотя бы один пример.", nameof(data));

        int size = algebraicStructures[0].Shape.Count;

        Mean = new double[size];
        Std = new double[size];

        var column = new double[algebraicStructures.Length];

        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < column.Length; i++) column[i] = algebraicStructures[i].Data[j];

            (Mean[j], double variance, _) = StatUtils.MeanAndVariance(column, skipNaN: false);
            Std[j] = Math.Sqrt(variance);
        }
    }

    /// <summary>
    /// Использование преобразователя (Перезапись значений алгебраической структуры)
    /// </summary>
    public override IAlgebraicStructure<double> Transform(IAlgebraicStructure<double> data)
    {
        if (Mean == null) return data;

        // Вектор не изменяется на месте — возвращается новый; остальные структуры правятся на месте, как прежде
        IAlgebraicStructure<double> result = data is Vector vector ? vector.Clone() : data;

        for (int i = 0; i < result.Data.Length; i++)
        {
            result.Data[i] = Std[i] > 0 ? (data.Data[i] - Mean[i]) / Std[i] : 0.0;
        }

        return result;
    }

    /// <summary>
    /// Восстановление нормализованных данных (Перезапись значений алгебраической структуры)
    /// </summary>
    /// <param name="normalizeData">Нормализованные данные</param>
    public override IAlgebraicStructure<double> Denormalize(IAlgebraicStructure<double> normalizeData)
    {
        if (Mean == null) return normalizeData;

        if (normalizeData is Vector)
        {
            return ((normalizeData as Vector) * Std) + Mean;
        }

        IAlgebraicStructure<double> dat = normalizeData;

        for (int i = 0; i < normalizeData.Data.Length; i++)
            dat.Data[i] = (normalizeData.Data[i] * Std[i]) + Mean[i];

        return dat;
    }
}
