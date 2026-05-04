using System;
using System.Collections.Generic;

namespace AI.Charts.ChartElements;

[Serializable]
internal static class Extention
{
    public static ScaleData GetScaleData(this IEnumerator<IChartElement> chartElements)
    {
        ScaleData scaleData = new ScaleData
        {
            MinX = double.MaxValue,
            MinY = double.MaxValue,
            MaxX = double.MinValue,
            MaxY = double.MinValue
        };

        bool any = false;
        while (chartElements.MoveNext())
        {
            IChartElement chartElement = chartElements.Current;
            if (chartElement is Base3DChart) continue;

            any = true;
            scaleData.MinX = Math.Min(scaleData.MinX, chartElement.GetXMin());
            scaleData.MinY = Math.Min(scaleData.MinY, chartElement.GetYMin());
            scaleData.MaxY = Math.Max(scaleData.MaxY, chartElement.GetYMax());
            scaleData.MaxX = Math.Max(scaleData.MaxX, chartElement.GetXMax());
        }

        if (!any)
        {
            scaleData.MinX = 0;
            scaleData.MaxX = 1;
            scaleData.MinY = 0;
            scaleData.MaxY = 1;
        }

        return scaleData;
    }
}
