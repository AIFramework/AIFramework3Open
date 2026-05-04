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
    #region РЎС‚Р°С‚РёС‡РµСЃРєРёРµ РјРµС‚РѕРґС‹ РёРЅРёС†РёР°Р»РёР·Р°С†РёРё
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ СЃС‚СЂРѕРєРё
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static Matrix Parse(string str)
    {
        return Parse(str, AISettings.GetProvider());
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ СЃС‚СЂРѕРєРё
    /// </summary>
    /// <param name="str"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static Matrix Parse(string str, NumberFormatInfo provider)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        string trimmed = str.Trim();

        string[] rows = trimmed.Split('\n');

        Vector[] vects = new Vector[rows.Length];

        for (int i = 0; i < rows.Length; i++)
        {
            vects[i] = Vector.Parse(rows[i].Trim('\r'), provider);
        }

        return FromVectorsAsRows(vects);
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ СЃС‚СЂРѕРєРё
    /// </summary>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(string str, out Matrix result)
    {
        return TryParse(str, out result, AISettings.GetProvider());
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ СЃС‚СЂРѕРєРё
    /// </summary>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static bool TryParse(string str, out Matrix result, NumberFormatInfo provider)
    {
        if (str == null)
        {
            result = null;
            return false;
        }

        if (provider == null)
        {
            result = null;
            return false;
        }

        string trimmed = str.Trim();
        string[] rows = trimmed.Split('\n');
        Vector[] vects = new Vector[rows.Length];
        int width = -1;

        for (int i = 0; i < rows.Length; i++)
        {
            if (!Vector.TryParse(rows[i].Trim('\r'), out Vector res, provider))
            {
                result = null;
                return false;
            }

            if (width == -1)
                width = res.Count;

            if (res.Count != width)
            {
                result = null;
                return false;
            }

            vects[i] = res;
        }

        result = FromVectorsAsRows(vects);
        return true;
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ РІРµРєС‚РѕСЂРѕРІ-СЃС‚СЂРѕРє
    /// </summary>
    /// <param name="rows">РЎС‚СЂРѕРєРё</param>
    /// <returns></returns>
    public static Matrix FromVectorsAsRows(IEnumerable<Vector> rows)
    {
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        Vector[] vectors = rows.ToArray();
        int width = vectors[0].Count;
        Matrix result = new Matrix(vectors.Length, width);

        for (int i = 0; i < vectors.Length; i++)
        {
            if (vectors[i].Count != width)
                throw new ArgumentException($"Р§РёСЃР»Рѕ СЌР»РµРјРµРЅС‚РѕРІ РІС…РѕРґРЅРѕРіРѕ РІРµРєС‚РѕСЂР° ({i}) РЅРµ СЂР°РІРЅРѕ С€РёСЂРёРЅРµ РјР°С‚СЂРёС†С‹", nameof(vectors));

            for (int j = 0; j < width; j++)
                result[i, j] = vectors[i][j];
        }

        return result;
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ РІРµРєС‚РѕСЂРѕРІ-СЃС‚РѕР»Р±С†РѕРІ
    /// </summary>
    /// <param name="colums">РЎС‚РѕР»Р±С†С‹ РјР°С‚СЂРёС†С‹</param>
    public static Matrix FromVectorsAsColumns(IEnumerable<Vector> colums)
    {
        if (colums == null)
            throw new ArgumentNullException(nameof(colums));

        Vector[] vectors = colums.ToArray();
        int height = vectors[0].Count;

        Matrix result = new Matrix(height, vectors.Length);

        for (int i = 0; i < vectors.Length; i++)
        {
            if (vectors[i].Count != height)
                throw new ArgumentException($"Р§РёСЃР»Рѕ СЌР»РµРјРµРЅС‚РѕРІ РІС…РѕРґРЅРѕРіРѕ РІРµРєС‚РѕСЂР° ({i}) РЅРµ СЂР°РІРЅРѕ РІС‹СЃРѕС‚Рµ РјР°С‚СЂРёС†С‹", nameof(vectors));

            for (int j = 0; j < height; j++)
                result[j, i] = vectors[i][j];
        }

        return result;
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ РґРІСѓС…РјРµСЂРЅРѕРіРѕ РјР°СЃСЃРёРІР° СЃС‚СЂРѕРє
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public static Matrix FromStrings(string[,] arr)
    {
        return FromStrings(arr, AISettings.GetProvider());
    }
    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РјР°С‚СЂРёС†С‹ СЃ РїРѕРјРѕС‰СЊСЋ РґРІСѓС…РјРµСЂРЅРѕРіРѕ РјР°СЃСЃРёРІР° СЃС‚СЂРѕРє
    /// </summary>
    /// <param name="arr"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static Matrix FromStrings(string[,] arr, NumberFormatInfo provider)
    {
        if (arr == null)
        {
            throw new ArgumentNullException(nameof(arr));
        }

        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        Matrix result = new Matrix(arr.GetLength(0), arr.GetLength(1));

        for (int i = 0; i < arr.GetLength(0); i++)
        {
            for (int j = 0; j < arr.GetLength(1); j++)
            {
                result[i, j] = double.Parse(arr[i, j], provider);
            }
        }

        return result;
    }
    #endregion
}
