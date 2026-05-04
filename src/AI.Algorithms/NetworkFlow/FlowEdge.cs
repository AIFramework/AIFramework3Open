using System;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Ребро в сети потоков с пропускной способностью и текущим потоком
/// </summary>
[Serializable]
public class FlowEdge
{
    /// <summary>
    /// Начальная вершина ребра
    /// </summary>
    public int From { get; set; }

    /// <summary>
    /// Конечная вершина ребра
    /// </summary>
    public int To { get; set; }

    /// <summary>
    /// Пропускная способность ребра
    /// </summary>
    public double Capacity { get; set; }

    /// <summary>
    /// Текущий поток через ребро
    /// </summary>
    public double Flow { get; set; }

    /// <summary>
    /// Создаёт ребро потоковой сети
    /// </summary>
    /// <param name="from">Начальная вершина</param>
    /// <param name="to">Конечная вершина</param>
    /// <param name="capacity">Пропускная способность</param>
    public FlowEdge(int from, int to, double capacity)
    {
        From = from;
        To = to;
        Capacity = capacity;
        Flow = 0.0;
    }

    /// <summary>
    /// Возвращает другой конец ребра
    /// </summary>
    /// <param name="v">Одна из вершин ребра</param>
    public int Other(int v)
    {
        if (v == From) return To;
        if (v == To) return From;
        throw new ArgumentException("Вершина не принадлежит ребру");
    }

    /// <summary>
    /// Остаточная пропускная способность в направлении вершины v
    /// </summary>
    /// <param name="v">Направление</param>
    public double ResidualCapacityTo(int v)
    {
        if (v == To) return Capacity - Flow;
        if (v == From) return Flow;
        throw new ArgumentException("Вершина не принадлежит ребру");
    }

    /// <summary>
    /// Добавляет поток delta в направлении вершины v
    /// </summary>
    /// <param name="v">Направление</param>
    /// <param name="delta">Величина потока</param>
    public void AddFlowTo(int v, double delta)
    {
        if (v == To) Flow += delta;
        else if (v == From) Flow -= delta;
        else throw new ArgumentException("Вершина не принадлежит ребру");
    }

    /// <summary>
    /// Строковое представление ребра
    /// </summary>
    public override string ToString()
    {
        return $"{From}->{To} ({Flow}/{Capacity})";
    }
}
