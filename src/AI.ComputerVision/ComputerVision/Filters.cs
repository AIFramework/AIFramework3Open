using AI.DataStructs.Algebraic;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AI.ComputerVision;

/// <summary>
/// Filters for images.
/// Горячий путь переписан: прямой доступ к <see cref="Matrix.Data"/>, разделение
/// «центр / рамка», без лишних копий и без аллокаций в inner loop.
/// Семантика (размер выходов, паддинги, взвешивание) совпадает с предыдущей реализацией.
/// </summary>
public static class ImgFilters
{
    // Порог, ниже которого не запускаем Parallel.For — иначе оверхед планировщика съест выигрыш
    private const int ParallelRowsThreshold = 64;

    /// <summary>
    /// Spatial grayscale filter (zero-padding по краям, результат того же размера).
    /// </summary>
    public static Matrix SpatialFilter(Matrix img, Matrix filter)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        int H = img.Height, W = img.Width;
        int fH = filter.Height, fW = filter.Width;
        int hfH = fH / 2, hfW = fW / 2;

        var result = new Matrix(H, W);
        double[] src = img.Data;
        double[] dst = result.Data;
        double[] flt = filter.Data;

        // Быстрый путь для самых частых ядер 3x3
        if (fH == 3 && fW == 3)
        {
            Conv3x3(src, dst, flt, H, W);
            return result;
        }

        // Общий путь: отдельная обработка центральной области (без границ) и рамки
        int yStart = hfH;
        int yEnd = H - hfH;        // exclusive
        int xStart = hfW;
        int xEnd = W - hfW;        // exclusive

        if (yEnd > yStart && xEnd > xStart)
        {
            void BodyInner(int y)
            {
                int rowDst = y * W;
                for (int x = xStart; x < xEnd; x++)
                {
                    double sum = 0.0;
                    int fIdx = 0;
                    int baseY = y - hfH;
                    for (int fy = 0; fy < fH; fy++)
                    {
                        int rowSrc = (baseY + fy) * W + (x - hfW);
                        for (int fx = 0; fx < fW; fx++)
                            sum += flt[fIdx++] * src[rowSrc + fx];
                    }
                    dst[rowDst + x] = sum;
                }
            }

            if (yEnd - yStart >= ParallelRowsThreshold)
                Parallel.For(yStart, yEnd, BodyInner);
            else
                for (int y = yStart; y < yEnd; y++) BodyInner(y);
        }

        // Рамка — медленный путь с проверкой границ
        for (int y = 0; y < H; y++)
        {
            bool innerRow = y >= yStart && y < yEnd;
            for (int x = 0; x < W; x++)
            {
                if (innerRow && x >= xStart && x < xEnd) continue;

                double sum = 0.0;
                int fIdx = 0;
                int baseY = y - hfH;
                int baseX = x - hfW;
                for (int fy = 0; fy < fH; fy++)
                {
                    int imgY = baseY + fy;
                    if ((uint)imgY >= (uint)H) { fIdx += fW; continue; }
                    int rowSrc = imgY * W;
                    for (int fx = 0; fx < fW; fx++)
                    {
                        int imgX = baseX + fx;
                        if ((uint)imgX < (uint)W)
                            sum += flt[fIdx] * src[rowSrc + imgX];
                        fIdx++;
                    }
                }
                dst[y * W + x] = sum;
            }
        }

