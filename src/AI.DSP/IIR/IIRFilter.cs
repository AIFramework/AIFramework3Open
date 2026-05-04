using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;
using System;

namespace AI.DSP.IIR;

/// <summary>
/// БИХ фильтр (рекурсивный фильтр с бесконечной импульсной характеристикой)
/// </summary>
[Serializable]
public class IIRFilter : IFilter
{
    private int aLen, bLen, ofA, ofB, bL2, aL2;
    private Vector inps, outps;
    /// <summary>
    /// Имя фильтра
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Коэффициенты "A" (знаменатель)
    /// </summary>
    public Vector A { get; set; }
    /// <summary>
    /// Коэффициенты "B" (числитель)
    /// </summary>
    public Vector B { get; set; }

    /// <summary>
    /// Ограничение амплитуды выходного сигнала
    /// </summary>
    public double Threshold { get; set; } = 1e+300;

    /// <summary>
    /// БИХ фильтр
    /// </summary>
    /// <param name="a">Коэффициенты "A" (знаменатель)</param>
    /// <param name="b">Коэффициенты "B" (числитель)</param>
    public IIRFilter(Vector a, Vector b)
    {
        Init(a, b);
    }

    private void Init(Vector a, Vector b)
    {
        aLen = a.Count;
        bLen = b.Count;

        A = a.Repeat(2);
        B = b.Repeat(2);

        aL2 = A.Count;
        bL2 = B.Count;

        Reset();
    }

    /// <summary>
    /// Расчёт выходного отсчёта фильтра
    /// </summary>
    /// <param name="inp">Входной отсчёт</param>
    /// <returns>Выходной отсчёт</returns>
    public double FilterOutp(double inp)
    {
        double outp = 0;
        inps[ofB] = inp;
        outps[ofA] = 0;

        for (int i = 0, j = bLen - ofB; i < bLen; i++, j++)
            outp += inps[i] * B[j];

        for (int i = 0, j = aLen - ofA; i < aLen; i++, j++)
            outp -= outps[i] * A[j];

        if (outp > Threshold) outp = Threshold;
        else if (outp < -Threshold) outp = -Threshold;

        outps[ofA] = outp;

        if (--ofB < 0)
            ofB = bLen - 1;

        if (--ofA < 0)
            ofA = aLen - 1;

        return outp;
    }

    /// <summary>
    /// Выход рекурсивного фильтра для вектора
    /// </summary>
    /// <param name="signal">Входной сигнал</param>
    /// <returns>Фильтрованный сигнал</returns>
    public Vector FilterOutp(Vector signal)
    {
        Reset();
        return signal.Transform(FilterOutp);
    }

    /// <summary>
    /// Выход рекурсивного фильтра (многократная фильтрация)
    /// </summary>
    /// <param name="signal">Входной сигнал</param>
    /// <param name="iteration">Число итераций фильтрации</param>
    /// <returns>Фильтрованный сигнал</returns>
    public Vector FilterOutp(Vector signal, int iteration)
    {
        Vector outp = signal.Clone();

        for (int i = 0; i < iteration; i++)
        {
            Reset();
            outp = outp.Transform(FilterOutp);
        }

        return outp;
    }

    /// <summary>
    /// Сброс состояния фильтра
    /// </summary>
    public void Reset()
    {
        inps = new Vector(bL2);
        outps = new Vector(aL2);
        ofA = aLen;
        ofB = bLen;
    }

    /// <summary>
    /// Экспорт состояния фильтра
    /// </summary>
    /// <returns>Кортеж (входной буфер, выходной буфер, смещение A, смещение B)</returns>
    public Tuple<Vector, Vector, int, int> ExportState()
    {
        return new Tuple<Vector, Vector, int, int>(inps, outps, ofA, ofB);
    }

    /// <summary>
    /// Импорт состояния фильтра
    /// </summary>
    /// <param name="inputs">Входы (длина bLen)</param>
    /// <param name="outputs">Выходы (длина aLen)</param>
    /// <param name="offsetA">Смещение выходов</param>
    /// <param name="offsetB">Смещение входов</param>
    public void ImportState(Vector inputs, Vector outputs, int offsetA, int offsetB)
    {
        if (inputs.Count != bLen || outputs.Count != aLen)
            throw new ArgumentException("Размерности не совпадают, импорт невозможен");

        ofA = offsetA;
        ofB = offsetB;

        inps = new Vector(bL2);
        for (int i = 0; i < bLen; i++)
            inps[i] = inputs[i];

        outps = new Vector(aL2);
        for (int i = 0; i < aLen; i++)
            outps[i] = outputs[i];
    }

    /// <summary>
    /// Сохранение фильтра в файл
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    public void Save(string path)
    {
        InMemoryDataStream bs = new InMemoryDataStream();
        bs.Write("iir").Write(Name).Write(A.CutAndZero(aLen)).Write(B.CutAndZero(bLen)).Zip().Save(path);
    }

    /// <summary>
    /// Загрузка фильтра из файла
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Загруженный фильтр</returns>
    public static IIRFilter Load(string path)
    {
        InMemoryDataStream bs = new InMemoryDataStream(path, isZipped: true);
        _ = bs.UnZip();
        _ = bs.ReadString();
        string name = bs.ReadString();
        Vector a = bs.ReadDoubles();
        Vector b = bs.ReadDoubles();

        return new IIRFilter(a, b) { Name = name };
    }

    /// <summary>
    /// Загрузка фильтра из буфера данных
    /// </summary>
    /// <param name="data">Буфер данных</param>
    /// <returns>Загруженный фильтр</returns>
    public static IIRFilter Load(byte[] data)
    {
        InMemoryDataStream bs = new InMemoryDataStream(data, isZipped: true);
        _ = bs.UnZip();
        _ = bs.ReadString();
        string name = bs.ReadString();
        Vector a = bs.ReadDoubles();
        Vector b = bs.ReadDoubles();

        return new IIRFilter(a, b) { Name = name };
    }
}
