# AI Framework UI Kit — Справочник токенов

Все токены объявлены в `tokens/tokens.css` в псевдоклассе `:root`.  
Префикс `--aif-` (AIFramework), заменяет устаревший `--cv-` из оригинального проекта.

---

## Фон (Backgrounds)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-bg-void` | `#02030a` | Самый тёмный слой — фон `<body>` по умолчанию |
| `--aif-bg-deep` | `#050710` | Глубокий фон страниц |
| `--aif-bg` | `#080b17` | Основной фон |

---

## Поверхности (Surfaces)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-surface-1` | `rgba(255,255,255, 0.04)` | Еле заметная поверхность |
| `--aif-surface-2` | `rgba(255,255,255, 0.07)` | Чуть более заметная |
| `--aif-glass` | `rgba(14,17,35, 0.85)` | Стекло с blur |
| `--aif-glass-strong` | `rgba(10,12,26, 0.95)` | Тёмное непрозрачное стекло |
| `--aif-card-bg` | `linear-gradient(160deg, ...)` | Фон всех карточек |
| `--aif-topbar-bg` | `rgba(5,6,16, 0.9)` | Фон вторичного топбара |
| `--aif-code-bg` | `rgba(3,4,14, 0.6)` | Фон инпутов и кода |

---

## Границы (Borders)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-border` | `rgba(255,255,255, 0.08)` | Стандартная граница |
| `--aif-border-bright` | `rgba(255,255,255, 0.14)` | Более заметная граница |
| `--aif-border-accent` | `rgba(99,102,241, 0.3)` | Акцентная граница (indigo) |
| `--aif-border-subtle` | `rgba(255,255,255, 0.06)` | Тонкая граница хедера |
| `--aif-border-dark` | `rgba(255,255,255, 0.05)` | Разделитель внутри карточки |

---

## Акцентный цвет (Primary Accent — Indigo)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-accent` | `#6366f1` | Основной акцент |
| `--aif-accent-light` | `#818cf8` | Светлый акцент (ссылки, API badge) |
| `--aif-accent-bright` | `#a5b4fc` | Яркий акцент (hover) |

### Лестница прозрачностей акцента

`--aif-accent-a04 … --aif-accent-a55` и `--aif-accent-a00` (прозрачный —
конечный кадр пульсации), плюс `--aif-accent-light-a40/a50/a00`.

Нужны для «хрома» приложения — фона страницы, hero, глобальных анимаций, —
который **не** меняет цвет вместе с модулем. В светлой теме вся лестница
переопределена на более тёмный индиго `#4338ca`, поэтому подсветки не
выцветают на белом.

```css
/* было: подсветка оставалась одинаковой в обеих темах */
box-shadow: 0 4px 20px -4px rgba(99, 102, 241, 0.4);

/* стало */
box-shadow: 0 4px 20px -4px var(--aif-accent-a40);
```

**Не используйте** её там, где элемент обязан следовать цвету модуля —
для этого есть контекстные `--aif-cur-*` (см. [theming.md](theming.md)).

---

## Семантические цвета

| Токен | Значение | Применение |
|---|---|---|
| `--aif-error` | `#f43f5e` | Красный (ошибки) |
| `--aif-error-bg` | `rgba(244,63,94, 0.08)` | Фон блока ошибки |
| `--aif-error-border` | `rgba(244,63,94, 0.25)` | Граница блока ошибки |
| `--aif-error-text` | `#fb7185` | Текст ошибки |

### Статусы

Четыре статуса, у каждого три варианта: `-text`, `-bg`, `-border`
(у `ok` дополнительно `-bg-hover`). В светлой теме текст берётся темнее,
а подложка светлее — тот же зелёный на белом фоне читается хуже.

| Группа | Токены |
|---|---|
| Успех | `--aif-status-ok-text`, `-bg`, `-bg-hover`, `-border` |
| Предупреждение | `--aif-status-warn-text`, `-bg`, `-border` |
| Ошибка | `--aif-status-err-text`, `-bg`, `-border` |
| Информация | `--aif-status-info-text`, `-bg`, `-border` |

```css
.aif-btn-sm--save {
  background: var(--aif-status-ok-bg);
  border-color: var(--aif-status-ok-border);
  color: var(--aif-status-ok-text);
}
```

Устаревшие `--aif-status-ok` и `--aif-status-err` (только цвет текста)
сохранены для совместимости; в новом коде берите `-text`-варианты.

---

## Цвета модулей (6 акцентов)

Каждый цвет имеет три варианта: `base`, `bg`, `border`.  
Используются в CSS-модификаторах `--{color}`.

