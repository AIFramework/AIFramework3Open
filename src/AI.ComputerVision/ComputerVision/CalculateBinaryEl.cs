using SkiaSharp;
using System;

namespace AI.ComputerVision;

/// <summary>
/// Подсчет объектов
/// </summary>
[Serializable]
public class CalculateBinaryEl
{
    /// <summary>
    /// Изображение
    /// </summary>
    public BinaryImg img;
    private readonly bool[][,] masksE = new bool[4][,];
    private readonly bool[][,] masksI = new bool[4][,];
    private int countE = 0, countI = 0;

    /// <summary>
    /// Подсчет объектов
    /// </summary>
    public CalculateBinaryEl()
    {
    }

    /// <summary>
    /// Подсчет объектов
    /// </summary>
    /// <param name="bmp">Изображение</param>
    /// <returns>Кол-во объектов</returns>
    public int CalculateBinElements(SKBitmap bmp)
    {
        Mascs();
        img = new BinaryImg(bmp);
        int m = img.M, n = img.Count;

        countE = 0;
        countI = 0;

        for (int i = 0; i < m - 1; i++)
            for (int j = 0; j < n - 1; j++)
                Filter(j, i);

        return (int)(((countE - countI) / 4.0) + 0.999);
    }

    private void FilterI(int dx, int dy)
    {
        for (int k = 0; k < 4; k++)
        {
            bool akkum = true;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    akkum = akkum && img[dy + i, dx + j] == masksI[k][i, j];

            if (akkum) { countI++; break; }
        }
    }

    private void FilterE(int dx, int dy)
    {
        for (int k = 0; k < 4; k++)
        {
            bool akkum = true;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    akkum = akkum && img[dy + i, dx + j] == masksE[k][i, j];

            if (akkum) { countE++; break; }
        }
    }

    private void Filter(int dx, int dy)
    {
        FilterE(dx, dy);
        FilterI(dx, dy);
    }

    private void Mascs()
    {
        masksE[0] = new bool[,] { { true, true }, { true, false } };
        masksE[1] = new bool[,] { { true, true }, { false, true } };
        masksE[2] = new bool[,] { { false, true }, { true, true } };
        masksE[3] = new bool[,] { { true, false }, { true, true } };

        masksI[0] = new bool[,] { { true, false }, { false, false } };
        masksI[1] = new bool[,] { { false, true }, { false, false } };
        masksI[2] = new bool[,] { { false, false }, { true, false } };
        masksI[3] = new bool[,] { { false, false }, { false, true } };
    }
}
