using AI.Charts.ChartElements;
using AI.Charts.Data;
using AI.Charts.Forms;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace AI.Charts.WinForms;

public partial class ChartVisual
{
    private int stepsMouse = 0;

    // Прокрутка мыши
    private void MouseScale(double scale, int xC, int yC)
    {
        try
        {

            if (IsScale)
            {
                double
                   xV = GetValueX(xC),
                   yV = GetValueY(yC);

                double lenX = 0;
                double lenY = 0;
                lenX = MaxX() - MinX();
                lenY = MaxY() - MinY();
                double
                    newMaxX = MaxX() + (scale * lenX / 2),
                    newMinX = MinX() - (scale * lenX / 2),
                    newMaxY,
                    newMinY;



                if (IsLogScale)
                {
                    newMinY = MinY() - (scale * lenY / 10);
                    newMaxY = MaxY() + (scale * lenY / 10);
                    newMinY = newMinY > 0 ? newMinY : 1e-200;

                }
                else
                {
                    newMinY = MinY() - (scale * lenY);
                    newMaxY = MaxY() + (scale * lenY);
                }

                Vector vstep = chartElements[0].Data.GetX();
                double step = vstep[1] - vstep[0];

                if (Math.Abs(newMaxX - newMinX) > step && ((newMaxY - newMinY) > 0))
                {
                    // Масштабирование по оси X
                    if (ModifierKeys == Keys.Shift)
                    {
                        SetScaleX(newMinX, newMaxX);
                        if (stepsMouse % 4 == 0)
                        {
                            Rec();
                        }

                        stepsMouse++;
                    }
                    // Масштабирование по оси Y
                    else if (ModifierKeys == Keys.Control)
                    {
                        SetScaleY(newMinY, newMaxY);
                    }

                    // Масштабирование по оси X и Y
                    else
                    {
                        SetScale(newMinX, newMaxX, newMinY, newMaxY);
                        if (stepsMouse % 2 == 0)
                        {
                            Rec();
                        }

                        stepsMouse++;
                    }


                }
            }
        }
        catch { }

    }

    /// <summary>
    /// Масштабирование по умолчанию
    /// </summary>
    public void AutoScale()
    {
        ScaleData scale = chartElements.GetEnumerator().GetScaleData();
        double xMin = scale.MinX, xMax = scale.MaxX, yMin = scale.MinY, yMax = scale.MaxY, yMin2, yMax2;

        if (IsLogScale)
        {
            if (yMin == 0)
            {
                throw new Exception("При использовании логарифмического масштаба, значение 0 не допустимо");
            }

            if (yMin < 0)
            {
                throw new Exception("При использовании логарифмического масштаба, значения ниже нуля не допустимы");
            }
        }
        double dY = Math.Abs(yMax - yMin);
        yMin2 = yMin - (0.2 * dY);
        yMax2 = yMax + (dY * 0.2);


        if (IsLogScale)
        {
            yMax2 = (yMax2 > 0) ? yMax : 1e-200;
            yMin2 = (yMin2 > 0) ? yMin2 : 1e-200;
        }

        if (yMin2 == yMax2)
        {
            yMax2 = 1;
        }

        SetScale(xMin, xMax, yMin2, yMax2);
        SetFormat();
        Rec();
    }





    private void Rec()
    {
        double min = MinX(), max = MaxX();

        foreach (IChartElement item in chartElements)
        {
            item.Recalc(min, max);
        }
    }


    // Установка масштаба
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetScale(double xMin, double xMax, double yMin, double yMax)
    {
        SetScaleX(xMin, xMax);
        SetScaleY(yMin, yMax);
        SetFormat();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetScaleX(double xMin, double xMax)
    {
        void Apply()
        {
            _axisXMin = xMin;
            _axisXMax = xMax;
            skChart.Invalidate();
        }

        if (skChart.InvokeRequired)
        {
            _ = skChart.Invoke((MethodInvoker)Apply);
        }
        else
        {
            Apply();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetScaleY(double yMin, double yMax)
    {
        void Apply()
        {
            _axisYMin = yMin;
            _axisYMax = yMax;
            skChart.Invalidate();
        }

        if (skChart.InvokeRequired)
        {
            _ = skChart.Invoke((MethodInvoker)Apply);
        }
        else
        {
            Apply();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFormat()
    {
        skChart.Invalidate();
    }



    public void SetDefaultColor() 
    {
        skChart.BackColor = BackColor;
        skChart.ForeColor = ForeColor;
        skChart.Invalidate();
    }

    /// <summary>
    /// Изменение цветов 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SkChart_BackColorChanged(object sender, EventArgs e)
    {
        SetDefaultColor();
    }

    private void SkChart_ForeColorChanged(object sender, EventArgs e)
    {
        SetDefaultColor();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValueX(int xPosition)
    {
        ChartViewport vp = BuildViewport(skChart.Width, skChart.Height);
        return vp.PxToX(xPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValueY(int yPosition)
    {
        ChartViewport vp = BuildViewport(skChart.Width, skChart.Height);
        return vp.PxToY(yPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MaxX()
    {
        return _axisXMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MinX()
    {
        return _axisXMin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MaxY()
    {
        return _axisYMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MinY()
    {
        return _axisYMin;
    }
}
