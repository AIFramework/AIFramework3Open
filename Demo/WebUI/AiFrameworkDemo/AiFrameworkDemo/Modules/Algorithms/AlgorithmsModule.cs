using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Algorithms
{
    public sealed class AlgorithmsModule : LibraryModuleBase
    {
        public override string Id => "algo";
        public override string Name => "AI.Algorithms";
        public override string Description => "Графы, потоки, назначение, MAPF, VRP, распределение задач";
        public override string Color => "amber";
        public override string TutorialFolder => "Algorithms";

        public override string IconSvg => """
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="5" cy="6" r="2"/><circle cx="19" cy="6" r="2"/>
              <circle cx="5" cy="18" r="2"/><circle cx="19" cy="18" r="2"/>
              <circle cx="12" cy="12" r="2"/>
              <line x1="7" y1="6" x2="10" y2="12"/>
              <line x1="14" y1="12" x2="17" y2="6"/>
              <line x1="7" y1="18" x2="10" y2="12"/>
              <line x1="14" y1="12" x2="17" y2="18"/>
              <line x1="7" y1="6" x2="17" y2="18"/>
            </svg>
            """;

        #region Наборы вариантов (AlgoChoice)
        private static readonly AlgoChoice[] FlowAlgoChoices =
        [
            new(0, "Ford–Fulkerson"),
            new(1, "Edmonds–Karp"),
            new(2, "Dinic"),
            new(3, "Push–Relabel"),
        ];

        private static readonly AlgoChoice[] MinCostAlgoChoices =
        [
            new(0, "SSP"),
            new(1, "Cycle-Cancel"),
            new(2, "Cost-Scale"),
        ];

        private static readonly AlgoChoice[] BipartiteAlgoChoices =
        [
            new(0, "Kuhn"),
            new(1, "Hopcroft–Karp"),
        ];

        private static readonly AlgoChoice[] MAPFBasicChoices =
        [
            new(0, "CBS"),
            new(1, "ECBS"),
            new(2, "PBS"),
        ];

        private static readonly AlgoChoice[] MAPFPriorityChoices =
        [
            new(0, "PIBT"),
            new(1, "Token Passing"),
        ];

        private static readonly AlgoChoice[] MAPFLocalChoices =
        [
            new(0, "Push & Swap"),
            new(1, "Push & Rotate"),
        ];

        private static readonly AlgoChoice[] MAPFCoopChoices =
        [
            new(0, "WHCA*"),
            new(1, "HCA*"),
        ];

        private static readonly AlgoChoice[] MAPFLacamChoices =
        [
            new(0, "LaCAM"),
            new(1, "LaCAM*"),
        ];

        private static readonly AlgoChoice[] VRPConstructChoices =
        [
            new(0, "Clarke–Wright"),
            new(1, "Sweep"),
            new(2, "Solomon I1"),
        ];

        private static readonly AlgoChoice[] TSPHeuristicChoices =
        [
            new(0, "2-opt"),
            new(1, "Lin–Kernighan"),
            new(2, "Christofides"),
        ];

        private static readonly AlgoChoice[] VRPMetaChoices =
        [
            new(0, "GA"),
            new(1, "Tabu"),
            new(2, "ACO"),
            new(3, "SA"),
            new(4, "ALNS"),
        ];

        private static readonly AlgoChoice[] AuctionChoices =
        [
            new(0, "CNP"),
            new(1, "SSI"),
            new(2, "Sequential"),
        ];

        private static readonly AlgoChoice[] DCOPChoices =
        [
            new(0, "ADOPT"),
            new(1, "DPOP"),
            new(2, "Max-Sum"),
            new(3, "DSA"),
            new(4, "MGM"),
        ];

        private static readonly AlgoChoice[] CBBAChoices =
        [
            new(0, "CBBA"),
            new(1, "Greedy"),
        ];

        #endregion

        public override IReadOnlyList<CategoryDef> Categories { get; } =
        [
            #region 1. Графы и кратчайшие пути
            new CategoryDef("graphs", "Графы и кратчайшие пути",
                "Обход, кратчайшие пути, топология, MST, динамический поиск",
                [
                    new AlgoDef("bfs_dfs", "BFS / DFS",
                        "Обход графа в ширину и в глубину",
                        "AI.Algorithms.EWG.BFS / DFS",
                        "bfs_dfs.md",
                        [
                            new AlgoParam("n", "Вершин", 5, 30, 12, 1, "шт.", "Число вершин графа"),
                            new AlgoParam("density", "Плотность", 0.1, 0.6, 0.25, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("dijkstra_demo", "Алгоритм Дейкстры",
                        "Кратчайший путь от источника во взвешенном графе",
                        "AI.Algorithms.EWG.DijkstraSPath",
                        "dijkstra.md",
                        [
                            new AlgoParam("n", "Вершин", 5, 30, 10, 1, "шт.", "Число вершин"),
                            new AlgoParam("density", "Плотность", 0.15, 0.6, 0.3, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("bellman_ford", "Беллман—Форд",
                        "Кратчайший путь с отрицательными весами",
                        "AI.Algorithms.EWG.BellmanFordSP",
                        "bellman_ford.md",
                        [
                            new AlgoParam("n", "Вершин", 4, 20, 8, 1, "шт.", "Число вершин"),
                            new AlgoParam("negativeEdges", "Отр. рёбер (%)", 0, 50, 20, 5, "%", "Доля рёбер с отрицательным весом"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("floyd_warshall", "Флойд—Уоршелл",
                        "Все кратчайшие пути между всеми парами вершин",
                        "AI.Algorithms.EWG.FloydWarshall",
                        "floyd_warshall.md",
                        [
                            new AlgoParam("n", "Вершин", 4, 12, 6, 1, "шт.", "Число вершин"),
                            new AlgoParam("density", "Плотность", 0.3, 0.8, 0.5, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("astar", "A* / IDA*",
                        "Эвристический поиск на графе/сетке",
                        "AI.Algorithms.EWG.AStarSearch",
                        "astar.md",
                        [
                            new AlgoParam("gridW", "Ширина сетки", 8, 30, 15, 1, "", "Число столбцов"),
                            new AlgoParam("gridH", "Высота сетки", 8, 30, 15, 1, "", "Число строк"),
                            new AlgoParam("obstacles", "Препятствий (%)", 0, 40, 20, 5, "%", "Доля заблокированных клеток"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("yen_k_shortest", "K кратчайших путей (Yen)",
                        "Поиск K различных кратчайших путей",
                        "AI.Algorithms.EWG.YenKShortestPaths",
                        "yen_k_shortest.md",
                        [
                            new AlgoParam("n", "Вершин", 5, 20, 8, 1, "шт.", "Число вершин"),
                            new AlgoParam("k", "K путей", 2, 10, 3, 1, "", "Количество путей"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("topo_sort_scc", "Топология / SCC / Мосты",
                        "Топологическая сортировка, компоненты связности, точки сочленения",
                        "AI.Algorithms.GraphStructure",
                        "topo_sort_scc.md",
                        [
                            new AlgoParam("n", "Вершин", 5, 25, 10, 1, "шт.", "Число вершин"),
                            new AlgoParam("density", "Плотность", 0.1, 0.5, 0.25, 0.05, "", "Вероятность дуги"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mst", "Остовные деревья",
                        "Kruskal, Prim, Borůvka — минимальное остовное дерево",
                        "AI.Algorithms.MST",
                        "mst.md",
                        [
                            new AlgoParam("n", "Вершин", 5, 25, 10, 1, "шт.", "Число вершин"),
                            new AlgoParam("density", "Плотность", 0.3, 0.8, 0.5, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 2. Потоки в сетях
            new CategoryDef("flows", "Потоки в сетях",
                "Максимальный поток, минимальный разрез, потоки минимальной стоимости",
                [
                    new AlgoDef("max_flow", "Максимальный поток",
                        "Ford–Fulkerson / Edmonds–Karp / Dinic / Push–Relabel",
                        "AI.Algorithms.NetworkFlow",
                        "max_flow.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 3, 0, 1, "", "Выбор алгоритма")
                                { Choices = FlowAlgoChoices },
                            new AlgoParam("n", "Вершин", 4, 16, 8, 1, "шт.", "Число вершин (исток=0, сток=N-1)"),
                            new AlgoParam("density", "Плотность", 0.2, 0.7, 0.4, 0.05, "", "Вероятность дуги"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("min_cost_flow", "Поток мин. стоимости",
                        "Successive Shortest Paths / Cycle-Canceling / Cost Scaling",
                        "AI.Algorithms.NetworkFlow",
                        "min_cost_flow.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 2, 0, 1, "", "Выбор алгоритма")
                                { Choices = MinCostAlgoChoices },
                            new AlgoParam("n", "Вершин", 4, 12, 6, 1, "шт.", "Число вершин"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("min_cut", "Минимальный разрез",
                        "Stoer–Wagner: глобальный минимальный разрез неориентированного графа",
                        "AI.Algorithms.NetworkFlow.StoerWagner",
                        "min_cut.md",
                        [
                            new AlgoParam("n", "Вершин", 4, 14, 8, 1, "шт.", "Число вершин"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("gomory_hu", "Дерево Гомори—Ху",
                        "Все попарные минимальные разрезы за V-1 запуск max-flow",
                        "AI.Algorithms.NetworkFlow.GomoryHu",
                        "min_cut.md",
                        [
                            new AlgoParam("n", "Вершин", 4, 10, 6, 1, "шт.", "Число вершин"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 3. Назначение и паросочетание
            new CategoryDef("matching", "Назначение и паросочетание",
                "Венгерский алгоритм, паросочетания, устойчивый брак",
                [
                    new AlgoDef("hungarian", "Венгерский алгоритм",
                        "Оптимальное назначение (Kuhn–Munkres), O(n³)",
                        "AI.Algorithms.Matching.Hungarian",
                        "hungarian.md",
                        [
                            new AlgoParam("n", "Размерность", 3, 12, 5, 1, "", "Размер матрицы стоимостей"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("bipartite_matching", "Двудольное паросочетание",
                        "Kuhn / Hopcroft–Karp: максимальное паросочетание в двудольном графе",
                        "AI.Algorithms.Matching.HopcroftKarp",
                        "bipartite_matching.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = BipartiteAlgoChoices },
                            new AlgoParam("left", "Левая доля", 3, 12, 5, 1, "шт.", "Число вершин слева"),
                            new AlgoParam("right", "Правая доля", 3, 12, 6, 1, "шт.", "Число вершин справа"),
                            new AlgoParam("density", "Плотность", 0.2, 0.8, 0.4, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("general_matching", "Edmonds Blossom",
                        "Максимальное паросочетание в произвольном графе",
                        "AI.Algorithms.Matching.EdmondsBlossom",
                        "general_matching.md",
                        [
                            new AlgoParam("n", "Вершин", 4, 16, 8, 1, "шт.", "Число вершин"),
                            new AlgoParam("density", "Плотность", 0.2, 0.6, 0.35, 0.05, "", "Вероятность ребра"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("stable_marriage", "Устойчивый брак",
                        "Алгоритм Гейла—Шепли: устойчивые паросочетания",
                        "AI.Algorithms.Matching.GaleShapley",
                        "stable_marriage.md",
                        [
                            new AlgoParam("n", "Пар", 3, 10, 5, 1, "", "Число мужчин = число женщин"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 4. Multi-Agent Path Finding
            new CategoryDef("mapf", "Multi-Agent Path Finding",
                "Поиск бесконфликтных путей для нескольких агентов",
                [
                    new AlgoDef("mapf_basic", "CBS / ECBS / PBS",
                        "Conflict-Based Search и его расширения",
                        "AI.Algorithms.MAPF.CBS",
                        "mapf_cbs.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 2, 0, 1, "", "Выбор алгоритма")
                                { Choices = MAPFBasicChoices },
                            new AlgoParam("gridSize", "Размер сетки", 5, 20, 8, 1, "", "Ширина = высота"),
                            new AlgoParam("agents", "Агентов", 2, 8, 3, 1, "шт.", "Число агентов"),
                            new AlgoParam("obstacles", "Препятствий (%)", 0, 30, 15, 5, "%", "Доля заблокированных клеток"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mapf_priority", "PIBT / Token Passing",
                        "Приоритетные однотактовые алгоритмы",
                        "AI.Algorithms.MAPF.PIBT",
                        "mapf_priority.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = MAPFPriorityChoices },
                            new AlgoParam("gridSize", "Размер сетки", 5, 20, 8, 1, "", "Ширина = высота"),
                            new AlgoParam("agents", "Агентов", 2, 10, 4, 1, "шт.", "Число агентов"),
                            new AlgoParam("obstacles", "Препятствий (%)", 0, 30, 10, 5, "%", "Доля заблокированных клеток"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mapf_local", "Push & Swap / Rotate",
                        "Локальные перестановочные алгоритмы MAPF",
                        "AI.Algorithms.MAPF.PushAndSwap",
                        "mapf_local.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = MAPFLocalChoices },
                            new AlgoParam("gridSize", "Размер сетки", 4, 12, 6, 1, "", "Ширина = высота"),
                            new AlgoParam("agents", "Агентов", 2, 6, 3, 1, "шт.", "Число агентов"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mapf_cooperative", "WHCA* / HCA*",
                        "Кооперативный A* с резервированием пространства-времени",
                        "AI.Algorithms.MAPF.WHCA",
                        "mapf_cooperative.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = MAPFCoopChoices },
                            new AlgoParam("gridSize", "Размер сетки", 5, 20, 8, 1, "", "Ширина = высота"),
                            new AlgoParam("agents", "Агентов", 2, 8, 3, 1, "шт.", "Число агентов"),
                            new AlgoParam("obstacles", "Препятствий (%)", 0, 30, 15, 5, "%", "Доля заблокированных клеток"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mapf_sipp", "SIPP (безопасные интервалы)",
                        "Планирование одного агента среди динамических препятствий: состояние — не «клетка и время», а «клетка и безопасный интервал»",
                        "AI.Algorithms.MAPF.SIPP",
                        "sipp.md",
                        [
                            new AlgoParam("scenario", "Сценарий карты", 0, 1, 0, 1, "",
                                "На открытой сетке тысячи равных по длине маршрутов, и агент всегда обходит препятствия. " +
                                "Ожидание становится выгоднее обхода только в узком месте")
                                { Choices = [
                                    new(0, "Стена с воротами"),
                                    new(1, "Случайная карта"),
                                ]},
                            new AlgoParam("gridSize", "Размер сетки", 5, 20, 10, 1, "", "Ширина = высота"),
                            new AlgoParam("obstacles", "Статических препятствий (%)", 0, 30, 10, 5, "%",
                                "Доля клеток, заблокированных навсегда. Только для сценария «Случайная карта»"),
                            new AlgoParam("movingObs", "Динамических препятствий", 1, 12, 3, 1, "шт.",
                                "Клетки, занятые лишь на интервале времени"),
                            new AlgoParam("blockLen", "Длина блокировки", 1, 15, 8, 1, "тактов",
                                "Сколько тактов динамическое препятствие занимает клетку"),
                            new AlgoParam("startTime", "Момент старта", 0, 20, 0, 1, "такт",
                                "Время, в которое агент выходит из стартовой клетки"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("mapf_lacam", "LaCAM / LaCAM*",
                        "Ленивое добавление ограничений (Lazy Constraints)",
                        "AI.Algorithms.MAPF.LaCAM",
                        "mapf_lacam.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = MAPFLacamChoices },
                            new AlgoParam("gridSize", "Размер сетки", 5, 20, 10, 1, "", "Ширина = высота"),
                            new AlgoParam("agents", "Агентов", 2, 12, 5, 1, "шт.", "Число агентов"),
                            new AlgoParam("obstacles", "Препятствий (%)", 0, 30, 15, 5, "%", "Доля заблокированных клеток"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 5. Транспортная задача
            new CategoryDef("transport", "Транспортная задача",
                "Оптимальное распределение поставок: метод потенциалов, Фогель, северо-западный угол",
                [
                    new AlgoDef("transport_task", "Метод потенциалов",
                        "Решение транспортной задачи методом потенциалов с начальным планом Фогеля",
                        "AI.Algorithms.TransportTask.Methods.PotentialMethod",
                        "transport_task.md",
                        [
                            new AlgoParam("suppliers", "Поставщиков", 2, 6, 3, 1, "шт.", "Число поставщиков (строк)"),
                            new AlgoParam("consumers", "Потребителей", 2, 6, 4, 1, "шт.", "Число потребителей (столбцов)"),
                            new AlgoParam("maxCost", "Макс. стоимость", 5, 30, 15, 1, "", "Максимальная стоимость перевозки"),
                            new AlgoParam("maxSupply", "Макс. объём", 10, 100, 50, 5, "", "Максимальный объём поставки/потребности"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 6. Маршрутизация транспорта (VRP)
            new CategoryDef("vrp", "Маршрутизация транспорта",
                "Задача маршрутизации транспортных средств (VRP) и TSP",
                [
                    new AlgoDef("vrp_constructive", "Конструктивные VRP",
                        "Clarke–Wright / Sweep / Solomon I1",
                        "AI.Algorithms.VRP",
                        "vrp_constructive.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 2, 0, 1, "", "Выбор алгоритма")
                                { Choices = VRPConstructChoices },
                            new AlgoParam("customers", "Клиентов", 5, 30, 12, 1, "шт.", "Число клиентов"),
                            new AlgoParam("vehicles", "Машин", 1, 6, 3, 1, "шт.", "Число транспортных средств"),
                            new AlgoParam("capacity", "Ёмкость", 20, 100, 50, 10, "", "Грузоподъёмность одного ТС"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("tsp_heuristic", "TSP-эвристики",
                        "2-opt / Lin–Kernighan / Christofides",
                        "AI.Algorithms.VRP",
                        "tsp_heuristic.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 2, 0, 1, "", "Выбор алгоритма")
                                { Choices = TSPHeuristicChoices },
                            new AlgoParam("cities", "Городов", 5, 40, 15, 1, "шт.", "Число городов"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("vrp_metaheuristic", "Метаэвристики VRP",
                        "GA / Tabu / ACO / SA / ALNS",
                        "AI.Algorithms.VRP",
                        "vrp_metaheuristic.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 4, 0, 1, "", "Выбор алгоритма")
                                { Choices = VRPMetaChoices },
                            new AlgoParam("customers", "Клиентов", 5, 30, 15, 1, "шт.", "Число клиентов"),
                            new AlgoParam("vehicles", "Машин", 1, 6, 3, 1, "шт.", "Число ТС"),
                            new AlgoParam("capacity", "Ёмкость", 20, 100, 50, 10, "", "Грузоподъёмность одного ТС"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 7. Распределение задач
            new CategoryDef("taskalloc", "Распределение задач",
                "Аукционы, DCOP, консенсусные протоколы",
                [
                    new AlgoDef("task_auction", "Аукционные методы",
                        "CNP / SSI / Sequential Auction",
                        "AI.Algorithms.TaskAllocation",
                        "task_auction.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 2, 0, 1, "", "Выбор алгоритма")
                                { Choices = AuctionChoices },
                            new AlgoParam("agents", "Агентов", 2, 10, 4, 1, "шт.", "Число агентов"),
                            new AlgoParam("tasks", "Задач", 3, 15, 8, 1, "шт.", "Число задач"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("task_dcop", "DCOP-решатели",
                        "ADOPT / DPOP / Max-Sum / DSA / MGM",
                        "AI.Algorithms.TaskAllocation",
                        "task_dcop.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 4, 0, 1, "", "Выбор алгоритма")
                                { Choices = DCOPChoices },
                            new AlgoParam("agents", "Агентов", 2, 8, 4, 1, "шт.", "Число агентов"),
                            new AlgoParam("tasks", "Задач", 3, 12, 6, 1, "шт.", "Число задач"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                    new AlgoDef("task_cbba", "CBBA / Greedy",
                        "Консенсусное распределение пакетов задач",
                        "AI.Algorithms.TaskAllocation.CBBA",
                        "task_cbba.md",
                        [
                            new AlgoParam("algo", "Алгоритм", 0, 1, 0, 1, "", "Выбор алгоритма")
                                { Choices = CBBAChoices },
                            new AlgoParam("agents", "Агентов", 2, 8, 4, 1, "шт.", "Число агентов"),
                            new AlgoParam("tasks", "Задач", 3, 15, 8, 1, "шт.", "Число задач"),
                            new AlgoParam("capacity", "Ёмкость агента", 1, 5, 2, 1, "", "Макс. задач на агента"),
                            new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                        ]),
                ]),
            #endregion
        ];

        protected override DemoResult RunCore(
            string algoKey,
            IReadOnlyDictionary<string, double> numericParams,
            IReadOnlyDictionary<string, string> textParams,
            DemoSettings settings) =>
            AlgorithmsDemoRunner.Run(algoKey, numericParams, settings);
    }
}
