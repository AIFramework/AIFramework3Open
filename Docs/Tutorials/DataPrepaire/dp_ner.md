# NER — Распознавание именованных сущностей (AI.DataPrepaire)

## Что такое NER?

Named Entity Recognition (NER) — извлечение из текста структурированных сущностей: имён, телефонов, дат, адресов и т. д.

## Архитектура SimpleNER

В AI.DataPrepaire NER реализован через регулярные выражения (rule-based approach):

```
Текст → NerProcessor.RunProcessor → Текст с токенами <entity_N>
                                            ↓
                               NerDecoder → Оригинальный текст
```

Каждый `NerProcessor` ведёт словарь `NerToNerToken` (сущность → токен) и `NerTokenToNer` (токен → сущность) для двустороннего маппинга.

## Встроенные процессоры

| Класс | Шаблон | Пример |
|-------|--------|--------|
| `PhoneNerProcessor` | `\+?[1-9](?:[\d\-\(\)\s]{5,}\d)` | `+7 999 123-45-67` |
| `EmailAdressProcessor` | email-паттерн | `user@example.com` |
| `TimeProcessor` | `HH:MM` | `15:30` |
| `AbbreviationsNerProcessor` | список аббревиатур | `т. е.`, `г.` |
| `NameRusNerProcessor` | русские имена | `Иван Иванович` |

## Пользовательский Regex-NER

```csharp
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER;

// Кастомный NER по регулярному выражению
var customNer = new RegexNer(@"\d{6}", "postal_code");
string result = customNer.RunProcessor("Москва 101000, Санкт-Петербург 190000");
// → "Москва <postal_code_1>, Санкт-Петербург <postal_code_2>"

// Декодирование обратно
string original = customNer.NerDecoder(result);
```

## Комбинированный пайплайн

```csharp
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;

var text = "Звоните +7 999 123-45-67. Email: test@example.com. Встреча в 15:30.";

var phoneNer = new PhoneNerProcessor();
var emailNer = new EmailAdressProcessor();
var timeNer  = new TimeProcessor();

// Последовательная обработка
text = phoneNer.RunProcessor(text);
text = emailNer.RunProcessor(text);
text = timeNer.RunProcessor(text);
// → "Звоните <phone_1>. Email: <mail_1>. Встреча в <time_1>."
```

## SentencesTokenizer

```csharp
using AI.DataPrepaire.NLPUtils.RegexpNLP;

var st = new SentencesTokenizer();
// С учётом аббревиатур:
var st2 = new SentencesTokenizer(new[] { "г.", "т. е.", "см.", "д-р" });

var sentences = st.Tokenize(longText);
// sentences — List<string>, каждый элемент — одно предложение
```
