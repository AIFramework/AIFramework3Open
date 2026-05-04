using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.NLP
{
    public sealed class NlpModule : LibraryModuleBase
    {
        public override string Id => "nlp";
        public override string Name => "AI.NLP";
        public override string Description => "NLP: нормализация, TF-IDF, BM25, стемминг, лемматизация, суммаризация, генерация текста (Марков), NER";
        public override string Color => "violet";
        public override string TutorialFolder => "NLP";

        public override string IconSvg => """
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M4 6h16M4 10h10M4 14h12M4 18h8"/>
              <circle cx="19" cy="16" r="3"/>
              <line x1="21.5" y1="18.5" x2="23" y2="20"/>
            </svg>
            """;

        #region Наборы вариантов (AlgoChoice)

        private static readonly AlgoChoice[] TextSetChoices =
        [
            new(0, "Технический RU"),
            new(1, "Новостной RU"),
            new(2, "Научный"),
        ];

        private static readonly AlgoChoice[] MorphModeChoices =
        [
            new(0, "Только стемминг"),
            new(1, "Только лемматизация"),
            new(2, "Сравнение"),
        ];

        #endregion

        public override IReadOnlyList<CategoryDef> Categories { get; } =
        [
            #region 1. Нормализация текста
            new CategoryDef("text_norm", "Нормализация текста",
                "TextStandard: Normalize, OnlyChars, OnlyRusChars, NoDoubleWord, Dice similarity",
                [
                    new AlgoDef("text_normalize", "Стандартизация TextStandard",
                        "Нормализация, фильтрация символов, удаление дублей, коэффициент Дайса между парами текстов",
                        "AI.NLP.TextStandard",
                        "nlp_text_norm.md",
                        [
                            new AlgoParam("textId", "Текст", 0, 2, 0, 1, "",
                                "Набор примеров текстов") { Choices = TextSetChoices },
                            new AlgoParam("isLower", "Lowercase", 0, 1, 1, 1, "",
                                "Приводить к нижнему регистру при нормализации"),
                        ]),
                ]),
            #endregion

            #region 2. Частотный анализ
            new CategoryDef("freq_analysis", "Частотный анализ слов",
                "ProbabilityDictionary и ProbabilityDictionaryHash: вероятности слов, стоп-слова, стемминг",
                [
                    new AlgoDef("prob_dict", "Вероятностный словарь",
                        "ProbabilityDictionary — ранжирование слов по частоте с опциональным стеммингом и удалением стоп-слов",
                        "AI.NLP.ProbabilityDictionary",
                        "nlp_prob_dict.md",
                        [
                            new AlgoParam("topN",      "Топ-N слов",      5, 40, 15, 5,  "шт.", "Показать N наиболее частых слов"),
                            new AlgoParam("isStop",    "Удал. стоп-сл.",  0,  1,  1,  1, "",   "Удалять стоп-слова из анализа"),
                            new AlgoParam("isStem",    "Стемминг",        0,  1,  1,  1, "",   "Применять стемминг"),
                            new AlgoParam("textId",    "Текст",           0,  2,  0,  1, "",   "Исходный текст для анализа")
                                { Choices = TextSetChoices },
                        ]),
                ]),
            #endregion

            #region 3. TF-IDF
            new CategoryDef("tfidf", "TF-IDF",
                "Весовая схема Term Frequency — Inverse Document Frequency для поиска и анализа документов",
                [
                    new AlgoDef("tfidf_demo", "TF-IDF и поиск",
                        "Вычисление TF, IDF, TF-IDF для корпуса документов; поиск релевантного документа по запросу",
                        "AI.NLP.TFIDF",
                        "nlp_tfidf.md",
                        [
                            new AlgoParam("queryId", "Запрос", 0, 4, 0, 1, "",
                                "Поисковый запрос к корпусу")
                                { Choices = new AlgoChoice[] {
                                    new(0, "нейронная сеть"),
                                    new(1, "экономика рынок"),
                                    new(2, "спорт чемпионат"),
                                    new(3, "наука открытие"),
                                    new(4, "погода климат"),
                                }},
                            new AlgoParam("topWords", "Топ слов/документ", 2, 10, 5, 1, "",
                                "Показывать топ-N слов по TF-IDF для каждого документа"),
                        ]),
                    new AlgoDef("bm25_demo", "BM25 и поиск",
                        "Okapi BM25: ранжирование документов с насыщением TF и нормализацией длины; сравнение с TF-IDF",
                        "AI.NLP.BM25",
                        "nlp_bm25.md",
                        [
                            new AlgoParam("queryId", "Запрос", 0, 4, 0, 1, "",
                                "Поисковый запрос к корпусу")
                                { Choices = new AlgoChoice[] {
                                    new(0, "нейронная сеть"),
                                    new(1, "экономика рынок"),
                                    new(2, "спорт чемпионат"),
                                    new(3, "наука открытие"),
                                    new(4, "погода климат"),
                                }},
                            new AlgoParam("k1", "k₁ (насыщение TF)", 0, 3, 15, 1, "",
                                "Параметр насыщения частоты термина (×0.1, т.е. 15 -> 1.5)"),
                            new AlgoParam("b", "b (норм. длины)", 0, 10, 8, 1, "",
                                "Параметр нормализации длины документа (×0.1, т.е. 8 -> 0.8)"),
                            new AlgoParam("topWords", "Топ слов/документ", 2, 10, 5, 1, "",
                                "Показывать топ-N слов по BM25 для каждого документа"),
                        ]),
                ]),
            #endregion

            #region 4. Токенизация
            new CategoryDef("tokenize", "Токенизация",
                "TextTokenizer: словарная токенизация с One-Hot и sequence encoding",
                [
                    new AlgoDef("text_tokenizer", "TextTokenizer",
                        "Обучение TextTokenizer на корпусе, кодирование последовательностей, one-hot векторы",
                        "AI.NLP.TextTokenizer",
                        "nlp_text_tok.md",
                        [
                            new AlgoParam("vocabSize", "Размер словаря", 5, 100, 30, 5,  "",   "Максимальное число токенов (Count)"),
                            new AlgoParam("isStem",    "Стемминг",      0,   1,  0,  1,  "",   "Применять стемминг при построении словаря"),
                            new AlgoParam("textId",    "Корпус",        0,   2,  0,  1,  "",   "Обучающий корпус") { Choices = TextSetChoices },
                        ]),
                ]),
            #endregion

            #region 5. Морфология
            new CategoryDef("morph", "Стемминг и лемматизация",
                "StemmerRus (правиловый стеммер), RussianLemmatizer, CachingLemmatizer",
                [
                    new AlgoDef("stemming", "Стемминг (StemmerRus)",
                        "Алгоритм отсечения окончаний для русских слов: сравнение словоформ со стеммами",
                        "AI.NLP.Stemmers.StemmerRus",
                        "nlp_stemmer.md",
                        [
                            new AlgoParam("wordSet", "Набор слов", 0, 2, 0, 1, "",
                                "Примеры словоформ для стемминга")
                                { Choices = new AlgoChoice[] {
                                    new(0, "Существительные"),
                                    new(1, "Глаголы"),
                                    new(2, "Прилагательные"),
                                }},
                        ]),
                    new AlgoDef("lemmatize", "Лемматизация",
                        "RussianLemmatizer: приведение к словарной форме. CachingLemmatizer: кеш запросов",
                        "AI.NLP.Lemmatization.RussianLemmatizer",
                        "nlp_lemma.md",
                        [
                            new AlgoParam("mode", "Режим", 0, 2, 0, 1, "",
                                "Только лемматизация / сравнение с стеммингом / тест кеша")
                                { Choices = new AlgoChoice[] {
                                    new(0, "Лемматизация"),
                                    new(1, "Стемм vs Лемма"),
                                    new(2, "Тест CachingLemmatizer"),
                                }},
                            new AlgoParam("wordSet", "Набор слов", 0, 2, 0, 1, "",
                                "Примеры словоформ") { Choices = new AlgoChoice[] {
                                    new(0, "Существительные"),
                                    new(1, "Глаголы"),
                                    new(2, "Смешанный"),
                                }},
                        ]),
                ]),
            #endregion

            #region 6. Генерация текста (Марковские цепи)
            new CategoryDef("text_gen", "Генерация текста",
                "HMMFast: n-граммные марковские цепи — обучение на корпусе и генерация текста по начальной фразе",
                [
                    new AlgoDef("markov_gen", "Марковские цепи (n-граммы)",
                        "Обучение модели на корпусе и генерация текста с заданным seed. Чем больше n-грамма, тем связнее текст",
                        "AI.DataPrepaire.NLPUtils.TextGeneration.HMMFast",
                        "nlp_markov.md",
                        [
                            new AlgoParam("ngram", "Размер n-граммы", 2, 5, 3, 1, "",
                                "Длина контекста в словах (2=биграмма, 3=триграмма и т.д.)"),
                            new AlgoParam("genLength", "Длина генерации", 10, 200, 60, 10, "слов",
                                "Максимальное число сгенерированных слов"),
                            new AlgoParam("textId", "Корпус", 0, 2, 0, 1, "",
                                "Встроенный обучающий корпус") { Choices = TextSetChoices },
                            new AlgoParam("_corpus", "Свой корпус", 0, 0, 0, 0, "",
                                "Вставьте свой текст для обучения (или оставьте пустым для встроенного)",
                                TextDefault: ""),
                            new AlgoParam("_seed", "Начальная фраза", 0, 0, 0, 0, "",
                                "Слова для затравки генерации",
                                TextDefault: "нейронные сети"),
                        ]),
                ]),
            #endregion

            #region 7. NER (распознавание сущностей)
            new CategoryDef("ner", "Распознавание сущностей (NER)",
                "CombineNerProcessor: извлечение именованных сущностей (время, email, телефон, адрес, имена) и разбиение на предложения",
                [
                    new AlgoDef("ner_demo", "Regexp NER + предложения",
                        "Маскирование сущностей по правилам (RegEx) и интеллектуальное разбиение текста на предложения с учётом сокращений",
                        "AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.CombineNerProcessor",
                        "nlp_ner.md",
                        [
                            new AlgoParam("_text", "Текст для анализа", 0, 0, 0, 0, "",
                                "Вставьте свой текст или оставьте пустым для примера",
                                TextDefault: "Добрый день. В 900 году до н. э. было это. Мой номер +8 999 666 555 4. А.В. Александров идет к И.К. Гаврилову. Сайт vkre.com/su. Почта zzszzs@mszk.com. Адрес ул. Гон, д. 56, кв. 882. Созвонимся в 22:39 или завтра в 09:15."),
                        ]),
                ]),
            #endregion

            #region 8. Суммаризация текста
            new CategoryDef("summarize", "Суммаризация текста",
                "TextSummarization: экстрактивная суммаризация на основе TF-IDF-весов предложений",
                [
                    new AlgoDef("text_summarize", "TextSummarization",
                        "Извлечение ключевых предложений из длинного текста по метрике значимости",
                        "AI.NLP.TextSummarization",
                        "nlp_summarize.md",
                        [
                            new AlgoParam("numSents", "Предложений в резюме", 1, 5, 2, 1, "шт.",
                                "Число предложений в итоговом резюме"),
                            new AlgoParam("textId", "Текст", 0, 2, 0, 1, "",
                                "Исходный документ для суммаризации") { Choices = TextSetChoices },
                        ]),
                ]),
            #endregion
        ];

        protected override DemoResult RunCore(
            string algoKey,
            IReadOnlyDictionary<string, double> numericParams,
            IReadOnlyDictionary<string, string> textParams,
            DemoSettings settings) =>
            NlpDemoRunner.Run(algoKey, numericParams, textParams, settings);
    }
}
