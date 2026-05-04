using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace AI.DataStructs;

public partial class InMemoryDataStream
{
    #region Запись

    #region Числа
    /// <summary>
    /// Writes int to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(int n)
    {
        WriteInternal(BitConverter.GetBytes(n));
        return this;
    }
    /// <summary>
    /// Writes short to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(short n)
    {
        WriteInternal(BitConverter.GetBytes(n));
        return this;
    }
    /// <summary>
    /// Writes byte to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(byte n)
    {
        WriteInternal(new[] { n });
        return this;
    }
    /// <summary>
    /// Writes double to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(double n)
    {
        WriteInternal(BitConverter.GetBytes(n));
        return this;
    }
    /// <summary>
    /// Writes float to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(float n)
    {
        WriteInternal(BitConverter.GetBytes(n));
        return this;
    }
    /// <summary>
    /// Writes long to the stream
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(long n)
    {
        WriteInternal(BitConverter.GetBytes(n));
        return this;
    }
    #endregion

    #region Строки
    /// <summary>
    /// Writes string in utf-8 encoding to the stream
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(string str)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        _ = Write(str, Encoding.UTF8);
        return this;
    }
    /// <summary>
    /// Writes string in utf-8 encoding to the stream
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(string str)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        _ = WriteOnlyContent(str, Encoding.UTF8);
        return this;
    }
    /// <summary>
    /// Writes string in custom encoding to the stream
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(string str, Encoding encoding)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        byte[] arr = encoding.GetBytes(str);
        _ = Write(arr.Length);
        WriteInternal(arr);
        return this;
    }
    /// <summary>
    /// Writes string in custom encoding to the stream
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(string str, Encoding encoding)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        byte[] arr = encoding.GetBytes(str);
        WriteInternal(arr);
        return this;
    }
    #endregion

    #region Массивы
    /// <summary>
    /// Writes byte array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(byte[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        WriteInternal(arr);
        return this;
    }
    /// <summary>
    /// Writes double array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(double[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes float array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(float[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes int array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(int[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes short array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(short[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes long array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(long[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes char array to the stream
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(char[] arr)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        _ = Write(arr.Length);
        _ = WriteOnlyContent(arr);
        return this;
    }
    /// <summary>
    /// Writes double array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(double[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(8 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    /// <summary>
    /// Writes float array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(float[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(4 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    /// <summary>
    /// Writes int array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(int[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(4 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    /// <summary>
    /// Writes long array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(long[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(8 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    /// <summary>
    /// Writes short array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(short[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(2 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    /// <summary>
    /// Writes char array content to the stream
    /// </summary>
    /// <param name="dat"></param>
    /// <returns></returns>
    public InMemoryDataStream WriteOnlyContent(char[] dat)
    {
        if (dat == null)
        {
            throw new ArgumentNullException(nameof(dat));
        }

        List<byte> btsL = new List<byte>(2 * dat.Length);

        for (int i = 0; i < dat.Length; i++)
        {
            btsL.AddRange(BitConverter.GetBytes(dat[i]));
        }

        WriteInternal(btsL.ToArray());

        return this;
    }
    #endregion

    /// <summary>
    /// Writes char to the stream
    /// </summary>
    /// <param name="ch"></param>
    /// <returns></returns>
    public InMemoryDataStream Write(char ch)
    {
        WriteInternal(BitConverter.GetBytes(ch));
        return this;
    }

    #endregion

}
