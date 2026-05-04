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
    #region Попытки прочитать

    #region Числа
    /// <summary>
    /// Tries to read int from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadInt(out int result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(int) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(int));

        try
        {
            result = BitConverter.ToInt32(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read int from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadInt(out int result, out bool succeeded)
    {
        succeeded = TryReadInt(out result);
        return this;
    }
    /// <summary>
    /// Tries to read long from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadLong(out long result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(long) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(long));

        try
        {
            result = BitConverter.ToInt64(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read long from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadLong(out long result, out bool succeeded)
    {
        succeeded = TryReadLong(out result);
        return this;
    }
    /// <summary>
    /// Tries to read short from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadShort(out short result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(short) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(short));

        try
        {
            result = BitConverter.ToInt16(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read short from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadShort(out short result, out bool succeeded)
    {
        succeeded = TryReadShort(out result);
        return this;
    }
    /// <summary>
    /// Tries to read byte from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadByte(out byte result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(byte) > _data.Length)
        {
            result = default;
            return false;
        }

        byte[] bytes = ReadInternal(sizeof(byte));

        result = bytes[0];
        return true;
    }
    /// <summary>
    /// Tries to read byte from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadByte(out byte result, out bool succeeded)
    {
        succeeded = TryReadByte(out result);
        return this;
    }
    /// <summary>
    /// Tries to read double from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadDouble(out double result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(double) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(double));

        try
        {
            result = BitConverter.ToDouble(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read double from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadDouble(out double result, out bool succeeded)
    {
        succeeded = TryReadDouble(out result);
        return this;
    }
    /// <summary>
    /// Tries to read float from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadFloat(out float result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(float) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(float));

        try
        {
            result = BitConverter.ToSingle(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read float from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadFloat(out float result, out bool succeeded)
    {
        succeeded = TryReadFloat(out result);
        return this;
    }
    #endregion

    #region Строки
    /// <summary>
    /// Tries to read string in utf-8 encoding from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadString(out string result)
    {
        return TryReadString(out result, Encoding.UTF8);
    }
    /// <summary>
    /// Tries to read string in utf-8 encoding from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadString(out string result, out bool succeeded)
    {
        succeeded = TryReadString(out result);
        return this;
    }
    /// <summary>
    /// Tries to read string in custom encoding from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public bool TryReadString(out string result, Encoding encoding)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(int) > _data.Length - 1)
        {
            result = string.Empty;
            return false;
        }

        int prevPos = _position;
        int length = ReadInt();

        if (length <= 0 || _position >= _data.Length - 1 || _position + length > _data.Length - 1)
        {
            _position = prevPos;
            result = string.Empty;
            return false;
        }

        try
        {
            result = encoding.GetString(ReadInternal(length));
            return true;
        }
        catch
        {
            _position = prevPos;
            result = string.Empty;
            return false;
        }
    }
    /// <summary>
    /// Tries to read string custom encoding from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="encoding"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadString(out string result, Encoding encoding, out bool succeeded)
    {
        succeeded = TryReadString(out result, encoding);
        return this;
    }
    #endregion

    /// <summary>
    /// Tries to read char from the stream. Returns if operation succeeded
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TryReadChar(out char result)
    {
        if (IsForWriting || IsZipped || IsEncrypted || _position >= _data.Length - 1 || _position + sizeof(char) > _data.Length)
        {
            result = default;
            return false;
        }

        int prevPos = _position;
        byte[] bytes = ReadInternal(sizeof(char));

        try
        {
            result = BitConverter.ToChar(bytes, 0);
            return true;
        }
        catch
        {
            _position = prevPos;
            result = default;
            return false;
        }
    }
    /// <summary>
    /// Tries to read char from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="succeeded"></param>
    /// <returns></returns>
    public InMemoryDataStream TryReadChar(out char result, out bool succeeded)
    {
        succeeded = TryReadChar(out result);
        return this;
    }

    #endregion

    #region Пропуски
    /// <summary>
    /// Skip bytes of count equal to next int in the stream
    /// </summary>
    /// <returns></returns>
    public InMemoryDataStream Skip()
    {
        _ = ReadInternal(ReadInt());
        return this;
    }
    /// <summary>
    /// Skip given count of bytes
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public InMemoryDataStream Skip(int count)
    {
        if (_position >= _data.Length - 1)
        {
            throw new InvalidOperationException("The end of stream was reached");
        }

        if (IsForWriting)
        {
            throw new InvalidOperationException("Stream is opened for writing");
        }

        if (IsZipped)
        {
            throw new InvalidOperationException("Data is zipped");
        }

        if (IsEncrypted)
        {
            throw new InvalidOperationException("Data is encrypted");
        }

        _position += count;
        return this;
    }
    /// <summary>
    /// Skips next int in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(int value)
    {
        int next = ReadInt();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    /// <summary>
    /// Skips next long in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(long value)
    {
        long next = ReadLong();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    /// <summary>
    /// Skips next short in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(short value)
    {
        short next = ReadShort();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    /// <summary>
    /// Skips next double in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(double value)
    {
        double next = ReadDouble();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    /// <summary>
    /// Skips next float in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(float value)
    {
        float next = ReadFloat();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    /// <summary>
    /// Skips next char in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(char value)
    {
        char next = ReadChar();

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }

    /// <summary>
    /// Skips next string in utf-8 encoding in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(string value)
    {
        return SkipIfEqual(value, Encoding.UTF8);
    }
    /// <summary>
    /// Skips next string in custom encoding in the stream if the value is equal to given
    /// </summary>
    /// <param name="value"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public InMemoryDataStream SkipIfEqual(string value, Encoding encoding)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        string next = ReadString(encoding);

        if (next != value)
        {
            throw new InvalidOperationException($"Next value in the stream is \"{next}\", but expected \"{value}\"");
        }

        return this;
    }
    #endregion

}
