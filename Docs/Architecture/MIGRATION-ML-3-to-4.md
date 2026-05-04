# Миграция AI.ML с 3.x на 4.x

Версия **4.0.0** вводит несовместимые изменения в пространствах имён и именах типов.

## Пространства имён

| Было (3.x) | Стало (4.x) |
|------------|-------------|
| `AI.HightLevelFunctions` | `AI.HighLevelFunctions` |
| `AI.ML.Classifiers` | `AI.ML.Classification` |
| `AI.ML.LinearModelTools` | `AI.ML.Classification.LinearModelTools` |
| `AI.ML.DataSets` | `AI.ML.DataHandling.DataSets` |
| `AI.ML.DataEncoding` | `AI.ML.DataHandling.DataEncoding` |
| `AI.ML.FeaturesTransforms` | `AI.ML.DataHandling.FeaturesTransforms` |
| `AI.ML.HMM` | `AI.ML.SequenceAnalysis.HMM` |
| `AI.ML.SeqAnalyze` | `AI.ML.SequenceAnalysis.SeqAnalyze` |
| `AI.ML.SeqPredict` | `AI.ML.SequenceAnalysis.SeqPredict` |
| `AI.ML.NeuralNetwork.CoreNNW` | `AI.ML.NeuralNetworks.Core` |
| `AI.ML.NeuralNetwork` | `AI.ML.NeuralNetworks` |
| `AI.ML.MetricsTools` | `AI.ML.Utils.MetricsTools` |
| `AI.NeuralSymbolic` | `AI.ML.Utils.NeuralSymbolic` |
| Корневые типы в `AI.ML` (`EmbeddingMatrix`, `CrossCorrMatrix`) | `AI.ML.Embeddings` |
| Градиентный спуск | `AI.ML.DataHandling` (`GradientDescent`, `GradientDescentDataset`) |

## Переименования типов

| Было | Стало |
|------|--------|
| `VectorClass` | `VectorDatasetItem` |
| `VectorIntDataset` | `VectorDataset` |
| `IClassifire` (файл) | `IClassifier` |
| `BatchReNormalization` | `BatchNormalization` |
| `EmbedingMatrix` | `EmbeddingMatrix` |
| `EmbedingLayer` | `EmbeddingLayer` |
| `GradientDecent` / `GradientDecentDataset` | `GradientDescent` / `GradientDescentDataset` |
| `Simillary` | `Similarity` |

## Структура каталогов

Исходники `AI.ML` сгруппированы по доменам: `Classification`, `Regression`, `Clustering`, `Embeddings`, `SequenceAnalysis`, `NeuralNetworks`, `DataHandling`, `Utils`, `Genetic`, `HighLevelFunctions`.

Активации: стандартные реализации в `NeuralNetworks/Core/Activations/Standard`, дополнительные — в `NeuralNetworks/Core/Activations/Extended`.
