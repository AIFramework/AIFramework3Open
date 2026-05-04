# Classification (`AI.ML.Classification`)

Классификаторы и линейные инструменты (SVM, байесовский классификатор, нейросетевой классификатор и др.).

## Пример

```csharp
using AI.DataStructs.Algebraic;
using AI.ML.Classification;
using AI.ML.DataHandling.DataSets;

var ds = new VectorDataset();
ds.Add(new VectorDatasetItem(new Vector(0.0, 1.0), 0));
var clf = new BayesianClassifier();
clf.Train(ds);
int c = clf.Classify(new Vector(0.1, 0.9));
```

См. интерфейс `IClassifier` в этом каталоге.
