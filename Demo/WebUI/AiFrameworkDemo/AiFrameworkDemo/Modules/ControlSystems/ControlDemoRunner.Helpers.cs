using System;
using System.Collections.Generic;
using System.Linq;
using AI.Charts;
using AI.ControlSystems.Adaptive;
using AI.ControlSystems.Identification;
using AI.ControlSystems.Linear;
using AI.ControlSystems.Nonlinear;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.ControlSystems.Pid;
using AI.DataStructs.Algebraic;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using SkiaSharp;
using Matrix = AI.DataStructs.Algebraic.Matrix;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.ControlSystems;

public static partial class ControlDemoRunner
{
    #region Matrix / Vector helpers

    private static Matrix M4(double a, double b, double c, double d)
    {
        var m = new Matrix(2, 2);
        m[0, 0] = a; m[0, 1] = b;
        m[1, 0] = c; m[1, 1] = d;
        return m;
    }

    private static Matrix M1(double v)
    {
        var m = new Matrix(1, 1);
        m[0, 0] = v;
        return m;
    }

    private static Matrix Col(double v0, double v1)
    {
        var m = new Matrix(2, 1);
        m[0, 0] = v0; m[1, 0] = v1;
        return m;
    }

    private static Matrix Row(double v0, double v1)
    {
        var m = new Matrix(1, 2);
        m[0, 0] = v0; m[0, 1] = v1;
        return m;
    }

    private static Matrix Zero1x1() => new Matrix(1, 1);

    private static Matrix Diag(params double[] v)
    {
        var m = new Matrix(v.Length, v.Length);
        for (int i = 0; i < v.Length; i++) m[i, i] = v[i];
        return m;
    }

    private static Vector Vs(double s) => new Vector(new double[] { s });

    private static Vector Tv(double[] arr) => new Vector(arr);

    private static Vector MV(Matrix A, Vector x)
    {
        var v = new Vector(A.Height);
        for (int i = 0; i < A.Height; i++)
        {
            double s = 0;
            for (int j = 0; j < A.Width; j++) s += A[i, j] * x[j];
            v[i] = s;
        }
        return v;
    }

    private static double KxScalar(Matrix K, Vector x)
    {
        double s = 0;
        for (int j = 0; j < K.Width; j++) s += K[0, j] * x[j];
        return s;
    }

    private static SKColor HexC(string hex)
    {
        hex = hex.TrimStart('#');
        return new SKColor(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    #endregion Matrix / Vector helpers

    #region Chart / PNG helpers

    private static ChartView MakeView(Settings cfg)
        => DemoRunnerBase.MakeView(cfg.Width, cfg.Height, cfg.DarkTheme);

    private static string ToPngDataUrl(ChartView cv, Settings cfg)
        => DemoRunnerBase.RenderPng(cv, cfg.Width, cfg.Height);

    private static string ErrorPng(string msg, int w, int h)
    {
        using var bmp    = new SKBitmap(Math.Max(w, 100), Math.Max(h, 60));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(40, 10, 10));
        using var paint  = new SKPaint { Color = new SKColor(255, 100, 100), TextSize = 12, IsAntialias = true };
        canvas.DrawText("Ошибка: " + msg[..Math.Min(msg.Length, 80)], 8, 30, paint);
        using var img     = SKImage.FromBitmap(bmp);
        using var encoded = img.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
    }

    #endregion Chart / PNG helpers
}
