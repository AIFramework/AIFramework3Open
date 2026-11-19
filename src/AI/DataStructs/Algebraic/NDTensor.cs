using AI.DataStructs.Shapes;
using AI.Extensions;
using System;
using System.Diagnostics;
using System.IO;

namespace AI.DataStructs.Algebraic;

/// <summary>
/// Многомерный тензор
/// </summary>
[Serializable]
[DebuggerDisplay("Shape = {Shape.ToString(),nq}")]
public class NDTensor : IAlgebraicStructure<double>, IEquatable<NDTensor>, ISavable, IByteConvertable
{
    #region Поля и свойства
    private readonly int[] _volumes;

    /// <summary>
    /// Данные тензора
    /// </summary>
    public double[] Data { get; private set; }
    /// <summary>
    /// Размерность тензора
    /// </summary>
    public Shape Shape { get; }
    /// <summary>
    /// Обращение к элементам через индексы
    /// </summary>
    /// <param name="indexes"></param>
    /// <returns></returns>
    public double this[params int[] indexes]
    {
        get => Data[GetIndex(indexes)];
        set => Data[GetIndex(indexes)] = value;
    }
    #endregion

    #region Конструкторы
    /// <summary>
    /// Многомерный тензор
    /// </summary>
    public NDTensor(Shape shape)
    {
        Shape = shape;

        _volumes = new int[shape.Rank - 1];
        _volumes[0] = shape[0];
        int len = 1;

        for (int i = 0; i < shape.Rank; i++)
        {
            len *= shape[i];
        }

        for (int i = 1; i < _volumes.Length; i++)
        {
            _volumes[i] *= shape[i];
        }

        Data = new double[len];
    }
    /// <summary>
    /// Многомерный тензор
    /// </summary>
    public NDTensor(int[] shape) : this(new Shape(shape)) { }
    #endregion

    #region Операторы
    /// <summary>
    /// Оператор "равно"
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(NDTensor left, NDTensor right)
    {
        if (left == null && right == null)
            return true;

        return left!.Shape == right.Shape && left.Data.ElementWiseEqual(right.Data);
    }
    /// <summary>
    /// Оператор "неравно"
    /// </summary>
    public static bool operator !=(NDTensor left, NDTensor right)
    {
        return !(left == right);
    }
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator +(NDTensor left, NDTensor right) =>
        ElementWise(SameShape(left, right), i => left.Data[i] + right.Data[i]);
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator +(double left, NDTensor right) =>
        ElementWise(right, i => left + right.Data[i]);
    /// <summary>
    /// Сложение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator +(NDTensor left, double right) =>
        ElementWise(left, i => left.Data[i] + right);
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator *(NDTensor left, NDTensor right) =>
        ElementWise(SameShape(left, right), i => left.Data[i] * right.Data[i]);
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator *(double left, NDTensor right) =>
        ElementWise(right, i => left * right.Data[i]);
    /// <summary>
    /// Умножение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator *(NDTensor left, double right) =>
        ElementWise(left, i => left.Data[i] * right);
    /// <summary>
    /// Разность
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator -(NDTensor left, NDTensor right) =>
        ElementWise(SameShape(left, right), i => left.Data[i] - right.Data[i]);
    /// <summary>
    /// Разность
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator -(double left, NDTensor right) =>
        ElementWise(right, i => left - right.Data[i]);
    /// <summary>
    /// Разность
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator -(NDTensor left, double right) =>
        ElementWise(left, i => left.Data[i] - right);
    /// <summary>
    /// Отношение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator /(NDTensor left, NDTensor right) =>
        ElementWise(SameShape(left, right), i => left.Data[i] / right.Data[i]);
    /// <summary>
    /// Отношение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator /(double left, NDTensor right) =>
        ElementWise(right, i => left / right.Data[i]);
    /// <summary>
    /// Отношение
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator /(NDTensor left, double right) =>
        ElementWise(left, i => left.Data[i] / right);
    /// <summary>
    /// Остаток от деления
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator %(NDTensor left, NDTensor right) =>
        ElementWise(SameShape(left, right), i => left.Data[i] % right.Data[i]);
    /// <summary>
    /// Остаток от деления
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator %(double left, NDTensor right) =>
        ElementWise(right, i => left % right.Data[i]);
    /// <summary>
    /// Остаток от деления
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static NDTensor operator %(NDTensor left, double right) =>
        ElementWise(left, i => left.Data[i] % right);
    #endregion

