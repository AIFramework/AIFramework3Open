# Аффинные преобразования 2D

Аффинное преобразование — линейное отображение с последующим переносом.
В однородных координатах любое аффинное преобразование записывается как умножение на матрицу 3×3.
Композиция преобразований — произведение матриц; порядок умножения важен.

## Однородные координаты

Точка $(x, y)$ записывается как $(x, y, 1)^T$. Преобразование:

$$\begin{pmatrix} x' \\ y' \\ 1 \end{pmatrix} = M \begin{pmatrix} x \\ y \\ 1 \end{pmatrix}$$

## Базовые преобразования

### Перенос (Translation)

$$T(t_x, t_y) = \begin{pmatrix} 1 & 0 & t_x \\ 0 & 1 & t_y \\ 0 & 0 & 1 \end{pmatrix}$$

### Масштаб (Scale)

$$S(s_x, s_y) = \begin{pmatrix} s_x & 0 & 0 \\ 0 & s_y & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

### Поворот (Rotation) на угол $\theta$

$$R(\theta) = \begin{pmatrix} \cos\theta & -\sin\theta & 0 \\ \sin\theta & \cos\theta & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

### Сдвиг (Shear)

$$H(h_x, h_y) = \begin{pmatrix} 1 & h_x & 0 \\ h_y & 1 & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

## Композиция

Поворот вокруг точки $(c_x, c_y)$:

$$M = T(c_x, c_y) \cdot R(\theta) \cdot T(-c_x, -c_y)$$

Матрицы умножаются справа налево: сначала перенос в начало, затем поворот, затем перенос обратно.

## Обратное преобразование

$$M^{-1} \text{ существует, если } \det(M) \neq 0$$

Для чистого поворота: $R^{-1}(\theta) = R(-\theta) = R^T$.

## API

Класс `AI.Geometry.AffineTransform2D` — методы `Translate`, `Scale`, `Rotate`, `Shear`, `Compose`, `Inverse`, `Apply`.
