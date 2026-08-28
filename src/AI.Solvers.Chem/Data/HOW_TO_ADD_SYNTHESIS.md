# Как добавить новый синтез в базу данных

## Быстрый старт

### 1. Откройте файл `synthesis_database.json`

### 2. Добавьте новое соединение в массив `Compounds`:

```json
{
  "Name": "toluene",
  "Aliases": ["methylbenzene", "толуол"],
  "Formula": "C7H8",
  "SMILES": "Cc1ccccc1",
  "IUPAC": "methylbenzene",
  "Description": "Toluene is a common solvent and chemical feedstock",
  "Routes": [
    {
      "StartingMaterial": "benzene",
      "RouteType": "industrial",
      "Difficulty": "easy",
      "Yield": "90-95%",
      "StepCount": 1,
      "Steps": [
        {
          "StepNumber": 1,
          "ReactionType": "Friedel-Crafts alkylation",
          "Description": "Methylation of benzene",
          "Equation": "C6H6 + CH3Cl → C6H5CH3 + HCl",
          "Reagents": ["CH3Cl", "AlCl3"],
          "Conditions": ["anhydrous", "Lewis acid catalyst"],
          "Catalyst": "AlCl3",
          "Temperature": "40-50°C",
          "Time": "2-3 hours",
          "Yield": "~90-95%",
          "Mechanism": "Electrophilic aromatic substitution",
          "Warnings": ["CH3Cl is toxic", "AlCl3 reacts violently with water"]
        }
      ],
      "Notes": "Classic Friedel-Crafts alkylation. Industrial method."
    }
  ]
}
```

### 3. Сохраните файл

### 4. Проверьте в программе:

```
> retrosynthesis toluene from benzene
```

## Минимальный пример

```json
{
  "Name": "название",
  "Aliases": [],
  "Formula": "формула",
  "SMILES": "smiles",
  "IUPAC": "",
  "Description": "",
  "Routes": [
    {
      "StartingMaterial": "исходное",
      "RouteType": "laboratory",
      "Difficulty": "easy",
      "Yield": "выход",
      "StepCount": 1,
      "Steps": [
        {
          "StepNumber": 1,
          "ReactionType": "тип_реакции",
          "Description": "описание",
          "Equation": "уравнение",
          "Reagents": ["реагент1", "реагент2"],
          "Conditions": [],
          "Catalyst": "",
          "Temperature": "",
          "Yield": ""
        }
      ],
      "Notes": ""
    }
  ]
}
```

## Проверка JSON

Используйте онлайн валидаторы:
- https://jsonlint.com/
- https://www.jsonformatter.io/

## Поиск информации

### Для SMILES:
- PubChem: https://pubchem.ncbi.nlm.nih.gov/
- ChemSpider: http://www.chemspider.com/

### Для синтезов:
- Organic Syntheses: http://www.orgsyn.org/
- Wikipedia (органическая химия)
- Учебники

## Советы

✅ **DO:**
- Проверяйте JSON синтаксис
- Используйте корректные SMILES
- Указывайте реалистичные выходы
- Добавляйте предупреждения о безопасности

❌ **DON'T:**
- Не оставляйте обязательные поля пустыми
- Не забывайте про запятые между элементами
- Не копируйте из ненадёжных источников

## Примеры доступных синтезов

Текущая база содержит:
- Aspirin (аспирин)
- Aniline (анилин)
- Ethanol (этанол)
- Benzoic acid (бензойная кислота)
- Phenol (фенол)

Вы можете использовать их как шаблоны!

