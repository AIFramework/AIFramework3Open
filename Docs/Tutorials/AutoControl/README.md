# Туториалы: Системы автоматического управления

**Сборка:** `AI.ControlSystems` · **Целевая платформа:** .NET Standard 2.0

Документация по всем алгоритмам модуля `AI.ControlSystems`. Каждый туториал содержит теоретическое описание и примеры кода на C#.

---

## Содержание

| # | Файл | Алгоритм | Пространство имён |
|---|------|----------|-------------------|
| 1 | [PID.md](PID.md) | PID-регулятор, векторный PID, IMC-настройка, ограничение скорости | `AI.ControlSystems.Pid` |
| 2 | [StateSpace.md](StateSpace.md) | Дискретная LTI-модель, ZOH-дискретизация | `AI.ControlSystems.Linear` |
| 3 | [PolePlacement.md](PolePlacement.md) | Размещение полюсов (формула Аккермана, SISO) | `AI.ControlSystems.Linear` |
| 4 | [LuenbergerObserver.md](LuenbergerObserver.md) | Наблюдатель Люенбергера | `AI.ControlSystems.Observers` |
| 5 | [KalmanFilter.md](KalmanFilter.md) | Фильтр Калмана (KF) и расширенный (EKF) | `AI.ControlSystems.Observers` |
| 6 | [LQR.md](LQR.md) | LQR и LQG (оптимальное управление) | `AI.ControlSystems.Optimal` |
| 7 | [MPC.md](MPC.md) | Линейный квадратичный MPC | `AI.ControlSystems.Optimal` |
| 8 | [SlidingMode.md](SlidingMode.md) | Регулятор скользящего режима | `AI.ControlSystems.Nonlinear` |
| 9 | [MRAC.md](MRAC.md) | Адаптивный регулятор с эталонной моделью | `AI.ControlSystems.Adaptive` |
| 10 | [RLS.md](RLS.md) | Рекурсивные МНК (идентификация параметров) | `AI.ControlSystems.Identification` |

---

## Быстрый выбор алгоритма

```
Нужен простой регулятор?
  └─ Линейный объект, известная модель → PID (+ IMC-настройка)
  └─ Нелинейный объект, нужна робастность → SlidingMode

Нужна оптимальность?
  └─ Полное состояние доступно → LQR
  └─ Только выход (шумные измерения) → LQG = LQR + KalmanFilter
  └─ Конечный горизонт, планирование → MPC

Состояние недоступно напрямую?
  └─ Линейная модель, нет шума → LuenbergerObserver
  └─ Линейная модель, есть шум → KalmanFilter
  └─ Нелинейная модель → ExtendedKalmanFilter

Параметры объекта неизвестны?
  └─ Онлайн-идентификация → RLS
  └─ Адаптивное управление → MRAC

Нужно перейти от непрерывной модели к дискретной?
  └─ ZOH-дискретизация → StateSpace (Discretization)

Нужно разместить полюса замкнутой системы?
  └─ SISO → PolePlacement (Ackermann)
  └─ MIMO → LQR (косвенно через штрафные матрицы)
```

---

## Зависимости между модулями

```
AI.ControlSystems.Linear
  ├── DiscreteLtiModel          ← базовая модель
  ├── Discretization (ZOH)      ← непрерывная → дискретная
  └── PolePlacement             ← синтез усиления

AI.ControlSystems.Observers
  ├── LuenbergerObserver        ← детерминированный наблюдатель
  ├── KalmanFilter              ← оптимальный (стохастический)
  └── ExtendedKalmanFilter      ← нелинейный

AI.ControlSystems.Optimal
  ├── DiscreteLqr               ← синтез K через DARE
  ├── LqgRegulator              ← LQR + KalmanFilter
  └── LinearQuadraticMpc        ← конечный горизонт

AI.ControlSystems.Pid
  ├── PidController             ← скалярный PID
  ├── VectorPidController       ← многоканальный
  ├── ImcPidTuning              ← настройка по IMC
  └── SlewRateLimiter           ← ограничение скорости

AI.ControlSystems.Nonlinear
  └── SlidingModeController     ← скользящий режим

AI.ControlSystems.Adaptive
  └── ModelReferenceAdaptiveController  ← MRAC

AI.ControlSystems.Identification
  └── RecursiveLeastSquares     ← RLS с забыванием
```

---

## Архитектурная документация

Общее описание модуля: [Docs/Architecture/ControlSystems.md](../../Architecture/ControlSystems.md)
