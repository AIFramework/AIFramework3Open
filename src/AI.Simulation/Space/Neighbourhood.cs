namespace AI.Simulation.Space;

/// <summary>Окрестность клетки на решётке</summary>
public enum Neighbourhood
{
    /// <summary>Фон Неймана: четыре соседа по сторонам; расстояние — сумма модулей сдвигов</summary>
    VonNeumann,

    /// <summary>Мура: восемь соседей, включая диагональных; расстояние — наибольший из сдвигов</summary>
    Moore
}
