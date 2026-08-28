# Synthesis Database Documentation

## Обзор

База данных синтезов (`synthesis_database.json`) содержит ретросинтетические маршруты для различных химических соединений. Формат JSON позволяет легко добавлять новые соединения и маршруты синтеза.

## Структура базы данных

```json
{
  "Version": "1.0",
  "LastUpdated": "2025-11-21",
  "Compounds": [...]
}
```

## Формат соединения (TargetCompound)

```json
{
  "Name": "aspirin",
  "Aliases": ["acetylsalicylic acid", "ASA", "аспирин"],
  "Formula": "C9H8O4",
  "SMILES": "CC(=O)Oc1ccccc1C(=O)O",
  "IUPAC": "2-acetoxybenzoic acid",
  "Description": "Описание соединения",
  "Routes": [...]
}
```

### Поля:

- **Name** (обязательно): Основное название соединения (строчными буквами)
- **Aliases**: Список альтернативных названий
- **Formula**: Молекулярная формула
- **SMILES**: SMILES-представление структуры
- **IUPAC**: Систематическое название по IUPAC
- **Description**: Описание и применение
- **Routes**: Массив маршрутов синтеза

## Формат маршрута синтеза (SynthesisRoute)

```json
{
  "StartingMaterial": "benzene",
  "RouteType": "industrial",
  "Difficulty": "medium",
  "Yield": "40-50%",
  "StepCount": 4,
  "Steps": [...],
  "Notes": "Дополнительные заметки"
}
```

### Поля:

- **StartingMaterial**: Исходное вещество
- **RouteType**: Тип маршрута
  - `industrial` - промышленный синтез
  - `laboratory` - лабораторный синтез
  - `biological` - биологический/ферментативный
  - `classic` - классический учебный пример
- **Difficulty**: Сложность
  - `easy` - простой
  - `medium` - средний
  - `hard` - сложный
- **Yield**: Общий выход (например, "40-50%")
- **StepCount**: Количество шагов
- **Steps**: Массив шагов синтеза
- **Notes**: Дополнительные заметки

## Формат шага синтеза (SynthesisStep)

```json
{
  "StepNumber": 1,
  "ReactionType": "nitration",
  "Description": "Nitration of benzene",
  "Equation": "C6H6 + HNO3 → C6H5NO2 + H2O",
  "Reagents": ["conc. HNO3", "conc. H2SO4"],
  "Conditions": ["mixed acid", "controlled temperature"],
  "Catalyst": "H2SO4",
  "Temperature": "50-60°C",
  "Pressure": "atmospheric",
  "Time": "1-2 hours",
  "Yield": "~95%",
  "Mechanism": "Electrophilic aromatic substitution",
  "Warnings": ["Exothermic reaction", "Must control temperature"]
}
```

### Поля:

- **StepNumber**: Номер шага (начиная с 1)
- **ReactionType**: Тип реакции (например, "nitration", "reduction")
- **Description**: Краткое описание шага
- **Equation**: Уравнение реакции
- **Reagents**: Список реагентов
- **Conditions**: Список условий
- **Catalyst**: Катализатор (если есть)
- **Temperature**: Температура
- **Pressure**: Давление (если важно)
- **Time**: Время реакции
- **Yield**: Выход на этом шаге
- **Mechanism**: Механизм реакции
- **Warnings**: Предупреждения о безопасности

## Как добавить новое соединение

### Шаг 1: Создайте структуру соединения

```json
{
  "Name": "новое_соединение",
  "Aliases": ["альтернативное_название"],
  "Formula": "C10H12O2",
  "SMILES": "ваш_smiles",
  "IUPAC": "iupac_название",
  "Description": "Описание",
  "Routes": []
}
```

### Шаг 2: Добавьте маршрут синтеза

```json
{
  "StartingMaterial": "исходное_вещество",
  "RouteType": "laboratory",
  "Difficulty": "medium",
  "Yield": "70-80%",
  "StepCount": 3,
  "Steps": [],
  "Notes": "Примечания"
}
```

### Шаг 3: Заполните шаги синтеза

Для каждого шага укажите:
- Номер шага
- Тип реакции
- Уравнение
- Реагенты и условия
- Выход
- (Опционально) Механизм и предупреждения

### Шаг 4: Добавьте в массив Compounds

Поместите ваше соединение в массив `Compounds` в файле `synthesis_database.json`.

## Примеры использования в коде

### Поиск синтеза

```csharp
var synthesisDb = new SynthesisDatabaseManager();
var compound = synthesisDb.FindCompound("aspirin");
var route = synthesisDb.FindRoute("aspirin", "benzene");
```

### Загрузка пользовательской базы

```csharp
var synthesisDb = new SynthesisDatabaseManager("path/to/custom_database.json");
```

### Добавление нового соединения программно

```csharp
var newCompound = new TargetCompound
{
    Name = "новое_соединение",
    Formula = "C10H12O2",
    Routes = new List<SynthesisRoute> { ... }
};

synthesisDb.AddCompound(newCompound);
synthesisDb.SaveDatabase();
```

## Типы реакций (примеры)

Общие типы реакций для поля `ReactionType`:

### Органическая химия:
- `nitration` - нитрование
- `reduction` - восстановление
- `oxidation` - окисление
- `halogenation` - галогенирование
- `sulfonation` - сульфонирование
- `alkylation` - алкилирование
- `acylation` - ацилирование
- `acetylation` - ацетилирование
- `esterification` - этерификация
- `hydrolysis` - гидролиз
- `condensation` - конденсация
- `addition` - присоединение
- `elimination` - элиминирование
- `substitution` - замещение
- `rearrangement` - перегруппировка

### Специфические именованные реакции:
- `Friedel-Crafts alkylation`
- `Friedel-Crafts acylation`
- `Grignard reaction`
- `Kolbe-Schmitt reaction`
- `Diels-Alder reaction`
- `Wittig reaction`
- `Claisen condensation`
- `Aldol condensation`
- `Cannizzaro reaction`

### Процессы:
- `fermentation` - ферментация
- `distillation` - дистилляция
- `crystallization` - кристаллизация
- `hydration` - гидратация
- `dehydration` - дегидратация

## Советы по заполнению

1. **Названия**: Используйте строчные буквы для основного названия
2. **Алиасы**: Добавляйте популярные альтернативные названия, включая русские
3. **SMILES**: Проверяйте корректность через ChemSpider или PubChem
4. **Уравнения**: Используйте Unicode символы для стрелок (→) и специальных символов
5. **Выход**: Указывайте реалистичные значения из литературы
6. **Предупреждения**: Всегда указывайте предупреждения о безопасности

## Валидация

Перед добавлением в базу проверьте:

Корректность JSON синтаксиса  
Все обязательные поля заполнены  
StepNumber соответствует порядку шагов  
StepCount соответствует количеству шагов  
SMILES корректен  
Уравнения сбалансированы  

## Источники информации

Рекомендуемые источники для синтезов:

- **Organic Syntheses** (www.orgsyn.org)
- **SciFinder** (scifinder.cas.org)
- **Reaxys** (www.reaxys.com)
- **Wikipedia** - для общих синтезов
- **Учебники по органической химии**
- **PubChem** - для свойств соединений

## Лицензия и авторство

При добавлении синтезов из литературы рекомендуется указывать источник в поле `Notes`:

```json
"Notes": "Based on: Smith, J. et al. (2020) J. Org. Chem. 85(10), 6789-6795"
```

## Контакты и поддержка

Для вопросов и предложений по расширению базы данных обращайтесь к разработчикам проекта.

