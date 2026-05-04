# Regression (`AI.ML.Regression`)

Линейная и нейросетевая регрессия, интерфейс `IRegression`.

## Пример

```csharp
using AI.DataStructs.Algebraic;
using AI.ML.Regression;

var X = new Vector(1, 2, 3);
var Y = new Vector(2, 4, 6);
var lr = new LinearRegression(X, Y);
double y = lr.Predict(4);
```