| Имя | Base | Background | Border |
|---|---|---|---|
| `sky` | `#38bdf8` | `rgba(14,165,233, 0.1)` | `rgba(14,165,233, 0.22)` |
| `indigo` | `#818cf8` | `rgba(79,70,229, 0.1)` | `rgba(79,70,229, 0.25)` |
| `violet` | `#a78bfa` | `rgba(124,58,237, 0.1)` | `rgba(124,58,237, 0.25)` |
| `emerald` | `#34d399` | `rgba(5,150,105, 0.1)` | `rgba(5,150,105, 0.22)` |
| `amber` | `#fbbf24` | `rgba(217,119,6, 0.1)` | `rgba(217,119,6, 0.25)` |
| `pink` | `#f472b6` | `rgba(219,39,119, 0.1)` | `rgba(219,39,119, 0.25)` |

Полные токены:
```css
--aif-c-sky          /* base color */
--aif-c-sky-bg       /* background */
--aif-c-sky-border   /* border */
--aif-c-sky-rgb      /* RGB для rgba() */
```

---

## Текст (Typography Scale)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-text-1` | `#f8fafc` | Primary — заголовки |
| `--aif-text-2` | `#e2e8f0` | Secondary — подзаголовки |
| `--aif-text-3` | `#94a3b8` | Muted — основной контент |
| `--aif-text-4` | `#64748b` | Faint — метки, подписи |
| `--aif-text-5` | `#475569` | Ghost — неактивное, placeholder |
| `--aif-text-6` | `#334155` | Dim — разделители, стрелки |

---

## Радиусы (Border Radius)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-r-xs` | `0.3rem` | Inline badge, маленькие элементы |
| `--aif-r-sm` | `0.375rem` | Кнопки, инпуты |
| `--aif-r-md` | `0.625rem` | Карточки среднего размера |
| `--aif-r-lg` | `0.875rem` | Крупные блоки |
| `--aif-r-xl` | `1.125rem` | Панели параметров |
| `--aif-r-2xl` | `1.5rem` | Overlay-боксы |
| `--aif-r-card` | `1.25rem` | Стандартный радиус карточек |
| `--aif-r-full` | `999px` | Pill-кнопки, аватары |

---

## Тени (Shadows)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-shadow-xs` | `0 1px 3px rgba(0,0,0, 0.3)` | Микро-тень |
| `--aif-shadow-sm` | `0 2px 10px -2px rgba(0,0,0, 0.45)` | Лёгкая тень |
| `--aif-shadow-md` | `0 8px 32px -8px rgba(0,0,0, 0.55)` | Средняя тень |
| `--aif-shadow-lg` | `0 20px 60px -16px rgba(0,0,0, 0.65)` | Тяжёлая тень |
| `--aif-shadow-accent` | `0 4px 24px -4px rgba(99,102,241, 0.4)` | Акцентная тень |
| `--aif-shadow-accent-lg` | `0 8px 48px -8px rgba(99,102,241, 0.35)` | Крупная акцентная |

---

## Шрифты (Fonts)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-font-sans` | Inter, system-ui, sans-serif | Основной интерфейс |
| `--aif-font-mono` | JetBrains Mono, Cascadia Code, monospace | Код, числа, API-классы |

Подключение Inter и JetBrains Mono (Google Fonts):
```html
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800;900&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet" />
```

---

## Анимации — Easing

| Токен | Значение | Применение |
|---|---|---|
| `--aif-ease-spring` | `cubic-bezier(0.34, 1.56, 0.64, 1)` | «Пружинный» эффект (hover карточек, кнопок) |
| `--aif-ease-out` | `cubic-bezier(0.21, 0.47, 0.32, 0.98)` | Плавное затухание |
| `--aif-ease-std` | `cubic-bezier(0.4, 0, 0.2, 1)` | Material стандарт |

---

## Макет (Layout)

| Токен | Значение | Применение |
|---|---|---|
| `--aif-max-w-wide` | `90rem` | AlgoPage (теория + демо) |
| `--aif-max-w-default` | `78rem` | Header, CategoryPage, footer |
| `--aif-max-w-narrow` | `72rem` | Home grid, LibraryPage |
| `--aif-header-height` | `3.75rem` | Высота sticky-хедера приложения |
| `--aif-topbar-height` | `2.9rem` | Высота вторичного топбара |
| `--aif-topbar-top` | `3.75rem` | Offset топбара: `top: var(--aif-topbar-top)` |

---

## Z-Индексы

| Токен | Значение | Применение |
|---|---|---|
| `--aif-z-topbar` | `40` | Вторичный топбар |
| `--aif-z-header` | `50` | Главный хедер |
| `--aif-z-tooltip` | `200` | CSS-тултипы |
| `--aif-z-overlay` | `9000` | Полноэкранные overlay |
