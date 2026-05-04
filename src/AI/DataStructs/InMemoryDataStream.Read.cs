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
    #region Чтение

    #region Числа
    /// <summary>
    /// Reads int from the stream
    /// </summary>
    /// <returns></returns>
    public int ReadInt()
    {
        return BitConverter.ToInt32(ReadInternal(sizeof(int)), 0);
    }
    /// <summary>
    /// Reads int from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadInt(out int result)
    {
        result = ReadInt();
        return this;
    }
    /// <summary>
    /// Reads long from the stream
    /// </summary>
    /// <returns></returns>
    public long ReadLong()
    {
        return BitConverter.ToInt64(ReadInternal(sizeof(long)), 0);
    }
    /// <summary>
    /// Reads long from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadLong(out long result)
    {
        result = ReadLong();
        return this;
    }
    /// <summary>
    /// Reads short from the stream
    /// </summary>
    /// <returns></returns>
    public short ReadShort()
    {
        return BitConverter.ToInt16(ReadInternal(sizeof(short)), 0);
    }
    /// <summary>
    /// Reads short from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadShort(out short result)
    {
        result = ReadShort();
        return this;
    }
    /// <summary>
    /// Reads byte from the stream
    /// </summary>
    /// <returns></returns>
    public byte ReadByte()
    {
        return ReadInternal(1)[0];
    }
    /// <summary>
    /// Reads byte from the stream
    /// </summary>
    /// <returns></returns>
    public InMemoryDataStream ReadByte(out byte result)
    {
        result = ReadByte();
        return this;
    }
    /// <summary>
    /// Reads double from the stream
    /// </summary>
    /// <returns></returns>
    public double ReadDouble()
    {
        return BitConverter.ToDouble(ReadInternal(sizeof(double)), 0);
    }
    /// <summary>
    /// Reads double from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadDouble(out double result)
    {
        result = ReadDouble();
        return this;
    }
    /// <summary>
    /// Reads float from the stream
    /// </summary>
    /// <returns></returns>
    public float ReadFloat()
    {
        return BitConverter.ToSingle(ReadInternal(sizeof(float)), 0);
    }
    /// <summary>
    /// Reads float from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadFloat(out float result)
    {
        result = ReadFloat();
        return this;
    }
    #endregion

    #region Строки
    /// <summary>
    /// Reads string in utf-8 encoding from the stream
    /// </summary>
    /// <returns></returns>
    public string ReadString()
    {
        return ReadString(Encoding.UTF8);
    }
    /// <summary>
    /// Reads string in utf-8 encoding from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadString(out string result)
    {
        result = ReadString();
        return this;
    }
    /// <summary>
    /// Reads string in custom encoding from the stream
    /// </summary>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public string ReadString(Encoding encoding)
    {
        int len = ReadInt();
        return encoding.GetString(ReadInternal(len));
    }
    /// <summary>
    /// Reads string in custom encoding from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadString(out string result, Encoding encoding)
    {
        result = ReadString(encoding);
        return this;
    }
    #endregion

    #region Массивы
    /// <summary>
    /// Reads byte array from the stream
    /// </summary>
    /// <returns></returns>
    public byte[] ReadBytes()
    {
        return ReadInternal(ReadInt());
    }
    /// <summary>
    /// Reads byte array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadBytes(out byte[] result)
    {
        result = ReadBytes();
        return this;
    }
    /// <summary>
    /// Reads double array from the stream
    /// </summary>
    /// <returns></returns>
    public double[] ReadDoubles()
    {
        return ReadDoubles(ReadInt());
    }
    /// <summary>
    /// Reads double array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadDoubles(out double[] result)
    {
        result = ReadDoubles();
        return this;
    }
    /// <summary>
    /// Reads float array from the stream
    /// </summary>
    /// <returns></returns>
    public float[] ReadFloats()
    {
        return ReadFloats(ReadInt());
    }
    /// <summary>
    /// Reads float array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadFloats(out float[] result)
    {
        result = ReadFloats();
        return this;
    }
    /// <summary>
    /// Reads int array from the stream
    /// </summary>
    /// <returns></returns>
    public int[] ReadInts()
    {
        return ReadInts(ReadInt());
    }
    /// <summary>
    /// Reads int array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadInts(out int[] result)
    {
        result = ReadInts();
        return this;
    }
    /// <summary>
    /// Reads short array from the stream
    /// </summary>
    /// <returns></returns>
    public short[] ReadShorts()
    {
        return ReadShorts(ReadInt());
    }
    /// <summary>
    /// Reads short array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadShorts(out short[] result)
    {
        result = ReadShorts();
        return this;
    }
    /// <summary>
    /// Reads long array from the stream
    /// </summary>
    /// <returns></returns>
    public long[] ReadLongs()
    {
        return ReadLongs(ReadInt());
    }
    /// <summary>
    /// Reads long array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadLongs(out long[] result)
    {
        result = ReadLongs();
        return this;
    }
    /// <summary>
    /// Reads char array from the stream
    /// </summary>
    /// <returns></returns>
    public char[] ReadChars()
    {
        return ReadChars(ReadInt());
    }
    /// <summary>
    /// Reads char array from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadChars(out char[] result)
    {
        result = ReadChars();
        return this;
    }
    /// <summary>
    /// Reads double array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public double[] ReadDoubles(int length)
    {
        if (length == 0)
        {
            return new double[0];
        }

        byte[] arr = ReadInternal(length * sizeof(double));
        double[] vect = new double[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs);

            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadDouble();
            }
        }

        return vect;
    }
    /// <summary>
    /// Reads double array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadDoubles(int length, out double[] result)
    {
        result = ReadDoubles(length);
        return this;
    }
    /// <summary>
    /// Reads float array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public float[] ReadFloats(int length)
    {
        if (length == 0)
        {
            return new float[0];
        }

        byte[] arr = ReadInternal(length * sizeof(float));
        float[] vect = new float[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs);

            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadSingle();
            }

        }

        return vect;
    }
    /// <summary>
    /// Reads double array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadFloats(int length, out float[] result)
    {
        result = ReadFloats(length);
        return this;
    }
    /// <summary>
    /// Reads int array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public int[] ReadInts(int length)
    {
        if (length == 0)
        {
            return new int[0];
        }

        byte[] arr = ReadInternal(length * sizeof(int));
        int[] vect = new int[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs);

            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadInt32();
            }

        }

        return vect;
    }
    /// <summary>
    /// Reads int array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadInts(int length, out int[] result)
    {
        result = ReadInts(length);
        return this;
    }
    /// <summary>
    /// Reads long array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public long[] ReadLongs(int length)
    {
        if (length == 0)
        {
            return new long[0];
        }

        byte[] arr = ReadInternal(length * sizeof(long));
        long[] vect = new long[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs);

            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadInt64();
            }

        }

        return vect;
    }
    /// <summary>
    /// Reads long array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadLongs(int length, out long[] result)
    {
        result = ReadLongs(length);
        return this;
    }
    /// <summary>
    /// Reads short array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public short[] ReadShorts(int length)
    {
        if (length == 0)
        {
            return new short[0];
        }

        byte[] arr = ReadInternal(length * sizeof(short));
        short[] vect = new short[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs);

            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadInt16();
            }

        }

        return vect;
    }
    /// <summary>
    /// Reads short array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadShorts(int length, out short[] result)
    {
        result = ReadShorts(length);
        return this;
    }
    /// <summary>
    /// Reads char array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public char[] ReadChars(int length)
    {
        if (length == 0)
        {
            return new char[0];
        }

        byte[] arr = ReadInternal(length * sizeof(char));
        char[] vect = new char[length];

        using (MemoryStream fs = new MemoryStream(arr, true))
        {
            BinaryReader br = new BinaryReader(fs, Encoding.Unicode);


            for (int i = 0; i < length; i++)
            {
                vect[i] = br.ReadChar();
            }

        }

        return vect;
    }
    /// <summary>
    /// Reads char array of a given length from the stream
    /// </summary>
    /// <param name="length"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadChars(int length, out char[] result)
    {
        result = ReadChars(length);
        return this;
    }
    #endregion

    /// <summary>
    /// Reads char from the stream
    /// </summary>
    /// <returns></returns>
    public char ReadChar()
    {
        return BitConverter.ToChar(ReadInternal(sizeof(char)), 0);
    }
    /// <summary>
    /// Reads char from the stream
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public InMemoryDataStream ReadChar(out char result)
    {
        result = ReadChar();
        return this;
    }

    #endregion

}