    #region Методы
    /// <summary>
    /// Преобразование в вектор
    /// </summary>
    public Vector ToVector()
    {
        return new Vector(Data);
    }
    /// <summary>
    /// Перевод в тензор
    /// </summary>
    public Tensor ToTensor()
    {
        if (Shape.Rank > 3)
            throw new Exception("Тензор не может быть преобразован в переменную нейросети, т.к. его ранг больше 3х");

        Shape3D shape = Shape3D.FromShape(Shape);
        Tensor tensor = new Tensor(shape);
        tensor.Data = Data;
        return tensor;
    }
    /// <summary>
    /// Переводит в матрицу
    /// </summary>
    public Matrix ToMatrix()
    {
        if (Shape.Rank > 2)
            throw new Exception("Невозможно представить матрицей, ранг > 2");

        Matrix matrix = Shape.Rank == 2 ? new Matrix(Shape[0], Shape[1]) : new Matrix(Shape[0], 1);
        matrix.Data = Data;
        return matrix;
    }
    /// <summary>
    /// Изменение формы
    /// </summary>
    /// <param name="new_shape">Новая форма</param>
    public NDTensor Reshape(Shape new_shape)
    {
        if (new_shape.Count != Shape.Count)
            throw new Exception("Невозможно выполнить изменение формы, число элементов старой формы не равно числу элементов новой");

        NDTensor nDTensor = new NDTensor(new_shape);
        nDTensor.Data = Data;
        return nDTensor;
    }
    #endregion

    #region Статические методы

    /// <summary>
    /// Создание тензора из алгебраической структуры
    /// </summary>
    public static NDTensor FromIAlgStruct(IAlgebraicStructure<double> alg_structure)
    {
        var ret = new NDTensor(alg_structure.Shape);
        ret.Data = alg_structure.Data;
        return ret;
    }


    #endregion

    #region Технические методы

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is NDTensor nD)
        {
            return nD == this;
        }
        else
        {
            return false;
        }
    }
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public bool Equals(NDTensor other)
    {
        return this == other;
    }

    /// <summary>
    /// Получение хэшкода тензора
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
    #endregion

    #region Сериализация

    #region Сохранение
    /// <summary>
    /// Сохранениеs NDTensor to file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path) => SafeSerializer.SaveBytes(path, GetBytes());

    /// <summary>
    /// Saves NDTensor to stream
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream) => SafeSerializer.SaveBytes(stream, GetBytes());
    /// <summary>
    /// Represents NDTensor as an array of bytes
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        return InMemoryDataStream.Create().Write(KeyWords.NDTensor).Write(Shape.GetDataCopy()).Write(Data).AsByteArray();
    }
    #endregion

    #region Загрузка
    /// <summary>
    /// Loads NDTensor from file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <returns></returns>
    public static NDTensor Load(string path) => FromBytes(SafeSerializer.LoadBytes(path));

    /// <summary>
    /// Loads NDTensor from stream
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static NDTensor Load(Stream stream) => FromBytes(SafeSerializer.LoadBytes(stream));
    /// <summary>
    /// Initialize NDTensor form an array of bytes
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static NDTensor FromBytes(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return FromDataStream(InMemoryDataStream.FromByteArray(data));
    }
    /// <summary>
    /// Initialize NDTensor form a data stream
    /// </summary>
    /// <param name="dataStream"></param>
    /// <returns></returns>
    public static NDTensor FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        _ = dataStream.SkipIfEqual(KeyWords.NDTensor).ReadInts(out int[] shapeVals).ReadDoubles(out double[] tData);
        NDTensor tensor = new NDTensor(shapeVals)
        {
            Data = tData
        };
        return tensor;
    }
    #endregion

    #endregion

    #region Приватные методы
    /// <summary>
    /// Поэлементный проход — единственный цикл арифметики NDTensor. Результат имеет форму
    /// <paramref name="shapeSource"/>, его i-й элемент равен <paramref name="element"/>(i).
    /// Число шагов — длина данных результата.
    /// </summary>
    private static NDTensor ElementWise(NDTensor shapeSource, Func<int, double> element)
    {
        ArgumentNullException.ThrowIfNull(shapeSource);

        var result = new NDTensor(shapeSource.Shape);
        double[] data = result.Data;

        for (int i = 0; i < data.Length; i++)
            data[i] = element(i);

        return result;
    }

    /// <summary>
    /// Проверяет операнды поэлементной операции двух тензоров: оба заданы и одной формы.
    /// </summary>
    /// <returns>Левый операнд — по нему строится форма результата.</returns>
    private static NDTensor SameShape(NDTensor left, NDTensor right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Shape != right.Shape)
            throw new ArgumentException($"Поэлементная операция над тензорами разной формы: {left.Shape} и {right.Shape}.", nameof(right));

        return left;
    }

    private int GetIndex(int[] indexes)
    {
        int ind = indexes[0];

        for (int i = 0; i < indexes.Length; i++)
            ind += indexes[i + 1] * _volumes[i];

        return ind;
    }
    #endregion


}
