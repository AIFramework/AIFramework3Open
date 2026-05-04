using AI.DataStructs.Algebraic;
using AI.DataStructs.Shapes;
using AI.DSP.DSPCore;
using AI.Extensions;
using System;
using System.IO;
using Complex = System.Numerics.Complex;
using System.Runtime.CompilerServices;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

public partial class ComplexMatrix
{
    #region Сериализация

    #region Сохранение
    /// <summary>
    /// Сохранениеs matrix to file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path)
    {
        using (var stream = File.Create(path))
        {
            Save(stream);
        }
    }
    /// <summary>
    /// Сохранениеs matrix to stream
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream)
    {
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Height);
            writer.Write(Width);
            for (int i = 0; i < Data.Length; i++)
            {
                writer.Write(Data[i].Real);
                writer.Write(Data[i].Imaginary);
            }
        }
    }
    /// <summary>
    /// Представить в виде массива байт
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        return InMemoryDataStream.Create().Write(KeyWords.ComplexMatrix).Write(RealMatrix).Write(ImaginaryMatrix).AsByteArray();
    }
    #endregion

    #region Загрузка
    /// <summary>
    /// Loads matrix from file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <returns></returns>
    public static ComplexMatrix Load(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File was not found", path);
        }

        using (var stream = File.OpenRead(path))
        {
            return Load(stream);
        }
    }
    /// <summary>
    /// Loads matrix from stream
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static ComplexMatrix Load(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using (var reader = new BinaryReader(stream))
        {
            int height = reader.ReadInt32();
            int width = reader.ReadInt32();
            var matrix = new ComplexMatrix(height, width);
            for (int i = 0; i < matrix.Data.Length; i++)
            {
                double real = reader.ReadDouble();
                double imag = reader.ReadDouble();
                matrix.Data[i] = new Complex(real, imag);
            }
            return matrix;
        }
    }
    /// <summary>
    /// Initializes matrix from byte array
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ComplexMatrix FromBytes(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return FromDataStream(new InMemoryDataStream(data));
    }
    /// <summary>
    /// Initilizes matrix from data stream
    /// </summary>
    /// <param name="dataStream"></param>
    /// <returns></returns>
    public static ComplexMatrix FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        _ = dataStream.SkipIfEqual(KeyWords.ComplexMatrix).ReadMatrix(out Matrix real).ReadMatrix(out Matrix imaginary);

        return new ComplexMatrix(real, imaginary);
    }
    #endregion

    #endregion
}
