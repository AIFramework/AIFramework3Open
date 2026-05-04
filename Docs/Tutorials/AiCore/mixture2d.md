# Двумерная смесь гауссиан

## Модель

$$
p(\mathbf{x}) = \sum_{k=1}^K w_k \,\mathcal{N}(\mathbf{x} \mid \boldsymbol{\mu}_k, \Sigma_k), \qquad \sum_k w_k = 1.
$$

Для диагональной ковариации каждая компонента разделяется по осям:

$$
\mathcal{N}(\mathbf{x} \mid \boldsymbol{\mu}_k, \mathrm{diag}(\sigma_{k1}^2, \sigma_{k2}^2)) = \frac{1}{2\pi\sigma_{k1}\sigma_{k2}} \exp\!\left(-\frac{(x_1-\mu_{k1})^2}{2\sigma_{k1}^2} - \frac{(x_2-\mu_{k2})^2}{2\sigma_{k2}^2}\right).
$$

## 3D-визуализация

Поверхность $z = p(x_1, x_2)$ имеет характерную форму с несколькими пиками — по одному на компоненту. Высота пика определяется весом $w_k$ и обратна произведению СКО.

## Подгонка EM

Алгоритм EM для 2D-случая идентичен 1D (см. [Смесь гауссиан и EM](mixture_em.md)), но E-шаг использует двумерную лог-плотность:

$$
\gamma_{ik} \propto w_k \cdot \mathcal{N}(\mathbf{x}_i \mid \boldsymbol{\mu}_k, \Sigma_k).
$$

## Применения

- Кластеризация 2D-данных (soft clustering);
- Моделирование плотности для генеративных моделей;
- Аппроксимация произвольной 2D-плотности.

## Код

```csharp
// Подгонка 2D-смеси
var data = new Vector[] { new Vector(x1, x2), ... };
var gmm = EM.Fit(data, numComponents: 3, seed: 42);

// gmm.CulcProb(new Vector(x, y)) — плотность в точке
```
