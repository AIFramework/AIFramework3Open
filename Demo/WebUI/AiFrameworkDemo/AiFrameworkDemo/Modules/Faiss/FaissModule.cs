using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Faiss
{
    public sealed class FaissModule : LibraryModuleBase
    {
        public override string Id => "faiss";
        public override string Name => "AI.Faiss";
        public override string Description => "Высокоэффективный поиск ближайших соседей на основе FAISS (Facebook AI Similarity Search)";
        public override string Color => "violet";
        public override string TutorialFolder => "Faiss";

        public override string IconSvg => """
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="2.5"/>
              <circle cx="4"  cy="5"  r="1.5"/>
              <circle cx="20" cy="5"  r="1.5"/>
              <circle cx="4"  cy="19" r="1.5"/>
              <circle cx="20" cy="19" r="1.5"/>
              <circle cx="12" cy="3"  r="1.5"/>
              <circle cx="12" cy="21" r="1.5"/>
              <line x1="5.1"  y1="5.8"  x2="11" y2="11"/>
              <line x1="18.9" y1="5.8"  x2="13" y2="11"/>
              <line x1="5.1"  y1="18.2" x2="11" y2="13"/>
              <line x1="12"   y1="4.5"  x2="12" y2="9.5"/>
            </svg>
            """;

        #region Наборы вариантов (AlgoChoice)

        private static readonly AlgoChoice[] MetricChoices =
        [
            new(0, "L2 (евклид)"),
            new(1, "Inner Product"),
        ];

        private static readonly AlgoChoice[] IndexChoices =
        [
            new(0, "Flat (точный)"),
            new(1, "HNSW32 (приближённый)"),
        ];

        #endregion

        public override IReadOnlyList<CategoryDef> Categories { get; } =
        [
            #region 1. Поиск ближайших соседей
            new CategoryDef("knn", "Поиск ближайших соседей",
                "Точный (Flat) и приближённый (HNSW) поиск K ближайших соседей",
                [
                    new AlgoDef("knn_search", "KNN-поиск",
                        "Поиск K ближайших соседей; визуализация 2D-пространства запрос/соседи/фон",
                        "AI.Faiss.FaissIndex",
                        "faiss_knn.md",
                        [
                            new AlgoParam("index",  "Индекс",      0,   1,   0,  1,  "",   "Тип индекса FAISS")
                                { Choices = IndexChoices },
                            new AlgoParam("metric", "Метрика",     0,   1,   0,  1,  "",   "Метрика расстояния")
                                { Choices = MetricChoices },
                            new AlgoParam("n",      "Векторов",   20, 500, 100, 10, "шт.", "Число векторов в индексе"),
                            new AlgoParam("k",      "Соседей K",   1,  20,   5,  1,  "",   "Число ближайших соседей"),
                            new AlgoParam("seed",   "Seed",        0, 100,  42,  1,  "",   "Инициализация генератора"),
                        ]),
                    new AlgoDef("batch_search", "Пакетный поиск",
                        "Одновременный поиск нескольких запросов; результаты всех запросов на одном графике",
                        "AI.Faiss.FaissIndex",
                        "faiss_batch.md",
                        [
                            new AlgoParam("n",       "Векторов",   50, 1000, 200, 50, "шт.", "Число векторов в индексе"),
                            new AlgoParam("queries", "Запросов",    1,   20,   5,  1, "шт.", "Число запросных векторов"),
                            new AlgoParam("k",       "Соседей K",   1,   10,   3,  1,  "",   "Число ближайших соседей"),
                            new AlgoParam("seed",    "Seed",        0,  100,  42,  1,  "",   "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 2. Сравнение метрик
            new CategoryDef("metrics", "Метрики расстояния",
                "Сравнение L2 и Inner Product: как метрика влияет на ранжирование соседей",
                [
                    new AlgoDef("metric_compare", "L2 vs Inner Product",
                        "Одни и те же векторы, один запрос — сравнение списков ближайших соседей по разным метрикам",
                        "AI.Faiss.FaissIndex",
                        "faiss_metrics.md",
                        [
                            new AlgoParam("n",    "Векторов",   20, 200,  50, 10, "шт.", "Число векторов"),
                            new AlgoParam("k",    "Соседей K",   1,  15,   5,  1,  "",   "Число ближайших соседей"),
                            new AlgoParam("seed", "Seed",        0, 100,  42,  1,  "",   "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 3. Кластеризация (Assign)
            new CategoryDef("assign", "Кластеризация (Assign)",
                "Назначение каждого вектора ближайшему центроиду через FaissIndex.Assign",
                [
                    new AlgoDef("assign_demo", "Assign-кластеризация",
                        "Центроиды добавляются в Flat-индекс; Assign распределяет точки по кластерам",
                        "AI.Faiss.FaissIndex",
                        "faiss_assign.md",
                        [
                            new AlgoParam("n",        "Точек данных",  30, 500, 150, 10, "шт.", "Число точек данных"),
                            new AlgoParam("clusters", "Кластеров",      2,  10,   5,  1,  "",   "Число центроидов"),
                            new AlgoParam("spread",   "Разброс",        1,  10,   3,  1,  "",   "Радиус разброса вокруг центроида"),
                            new AlgoParam("seed",     "Seed",           0, 100,  42,  1,  "",   "Инициализация генератора"),
                        ]),
                ]),
            #endregion
        ];

        protected override DemoResult RunCore(
            string algoKey,
            IReadOnlyDictionary<string, double> numericParams,
            IReadOnlyDictionary<string, string> textParams,
            DemoSettings settings) =>
            FaissDemoRunner.Run(algoKey, numericParams, settings);
    }
}
