using System;
using System.IO;

namespace AI.DataStructs.Algebraic;

public partial class Vector
{
    #region Сериализация

    #region Сохранение

    /// <summary>
    /// Сохранить в файл (формат AIFW_V1 с SHA-256)
    /// </summary>
    public void Save(string path) => SafeSerializer.SaveBytes(path, GetBytes());

    /// <summary>
    /// Сохранить в поток (формат AIFW_V1 с SHA-256)
    /// </summary>
    public void Save(Stream stream) => SafeSerializer.SaveBytes(stream, GetBytes());

    /// <summary>
    /// Сохранить в текстовый файл
    /// </summary>
    public void SaveAsText(string path) => File.WriteAllText(path, ToString());

    /// <summary>
    /// Представить в виде массива байт (InMemoryDataStream, без SHA-обёртки)
    /// </summary>
    public byte[] GetBytes()
        => InMemoryDataStream.Create().Write(KeyWords.Vector).Write(ToArray()).AsByteArray();

    /// <summary>
    /// Сохранение в бинарный файл как массив double (raw, без заголовка)
    /// </summary>
    public static void SaveAsBinary(string path, Vector vect)
    {
        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        for (int i = 0; i < vect.Count; i++)
            fs.Write(BitConverter.GetBytes(vect[i]), 0, 8);
    }

    #endregion

    #region Загрузка

    /// <summary>
    /// Загрузить из файла (формат AIFW_V1 с SHA-256)
    /// </summary>
    public static Vector Load(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("File was not found", path);
        return FromBytes(SafeSerializer.LoadBytes(path));
    }

    /// <summary>
    /// Загрузить из потока (формат AIFW_V1 с SHA-256)
    /// </summary>
    public static Vector Load(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return FromBytes(SafeSerializer.LoadBytes(stream));
    }

    /// <summary>
    /// Загрузить из текстового файла
    /// </summary>
    public static Vector LoadAsText(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Загрузить из бинарного файла как массив double (raw, без заголовка)
    /// </summary>
    public static Vector LoadAsBinary(string path)
    {
        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        int len = (int)(fs.Length / 8);
        BinaryReader br = new BinaryReader(fs);
        Vector vect = new Vector(len);
        for (int i = 0; i < len; i++)
            vect[i] = br.ReadDouble();
        return vect;
    }

    /// <summary>
    /// Инициализировать из массива байт (InMemoryDataStream)
    /// </summary>
    public static Vector FromBytes(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return FromDataStream(new InMemoryDataStream(data));
    }

    /// <summary>
    /// Инициализировать из потока данных
    /// </summary>
    public static Vector FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null) throw new ArgumentNullException(nameof(dataStream));
        return dataStream.SkipIfEqual(KeyWords.Vector).ReadDoubles();
    }

    #endregion

    #endregion
}
