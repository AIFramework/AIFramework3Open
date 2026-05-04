using AI.DataStructs.Shapes;
using AI.Extensions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

public partial class Tensor
{
    #region Сериализация

    #region Сохранение
    /// <summary>
    /// Сохранить в файл
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path) => SafeSerializer.SaveBytes(path, GetBytes());

    /// <summary>
    /// Сохранить в  поток
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream) => SafeSerializer.SaveBytes(stream, GetBytes());
    /// <summary>
    /// Представить в виде массива байт
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        return InMemoryDataStream.Create().Write(KeyWords.Tensor).Write(Height).Write(Width).Write(Depth).Write(Data).AsByteArray();
    }
    #endregion

    #region Загрузка
    /// <summary>
    /// Загрузить из файла
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <returns></returns>
    public static Tensor Load(string path) => FromBytes(SafeSerializer.LoadBytes(path));

    /// <summary>
    /// Загрузить из потока
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static Tensor Load(Stream stream) => FromBytes(SafeSerializer.LoadBytes(stream));
    /// <summary>
    /// Загрузить из массива байт
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Tensor FromBytes(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return FromDataStream(InMemoryDataStream.FromByteArray(data));
    }
    /// <summary>
    /// Загрузить из массива объекта  InMemoryDataStream
    /// </summary>
    /// <param name="dataStream"></param>
    /// <returns></returns>
    public static Tensor FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        _ = dataStream.SkipIfEqual(KeyWords.Tensor).ReadInt(out int height).ReadInt(out int width).ReadInt(out int depth).ReadDoubles(out double[] tData);
        Tensor result = new Tensor(height, width, depth)
        {
            Data = tData
        };
        return result;
    }
    #endregion

    #endregion
}
