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
    #region CheckIfEqual

    #region Числа
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(int n)
    {
        int prevPos = _position;
        bool tryRes = TryReadInt(out int read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(int n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(long n)
    {
        int prevPos = _position;
        bool tryRes = TryReadLong(out long read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(long n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(short n)
    {
        int prevPos = _position;
        bool tryRes = TryReadShort(out short read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(short n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(byte n)
    {
        int prevPos = _position;
        bool tryRes = TryReadByte(out byte read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(byte n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(double n)
    {
        int prevPos = _position;
        bool tryRes = TryReadDouble(out double read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(double n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public bool CheckIfEqual(float n)
    {
        int prevPos = _position;
        bool tryRes = TryReadFloat(out float read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == n;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(float n, out bool result)
    {
        result = CheckIfEqual(n);
        return this;
    }
    #endregion

    #region Строки
    /// <summary>
    /// Checks if the next string in utf-8 encoding in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public bool CheckIfEqual(string str)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        return CheckIfEqual(str, Encoding.UTF8);
    }
    /// <summary>
    /// Checks if the next string in utf-8 encoding in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(string str, out bool result)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        result = CheckIfEqual(str);
        return this;
    }
    /// <summary>
    /// Checks if the next string in custom encoding in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public bool CheckIfEqual(string str, Encoding encoding)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        int prevPos = _position;
        bool tryRes = TryReadString(out string read, encoding);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == str;
    }
    /// <summary>
    /// Checks if the next string in custom encoding in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(string str, Encoding encoding, out bool result)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        result = CheckIfEqual(str, encoding);
        return this;
    }
    #endregion

    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="ch"></param>
    /// <returns></returns>
    public bool CheckIfEqual(char ch)
    {
        int prevPos = _position;
        bool tryRes = TryReadChar(out char read);

        if (!tryRes)
        {
            return false;
        }

        _position = prevPos;
        return read == ch;
    }
    /// <summary>
    /// Checks if the next value in the stream is equal to given. Position in the stream is not changed
    /// </summary>
    /// <param name="ch"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream CheckIfEqual(char ch, out bool result)
    {
        result = CheckIfEqual(ch);
        return this;
    }

    #endregion

    #region NullIfEqual

    #region Числа
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(int n)
    {
        int prevPos = _position;
        bool success = TryReadInt(out int read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(long n)
    {
        int prevPos = _position;
        bool success = TryReadLong(out long read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(short n)
    {
        int prevPos = _position;
        bool success = TryReadShort(out short read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(byte n)
    {
        int prevPos = _position;
        bool success = TryReadByte(out byte read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(double n)
    {
        int prevPos = _position;
        bool success = TryReadDouble(out double read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(float n)
    {
        int prevPos = _position;
        bool success = TryReadFloat(out float read);

        if (!success)
        {
            return this;
        }

        if (n == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    #endregion

    #region Строки
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(string str)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        return NullIfEqual(str, Encoding.UTF8);
    }
    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(string str, Encoding encoding)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        int prevPos = _position;
        bool resOfTry = TryReadString(out string read, encoding);

        if (!resOfTry)
        {
            return this;
        }

        if (str == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }
    #endregion

    /// <summary>
    /// Returns null if the next value in the stream is equal to the given. If not, position in the stream is not changed
    /// </summary>
    /// <param name="ch"></param>
    /// <returns></returns>
    public InMemoryDataStream NullIfEqual(char ch)
    {
        int prevPos = _position;
        bool success = TryReadChar(out char read);

        if (!success)
        {
            return this;
        }

        if (ch == read)
        {
            return null;
        }

        _position = prevPos;
        return this;
    }

    #endregion

}
