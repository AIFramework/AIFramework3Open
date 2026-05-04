# Проекция вектора

**Проекция** вектора $A$ на направление $B$ — это компонента $A$, сонаправленная с $B$:

$$
\mathrm{proj}_B A \;=\; \frac{\langle A, B\rangle}{\|B\|^2}\,B.
$$

Длина проекции (скалярная):

$$
\mathrm{comp}_B A \;=\; \frac{\langle A, B\rangle}{\|B\|} \;=\; \|A\|\cos\theta,
$$

где $\theta$ — угол между векторами.

## Разложение Грама–Шмидта

Любой вектор $A$ можно однозначно разложить на составляющие параллельно и перпендикулярно $B$:

$$
A \;=\; \mathrm{proj}_B A \;+\; A_\perp, \qquad \langle A_\perp, B\rangle = 0.
$$

Это ядро процедуры **Грама–Шмидта** построения ортонормированного базиса.

## Геометрические величины

| Величина | Формула | Смысл |
|---|---|---|
| Скалярное произведение | $\langle A, B\rangle = \sum A_i B_i$ | Мера «сонаправленности» |
| Длина | $\|A\| = \sqrt{\langle A, A\rangle}$ | L2-норма |
| Косинус угла | $\cos\theta = \dfrac{\langle A, B\rangle}{\|A\|\cdot\|B\|}$ | Безразмерное сходство |
| Угол | $\theta = \arccos(\cos\theta)$ | В радианах |

## Применения

1. **Регрессия**: OLS-решение $y \approx \mathrm{proj}_{\mathrm{col}(X)} y$;
2. **PCA**: проекция данных на главные компоненты;
3. **Компьютерная графика**: нормализация векторов, теневые расчёты;
4. **DSP**: согласованные фильтры (матчинг как максимизация проекции).

## Код

```csharp
using AI.HighLevelFunctions;

Vector proj = AnalyticGeometryFunctions.ProjectionAtoB(A, B);
double dot  = AnalyticGeometryFunctions.Dot(A, B);
double cos  = AnalyticGeometryFunctions.Cos(A, B);
double ang  = AnalyticGeometryFunctions.AngleVect(A, B); // радианы
double dst  = AnalyticGeometryFunctions.DistanceFromAToB(A, B);
```
