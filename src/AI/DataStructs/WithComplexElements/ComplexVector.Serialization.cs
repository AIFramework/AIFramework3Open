using AI.DataStructs.Shapes;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

public partial class ComplexVector
{
    #region Сериализация

    #region Сохранение
    /// <summary>
    /// Сохранить в файл
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path) => SafeSerializer.SaveBytes(path, GetBytes());

    /// <summary>
    /// Сохранение в поток
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream) => SafeSerializer.SaveBytes(stream, GetBytes());
    /// <summary>
    /// Представить в виде массива байт
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        return InMemoryDataStream.Create().Write(KeyWords.ComplexVector).Write(RealVector).WriteOnlyContent(ImaginaryVector).AsByteArray();
    }
    #endregion

    #region Загрузка
    /// <summary>
    /// Загрузить из файла
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <returns></returns>
    public static ComplexVector Load(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File was not found", path);
        }

        return FromBytes(SafeSerializer.LoadBytes(path));
    }
    /// <summary>
    /// Загрузить из потока
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static ComplexVector Load(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        return FromBytes(SafeSerializer.LoadBytes(stream));
    }
    /// <summary>
    /// Инициализировать массивом байт
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ComplexVector FromBytes(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return FromDataStream(new InMemoryDataStream(data));
    }
    /// <summary>
    /// Инициализировать потоком данных
    /// </summary>
    /// <param name="dataStream"></param>
    /// <returns></returns>
    public static ComplexVector FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        int length = dataStream.SkipIfEqual(KeyWords.ComplexVector).ReadInt();

        double[] reals = dataStream.ReadDoubles(length);
        double[] imgs = dataStream.ReadDoubles(length);

        return new ComplexVector(reals, imgs);
    }
    #endregion

    #endregion
}
