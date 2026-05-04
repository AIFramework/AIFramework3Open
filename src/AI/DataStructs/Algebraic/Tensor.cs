using AI.DataStructs.Shapes;
using AI.Extensions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

/// <summary>
/// Тензор 3-го ранга
/// </summary>
[Serializable]
[DebuggerDisplay("Height = {Height}, Width = {Width}, Depth = {Depth}")]
public partial class Tensor : IAlgebraicStructure<double>, IEquatable<Tensor>, ISavable, IByteConvertable
{
    #region Поля и свойства
    /// <summary>
    /// Высота
    /// </summary>
    public int Height => Shape[1];
    /// <summary>
    /// Ширина
    /// </summary>
    public int Width => Shape[0];
    /// <summary>
    /// Глубина
    /// </summary>
    public int Depth => Shape[2];
    /// <summary>
    /// Форма тензора
    /// </summary>
    public Shape Shape { get; }
    /// <summary>
    /// Данные
    /// </summary>
    public double[] Data { get; set; }
    /// <summary>
    /// Доступ к элементу по индексу
    /// </summary>
    /// <param name="i">Высота</param>
    /// <param name="j">Ширина</param>
    /// <param name="k">Глубина</param>
    public double this[int i, int j, int k]
    {
        get => Data[GetByIndex(i, j, k)];
        set => Data[GetByIndex(i, j, k)] = value;
    }
    #endregion

    #region Конструкторы
    /// <summary>
    /// Инициализация 3х-мерным массивом
    /// </summary>
    public Tensor(double[,,] data)
    {
        Shape = new Shape3D(data.GetLength(0), Data!.GetLength(1), data.GetLength(2));

        Data = new double[Height * Width * Depth];

        //ToDo: Оптимизировать
        for (int i = 0; i < Depth; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                for (int k = 0; k < Width; k++)
                {
                    this[j, k, i] = data[j, k, i];
                }
            }

        }
    }
    /// <summary>
    /// Создает тензор заполненный нулями
    /// </summary>
    public Tensor(Shape3D shape)
    {
        if (shape.Rank > 3)
        {
            throw new ArgumentException("Максимальный ранг(размерность) формы = 3", nameof(shape));
        }

        switch (shape.Rank)
        {
            case 1:
                Shape = new Shape3D(1, shape[0]);
                break;
            case 2:
                Shape = new Shape3D(shape[1], shape[0]);
                break;
            case 3:
                Shape = new Shape3D(shape[1], shape[0], shape[2]);
                break;
        }

        Data = new double[Shape!.Count];
    }
    /// <summary>
    /// Создать тензор инициализированный нулями
    /// </summary>
    /// <param name="height">Высота</param>
    /// <param name="width">Ширина</param>
    /// <param name="depth">Глубина</param>
    public Tensor(int height, int width, int depth) : this(new Shape3D(height, width, depth)) { }
    /// <summary>
    /// Инициализация массивом
    /// </summary>
    public Tensor(double[] data)
    {
        Shape = new Shape3D(1, 1, data.Length);
        Data = new double[Depth];
        Buffer.BlockCopy(data, 0, Data, 0, 8 * Shape.Count);
    }
    #endregion

    #region Приватные методы
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetByIndex(int h, int w, int d)
    {
        return (Width * h) + w + (Height * Width * d);
    }
    #endregion
}
