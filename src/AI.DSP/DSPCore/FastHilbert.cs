using AI.DataStructs.WithComplexElements;
using AI.HighLevelFunctions;
using System;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DSP.DSPCore;

/// <summary>
/// Преобразование Гильберта и квадратурная обработка сигналов
/// </summary>
public static class FastHilbert
{
    /// <summary>
    /// Сигнал сопряженный по Гильберту
    /// </summary>
    /// <param name="st">Исходный сигнал</param>
    public static Vector ConjugateToTheHilbert(Vector st)
    {
        Vector stNew = st.CutAndZero(Functions.NextPow2(st.Count));
        ComplexVector cv = FFT.CalcFFT(stNew);

        Complex j = new Complex(0, 1);
        Complex mj = -j;

        int n1 = stNew.Count / 2, n2 = stNew.Count;

        for (int i = 0; i < n1; i++)
            cv[i] = cv[i] * mj;

        for (int i = n1; i < n2; i++)
            cv[i] = cv[i] * j;

        cv = FFT.CalcIFFT(cv).CutAndZero(st.Count);
        return cv.RealVector;
    }

    /// <summary>
    /// Аналитический сигнал
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    public static ComplexVector GetAnalSig(Vector st)
    {
        ComplexVector cv = new ComplexVector(st.Count);
        Vector stH = ConjugateToTheHilbert(st);

        for (int i = 0; i < st.Count; i++)
            cv[i] = new Complex(st[i], stH[i]);

        return cv;
    }

    /// <summary>
    /// Огибающая
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    public static Vector Envelope(Vector st)
    {
        return GetAnalSig(st).MagnitudeVector;
    }

    /// <summary>
    /// Мгновенная фаза
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    public static Vector Phase(Vector st)
    {
        return GetAnalSig(st).PhaseVector;
    }

    /// <summary>
    /// Мгновенная частота
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    public static Vector Frequency(Vector st)
    {
        return Functions.Diff(GetAnalSig(st).PhaseVector);
    }

    /// <summary>
    /// Вычисление I/Q компонент сигнала через квадратурную демодуляцию с ФНЧ Баттерворта
    /// </summary>
    private static Tuple<Vector, Vector> ComputeIQ(Vector st, double fd, double f0)
    {
        double _2pi = Math.PI * 2;
        ComplexVector complexVector = Filters.ButterworthLowCFH(st.Count, f0, (int)fd, 5);

        Vector cos = new Vector(st.Count);
        Vector sin = new Vector(st.Count);

        for (int i = 0; i < st.Count; i++)
        {
            double arg = _2pi * f0 * (i / fd);
            cos[i] = st[i] * Math.Cos(arg);
            sin[i] = st[i] * Math.Sin(arg);
        }

        cos = Filters.Filter(cos, complexVector, true);
        sin = Filters.Filter(sin, complexVector, true);

        return new Tuple<Vector, Vector>(cos, sin);
    }

    /// <summary>
    /// Выделение огибающей на базе квадратурных составляющих: sqrt(I² + Q²)
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="f0">Несущая частота</param>
    public static Vector EnvelopeIQ(Vector st, double fd, double f0)
    {
        var iq = ComputeIQ(st, fd, f0);
        Vector cosF = iq.Item1;
        Vector sinF = iq.Item2;
        return ((cosF * cosF) + (sinF * sinF)).Transform(Math.Sqrt);
    }

    /// <summary>
    /// Выделение фазы на базе квадратурных составляющих: atan2(Q, I)
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="f0">Несущая частота</param>
    public static Vector PhaseIQ(Vector st, double fd, double f0)
    {
        var iq = ComputeIQ(st, fd, f0);
        Vector cosF = iq.Item1;
        Vector sinF = iq.Item2;

        Vector ph = new Vector(st.Count);
        for (int i = 0; i < st.Count; i++)
            ph[i] = Math.Atan2(sinF[i], cosF[i]);

        ph = FunctionsForEachElements.Unwrap(ph);
        return ph;
    }

    /// <summary>
    /// Квадратурные I/Q компоненты сигнала
    /// </summary>
    /// <param name="st">Входной сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="f0">Несущая частота</param>
    public static Tuple<Vector, Vector> IQ(Vector st, double fd, double f0)
    {
        return ComputeIQ(st, fd, f0);
    }
}
