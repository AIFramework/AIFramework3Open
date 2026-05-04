# Вероятностный словарь (AI.NLP)

## Частотный анализ слов

`ProbabilityDictionary` строит ранжированный словарь слов с их относительными частотами (вероятностями) в тексте.

## Предобработка

Перед подсчётом можно:
- **Удалить стоп-слова** (`isStopDel`) — служебные слова (предлоги, союзы), не несущие тематической нагрузки
- **Удалить числа** (`isDigitDel`) — числовые токены
- **Применить стемминг** (`isStem`) — привести к корневой форме

## Вероятность слова

$$
P(w) = \frac{C(w)}{\sum_{w'} C(w')}
$$

где $C(w)$ — число вхождений слова $w$ в текст.

## Закон Ципфа

В естественных языках ранжированные по частоте слова следуют степенному закону:

$$
P(w_n) \propto \frac{1}{n}
$$

Топ-100 слов покрывают ~50% объёма текста; топ-1000 — около 80%.

## API

```csharp
using AI.NLP;

// Вероятностный словарь
var pd = new ProbabilityDictionary(
    isStopDel:  true,   // удалить стоп-слова
    isDigitDel: true,   // удалить числа
    isStem:     true    // применить стемминг
);

// Получить ранжированный массив
ProbabilityDictionaryData<string>[] result = pd.Run(text);
// result[i].Word — слово; result[i].Probability — вероятность

// Топ-N слов как строковый массив
string[] top20 = pd.GetWordsRun(text, numW: 20);

// Все слова
string[] all = pd.GetWordsRunAll(text);

// Хеш-версия (быстрее, без сортировки)
var pdh = new ProbabilityDictionaryHash(isStem: true);
Dictionary<string, double> dict = pdh.Run(text);

// Список стоп-слов
string[] stopWords = ProbabilityDictionary.StopWords;
```
