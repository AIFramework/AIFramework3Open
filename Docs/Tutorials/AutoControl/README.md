# Туториалы: Системы автоматического управления

**Сборка:** `AI.ControlSystems` · **Целевая платформа:** .NET 9.0

Документация по всем алгоритмам модуля `AI.ControlSystems`. Каждый туториал содержит теоретическое описание и примеры кода на C#.

---

## Содержание

| # | Файл | Алгоритм | Пространство имён |
|---|------|----------|-------------------|
| 1 | [PID.md](PID.md) | PID-регулятор, векторный PID, IMC-настройка, ограничение скорости | `AI.ControlSystems.Pid` |
| 2 | [StateSpace.md](StateSpace.md) | Дискретная LTI-модель, точный ZOH, шум по Ван Лоану, управляемость, полюса, уравнения Ляпунова | `AI.ControlSystems.Linear` |
| 3 | [PolePlacement.md](PolePlacement.md) | Размещение полюсов (формула Аккермана, SISO), наблюдатель по двойственности | `AI.ControlSystems.Linear` |
| 4 | [LuenbergerObserver.md](LuenbergerObserver.md) | Наблюдатель Люенбергера | `AI.ControlSystems.Observers` |
| 5 | [KalmanFilter.md](KalmanFilter.md) | Фильтр Калмана (KF), установившийся KF и расширенный (EKF) | `AI.ControlSystems.Observers` |
| 6 | [LQR.md](LQR.md) | LQR через уравнение Риккати (метод удвоения), синтез LQG с ожидаемой стоимостью | `AI.ControlSystems.Optimal` |
| 7 | [MPC.md](MPC.md) | MPC с ограничениями на управление, его скорость и состояние | `AI.ControlSystems.Optimal` |
| 8 | [SlidingMode.md](SlidingMode.md) | Скользящий режим: скалярный, по состоянию, super-twisting | `AI.ControlSystems.Nonlinear` |
| 9 | [MRAC.md](MRAC.md) | MRAC первого порядка и по состоянию | `AI.ControlSystems.Adaptive` |
| 10 | [RLS.md](RLS.md) | Рекурсивные МНК (идентификация параметров) | `AI.ControlSystems.Identification` |

---

## Быстрый выбор алгоритма

```
Нужен простой регулятор?
  └─ Линейный объект, известная модель → PID (+ IMC-настройка)
  └─ Нужна робастность к возмущениям по каналу управления → SlidingMode

Нужна оптимальность?
  └─ Полное состояние доступно → LQR
  └─ Только выход (шумные измерения) → LQG
  └─ Есть ограничения на управление или состояние → MPC

Состояние недоступно напрямую?
  └─ Линейная модель, нет шума → LuenbergerObserver
  └─ Линейная модель, есть шум → KalmanFilter
  └─ Нелинейная модель → ExtendedKalmanFilter

Параметры объекта неизвестны?
  └─ Онлайн-идентификация → RLS
  └─ Адаптивное управление → MRAC

Нужно перейти от непрерывной модели к дискретной?
  └─ ZOH и шум процесса → StateSpace (Discretization)

Нужно разместить полюса замкнутой системы?
  └─ SISO → PolePlacement (Ackermann)
  └─ MIMO → LQR (косвенно через штрафные матрицы)
```

---

## Зависимости между модулями

```
AI.ControlSystems.Linear
  ├── DiscreteLtiModel          ← базовая модель
  ├── Discretization            ← ZOH, шум по Ван Лоану
  ├── SystemAnalysis            ← управляемость, наблюдаемость, полюса
  ├── RiccatiEquation           ← DARE методом удвоения
  ├── LyapunovEquation          ← непрерывное и дискретное уравнения Ляпунова
  └── PolePlacement             ← синтез усиления

AI.ControlSystems.Observers
  ├── LuenbergerObserver        ← детерминированный наблюдатель
  ├── KalmanFilter              ← оптимальный (стохастический), установившийся режим
  └── ExtendedKalmanFilter      ← нелинейный

AI.ControlSystems.Optimal
  ├── DiscreteLqr               ← синтез K через DARE
  ├── LqgRegulator              ← LQR + установившийся фильтр Калмана
  ├── ModelPredictiveController ← MPC с ограничениями (QP на каждом такте)
  └── LinearQuadraticMpc        ← конечный горизонт без ограничений

AI.ControlSystems.Pid
  ├── PidController             ← скалярный PID
  ├── VectorPidController       ← многоканальный
  ├── ImcPidTuning              ← настройка по IMC
  └── SlewRateLimiter           ← ограничение скорости

AI.ControlSystems.Nonlinear
  ├── SlidingModeController     ← скалярный по ошибке
  ├── StateSlidingModeController← по состоянию с эквивалентным управлением
  └── SuperTwistingController   ← второго порядка, непрерывное управление

AI.ControlSystems.Adaptive
  ├── ModelReferenceAdaptiveController       ← MRAC первого порядка
  └── StateModelReferenceAdaptiveController  ← MRAC по состоянию

AI.ControlSystems.Identification
  └── RecursiveLeastSquares     ← RLS с забыванием
```

Собственные значения берутся из `AI.ClassicMath` (`Eigen.General`), квадратичное
программирование для MPC — из `AI.Solvers.Optimization` (`QpSolver`).

---

## Архитектурная документация

Общее описание модуля: [Docs/Architecture/ControlSystems.md](../../Architecture/ControlSystems.md)
