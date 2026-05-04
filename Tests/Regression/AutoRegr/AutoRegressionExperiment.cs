using AI.DataStructs.Algebraic;
using AI.ML.SequenceAnalysis.SeqPredict;
using AI.Statistics;
using System;
using System.Diagnostics;

namespace AutoRegr
{
    /// <summary>
    /// Демонстрация авторегрессии AR: обучение на зашумлённом ряду и прогноз на хвосте.
    /// </summary>
    internal static class AutoRegressionExperiment
    {
        public const int DefaultWindowSize = 100;
        public const int DefaultTrainLength = 600;
        public const int DefaultPredictHorizon = 600;

        public sealed class Result
        {
            public Vector TimeTrain { get; init; }
            public Vector TimeFull { get; init; }
            public Vector SeriesNoisy { get; init; }
            public Vector SeriesClean { get; init; }
            public Vector Prediction { get; init; }
            public double RSquared { get; init; }
            public long TrainTimeMs { get; init; }
        }

        /// <summary>
        /// Синтетический сигнал: сумма синусов и косинуса, нормализация Minimax, квантование, шум.
        /// </summary>
        public static Result Run(int windowSize, int trainLen, int predictHorizon, Random random)
        {
            int fullLen = trainLen + predictHorizon;

            Vector tTrain = Vector.Seq(0, 1, trainLen);
            Vector tFull = Vector.Seq(0, 1, fullLen);

            Vector xTrain = SyntheticSignal(tTrain, trainLen);
            Vector xFull = SyntheticSignal(tFull, trainLen);

            var trainQuantized = xTrain.Transform(v => (int)(15 * v));
            var fullQuantized = xFull.Transform(v => (int)(15 * v));

            Vector noisy = trainQuantized.Transform(v => v + random.Next(-3, 4));

            var ar = new AR(windowSize);
            var sw = Stopwatch.StartNew();
            ar.Train(noisy);
            sw.Stop();

            Vector pred = ar.Predict(noisy, predictHorizon);

            var yPred = new Vector(pred.GetRange(trainLen, predictHorizon - 1));
            var yTrue = new Vector(fullQuantized.GetRange(trainLen, predictHorizon - 1));
            double r = Statistic.CorrelationCoefficient(yPred, yTrue);
            double r2 = r * r;

            return new Result
            {
                TimeTrain = tTrain,
                TimeFull = tFull,
                SeriesNoisy = noisy,
                SeriesClean = fullQuantized,
                Prediction = pred,
                RSquared = r2,
                TrainTimeMs = sw.ElapsedMilliseconds
            };
        }

        private static Vector SyntheticSignal(Vector t, int periodScale)
        {
            Vector x = t.Transform(tt =>
                Math.Sin(2 * 10 * tt * Math.PI / periodScale)
                + Math.Sin(2 * 13 * tt * Math.PI / periodScale)
                + Math.Cos(2 * 2 * tt * Math.PI / periodScale));
            return x.Minimax();
        }
    }
}