        return result;
    }

    // Специализация для ядра 3x3 — избавляемся от всех внутренних циклов свёртки
    private static void Conv3x3(double[] src, double[] dst, double[] k, int H, int W)
    {
        double k00 = k[0], k01 = k[1], k02 = k[2];
        double k10 = k[3], k11 = k[4], k12 = k[5];
        double k20 = k[6], k21 = k[7], k22 = k[8];

        // Центральная область: без проверок границ
        int yEnd = H - 1;
        int xEnd = W - 1;

        void Body(int y)
        {
            int rPrev = (y - 1) * W;
            int rCur = y * W;
            int rNext = (y + 1) * W;
            int rDst = rCur;
            for (int x = 1; x < xEnd; x++)
            {
                double s =
                    k00 * src[rPrev + x - 1] + k01 * src[rPrev + x] + k02 * src[rPrev + x + 1] +
                    k10 * src[rCur + x - 1] + k11 * src[rCur + x] + k12 * src[rCur + x + 1] +
                    k20 * src[rNext + x - 1] + k21 * src[rNext + x] + k22 * src[rNext + x + 1];
                dst[rDst + x] = s;
            }
        }

        if (yEnd - 1 >= ParallelRowsThreshold)
            Parallel.For(1, yEnd, Body);
        else
            for (int y = 1; y < yEnd; y++) Body(y);

        // Рамка
        BorderConv3x3(src, dst, k, H, W);
    }

    private static void BorderConv3x3(double[] src, double[] dst, double[] k, int H, int W)
    {
        for (int y = 0; y < H; y++)
        {
            // На внутренних строках считаем только крайние 2 столбца; на крайних — всю строку
            bool borderRow = (y == 0) || (y == H - 1);
            int xFrom = 0, xTo = W;
            for (int x = xFrom; x < xTo; x++)
            {
                if (!borderRow && x != 0 && x != W - 1) continue;

                double sum = 0.0;
                int fi = 0;
                for (int fy = -1; fy <= 1; fy++)
                {
                    int iy = y + fy;
                    if ((uint)iy >= (uint)H) { fi += 3; continue; }
                    int row = iy * W;
                    for (int fx = -1; fx <= 1; fx++)
                    {
                        int ix = x + fx;
                        if ((uint)ix < (uint)W)
                            sum += k[fi] * src[row + ix];
                        fi++;
                    }
                }
                dst[y * W + x] = sum;
            }
        }
    }

    /// <summary>
    /// Медианный фильтр по окну, заданному маской (взвешенные выборки).
    /// Выход размера (H - fH + 1) × (W - fW + 1).
    /// </summary>
    public static Matrix MedianFilterMask(Matrix img, Matrix filter)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        int H = img.Height - filter.Height + 1;
        int W = img.Width - filter.Width + 1;
        int fH = filter.Height, fW = filter.Width;
        int iW = img.Width;
        int winSize = fH * fW;

        var newMatr = new Matrix(H, W);
        double[] src = img.Data;
        double[] dst = newMatr.Data;
        double[] flt = filter.Data;

        void Body(int i)
        {
            Span<double> buf = winSize <= 256 ? stackalloc double[winSize] : new double[winSize];
            for (int j = 0; j < W; j++)
            {
                int k = 0;
                for (int fy = 0; fy < fH; fy++)
                {
                    int row = (i + fy) * iW + j;
                    int fRow = fy * fW;
                    for (int fx = 0; fx < fW; fx++)
                        buf[k++] = src[row + fx] * flt[fRow + fx];
                }
                dst[i * W + j] = MedianInPlace(buf);
            }
        }

        if (H >= ParallelRowsThreshold)
            Parallel.For(0, H, Body);
        else
            for (int i = 0; i < H; i++) Body(i);

        return newMatr;
    }

    /// <summary>
    /// Медианный фильтр, прямоугольное окно h × w.
    /// Выход: (H - h + 1) × (W - w) — оставлено для совместимости с прежней семантикой.
    /// </summary>
    public static Matrix MedianFilter(Matrix img, int h = 3, int w = 3)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        int H = img.Height - h + 1;
        int W = img.Width - w;                 // поведение, совпадающее со старой реализацией
        int iW = img.Width;
        int winSize = h * w;

        var newMatr = new Matrix(H, W);
        double[] src = img.Data;
        double[] dst = newMatr.Data;

        void Body(int i)
        {
            Span<double> buf = winSize <= 256 ? stackalloc double[winSize] : new double[winSize];
            for (int j = 0; j < W; j++)
            {
                int k = 0;
                for (int fy = 0; fy < h; fy++)
                {
                    int row = (i + fy) * iW + j;
                    for (int fx = 0; fx < w; fx++)
                        buf[k++] = src[row + fx];
                }
                dst[i * W + j] = MedianInPlace(buf);
            }
        }

        if (H >= ParallelRowsThreshold)
            Parallel.For(0, H, Body);
        else
            for (int i = 0; i < H; i++) Body(i);

        return newMatr;
    }

    /// <summary>
    /// Локальное СКО, результат (H - fH + 1) × (W - fW + 1).
    /// Семантика: значения предварительно взвешиваются маской (как было раньше),
    /// затем считается std по массиву взвешенных значений.
    /// </summary>
    public static Matrix StdFilter(Matrix img, Matrix filter)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        int H = img.Height - filter.Height + 1;
        int W = img.Width - filter.Width + 1;
        int fH = filter.Height, fW = filter.Width;
        int iW = img.Width;
        int winSize = fH * fW;

        var newMatr = new Matrix(H, W);
        double[] src = img.Data;
        double[] dst = newMatr.Data;
        double[] flt = filter.Data;

        void Body(int i)
        {
            for (int j = 0; j < W; j++)
            {
                double sum = 0.0;
                double sumSq = 0.0;
                for (int fy = 0; fy < fH; fy++)
                {
                    int row = (i + fy) * iW + j;
                    int fRow = fy * fW;
                    for (int fx = 0; fx < fW; fx++)
                    {
                        double v = src[row + fx] * flt[fRow + fx];
                        sum += v;
                        sumSq += v * v;
                    }
                }
                double mean = sum / winSize;
                double var = (sumSq / winSize) - (mean * mean);
                if (var < 0) var = 0; // численная защита
                dst[i * W + j] = Math.Sqrt(var);
            }
        }

        if (H >= ParallelRowsThreshold)
            Parallel.For(0, H, Body);
        else
            for (int i = 0; i < H; i++) Body(i);

        return newMatr;
    }

    /// <summary>
    /// Фильтрация произвольной функцией-агрегатом по окну w×h.
    /// Выход: (H - h + 1) × (W - w + 1).
    /// </summary>
    public static Matrix FunctionFilter(Matrix img, Func<Vector, double> func_filter, int w = 3, int h = 3)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        if (func_filter is null) throw new ArgumentNullException(nameof(func_filter));

        int H = img.Height - h + 1;
        int W = img.Width - w + 1;
        int iW = img.Width;
        int winSize = w * h;

        var newMatr = new Matrix(H, W);
        double[] src = img.Data;
        double[] dst = newMatr.Data;

        // Переиспользуем Vector-обёртку на поток: один массив и один Vector per thread.
        // Vector : List<double>, используем CollectionsMarshal.AsSpan для прямой записи во внутренний буфер.
        Parallel.For(0, H,
            localInit: () => new Vector(winSize),
            body: (i, _, buf) =>
            {
                Span<double> bd = CollectionsMarshal.AsSpan(buf);
                for (int j = 0; j < W; j++)
                {
                    int k = 0;
                    for (int fy = 0; fy < h; fy++)
                    {
                        int row = (i + fy) * iW + j;
                        for (int fx = 0; fx < w; fx++)
                            bd[k++] = src[row + fx];
                    }
                    dst[i * W + j] = func_filter(buf);
                }
                return buf;
            },
            localFinally: _ => { });

        return newMatr;
    }

    /// <summary>
    /// Медиана значений Span (in-place, O(n log n) для малых n — insertion sort быстрее, чем generic Sort).
    /// </summary>
    private static double MedianInPlace(Span<double> buf)
    {
        int n = buf.Length;
        // Для типичных окон (9, 25, 49) insertion sort быстрее Quicksort и без аллокаций
        if (n <= 64)
        {
            for (int i = 1; i < n; i++)
            {
                double v = buf[i];
                int j = i - 1;
                while (j >= 0 && buf[j] > v)
                {
                    buf[j + 1] = buf[j];
                    j--;
                }
                buf[j + 1] = v;
            }
        }
        else
        {
            buf.Sort();
        }
        return buf[n / 2];
    }
}
