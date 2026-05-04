using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.ONNX
{
    public sealed class OnnxModule : LibraryModuleBase
    {
        public override string Id => "onnx";
        public override string Name => "AI.ONNX";
        public override string Description => "Инференс ONNX-моделей: тензорные трансформации, классификаторы, BERT-эмбеддинги";
        public override string Color => "emerald";
        public override string TutorialFolder => "ONNX";

        public override string IconSvg => """
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <rect x="2"  y="8"  width="5" height="8" rx="1"/>
              <rect x="17" y="8"  width="5" height="8" rx="1"/>
              <rect x="9"  y="2"  width="6" height="6" rx="1"/>
              <rect x="9"  y="16" width="6" height="6" rx="1"/>
              <line x1="7"  y1="12" x2="9"  y2="8"/>
              <line x1="7"  y1="12" x2="9"  y2="16"/>
              <line x1="17" y1="12" x2="15" y2="8"/>
              <line x1="17" y1="12" x2="15" y2="16"/>
            </svg>
            """;

        #region Наборы вариантов (AlgoChoice)

        private static readonly AlgoChoice[] ActivationChoices =
        [
            new(0, "ReLU"),
            new(1, "Sigmoid"),
            new(2, "Tanh"),
            new(3, "Linear"),
        ];

        private static readonly AlgoChoice[] PoolingChoices =
        [
            new(0, "Mean"),
            new(1, "Max"),
            new(2, "CLS-token"),
        ];

        #endregion

        public override IReadOnlyList<CategoryDef> Categories { get; } =
        [
            #region 1. Tensor2Tensor / Dense
            new CategoryDef("inference", "Инференс тензоров",
                "Tensor2Tensor и Dense: прямой проход через слои ONNX-модели",
                [
                    new AlgoDef("dense_inference", "Dense Layer (Линейный слой)",
                        "Прямой проход: y = W·x + b; визуализация входного и выходного векторов",
                        "AI.ONNX.Base.LayersModel.Dense",
                        "onnx_dense.md",
                        [
                            new AlgoParam("inputDim",  "Вход (N)",  2, 64, 16, 2, "нейр.", "Размерность входного вектора"),
                            new AlgoParam("outputDim", "Выход (M)", 2, 32,  8, 2, "нейр.", "Размерность выходного вектора"),
                            new AlgoParam("activation","Активация", 0,  3,  0, 1,  "",     "Нелинейная функция активации")
                                { Choices = ActivationChoices },
                            new AlgoParam("seed",      "Seed",      0, 100, 42, 1, "",     "Инициализация генератора"),
                        ]),
                    new AlgoDef("t2t_image", "Tensor2Tensor (изображение)",
                        "Симуляция прохода изображения [H×W×C] через ONNX-трансформацию; Keras/PyTorch LibType",
                        "AI.ONNX.Tensor2Tensor",
                        "onnx_t2t.md",
                        [
                            new AlgoParam("H",     "Высота",        4, 32, 16, 4,  "пкс.", "Высота входного тензора"),
                            new AlgoParam("W",     "Ширина",        4, 32, 16, 4,  "пкс.", "Ширина входного тензора"),
                            new AlgoParam("C",     "Каналов",       1,  4,  1, 1,  "",     "Глубина (каналы)"),
                            new AlgoParam("outDim","Выход (M)",     4, 64, 16, 4,  "",     "Размер выходного вектора"),
                            new AlgoParam("seed",  "Seed",          0, 100, 42, 1,  "",     "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 2. Классификаторы
            new CategoryDef("classifiers", "Классификаторы",
                "GrayScaleClassifier и многоклассовый softmax",
                [
                    new AlgoDef("softmax_cls", "Softmax-классификатор",
                        "Многоклассовый линейный классификатор с softmax; визуализация вероятностей классов",
                        "AI.ONNX.Classifiers.GrayScaleClassifier",
                        "onnx_classifier.md",
                        [
                            new AlgoParam("inputDim",  "Признаков", 2, 64, 16, 2, "",    "Размерность входного вектора"),
                            new AlgoParam("numClasses","Классов",   2, 20,  5, 1, "шт.", "Число классов"),
                            new AlgoParam("seed",      "Seed",      0, 100, 42, 1, "",    "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 3. NLP / BERT-эмбеддинги
            new CategoryDef("nlp", "NLP / BERT-эмбеддинги",
                "BertEmbedder, BertInfer, BertConfig: получение и анализ текстовых эмбеддингов",
                [
                    new AlgoDef("embed_cosine", "Сходство эмбеддингов",
                        "Косинусное сходство между токенами/предложениями; тепловая карта матрицы сходства",
                        "AI.ONNX.NLP.Bert.BertEmbedder",
                        "onnx_bert_embed.md",
                        [
                            new AlgoParam("numTokens", "Токенов",     3, 20,  8, 1, "шт.", "Число токенов/предложений"),
                            new AlgoParam("embedDim",  "Размерность", 4, 64, 16, 4, "",    "Размерность эмбеддинга"),
                            new AlgoParam("pooling",   "Пулинг",      0,  2,  0, 1, "",    "Метод получения вектора предложения")
                                { Choices = PoolingChoices },
                            new AlgoParam("seed",      "Seed",        0, 100, 42, 1, "",    "Инициализация генератора"),
                        ]),
                    new AlgoDef("bert_config", "BertConfig / Архитектура",
                        "Разбор конфигурации BERT-модели; визуализация параметров архитектуры",
                        "AI.ONNX.NLP.Bert.BertConfig",
                        "onnx_bert_config.md",
                        [
                            new AlgoParam("hiddenSize",      "Hidden size",     64, 1024, 384, 64, "",   "Размер скрытого состояния"),
                            new AlgoParam("numLayers",       "Слоёв",            1,   24,   6,  1, "",   "Число слоёв трансформера"),
                            new AlgoParam("numHeads",        "Голов",            1,   16,   6,  1, "",   "Число голов внимания"),
                            new AlgoParam("intermediateSize","FFN размер",      64, 4096, 1536, 64, "",  "Размер FFN-слоя"),
                            new AlgoParam("vocabSize",       "Словарь",        100, 50000, 30522, 100, "", "Размер словаря"),
                        ]),
                ]),
            #endregion
        ];

        protected override DemoResult RunCore(
            string algoKey,
            IReadOnlyDictionary<string, double> numericParams,
            IReadOnlyDictionary<string, string> textParams,
            DemoSettings settings) =>
            OnnxDemoRunner.Run(algoKey, numericParams, settings);
    }
}
