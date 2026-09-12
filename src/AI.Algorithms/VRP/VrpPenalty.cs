using System;

namespace AI.Algorithms.VRP;

/// <summary>
/// Штраф за перегруз машин, соразмерный масштабу задачи
/// </summary>
/// <remarks>
/// Единица перегруза стоит больше, чем объезд всех клиентов поодиночке, — то есть больше любого
/// разумного допустимого решения. Постоянный штраф вроде 10⁶ этим свойством не обладает: при
/// координатах в миллионы недопустимое решение оказывается дешевле допустимого.
/// </remarks>
internal static class VrpPenalty
{
    /// <summary>Цена единицы перегруза: сумма поездок «депо — клиент — депо» плюс единица</summary>
    internal static double Scale(VRPInstance inst)
    {
        double scale = 1;

        for (int c = 0; c < inst.N; c++)
            scale += 2 * inst.Distance(0, c + 1);

        return scale;
    }

    /// <summary>Суммарный перегруз по маршрутам</summary>
    internal static double Overload(VRPInstance inst, VRPSolution solution)
    {
        double overload = 0;

        foreach (var route in solution.Routes)
        {
            double load = 0;
            foreach (int c in route)
                load += inst.Demand[c];

            overload += Math.Max(0, load - inst.VehicleCapacity);
        }

        return overload;
    }

    /// <summary>Длина плюс штраф за перегруз</summary>
    internal static double Cost(VRPInstance inst, VRPSolution solution, double scale)
        => solution.TotalDistance(inst) + (scale * Overload(inst, solution));
}
