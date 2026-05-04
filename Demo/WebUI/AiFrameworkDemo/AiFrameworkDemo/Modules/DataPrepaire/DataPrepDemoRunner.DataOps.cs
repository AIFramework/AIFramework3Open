using AI.DataPrepaire.DataNormalizers;
using AI.DataPrepaire.DataLoader;
using AI.DataPrepaire.DataLoader.Formats;
using AI.DataPrepaire.Tokenizers.TextTokenizers;
using AI.DataPrepaire.NLPUtils;
using AI.DataPrepaire.NLPUtils.RegexpNLP;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;
using AI.DataPrepaire.NLPUtils.TextClassification;
using AI.DataPrepaire.NLPUtils.TextGeneration;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.DataPrepaire
{
    public static partial class DataPrepDemoRunner
    {
        private static string DoNormalizers(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n       = I(p, "n",       200);
            int dims    = I(p, "dims",      3);
            int distrib = I(p, "distrib",   0);
            int seed    = I(p, "seed",     42);

            var rng = new Random(seed);

            var data = new Vector[n];
            for (int i = 0; i < n; i++)
            {
                data[i] = new Vector(dims);
                for (int d = 0; d < dims; d++)
                    data[i][d] = distrib switch
                    {
                        1 => Math.Exp(rng.NextDouble() * 2 - 1),
                        2 => rng.NextDouble() * 10,
                        _ => rng.NextGaussian(d * 3, 1 + d),
                    };
            }

            var zn = new ZNormalizer();
            var mm = new MinimaxNomalizer();

            zn.Train(data);
            mm.Train(data);

            var zNormed = zn.Transform(data).Cast<Vector>().ToArray();
            var mmNormed = mm.Transform(data).Cast<Vector>().ToArray();

            var xIdx   = new Vector(n); for (int i = 0; i < n; i++) xIdx[i] = i;
            var yOrig  = new Vector(n); for (int i = 0; i < n; i++) yOrig[i]  = data[i][0];
            var yZ     = new Vector(n); for (int i = 0; i < n; i++) yZ[i]     = zNormed[i][0];
            var yMM    = new Vector(n); for (int i = 0; i < n; i++) yMM[i]    = mmNormed[i][0];

            cv.AddScatter(xIdx, yOrig, "Исходные", C(0));
            cv.AddScatter(xIdx, yZ,    "Z-норм.",  C(1));
            cv.AddScatter(xIdx, yMM,   "MinMax",   C(2));

            var sb = new StringBuilder();
            sb.AppendLine($"Точек: {n}, Размерность: {dims}, Распределение: {DistribName(distrib)}");
            sb.AppendLine();
            sb.AppendLine("ZNormalizer:");
            sb.AppendLine($"  Mean: [{string.Join(", ", zn.Mean.Take(dims).Select(v => $"{v:F3}"))}]");
            sb.AppendLine($"  Std:  [{string.Join(", ", zn.Std.Take(dims).Select(v => $"{v:F3}"))}]");
            sb.AppendLine();
            sb.AppendLine("MinimaxNormalizer:");
            sb.AppendLine($"  Min: [{string.Join(", ", mm.Min.Take(dims).Select(v => $"{v:F3}"))}]");
            sb.AppendLine($"  Max: [{string.Join(", ", mm.Max.Take(dims).Select(v => $"{v:F3}"))}]");
            sb.AppendLine();
            sb.AppendLine("После нормализации (dim 0):");
            sb.AppendLine($"  Z-норм.: mean={zNormed.Average(v => v[0]):F4}  std={StdDev(zNormed.Select(v => v[0]).ToArray()):F4}");
            sb.AppendLine($"  MinMax:  min={mmNormed.Min(v => v[0]):F4}  max={mmNormed.Max(v => v[0]):F4}");

            var restored = zn.Denormalize(zNormed).Cast<Vector>().ToArray();
            double err = data.Zip(restored, (a, b) => Math.Abs(a[0] - b[0])).Average();
            sb.AppendLine();
            sb.AppendLine($"Ошибка денормализации (Z-норм.): {err:E2}");

            return sb.ToString();
        }

        private static string DoDataTable(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int datasetId = I(p, "datasetId",  0);
            int showRows  = I(p, "showRows",    8);

            string csv = datasetId switch
            {
                1 => "student,math,russian,english,grade\n" +
                     "Alice,85,90,78,A\nBob,72,65,80,B\nCarol,95,88,92,A\n" +
                     "Dave,60,70,55,C\nEve,88,82,85,A\nFrank,45,50,60,D\n" +
                     "Grace,91,95,90,A\nHank,70,75,68,B\nIris,55,60,70,C\nJack,80,78,85,B",
                2 => "name,dept,salary,years,level\n" +
                     "Иван,ИТ,120000,5,Мид\nМария,HR,80000,3,Джун\nПётр,ИТ,180000,8,Сеньор\n" +
                     "Анна,Финансы,100000,4,Мид\nСергей,ИТ,220000,12,Лид\nОльга,HR,90000,6,Мид\n" +
                     "Дмитрий,Маркетинг,110000,7,Мид\nЕлена,Финансы,130000,9,Сеньор",
                _ => "sepal_length,sepal_width,petal_length,petal_width,species\n" +
                     "5.1,3.5,1.4,0.2,setosa\n4.9,3.0,1.4,0.2,setosa\n5.7,2.8,4.1,1.3,versicolor\n" +
                     "6.3,3.3,6.0,2.5,virginica\n5.0,3.6,1.4,0.2,setosa\n5.5,2.3,4.0,1.3,versicolor\n" +
                     "6.5,3.0,5.8,2.2,virginica\n4.6,3.1,1.5,0.2,setosa\n5.9,3.0,4.2,1.5,versicolor\n" +
                     "7.1,3.0,5.9,2.1,virginica",
            };

            DataTable dt;
            using (var sr = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(csv))))
                dt = CSVLoader.Read(sr, ',');

            string[] cols = dt.GetColums();
            int rows = dt.Len;
            showRows = Math.Min(showRows, rows);

            var numCols = cols.Where(c =>
            {
                var col = dt[c];
                col.Convert();
                return col.TypeColum == TypeData.DigitP || col.TypeColum == TypeData.DigitC;
            }).Take(2).ToArray();

            if (numCols.Length >= 2)
            {
                var xs = dt[numCols[0]].ToVector();
                var ys = dt[numCols[1]].ToVector();
                cv.AddScatter(xs, ys, $"{numCols[0]} vs {numCols[1]}", C(0));
            }
            else if (numCols.Length == 1)
            {
                var xs = new Vector(rows); for (int i = 0; i < rows; i++) xs[i] = i;
                var ys = dt[numCols[0]].ToVector();
                cv.AddScatter(xs, ys, numCols[0], C(0));
            }

            var strCols = cols.Where(c => dt[c].TypeColum == TypeData.String ||
                                          dt[c].TypeColum == TypeData.UnDef).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine($"DataTable: {rows} строк × {cols.Length} столбцов");
            sb.AppendLine($"Столбцы: [{string.Join(", ", cols)}]");
            sb.AppendLine();

            sb.AppendLine($"Первые {showRows} строк:");
            sb.Append("  " + string.Join(" | ", cols.Select(c => c.PadRight(10).Substring(0, Math.Min(10, c.Length + 5)).PadRight(10))));
            sb.AppendLine();
            sb.AppendLine("  " + new string('-', cols.Length * 12));
            for (int r = 0; r < showRows; r++)
            {
                var row = dt.GetRow(r);
                sb.AppendLine("  " + string.Join(" | ", row.Select(v => { var s = v?.ToString() ?? ""; return s.PadRight(10)[..Math.Min(10, s.Length + 2)]; })));
            }

            if (strCols.Length > 0)
            {
                sb.AppendLine();
                var catCol = strCols.Last();
                var catIdx = dt.ColumnToCategorical(catCol);
                sb.AppendLine($"Категориальное кодирование «{catCol}»:");
                foreach (var kv in catIdx)
                    sb.AppendLine($"  «{kv.Key}» -> {kv.Value}");
            }

            return sb.ToString();
        }
    }
}
