using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace AI.DataStructs;

/// <summary>
/// Class for simple IO operations
/// </summary>
[Serializable]
[DebuggerDisplay("Length = {_data.Length}")]
public partial class InMemoryDataStream
{
    #region Поля и свойства
    private byte[] _data;
    private int _position = 0;

    /// <summary>
    /// Tells if data in the stream is zipped
    /// </summary>
    public bool IsZipped { get; private set; } = false;
    /// <summary>
    /// Tells if data in the stream is encrypted
    /// </summary>
    public bool IsEncrypted { get; private set; } = false;
    /// <summary>
    /// Tells if stream is opened for reading
    /// </summary>
    public bool IsForReading { get; private set; }
    /// <summary>
    /// Tells if stream is opened for writing
    /// </summary>
    public bool IsForWriting { get; private set; }

    /// <summary>
    /// AES algorithm initialization vector
    /// </summary>
    public static byte[] IV { get; set; } = { 0, 32, 27, 12, 13, 91, 1, 141, 200, 210, 211, 212, 213, 214, 115, 16 };
    #endregion

    #region Конструкторы
    /// <summary>
    /// Creates DataStream for writing data
    /// </summary>
    public InMemoryDataStream()
    {
        IsForReading = false;
        IsForWriting = true;
        _data = new byte[0];
    }
    /// <summary>
    /// Creates DataStream for reading data from file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    public InMemoryDataStream(string path, bool isEncrypted = false, bool isZipped = false)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("File does not exist", path);

        IsEncrypted = isEncrypted;
        IsZipped = isZipped;
        IsForWriting = false;
        IsForReading = true;

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using MemoryStream ms = new MemoryStream();
        fs.CopyTo(ms);
        _data = ms.ToArray();
    }
    /// <summary>
    /// Creates DataStream for reading data from byte array
    /// </summary>
    /// <param name="data"></param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    public InMemoryDataStream(byte[] data, bool isEncrypted = false, bool isZipped = false)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length == 0)
            throw new ArgumentException("Data is empty", nameof(data));

        IsEncrypted = isEncrypted;
        IsZipped = isZipped;
        IsForWriting = false;
        IsForReading = true;

        _data = data;
    }
    /// <summary>
    /// Creates DataStream for reading data from System.IO.Stream
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    public InMemoryDataStream(Stream stream, bool isEncrypted = false, bool isZipped = false)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        IsEncrypted = isEncrypted;
        IsZipped = isZipped;
        IsForWriting = false;
        IsForReading = true;

        using MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        _data = ms.ToArray();
    }
    #endregion

}
