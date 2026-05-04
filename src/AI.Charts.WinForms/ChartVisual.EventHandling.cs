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
    #region Масштабирование

    private bool startM = true, endM;
    private int xMouseE, xMouseB, yMouseE, yMouseB, x, y, wM, hM;
    private double xC, yC;
    private Point mouseOld = Point.Empty;
    private int dxInt = 0;

    private void LabelXY_Click(object sender, EventArgs e)
    {

    }





    //Прокрутка мыши
    private void Chart1_MouseWheel(object sender, MouseEventArgs e)
    {
        if (Is3DMode)
        {
            Camera3D.Distance -= e.Delta > 0 ? 0.15 : -0.15;
            skChart.Invalidate();
            return;
        }

        if (e.Delta > 0)
        {
            MouseScale(0.01, e.X, e.Y);
        }
        else
        {
            MouseScale(-0.01, e.X, e.Y);
        }
    }


    //Движение мыши
    private void SkChart_MouseMove(object sender, MouseEventArgs e)
    {
        if (Is3DMode && e.Button == MouseButtons.Left)
        {
            if (!_drag3D)
            {
                _drag3D = true;
                _drag3DLast = e.Location;
                return;
            }

            Camera3D.Azimuth += (e.X - _drag3DLast.X) * 0.5;
            Camera3D.Elevation += (e.Y - _drag3DLast.Y) * 0.5;
            _drag3DLast = e.Location;
            skChart.Invalidate();
            return;
        }

        try
        {
            if (chartElements.Count > 0 && HasRenderableData())
            {
                // Зажата левая кнопка мыши
                if (e.Button == MouseButtons.Left)
                {
                    // Перетаскивание графика
                    if (ModifierKeys == Keys.Control && IsMoove)
                    {
                        double
                           xV = GetValueX(e.X),
                           yV = GetValueY(e.Y),
                           maxX = MaxX(),
                           maxY = MaxY(),
                           minX = MinX(),
                           minY = MinY(),
                           xVOld = GetValueX(mouseOld.X),
                           yVOld = GetValueY(mouseOld.Y),
                           dX = xV - xVOld,
                           dY = yV - yVOld;

                        if (IsLogScale)
                        {
                            dY = (Math.Pow(10, yV) - Math.Pow(10, yVOld)) / 10;
                        }

                        double
                        newMaxX = maxX - dX,
                        newMinX = minX - dX,
                        newMaxY = maxY - dY,
                        newMinY = minY - dY;

                        if (IsLogScale)
                        {
                            newMaxY = newMaxY > 0 ? newMaxY : 1e-200;
                            newMinY = newMinY > 0 ? newMinY : 1e-200;
                        }

                        if (((newMaxX - newMinX) > 0) && ((newMaxY - newMinY) > 0))
                        {
                            SetScale(newMinX, newMaxX, newMinY, newMaxY);
                            dxInt += e.X - mouseOld.X;

                            if (Math.Abs(dxInt) > skChart.Width / 4)
                            {
                                Rec(); // Перерасчет масштаба
                                dxInt = 0;
                            }

                        }
                    }
                    // ---------------------------------------------------------------------------------------------------------------//
                    // Выделение зоны интереса
                    else
                    {

                        if (startM)
                        {
                            startM = false;
                            endM = true;

                            xMouseB = e.X;
                            yMouseB = e.Y;
                        }

                        xMouseE = e.X;
                        yMouseE = e.Y;

                        skChart.Invalidate();
                    }
                }



                mouseOld.X = e.X;
                mouseOld.Y = e.Y;


                if (HasRenderableData())
                {
                    TryMapMouseToValues(e.X, e.Y, out xC, out yC);

                    if (IsShowXY)
                    {
                        labelXY.Text = "X: " + Math.Round(xC, 6) + "  Y:" + Math.Round(yC, 6);
                    }
                    else
                    {
                        labelXY.Text = "";
                    }

                }
            }

        }

        catch { }


    }

    private void SkChart_MouseUp(object sender, MouseEventArgs e)
    {
        _drag3D = false;

        if (endM)
        {
            ScaleNonRepaint();
            startM = true;
            xMouseB = 0;
            xMouseE = 0;
            yMouseB = 0;
            yMouseE = 0;
            skChart.Invalidate();
            endM = false;
        }

    }

    // Масштабирование прямоугольник
    private void ScaleNonRepaint()
    {
        if (IsScale)
        {
            x = xMouseB > xMouseE ? xMouseE : xMouseB;
            y = yMouseB > yMouseE ? yMouseE : yMouseB;
            wM = Math.Abs(xMouseB - xMouseE);
            hM = Math.Abs(yMouseB - yMouseE);

            x = (x < 0) ? 0 : x;
            y = (y < 0) ? 0 : y;

            try
            {

                double
                    xb = GetValueX(x),
                    xe = GetValueX(x + wM),
                    ye = GetValueY(y),
                    yb = GetValueY(y + hM);

                Vector vstep = chartElements[0].Data.GetX();
                double step = vstep[1] - vstep[0];

                if (Math.Abs(xe - xb) > step && ye - yb > 0)
                {
                    SetScale(xb, xe, yb, ye);
                    Rec();
                }


            }
            catch { }
        }
    }

    #endregion
}
