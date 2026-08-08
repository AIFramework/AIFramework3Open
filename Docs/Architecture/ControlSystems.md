# Системы автоматического управления (САУ) — `AI.ControlSystems`

Сборка **`AI.ControlSystems`** (`AI.ControlSystems.dll`, **.NET 9.0**) содержит типы для учебных сценариев **моделирования объекта управления** и **обратной модели** (по состоянию — восстановить управление) на базе регрессии `AI.ML` (в т.ч. `NeuralMultyRegression`), а также **классические дискретные PID-регуляторы**. Зависимости: **`AI`**, **`AI.ML`**.

Пространства имён:

- **`AI.ControlSystems.ComplexObjectControl.Base`** — модели объекта и данных для обучения.
- **`AI.ControlSystems.Linear`** — дискретная LTI-модель, ZOH-дискретизация, размещение полюсов (Аккерман).
- **`AI.ControlSystems.Observers`** — наблюдатель Люенбергера, линейный КФ, расширенный КФ (с якобианами).
- **`AI.ControlSystems.Optimal`** — дискретный LQR, LQG, линейный MPC без ограничений (первый шаг горизонта).
- **`AI.ControlSystems.Pid`** — PID, векторный PID, IMC-настройка PI, ограничение скорости выхода.
- **`AI.ControlSystems.Identification`** — рекурсивные МНК (RLS).
- **`AI.ControlSystems.Nonlinear`** — скользящий режим (скалярный).
- **`AI.ControlSystems.Adaptive`** — упрощённый MRAC (1-го порядка).

| Тип | Назначение |
|-----|------------|
| `ComplexObjectBase` | Абстрактный объект: `GetState(action)` — состояние по управлению. |
| `ObjModelDataset` | Пары «состояние — управление» для обучения. |
| `ObjectModel` | Прямая модель: управление → реакция (`IMultyRegression<Vector>`). |
| `ObjectInversModel` | Обратная модель: состояние → управление. |
| `DiscreteLtiModel` | $x_{k+1}=Ax_k+Bu_k$, $y_k=Cx_k+Du_k$. |
| `Discretization` | ZOH: непрерывные $(A_c,B_c)$ → $(A_d,B_d)$. |
| `PolePlacement` | SISO: матрица управляемости, усиление Аккермана. |
| `LuenbergerObserver` | Дискретный наблюдатель с матрицей $L$. |
| `KalmanFilter` | Линейный дискретный КФ ($Q$, $R$, $D u$ в выходе). |
| `ExtendedKalmanFilter` | ЭКФ: шаги с заданными $F$, $H$ и предсказанными $x$, $\hat y$. |
| `DiscreteLqr` | $u=-Kx$, решение Риккати. |
| `LqgRegulator` | LQR + Калман, шаг `Step(uPrev, y)`. |
| `LinearQuadraticMpc` | Первое усиление конечного горизонта без ограничений. |
| `PidController` | PID + фильтр D, anti-windup tracking, ограничение выхода. |
| `VectorPidController` | Независимые SISO PID по координатам. |
| `ImcPidTuning` | PI по IMC для $K/(\tau s+1)$. |
| `SlewRateLimiter` | Ограничение приращения сигнала за шаг. |
| `RecursiveLeastSquares` | RLS с забыванием. |
| `SlidingModeController` | Релейный закон по поверхности $s$. |
| `ModelReferenceAdaptiveController` | Учебный MRAC 1-го порядка. |

Нечёткий PID по правилам Мамдани/Сугено остаётся в **`AI.Fuzzy.Control`** (`FuzzyPIDController`).

Сборка **`AICrossPlatform`** (`AI.CP.dll`) ссылается на **`AI.ControlSystems`** наряду с **`AI.Fuzzy`**, **`AI.ML`**, **`AI.NLP`** и **`AI`**.

### Дискретный PID

Параллельная форма: $u = K_p e + K_i \int e\,dt + K_d \frac{de}{dt} + u_{ff}$, интеграл — сумма $e\,\Delta t$; производная по ошибке или по измерению (`DerivativeOnMeasurement`); опционально фильтр на D (`DerivativeFilterTau`), ограничение $|\int e\,dt|$, anti-windup tracking (`UseAntiWindupTracking`), насыщение выхода, `Feedforward`.

Умножение «матрица × вектор-столбец» в линейных классах выполняется в соглашении классической алгебры (в отличие от части операторов `Matrix`/`Vector` в `AI.DataStructs`, где вектор хранится как строка).

```bash
dotnet build src/AI.ControlSystems/AI.ControlSystems.csproj -c Release
```

**Обратная совместимость:** типы ранее находились в пространстве имён `AI.Controls.ComplexObjectControl.Base` внутри `AICrossPlatform`; теперь используйте **`AI.ControlSystems.ComplexObjectControl.Base`** и отдельную сборку **`AI.ControlSystems`**.
