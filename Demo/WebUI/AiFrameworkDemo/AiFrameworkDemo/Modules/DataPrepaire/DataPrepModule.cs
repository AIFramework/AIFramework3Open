using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.DataPrepaire
{
    public sealed class DataPrepModule : LibraryModuleBase
    {
        public override string Id => "dataprep";
        public override string Name => "AI.DataPrepaire";
        public override string Description => "Нормализация, токенизация, NER, генерация текста, DataTable, конвейеры подготовки данных";
        public override string Color => "sky";
        public override string TutorialFolder => "DataPrepaire";

        public override string IconSvg => """
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <rect x="2" y="4" width="20" height="4" rx="1"/>
              <rect x="2" y="10" width="20" height="4" rx="1"/>
              <rect x="2" y="16" width="14" height="4" rx="1"/>
              <circle cx="20" cy="18" r="2"/>
              <line x1="20" y1="14" x2="20" y2="16"/>
            </svg>
            """;

        #region Наборы вариантов (AlgoChoice)

        private static readonly AlgoChoice[] NormTypeChoices =
        [
            new(0, "Z-нормализация"),
            new(1, "Min-Max"),
            new(2, "Сравнение обоих"),
        ];

        private static readonly AlgoChoice[] DistribChoices =
        [
            new(0, "Нормальное"),
            new(1, "Лог-нормальное"),
            new(2, "Равномерное"),
        ];

        private static readonly AlgoChoice[] NERTypeChoices =
        [
            new(0, "Телефон"),
            new(1, "Email"),
            new(2, "Время"),
            new(3, "Пользовательский Regex"),
        ];

        #endregion

        public override IReadOnlyList<CategoryDef> Categories { get; } =
        [
            #region 1. Нормализация данных
            new CategoryDef("normalizers", "Нормализация данных",
                "ZNormalizer (z-score) и MinimaxNormalizer: обучение, преобразование, денормализация",
                [
                    new AlgoDef("normalizers_demo", "ZNorm vs MinMax",
                        "Сравнение Z-нормализации и Min-Max нормализации на одном наборе данных",
                        "AI.DataPrepaire.DataNormalizers",
                        "dp_normalizers.md",
                        [
                            new AlgoParam("n",       "Число точек",  20, 500, 200, 20,  "шт.", "Размер набора данных"),
                            new AlgoParam("dims",    "Размерность",   1,   8,   3,  1,   "",   "Число признаков (размерность вектора)"),
                            new AlgoParam("distrib", "Распределение", 0,   2,   0,  1,   "",   "Тип исходного распределения")
                                { Choices = DistribChoices },
                            new AlgoParam("seed",    "Seed",          0, 100,  42,  1,   "",   "Инициализация генератора"),
                        ]),
                ]),
            #endregion

            #region 2. Токенизаторы
            new CategoryDef("tokenizers", "Токенизаторы",
                "WordTokenizer, CharTokenizer, BPECore: обучение на тексте, кодирование/декодирование",
                [
                    new AlgoDef("word_tokenizer", "WordTokenizer",
                        "Словарный токенизатор: обучение на корпусе, кодирование, распределение токенов (закон Ципфа)",
                        "AI.DataPrepaire.Tokenizers.TextTokenizers.WordTokenizer",
                        "dp_word_tok.md",
                        [
                            new AlgoParam("topK",    "Топ-N токенов",    5,  50, 20,  5,  "шт.", "Показать N самых частых токенов"),
                            new AlgoParam("corpusId", "Корпус",          0,   2,  0,  1,   "",   "Выбор обучающего корпуса")
                                { Choices = new AlgoChoice[] { new(0, "AI-термины"), new(1, "Сказки"), new(2, "Смешанный") } },
                            new AlgoParam("isLower", "Lowercase",        0,   1,  1,  1,   "",   "Приводить к нижнему регистру"),
                        ]),
                    new AlgoDef("bpe_demo", "BPE-токенизатор",
                        "Byte-Pair Encoding: обучение на байтах, коэффициент сжатия, сравнение с символьным токенизатором",
                        "AI.DataPrepaire.Tokenizers.TextTokenizers.BPE",
                        "dp_bpe.md",
                        [
                            new AlgoParam("maxNGram", "Макс. n-грамм", 2, 16,  8, 2,  "",   "Максимальный размер BPE-подслова"),
                            new AlgoParam("corpusId", "Корпус",        0,  2,  0, 1,  "",   "Выбор корпуса")
                                { Choices = new AlgoChoice[] { new(0, "Английский"), new(1, "Русский"), new(2, "Код") } },
                        ]),
                ]),
            #endregion

            #region 3. Метрики строк и NER
            new CategoryDef("nlp_utils", "NLP-утилиты",
                "Метрики сходства строк, распознавание именованных сущностей, токенизация предложений",
                [
                    new AlgoDef("str_metrics", "Метрики строк",
                        "Расстояние Левенштейна, корреляция слов, гистограммный косинус для N пар строк",
                        "AI.DataPrepaire.NLPUtils.CompareStringMethods",
                        "dp_str_metrics.md",
                        [
                            new AlgoParam("pairSet", "Набор пар", 0, 2, 0, 1, "",
                                "Набор строковых пар для сравнения")
                                { Choices = new AlgoChoice[] { new(0, "Слова"), new(1, "Фразы RU"), new(2, "Фразы EN") } },
                        ]),
                    new AlgoDef("ner_demo", "NER (Сущности)",
                        "Распознавание именованных сущностей: телефоны, email, время, пользовательский regex",
                        "AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER",
                        "dp_ner.md",
                        [
                            new AlgoParam("nerType",  "Тип NER",  0, 3, 0, 1, "", "Тип распознаваемой сущности")
                                { Choices = NERTypeChoices },
                            new AlgoParam("textSet",  "Текст",    0, 2, 0, 1, "", "Пример текста для анализа")
                                { Choices = new AlgoChoice[] { new(0, "Контакты"), new(1, "Расписание"), new(2, "Смешанный") } },
                        ]),
                    new AlgoDef("sent_tokenizer", "Токенизатор предложений",
                        "SentencesTokenizer: разбивка текста на предложения с учётом аббревиатур",
                        "AI.DataPrepaire.NLPUtils.RegexpNLP.SentencesTokenizer",
                        "dp_sent_tok.md",
                        [
                            new AlgoParam("textId", "Текст", 0, 2, 0, 1, "", "Пример текста")
                                { Choices = new AlgoChoice[] { new(0, "Научный"), new(1, "Деловой"), new(2, "Новостной") } },
                        ]),
                ]),
            #endregion

            #region 4. Генерация и классификация текста
            new CategoryDef("text_gen_cls", "Генерация и классификация",
                "HMMFast (цепи Маркова), TextRuleClassifier",
                [
                    new AlgoDef("hmm_gen", "Марковская генерация (HMM)",
                        "HMMFast: обучение n-граммной модели, генерация текста, распределение вероятностей токенов",
                        "AI.DataPrepaire.NLPUtils.TextGeneration.HMMFast",
                        "dp_hmm.md",
                        [
                            new AlgoParam("genWords", "Генерировать слов", 5, 50, 20,  5, "шт.", "Число генерируемых слов"),
                            new AlgoParam("corpusId", "Корпус",            0,  2,  0,  1,  "",   "Обучающий корпус")
                                { Choices = new AlgoChoice[] { new(0, "Сказки"), new(1, "Технический"), new(2, "Смешанный") } },
                            new AlgoParam("seed",     "Seed",              0, 100, 42, 1,  "",   "Инициализация генератора"),
                        ]),
                    new AlgoDef("text_cls", "TextRuleClassifier",
                        "Классификация текста по правилам и n-граммным признакам",
                        "AI.DataPrepaire.NLPUtils.TextClassification.TextRuleClassifier",
                        "dp_text_cls.md",
                        [
                            new AlgoParam("topP",     "top_p",      0, 1, 0.5, 0.1, "",   "Порог вероятности для классификации"),
                            new AlgoParam("maxNGram", "max_ngram",  1, 5,   3,   1,  "",   "Максимальный размер n-граммы"),
                            new AlgoParam("classSet", "Тематика",   0, 2,   0,   1,  "",   "Набор классов")
                                { Choices = new AlgoChoice[] { new(0, "Новости"), new(1, "Чат-бот"), new(2, "Тех.поддержка") } },
                        ]),
                ]),
            #endregion

            #region 5. DataTable и загрузка данных
            new CategoryDef("datatable", "DataTable и загрузка данных",
                "CSVLoader, DataTable, DataItem: чтение, статистика, категориальное кодирование",
                [
                    new AlgoDef("datatable_demo", "DataTable / CSV",
                        "Создание DataTable из CSV, статистика столбцов, категориальное кодирование, срезы",
                        "AI.DataPrepaire.DataLoader.DataTable",
                        "dp_datatable.md",
                        [
                            new AlgoParam("datasetId", "Набор данных", 0, 2, 0, 1, "",
                                "Встроенный CSV-набор данных")
                                { Choices = new AlgoChoice[] { new(0, "Ирисы"), new(1, "Оценки"), new(2, "Сотрудники") } },
                            new AlgoParam("showRows", "Строк показать", 3, 20, 8, 1, "шт.", "Число строк для вывода"),
                        ]),
                ]),
            #endregion
        ];

        protected override DemoResult RunCore(
            string algoKey,
            IReadOnlyDictionary<string, double> numericParams,
            IReadOnlyDictionary<string, string> textParams,
            DemoSettings settings) =>
            DataPrepDemoRunner.Run(algoKey, numericParams, settings);
    }
}
