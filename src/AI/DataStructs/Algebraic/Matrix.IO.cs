using AI.DataStructs.Shapes;
using AI.Extensions;
using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

public partial class Matrix
{
    #region РўРµС…РЅРёС‡РµСЃРєРёРµ РјРµС‚РѕРґС‹
    /// <summary>
    /// РџСЂРµРѕР±СЂР°Р·РѕРІР°РЅРёРµ РјР°С‚СЂРёС†С‹ РІ СЃС‚СЂРѕРєСѓ
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return ToString(AISettings.GetProvider());
    }

    /// <summary>
    /// РџСЂРµРѕР±СЂР°Р·РѕРІР°РЅРёРµ РјР°С‚СЂРёС†С‹ РІ СЃС‚СЂРѕРєСѓ
    /// </summary>
    public string ToString(NumberFormatInfo provider)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < Height; i++)
        {
            _ = sb.Append("[ ");

            for (int j = 0; j < Width; j++)
            {
                _ = sb.AppendFormat(provider, "{0,8:F4} ", this[i, j]);
                _ = sb.Append("  ");
            }

            sb.Length--;
            _ = sb.AppendLine("]");
        }

        sb.Length -= Environment.NewLine.Length;
        return sb.ToString();
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂРєР° СЂР°РІРµРЅСЃС‚РІР°
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is Matrix matrix)
        {
            return matrix == this;
        }
        else
        {
            return false;
        }
    }
    /// <summary>
    /// РџСЂРѕРІРµСЂРєР° СЂР°РІРµРЅСЃС‚РІР°
    /// </summary>
    public bool Equals(Matrix other)
    {
        return this == other;
    }
    /// <summary>
    /// РџРѕР»СѓС‡РµРЅРёРµ С…СЌС€Р°
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ((Vector)Data).GetHashCode();
            hash = (hash * 13) + Height;
            hash = (hash * 13) + Width;
            return hash;
        }
    }
    #endregion

    #region РЎРµСЂРёР°Р»РёР·Р°С†РёСЏ

    #region РЎРѕС…СЂР°РЅРµРЅРёРµ
    /// <summary>
    /// РЎРѕС…СЂР°РЅРµРЅРёРµ РјР°С‚СЂРёС†С‹ РІ С„Р°Р№Р»
    /// </summary>
    /// <param name="path">РџСѓС‚СЊ РґРѕ С„Р°Р№Р»Р°</param>
    public void Save(string path) => SafeSerializer.SaveBytes(path, GetBytes());

    /// <summary>
    /// Сохранение матрицы в поток
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream) => SafeSerializer.SaveBytes(stream, GetBytes());
    /// <summary>
    /// РЎРѕС…СЂР°РЅРµРЅРёРµ РјР°С‚СЂРёС†С‹ РІ С„Р°Р№Р» РІ С‚РµРєСЃС‚РѕРІРѕРј С„РѕСЂРјР°С‚Рµ
    /// </summary>
    /// <param name="path">РџСѓС‚СЊ РґРѕ С„Р°Р№Р»Р°</param>
    public void SaveAsText(string path)
    {
        File.WriteAllText(path, ToString());
    }
    /// <summary>
    /// РџСЂРµРґСЃС‚Р°РІР»РµРЅРёРµ РјР°СЃСЃРёРІРѕРј Р±Р°Р№С‚
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        return InMemoryDataStream.Create().Write(KeyWords.Matrix).Write((byte)DataType).Write(Height).Write(Width).Write(Data).AsByteArray();
    }
    #endregion

    #region Р—Р°РіСЂСѓР·РєР°
    /// <summary>
    /// Р—Р°РіСЂСѓР·РєР° РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <param name="path">РџСѓС‚СЊ РґРѕ С„Р°Р№Р»Р°</param>
    /// <returns></returns>
    public static Matrix Load(string path) => FromBytes(SafeSerializer.LoadBytes(path));

    /// <summary>
    /// Загрузка матрицы
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <returns></returns>
    public static Matrix Load(Stream stream) => FromBytes(SafeSerializer.LoadBytes(stream));
    /// <summary>
    /// Р—Р°РіСЂСѓР·РєР° РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <param name="path">РџСѓС‚СЊ РґРѕ С„Р°Р№Р»Р°</param>
    /// <returns></returns>
    public static Matrix LoadAsText(string path)
    {
        return Parse(File.ReadAllText(path));
    }
    /// <summary>
    /// Р—Р°РіСЂСѓР·РєР° РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Matrix FromBytes(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return FromDataStream(InMemoryDataStream.FromByteArray(data));
    }
    /// <summary>
    /// Р—Р°РіСЂСѓР·РєР° РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <param name="dataStream"></param>
    /// <returns></returns>
    public static Matrix FromDataStream(InMemoryDataStream dataStream)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        _ = dataStream.SkipIfEqual(KeyWords.Matrix);
        MatrixType type = (MatrixType)dataStream.ReadByte();
        _ = dataStream.ReadInt(out int height).ReadInt(out int width).ReadDoubles(out double[] mData);
        Matrix result = new Matrix(height, width)
        {
            DataType = type,
            Data = mData
        };
        return result;
    }
    #endregion

    #endregion

    #region РџСЂРёРІР°С‚РЅС‹Рµ РјРµС‚РѕРґС‹
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int i, int j)
    {
        return (Width * i) + j;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Get(int i, int j)
    {
        return Data[GetIndex(i, j)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Set(int i, int j, double value)
    {
        Data[GetIndex(i, j)] = value;
    }
    #endregion
}

/// <summary>
/// РўРёРї РјР°С‚СЂРёС†С‹
/// </summary>
public enum MatrixType : byte
{
    /// <summary>
    /// РР·РѕР±СЂР°Р¶РµРЅРёРµ
    /// </summary>
    Image,
    /// <summary>
    /// РњР°С‚РµРјР°С‚РёС‡РµСЃРєР°СЏ СЃС‚СЂСѓРєС‚СѓСЂР°
    /// </summary>
    MatStruct
}
