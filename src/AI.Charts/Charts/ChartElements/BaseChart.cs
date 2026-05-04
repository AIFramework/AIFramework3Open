using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using System;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class BaseChart : IChartElement
{
    public string Name { get; protected set; }
    public IData Data => data;
    protected IData data;
    public SKColor ElementColor { get; protected set; } = SKColors.Black;
    public int BorderWidth { get; protected set; } = 2;
    public ChartLayoutKind LayoutKind { get; protected set; } = ChartLayoutKind.Cartesian;

    protected Vector drawX;
    protected Vector drawY;

    protected BaseChart(string name)
    {
        Name = name ?? string.Empty;
    }

    public virtual void SetColor(SKColor color)
    {
        ElementColor = color;
    }

    public virtual void SetWidth(int width)
    {
        BorderWidth = width;
    }

    public virtual void LoadData(Vector x, Vector y)
    {
        data = new VectorBasedData();
        data.LoadData(x, y);
    }

    public void LoadData(IData d)
    {
        LoadData(d.GetX(), d.GetY());
    }

    public virtual void Recalc(double min, double max)
    {
        if (data == null)
        {
            return;
        }

        drawX = null;
        drawY = null;

        Vector xv = data.GetX();
        int n = xv.Count;
        if (n == 0)
        {
            return;
        }

        int minI = n;
        for (int i = 0; i < n; i++)
        {
            if (xv[i] >= min)
            {
                minI = i;
                break;
            }
        }

        int maxI = -1;
        for (int i = n - 1; i >= 0; i--)
        {
            if (xv[i] <= max)
            {
                maxI = i;
                break;
            }
        }

        if (minI >= n || maxI < 0 || minI > maxI)
        {
            drawX = new Vector();
            drawY = new Vector();
            return;
        }

        if (minI > 0)
        {
            minI--;
        }

        if (maxI < n - 1)
        {
            maxI++;
        }

        Vector xN = data.GetRegionX(minI, maxI);
        Vector yN = data.GetRegionY(minI, maxI);

        Tuple<Vector, Vector> dat = ReducMethod(xN, yN);
        xN = dat.Item1;
        yN = dat.Item2;
        drawX = xN;
        drawY = yN;
    }

    public virtual Tuple<Vector, Vector> ReducMethod(Vector xN, Vector yN)
    {
        return DataMethods.ReducDataPlot(xN, yN);
    }

    public virtual void Draw(SKCanvas canvas, ChartViewport vp)
    {
    }

    public double GetXMin()
    {
        return data.MinX();
    }

    public double GetXMax()
    {
        return data.MaxX();
    }

    public double GetYMin()
    {
        return data.MinY();
    }

    public double GetYMax()
    {
        return data.MaxY();
    }

}
