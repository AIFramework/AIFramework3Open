# Туториалы: AI.NLP

Краткая теория и соответствие методам API по исходным файлам сборки **`AI.NLP`**. Обзор архитектуры и зависимостей: [../../Architecture/NLP.md](../../Architecture/NLP.md).

| Файл | Документ |
|------|----------|
| `ProbabilityDictionary.cs`, `ProbabilityDictionaryData.cs` | [ProbabilityDictionary.md](ProbabilityDictionary.md) |
| `ProbDictHash.cs` | [ProbabilityDictionaryHash.md](ProbabilityDictionaryHash.md) |
| `TFIDF.cs` | [TFIDF.md](TFIDF.md) |
| `TFIDFDictionary.cs` | [TFIDFDictionary.md](TFIDFDictionary.md) |
| `BoWModel.cs` | [BoWModel.md](BoWModel.md) |
| `TextTokenizer.cs` | [TextTokenizer.md](TextTokenizer.md) |
| `TextStandard.cs` | [TextStandard.md](TextStandard.md) |
| `TextSummarization.cs` | [TextSummarization.md](TextSummarization.md) |
| `StringExtention.cs` | [StringExtention.md](StringExtention.md) |
| `Stemmers/Stemmer.cs` | [StemmerRus.md](StemmerRus.md) |
| `Stemmers/WordEndingsRU.cs` | [WordEndingsRU.md](WordEndingsRU.md) |

Примеры запуска: **`Tests/NLP/NlpExamples`**.

```bash
dotnet run --project Tests/NLP/NlpExamples -c Release
```
