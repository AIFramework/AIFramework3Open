using System;

namespace AI.Algorithms.VRP;

/// <summary>
/// Определение задачи маршрутизации транспортных средств (VRP)
/// </summary>
[Serializable]
public class VRPInstance
{
    /// <summary>
    /// X-координаты депо (индекс 0)
    /// </summary>
    public double[] DepotX { get; }

    /// <summary>
    /// Y-координаты депо (индекс 0)
    /// </summary>
    public double[] DepotY { get; }

    /// <summary>
    /// X-координаты клиентов
    /// </summary>
    public double[] CustomerX { get; }

    /// <summary>
    /// Y-координаты клиентов
    /// </summary>
    public double[] CustomerY { get; }

    /// <summary>
    /// Спрос каждого клиента
    /// </summary>
    public double[] Demand { get; }

    /// <summary>
    /// Грузоподъёмность транспортного средства
    /// </summary>
    public double VehicleCapacity { get; }

    /// <summary>
    /// Количество транспортных средств
    /// </summary>
    public int NumVehicles { get; }

    /// <summary>
    /// Предвычисленная матрица расстояний (узел 0 = депо, 1..N = клиенты)
    /// </summary>
    public double[,] DistanceMatrix { get; }

    /// <summary>
    /// Количество клиентов
    /// </summary>
    public int N => CustomerX.Length;

    /// <summary>
    /// Общее число узлов (депо + клиенты)
    /// </summary>
    public int TotalNodes => N + 1;

    /// <summary>
    /// Создаёт экземпляр задачи VRP
    /// </summary>
    /// <param name="depotX">X-координата депо</param>
    /// <param name="depotY">Y-координата депо</param>
    /// <param name="custX">X-координаты клиентов</param>
    /// <param name="custY">Y-координаты клиентов</param>
    /// <param name="demand">Спрос клиентов</param>
    /// <param name="vehicleCapacity">Грузоподъёмность</param>
    /// <param name="numVehicles">Число транспортных средств</param>
    public VRPInstance(double depotX, double depotY, double[] custX, double[] custY,
        double[] demand, double vehicleCapacity, int numVehicles)
    {
        DepotX = new[] { depotX };
        DepotY = new[] { depotY };
        CustomerX = custX ?? throw new ArgumentNullException(nameof(custX));
        CustomerY = custY ?? throw new ArgumentNullException(nameof(custY));
        Demand = demand ?? throw new ArgumentNullException(nameof(demand));
        VehicleCapacity = vehicleCapacity;
        NumVehicles = numVehicles;

        int total = TotalNodes;
        DistanceMatrix = new double[total, total];

        for (int i = 0; i < total; i++)
        {
            for (int j = i + 1; j < total; j++)
            {
                double xi = (i == 0) ? depotX : custX[i - 1];
                double yi = (i == 0) ? depotY : custY[i - 1];
                double xj = (j == 0) ? depotX : custX[j - 1];
                double yj = (j == 0) ? depotY : custY[j - 1];

                double dist = Math.Sqrt((xi - xj) * (xi - xj) + (yi - yj) * (yi - yj));
                DistanceMatrix[i, j] = dist;
                DistanceMatrix[j, i] = dist;
            }
        }
    }

    /// <summary>
    /// Возвращает расстояние между узлами i и j
    /// </summary>
    public double Distance(int i, int j)
    {
        return DistanceMatrix[i, j];
    }
}
