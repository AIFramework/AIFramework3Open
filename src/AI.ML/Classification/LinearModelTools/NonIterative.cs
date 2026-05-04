using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.ML.Classification.LinearModelTools;

/// <summary>
/// Безытеративное обучение (Двуклассовый)
/// </summary>
[Serializable]
public class NonIterativeTwoClass
{
    /// <summary>
    /// Вектор весов
    /// </summary>
    public Vector W { get; set; }

    /// <summary>
    /// Вес смещения
    /// </summary>
    public double B { get; set; }

    /// <summary>
    /// Обучение классификатора
    /// </summary>
    /// <param name="vectorsCL1">Объекты класса 1</param>
    /// <param name="vectorsCL2">Объекты класса 2</param>
    public void Train(IEnumerable<Vector> vectorsCL1, IEnumerable<Vector> vectorsCL2)
    {
        Vector[] vectorsArr1 = vectorsCL1.ToArray();
        Vector[] vectorsArr2 = vectorsCL2.ToArray();

        Vector mean1 = Vector.Mean(vectorsArr1);
        Vector mean2 = Vector.Mean(vectorsArr2);

        W = mean2 - mean1;

        Vector v1Proj = ProjW(vectorsCL1);
        Vector v2Proj = ProjW(vectorsCL2);
        B = -QSolve(v1Proj, v2Proj);
    }

    /// <summary>
    /// Прямой проход
    /// </summary>
    /// <param name="vect">Вектор</param>
    public bool Forward(Vector vect)
    {
        var dot = AnalyticGeometryFunctions.Dot(W, vect) + B;
        return dot > 0;
    }

    // Решения квадратного уравнения для поиска значения B
    private double QSolve(Vector v1, Vector v2)
    {
        double pv1 = v1.Mean(), pv2 = v2.Mean();
        double ps1 = v1.Std(), ps2 = v2.Std();

        // Защита от вырожденного случая (нулевая дисперсия)
        if (ps1 < 1e-10) ps1 = 1e-10;
        if (ps2 < 1e-10) ps2 = 1e-10;

        double pls1 = Math.Log(ps1), pls2 = Math.Log(ps2);

        ps1 *= ps1;
        ps2 *= ps2;

        double a = 2 * (ps1 - ps2);
        double b = 4 * (ps2 * pv1 - ps1 * pv2);
        double c = 2 * ps1 * pv2 * pv2 - 2 * ps2 * pv1 * pv1 - ps2 * ps1 * (pls1 - pls2);

        // Если классы имеют одинаковую дисперсию — уравнение линейное
        if (Math.Abs(a) < 1e-12)
        {
            return (Math.Abs(b) < 1e-12) ? (pv1 + pv2) / 2.0 : -c / b;
        }

        double discriminant = b * b - 4 * a * c;

        // При отрицательном дискриминанте берём середину между средними
        if (discriminant < 0)
            return (pv1 + pv2) / 2.0;

        double d = Math.Sqrt(discriminant);
        double m1 = (-b + d) / (2 * a);
        double m2 = (-b - d) / (2 * a);

        // Выбираем корень, лежащий между средними двух классов
        double lo = Math.Min(pv1, pv2);
        double hi = Math.Max(pv1, pv2);

        bool m1Between = m1 >= lo && m1 <= hi;
        bool m2Between = m2 >= lo && m2 <= hi;

        if (m1Between && !m2Between) return m1;
        if (m2Between && !m1Between) return m2;

        // Если оба (или ни один) лежат между средними — выбираем ближайший к середине
        double mid = (pv1 + pv2) / 2.0;
        return Math.Abs(m1 - mid) < Math.Abs(m2 - mid) ? m1 : m2;
    }

    // Проекция каждого вектора на вектор весов W -> скалярный вектор проекций
    private Vector ProjW(IEnumerable<Vector> vectors)
    {
        Vector[] vectorsArr = vectors.ToArray();
        Vector vectProj = new Vector(vectorsArr.Length);

        for (int i = 0; i < vectorsArr.Length; i++)
        {
            double dot = 0;
            for (int j = 0; j < W.Count; j++)
                dot += vectorsArr[i][j] * W[j];
            vectProj[i] = dot;
        }

        return vectProj;
    }


}
