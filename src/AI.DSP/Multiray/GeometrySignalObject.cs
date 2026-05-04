using AI.DataStructs.Algebraic;

namespace AI.DSP.Multiray;

/// <summary>
/// Объект с пространственными координатами
/// </summary>
public class GeometrySignalObject
{
    /// <summary>
    /// Координаты объекта
    /// </summary>
    public Vector Coordinates { get; set; }

    /// <summary>
    /// Конструктор по умолчанию (2D, начало координат)
    /// </summary>
    public GeometrySignalObject()
    {
        Coordinates = new Vector(2);
    }

    /// <summary>
    /// Конструктор с координатами
    /// </summary>
    /// <param name="coord">Координаты</param>
    public GeometrySignalObject(params double[] coord)
    {
        Coordinates = coord;
    }
}
