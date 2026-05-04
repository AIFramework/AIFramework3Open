# DataTable и загрузка данных (AI.DataPrepaire)

## DataTable

`DataTable` — колоночное хранилище данных (аналог pandas DataFrame):

```
DataTable
├── DataItem "sepal_length"  [5.1, 4.9, 5.7, ...]  TypeData.DigitP
├── DataItem "sepal_width"   [3.5, 3.0, 2.8, ...]  TypeData.DigitP
├── DataItem "species"       ["setosa", "setosa", ...] TypeData.String
└── ...
```

## Типы данных столбца (DataItem.TypeData)

| Значение | Описание |
|----------|----------|
| `DigitC` | Целые числа |
| `DigitP` | Числа с плавающей точкой |
| `String` | Строки |
| `UnDef`  | Не определён |

## CSV-загрузка

```csharp
using AI.DataPrepaire.DataLoader;
using AI.DataPrepaire.DataLoader.Formats;

// Из файла
DataTable dt = CSVLoader.Read("data.csv", separator: ',');

// Из строки (in-memory)
string csvContent = "name,age,score\nAlice,25,0.8\nBob,30,0.9";
using var sr = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(csvContent)));
DataTable dt2 = CSVLoader.Read(sr, ',');
```

## Основные операции

```csharp
// Доступ по имени столбца
DataItem col = dt["sepal_length"];
Vector numVec = col.ToVector();

// Строк в таблице
int n = dt.Len;

// Все имена столбцов
string[] cols = dt.GetColums();

// Получить строку как массив
object[] row = dt.GetRow(0);

// Преобразовать в матрицу (только числовые столбцы)
Matrix matrix = dt.ToMatrix();

// Срез строк
DataTable slice = dt.GetSlice(0, 50);

// Подтаблица (выбор столбцов)
DataTable sub = dt.GetSubTable(new[] { "sepal_length", "sepal_width" });

// Категориальное кодирование
Dictionary<object, int> catMap = dt.ColumnToCategorical("species");
// → { "setosa": 0, "versicolor": 1, "virginica": 2 }
```

## DataItem

```csharp
// Создание вручную
var item = new DataItem("scores", new List<dynamic> { 85, 72, 95, 60 });
item.Convert();  // Определить тип

Vector vec = item.ToVector();                  // → Vector[85, 72, 95, 60]
List<double> vals = item.ToType<double>();     // → List<double>
var catIdx = item.SelfCategoryToIndex();       // Заменить на индексы

// Трансформация значений
item.TransformSelf(x => (double)x / 100.0);  // Нормализация 0..1
```
