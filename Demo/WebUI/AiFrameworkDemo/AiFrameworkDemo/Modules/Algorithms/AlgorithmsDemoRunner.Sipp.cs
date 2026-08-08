using AI.Algorithms.MAPF;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Algorithms
{
    public static partial class AlgorithmsDemoRunner
    {
        /// <summary>
        /// SIPP — планирование одного агента среди ДИНАМИЧЕСКИХ препятствий.
        ///
        /// Ключевая идея, которую и должно показать демо: вместо состояния
        /// «клетка + момент времени» (что даёт огромное пространство поиска)
        /// SIPP работает с состоянием «клетка + безопасный интервал». Число
        /// интервалов в клетке равно числу занятий её препятствиями плюс один,
        /// то есть на порядки меньше горизонта планирования.
        /// </summary>
        private static string DoSipp(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int gridSize   = I(p, "gridSize", 10);
            double obsPct  = N(p, "obstacles", 10);
            int movingObs  = I(p, "movingObs", 3);
            int blockLen   = I(p, "blockLen", 8);
            int startTime  = I(p, "startTime", 0);
            int scenario   = I(p, "scenario", 0);
            var rng        = new Random(I(p, "seed", 42));

            var map = new GridMap(gridSize, gridSize);
            var dynamic = new List<(int X, int Y, int TimeStart, int TimeEnd)>();
            int gateX = -1, gateY = -1;

            if (scenario == 0)
            {
                // -- Сценарий «стена с воротами» ------------------------
                // На открытой сетке 10×10 существует ~48 000 одинаково коротких
                // маршрутов, поэтому сколько препятствий ни ставь, агент просто
                // обойдёт их без потери времени и ожиданий не возникнет вовсе.
                // Ожидание становится выгоднее обхода только в узком месте —
                // именно этот случай SIPP и решает нетривиально.
                gateX = gridSize / 2;
                gateY = gridSize / 2;
                for (int y = 0; y < gridSize; y++)
                    if (y != gateY) map.SetBlocked(gateX, y, true);

                // Ворота заняты как раз тогда, когда агент до них доходит
                int arrival = startTime + gateX + Math.Abs(gateY - 0);
                for (int i = 0; i < Math.Max(1, movingObs); i++)
                {
                    int t0 = Math.Max(0, arrival - 1 + i * blockLen);
                    dynamic.Add((gateX, gateY, t0, t0 + blockLen - 1));
                    if (i == 0) continue;
                    // Последующие препятствия расширяют окно занятости ворот,
                    // так что ждать придётся дольше.
                }
            }
            else
            {
                // -- Сценарий «случайная карта» -------------------------
                int obs = (int)(gridSize * gridSize * obsPct / 100.0);
                for (int placed = 0; placed < obs; )
                {
                    int x = rng.Next(gridSize), y = rng.Next(gridSize);
                    if ((x == 0 && y == 0) || (x == gridSize - 1 && y == gridSize - 1)) continue;
                    if (!map.IsBlocked(x, y)) { map.SetBlocked(x, y, true); placed++; }
                }

                for (int i = 0; i < movingObs; i++)
                {
                    int x, y, guard = 0;
                    do
                    {
                        x = rng.Next(gridSize);
                        y = rng.Next(gridSize);
                    } while ((map.IsBlocked(x, y) || (x == 0 && y == 0) ||
                              (x == gridSize - 1 && y == gridSize - 1)) && ++guard < 200);

                    int ts = rng.Next(0, Math.Max(1, gridSize));
                    dynamic.Add((x, y, ts, ts + blockLen - 1));
                }
            }

            var sipp = new SIPP(map, dynamic);
            var path = sipp.FindPath(0, 0, gridSize - 1, gridSize - 1, startTime);

            // -- Отрисовка -----------------------------------------------
            cv.ChartName = path.Count > 0
                ? $"SIPP: путь длиной {path.Count - 1} шагов среди {movingObs} динамических препятствий"
                : "SIPP: путь не найден";
            cv.LabelX = "X";
            cv.LabelY = "Y";

            // Статические препятствия
            var sxv = new List<double>(); var syv = new List<double>();
            for (int x = 0; x < gridSize; x++)
                for (int y = 0; y < gridSize; y++)
                    if (map.IsBlocked(x, y)) { sxv.Add(x); syv.Add(y); }

            if (sxv.Count > 0)
                cv.AddScatterMark6(ToVec(sxv), ToVec(syv), "статические препятствия",
                    new SKColor(0x55, 0x55, 0x55));

            // Динамические препятствия
            if (dynamic.Count > 0)
                cv.AddScatterMark6(ToVec(dynamic.Select(d => (double)d.X)),
                                   ToVec(dynamic.Select(d => (double)d.Y)),
                                   "динамические (временные окна)",
                                   new SKColor(0xF8, 0x71, 0x71));

            int waits = 0;
            if (path.Count > 0)
            {
                var px = new Vector(path.Count);
                var py = new Vector(path.Count);
                for (int i = 0; i < path.Count; i++) { px[i] = path[i].X; py[i] = path[i].Y; }
                cv.AddPlot(px, py, "путь агента", new SKColor(0x4A, 0xDE, 0x80), 3);

                // Ожидания на месте: подряд идущие одинаковые координаты
                var wx = new List<double>(); var wy = new List<double>();
                for (int i = 1; i < path.Count; i++)
                    if (path[i].X == path[i - 1].X && path[i].Y == path[i - 1].Y)
                    {
                        waits++;
                        wx.Add(path[i].X); wy.Add(path[i].Y);
                    }

                if (wx.Count > 0)
                    cv.AddScatterMark6(ToVec(wx), ToVec(wy), $"ожидания ({waits})",
                        new SKColor(0xFB, 0xBF, 0x24));
            }

            var startV = new Vector(1) { [0] = 0 };
            cv.AddScatterMark6(startV, startV, "старт (0,0)", new SKColor(0x38, 0xBD, 0xF8));
            var goalV = new Vector(1) { [0] = gridSize - 1 };
            cv.AddScatterMark6(goalV, goalV, $"цель ({gridSize - 1},{gridSize - 1})",
                new SKColor(0xA7, 0x8B, 0xFA));

            // -- Метрики --------------------------------------------------
            bool found = path.Count > 0;
            int steps  = found ? path.Count - 1 : 0;
            int manhattan = 2 * (gridSize - 1);

            rep.Metric("Путь найден", found ? "да" : "нет",
                       tone: found ? MetricTone.Good : MetricTone.Bad,
                       hint: "SIPP возвращает пустой список, если безопасного маршрута нет")
               .Metric("Шагов", steps, hint: "Включая шаги ожидания на месте")
               .Metric("Ожиданий", waits, "шт.",
                       hint: "Агент стоит на месте, пропуская динамическое препятствие",
                       tone: waits > 0 ? MetricTone.Warn : MetricTone.Good)
               .Metric("Манхэттенский минимум", manhattan,
                       hint: "Нижняя граница: путь по прямой без препятствий")
               .Metric("Избыточность", found && manhattan > 0 ? (double)steps / manhattan : 0,
                       hint: "Во сколько раз путь длиннее нижней границы", format: "F2")
               .Note(scenario == 0
                   ? $"Стена по X={gateX} с единственным проходом в ({gateX}, {gateY}), занятым на время. " +
                     "Обойти нельзя, поэтому агент ждёт у ворот — жёлтые точки на графике. " +
                     "Увеличьте «Длину блокировки», и ожидание станет длиннее."
                   : "На открытой сетке существуют тысячи одинаково коротких маршрутов, поэтому агент " +
                     "почти всегда обходит препятствия без потери времени и ожиданий не возникает. " +
                     "Чтобы увидеть ожидание, переключитесь на сценарий «Стена с воротами».");

            var obsTable = rep.Table("Динамические препятствия",
                ["#", "Клетка", "Занята с", "Занята по", "Длительность"],
                numeric: [true, false, true, true, true],
                note: "Вне указанного окна клетка свободна — это и есть «безопасные интервалы» SIPP. " +
                      "Препятствия расставлены вдоль кратчайшего пути в моменты, когда агент до них доходит: " +
                      "иначе они бы ни на что не влияли.");

            for (int i = 0; i < dynamic.Count; i++)
                obsTable.Row((i + 1).ToString(), $"({dynamic[i].X}, {dynamic[i].Y})",
                             dynamic[i].TimeStart.ToString(), dynamic[i].TimeEnd.ToString(),
                             (dynamic[i].TimeEnd - dynamic[i].TimeStart + 1).ToString());

            if (found)
            {
                var pathTable = rep.Table("Траектория по шагам",
                    ["t", "X", "Y", "Действие"],
                    numeric: [true, true, true, false]);

                for (int i = 0; i < path.Count; i++)
                {
                    string action = i == 0 ? "старт"
                        : path[i].X == path[i - 1].X && path[i].Y == path[i - 1].Y ? "ожидание"
                        : "переход";
                    pathTable.Row((startTime + i).ToString(), path[i].X.ToString(), path[i].Y.ToString(), action);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"SIPP — Safe Interval Path Planning");
            sb.AppendLine($"Сценарий: {(scenario == 0 ? $"стена с воротами в ({gateX}, {gateY})" : "случайная карта")}");
            sb.AppendLine($"Сетка: {gridSize}×{gridSize}, статических препятствий: {sxv.Count}");
            sb.AppendLine($"Динамических препятствий: {dynamic.Count}, окно блокировки: {blockLen} тактов");
            sb.AppendLine($"Старт: (0,0) в момент t={startTime}, цель: ({gridSize - 1},{gridSize - 1})");
            sb.AppendLine();

            if (!found)
            {
                sb.AppendLine("Путь не найден: все маршруты перекрыты статическими препятствиями");
                sb.AppendLine("или стартовая клетка занята в момент startTime.");
                return sb.ToString();
            }

            sb.AppendLine($"Шагов: {steps} (из них ожиданий: {waits})");
            sb.AppendLine();
            sb.AppendLine("Траектория:");
            for (int i = 0; i < path.Count; i++)
                sb.AppendLine($"  t={startTime + i,3}  ({path[i].X}, {path[i].Y})");

            return sb.ToString();
        }

        private static Vector ToVec(IEnumerable<double> values)
        {
            var arr = values.ToArray();
            var v = new Vector(arr.Length);
            for (int i = 0; i < arr.Length; i++) v[i] = arr[i];
            return v;
        }
    }
}
