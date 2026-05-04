# Примеры NLP (AI.NLP)

Документация: [../../Docs/Architecture/NLP.md](../../Docs/Architecture/NLP.md), туториалы по API: [../../Docs/Tutorials/NLP/README.md](../../Docs/Tutorials/NLP/README.md).

Консольный проект **`NlpExamples`** показывает базовые сценарии сборки **`AI.NLP`**:

- **`ProbabilityDictionary`** — частоты и «вероятности» по токенам, топ слов.
- **`ProbabilityDictionaryHash`** — те же частоты в виде `Dictionary<string, double>`.
- **`TFIDF`** — TF, IDF, произведение TF‑IDF, поиск документа по запросу (`Search`).
- **`ProbabilityDictionary.GetWords(text, IsStem)`** — статическая токенизация для последующих расчётов.
- **`TextTokenizer`** — обучение на тексте и преобразование фрагмента в вектор индексов.
- **`BoWModel`** — словарь из файла (временный), вектор мешка слов.
- **`TextStandard`** и **`StemmerRus`** — нормализация и стемминг.

## Запуск

Из корня репозитория:

```bash
dotnet run --project Tests/NLP/NlpExamples -c Release
```

Требуется подключённый к решению проект **`src/AI.NLP/AI.NLP.csproj`** (сборка `AI.NLP.dll`).
