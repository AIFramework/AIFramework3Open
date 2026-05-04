using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public sealed class NeuralNetworksModule : LibraryModuleBase
{
    public override string Id => "nn";
    public override string Name => "AI.NeuralNetworks";
    public override string Description => "Нейросети V2: MLP, GRU/LSTM/RNN/Filter/Transformer — классификация, регрессия, прогноз рядов, сравнение архитектур, языковые модели, автоэнкодер";
    public override string Color => "emerald";
    public override string TutorialFolder => "NeuralNetworks";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="5"  cy="6"  r="1.8"/>
          <circle cx="5"  cy="12" r="1.8"/>
          <circle cx="5"  cy="18" r="1.8"/>
          <circle cx="12" cy="8"  r="1.8"/>
          <circle cx="12" cy="16" r="1.8"/>
          <circle cx="19" cy="12" r="1.8"/>
          <line x1="6.6" y1="6"  x2="10.4" y2="8"/>
          <line x1="6.6" y1="12" x2="10.4" y2="8"/>
          <line x1="6.6" y1="12" x2="10.4" y2="16"/>
          <line x1="6.6" y1="18" x2="10.4" y2="16"/>
          <line x1="13.6" y1="8"  x2="17.4" y2="12"/>
          <line x1="13.6" y1="16" x2="17.4" y2="12"/>
        </svg>
        """;

    private static readonly AlgoChoice[] ClsDatasetChoices =
    [
        new AlgoChoice(0, "Линейный",  "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><line x1=\"4\" y1=\"20\" x2=\"20\" y2=\"4\"/><circle cx=\"7\" cy=\"8\" r=\"1\"/><circle cx=\"9\" cy=\"6\" r=\"1\"/><circle cx=\"15\" cy=\"18\" r=\"1\"/><circle cx=\"17\" cy=\"16\" r=\"1\"/></svg>"),
        new AlgoChoice(1, "Луны",      "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><path d=\"M4 13 A6 6 0 0 1 14 9\"/><path d=\"M20 11 A6 6 0 0 0 10 15\"/></svg>"),
        new AlgoChoice(2, "Кольца",    "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><circle cx=\"12\" cy=\"12\" r=\"3.5\"/><circle cx=\"12\" cy=\"12\" r=\"8\"/></svg>"),
        new AlgoChoice(3, "Шахматка",  "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><rect x=\"4\" y=\"4\" width=\"7\" height=\"7\"/><rect x=\"13\" y=\"4\" width=\"7\" height=\"7\" fill=\"currentColor\"/><rect x=\"4\" y=\"13\" width=\"7\" height=\"7\" fill=\"currentColor\"/><rect x=\"13\" y=\"13\" width=\"7\" height=\"7\"/></svg>"),
    ];

    private static readonly AlgoChoice[] AeDatasetChoices =
    [
        new AlgoChoice(0, "Кольцо",   "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><circle cx=\"12\" cy=\"12\" r=\"7\"/></svg>"),
        new AlgoChoice(1, "Спираль",  "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><path d=\"M12 12 m -3 0 a 3 3 0 1 0 6 0 a 3 3 0 1 0 -6 0 M12 12 m -6 0 a 6 6 0 1 0 12 0\"/></svg>"),
        new AlgoChoice(2, "Эллипс",   "<svg width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"><ellipse cx=\"12\" cy=\"12\" rx=\"8\" ry=\"4\"/></svg>"),
    ];

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new CategoryDef("nn_classification", "Классификация",
            "MLP-классификатор на основе полносвязных слоёв ReLU + CrossEntropy (V2)",
            [
                new AlgoDef(
                    "mlp_cls", "MLP-классификатор",
                    "Многослойный перцептрон с конфигурируемыми скрытыми слоями ReLU и выходом Softmax (V2 Adam + CrossEntropy)",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "mlp_cls.md",
                    [
                        new AlgoParam("n",       "Число точек",        40, 400, 160, 10,    "шт.", "Суммарный объём обучающей выборки"),
                        new AlgoParam("hidden",  "Нейронов в скрытом",  4,  64,  16,  2,       "", "Число нейронов в скрытом слое"),
                        new AlgoParam("layers",  "Скрытых слоёв",       1,   3,   2,  1,       "", "Число скрытых слоёв"),
                        new AlgoParam("epochs",  "Эпохи",              10, 400,  80, 10,       "", "Число проходов обучения"),
                        new AlgoParam("lr",      "Скорость обучения", 0.001, 0.05, 0.01, 0.001, "", "Learning rate (Adam)"),
                        new AlgoParam("dataset", "Датасет",             0,   3,   1,  1,       "", "Тип распределения") { Choices = ClsDatasetChoices },
                        new AlgoParam("seed",    "Seed",                0, 200,  42,  1,       "", "Инициализация генератора"),
                    ]),
            ]),

        new CategoryDef("nn_regression", "Регрессия",
            "Аппроксимация нелинейных функций через полносвязную нейросеть V2",
            [
                new AlgoDef(
                    "mlp_reg_1d", "Нейрорегрессия 1D",
                    "Нелинейная регрессия: Linear(1,h)->ReLU->Linear(h,1). Аппроксимация y = sin(1.5x) + 0.5·x",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "mlp_reg_1d.md",
                    [
                        new AlgoParam("hidden", "Нейронов в скрытом",  4,  64, 20,  2,       "", "Число нейронов в скрытом слое"),
                        new AlgoParam("epochs", "Эпохи",              20, 500, 200, 20,       "", "Число эпох обучения"),
                        new AlgoParam("lr",     "Скорость обучения", 0.001, 0.1, 0.01, 0.001, "", "Learning rate (Adam)"),
                        new AlgoParam("noise",  "Шум σ",             0.0, 1.0, 0.15, 0.05,   "", "Стандартное отклонение шума"),
                    ]),

                new AlgoDef(
                    "mlp_reg_2d", "Нейрорегрессия 2D",
                    "Аппроксимация двумерной функции z = sin(x)·cos(y). Выводится тепловая карта предсказаний",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "mlp_reg_2d.md",
                    [
                        new AlgoParam("hidden", "Нейронов в скрытом",  8,  64, 24,  2,       "", "Число нейронов в скрытом слое"),
                        new AlgoParam("n",      "Точек обучения",     50, 400, 160, 10,   "шт.", "Число обучающих точек"),
                        new AlgoParam("epochs", "Эпохи",              20, 400, 120, 20,       "", "Число эпох обучения"),
                        new AlgoParam("lr",     "Скорость обучения", 0.001, 0.1, 0.02, 0.001, "", "Learning rate"),
                    ]),

                new AlgoDef(
                    "mlp_reg_2d_3d", "Нейрорегрессия 2D -> 3D",
                    "3D-поверхность предсказаний нейросети для z = sin(x)·cos(y) с обучающими точками",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "mlp_reg_2d.md",
                    [
                        new AlgoParam("hidden",    "Нейронов в скрытом",  8,  64, 24,  2,       "", "Число нейронов в скрытом слое"),
                        new AlgoParam("n",         "Точек обучения",     50, 400, 160, 10,   "шт.", "Число обучающих точек"),
                        new AlgoParam("epochs",    "Эпохи",              20, 400, 120, 20,       "", "Число эпох обучения"),
                        new AlgoParam("lr",        "Скорость обучения", 0.001, 0.1, 0.02, 0.001, "", "Learning rate"),
                        new AlgoParam("azimuth",   "Азимут камеры",    -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры",  -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),
            ]),

        new CategoryDef("nn_sequence", "Прогноз рядов",
            "RNN/LSTM/GRU/Filter/Transformer для предсказания временных рядов (V2)",
            [
                new AlgoDef(
                    "gru_predict", "GRU-прогноз",
                    "Прогнозирование временного ряда рекуррентной сетью GRU(16). Sliding-window -> autoregressive decode",
                    "AI.ML.NeuralNetworks.V2.Nn.GRU",
                    "gru_predict.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",   150, 400, 240, 10, "шт.", "Длина обучающего ряда"),
                        new AlgoParam("predLen",  "Горизонт прогноза", 10, 100,  40,  5, "шт.", "Сколько точек предсказать вперёд"),
                        new AlgoParam("window",   "Окно контекста",     4,  20,   8,  1,    "", "Длина входного окна для GRU"),
                        new AlgoParam("freq",     "Частота ряда",    0.05, 0.4, 0.12, 0.01, "", "Базовая частота синусоиды"),
                    ]),
                new AlgoDef(
                    "lstm_predict", "LSTM-прогноз",
                    "LSTM(16): долгосрочная память позволяет лучше улавливать длинные зависимости в рядах",
                    "AI.ML.NeuralNetworks.V2.Nn.LSTM",
                    "lstm_predict.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",    150, 400, 240, 10, "шт.", "Длина обучающего ряда"),
                        new AlgoParam("predLen",  "Горизонт прогноза",  10, 100,  40,  5, "шт.", "Сколько точек предсказать"),
                        new AlgoParam("window",   "Окно контекста",      4,  20,   8,  1,    "", "Длина входного окна"),
                        new AlgoParam("freq",     "Частота ряда",     0.05, 0.4, 0.12, 0.01, "", "Базовая частота синусоиды"),
                        new AlgoParam("epochs",   "Эпохи",              10, 200,  80, 10,    "", "Число эпох обучения"),
                        new AlgoParam("lr",       "Скорость обучения", 0.001, 0.05, 0.005, 0.001, "", "Learning rate (Adam)"),
                    ]),
                new AlgoDef(
                    "filter_predict", "Фильтр (MLP)",
                    "Полносвязная сеть без рекуррентности — базовая линия: Linear->ReLU->Linear->ReLU->Linear",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "filter_predict.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",    150, 400, 240, 10, "шт.", "Длина обучающего ряда"),
                        new AlgoParam("predLen",  "Горизонт прогноза",  10, 100,  40,  5, "шт.", "Сколько точек предсказать"),
                        new AlgoParam("window",   "Окно контекста",      4,  20,   8,  1,    "", "Длина входного окна"),
                        new AlgoParam("freq",     "Частота ряда",     0.05, 0.4, 0.12, 0.01, "", "Базовая частота синусоиды"),
                        new AlgoParam("hidden",   "Скрытых нейронов",    8,  64,  16,  4,    "", "Нейронов в скрытом слое"),
                        new AlgoParam("epochs",   "Эпохи",              10, 200,  80, 10,    "", "Число эпох обучения"),
                        new AlgoParam("lr",       "Скорость обучения", 0.001, 0.05, 0.005, 0.001, "", "Learning rate"),
                    ]),
                new AlgoDef(
                    "transformer_predict", "Transformer-прогноз",
                    "TransformerEncoder с позиционным кодированием для предсказания рядов",
                    "AI.ML.NeuralNetworks.V2.Nn.TransformerEncoderLayer",
                    "transformer_predict.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",    150, 400, 240, 10, "шт.", "Длина обучающего ряда"),
                        new AlgoParam("predLen",  "Горизонт прогноза",  10, 100,  40,  5, "шт.", "Сколько точек предсказать"),
                        new AlgoParam("window",   "Окно контекста",      4,  20,   8,  1,    "", "Длина входного окна"),
                        new AlgoParam("freq",     "Частота ряда",     0.05, 0.4, 0.12, 0.01, "", "Базовая частота синусоиды"),
                        new AlgoParam("dModel",   "d_model",             8,  64,  16,  8,    "", "Размер модели / эмбеддинга"),
                        new AlgoParam("nHead",    "Число голов",          1,   4,   2,  1,    "", "Число голов внимания"),
                        new AlgoParam("epochs",   "Эпохи",              10, 200,  80, 10,    "", "Число эпох обучения"),
                        new AlgoParam("lr",       "Скорость обучения", 0.001, 0.05, 0.005, 0.001, "", "Learning rate"),
                    ]),
            ]),

        new CategoryDef("nn_compare", "Сравнение архитектур",
            "Обучение и сравнение Filter, RNN, LSTM, GRU и Transformer на одном временном ряде",
            [
                new AlgoDef(
                    "rnn_compare", "RNN vs LSTM vs GRU vs Filter vs Transformer",
                    "5 архитектур на одних данных: MSE и время обучения/инференса каждой модели",
                    "AI.ML.NeuralNetworks.V2.Nn",
                    "rnn_compare.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",    150, 400, 240, 10, "шт.", "Длина обучающего ряда"),
                        new AlgoParam("predLen",  "Горизонт прогноза",  10, 100,  40,  5, "шт.", "Сколько точек предсказать"),
                        new AlgoParam("window",   "Окно контекста",      4,  20,   8,  1,    "", "Длина входного окна"),
                        new AlgoParam("epochs",   "Эпохи",              10, 200,  60, 10,    "", "Число эпох обучения"),
                    ]),
            ]),

        new CategoryDef("nn_language", "Языковые модели",
            "LSTM-языковая модель: обучение на текстовом корпусе и генерация продолжений (V2)",
            [
                new AlgoDef(
                    "lstm_lm", "LSTM языковая модель",
                    "Embedding -> LSTMCell -> Linear: обучение на тексте, генерация продолжения по промпту",
                    "AI.ML.NeuralNetworks.V2.Nn.LSTMCell",
                    "lstm_lm.md",
                    [
                        new AlgoParam("epochs",    "Эпохи",             5,  50,  15,  5,    "", "Число эпох обучения"),
                        new AlgoParam("hiddenSize","Размер скрытого",  16,  64,  32,  8,    "", "Hidden state LSTM"),
                        new AlgoParam("embDim",    "Размер эмбеддинга", 8,  32,  16,  4,    "", "Embedding dimension"),
                        new AlgoParam("maxTokens", "Макс. токенов",     5,  50,  20,  5, "шт.", "Максимум слов в генерации"),
                        new AlgoParam("_corpus",   "Корпус", 0, 0, 0, 0, "",
                            "Вставьте свой текст или оставьте пустым для встроенного",
                            TextDefault: ""),
                        new AlgoParam("_prompt",   "Промпт", 0, 0, 0, 0, "",
                            "Начальная фраза для генерации",
                            TextDefault: "нейронные"),
                    ]),
            ]),

        new CategoryDef("nn_features", "Обучение представлений",
            "Автоэнкодер — нейросетевое снижение размерности и реконструкция (V2)",
            [
                new AlgoDef(
                    "autoencoder", "Автоэнкодер",
                    "Обучение латентного пространства малой размерности с последующей реконструкцией входа (V2 Sequential + MSE)",
                    "AI.ML.NeuralNetworks.V2.Nn.Sequential",
                    "autoencoder.md",
                    [
                        new AlgoParam("n",       "Число точек",     50, 400, 200, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("latent",  "Размер латента",   1,   3,   1,  1,    "", "Размерность скрытого представления"),
                        new AlgoParam("epochs",  "Эпохи",           10, 300,  80, 10,    "", "Число эпох обучения"),
                        new AlgoParam("lr",      "Скорость обучения", 0.001, 0.05, 0.01, 0.001, "", "Learning rate (Adam)"),
                        new AlgoParam("dataset", "Датасет",
                                                                     0,   2,   0,  1,    "", "Тип многообразия") { Choices = AeDatasetChoices },
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
        => NeuralNetworksDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
