using AI.DataStructs.Algebraic;

namespace AI.Solvers.Pde;

/// <summary>
/// Равномерная сетка на отрезке
/// </summary>
/// <param name="Left">Левая граница</param>
/// <param name="Right">Правая граница</param>
/// <param name="Count">Число узлов, включая границы</param>
public readonly record struct Grid1D(double Left, double Right, int Count)
{
    /// <summary>Шаг сетки</summary>
    public double Step => (Right - Left) / (Count - 1);

    /// <summary>Координата узла</summary>
    /// <param name="index">Номер узла</param>
    public double Node(int index) => Left + (index * Step);

    /// <summary>Координаты всех узлов</summary>
    public Vector Nodes()
    {
        var nodes = new Vector(Count);

        for (int i = 0; i < Count; i++)
            nodes[i] = Node(i);

        return nodes;
    }

    /// <summary>Значения функции в узлах сетки</summary>
    /// <param name="function">Функция одной переменной</param>
    public Vector Sample(Func<double, double> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var values = new Vector(Count);

        for (int i = 0; i < Count; i++)
            values[i] = function(Node(i));

        return values;
    }

    /// <summary>Проверяет корректность описания сетки</summary>
    public void Validate()
    {
        if (Count < 3)
            throw new ArgumentException("Сетке нужно не меньше трёх узлов", nameof(Count));

        if (Right <= Left)
            throw new ArgumentException("Правая граница должна быть больше левой", nameof(Right));
    }
}

/// <summary>
/// Равномерная сетка на прямоугольнике
/// </summary>
/// <param name="Left">Левая граница по x</param>
/// <param name="Right">Правая граница по x</param>
/// <param name="Bottom">Нижняя граница по y</param>
/// <param name="Top">Верхняя граница по y</param>
/// <param name="CountX">Число узлов по x</param>
/// <param name="CountY">Число узлов по y</param>
public readonly record struct Grid2D(double Left, double Right, double Bottom, double Top, int CountX, int CountY)
{
    /// <summary>Шаг по x</summary>
    public double StepX => (Right - Left) / (CountX - 1);

    /// <summary>Шаг по y</summary>
    public double StepY => (Top - Bottom) / (CountY - 1);

    /// <summary>Число узлов</summary>
    public int NodeCount => CountX * CountY;

    /// <summary>Координата x узла</summary>
    /// <param name="i">Номер по x</param>
    public double X(int i) => Left + (i * StepX);

    /// <summary>Координата y узла</summary>
    /// <param name="j">Номер по y</param>
    public double Y(int j) => Bottom + (j * StepY);

    /// <summary>Сквозной номер узла</summary>
    /// <param name="i">Номер по x</param>
    /// <param name="j">Номер по y</param>
    public int Index(int i, int j) => (j * CountX) + i;

    /// <summary>Лежит ли узел на границе</summary>
    /// <param name="i">Номер по x</param>
    /// <param name="j">Номер по y</param>
    public bool IsBoundary(int i, int j) => i == 0 || j == 0 || i == CountX - 1 || j == CountY - 1;

    /// <summary>Значения функции в узлах, строка матрицы — постоянное y</summary>
    /// <param name="function">Функция двух переменных</param>
    public Matrix Sample(Func<double, double, double> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var values = new Matrix(CountY, CountX);

        for (int j = 0; j < CountY; j++)
            for (int i = 0; i < CountX; i++)
                values[j, i] = function(X(i), Y(j));

        return values;
    }

    /// <summary>Проверяет корректность описания сетки</summary>
    public void Validate()
    {
        if (CountX < 3 || CountY < 3)
            throw new ArgumentException("Сетке нужно не меньше трёх узлов по каждой оси", nameof(CountX));

        if (Right <= Left || Top <= Bottom)
            throw new ArgumentException("Границы прямоугольника заданы неверно", nameof(Right));
    }
}
