# AI Framework UI Kit

Дизайн-система и UI Kit, извлечённый из проекта **AIFramework 3.0 Open** WebUI-демонстратора.

Содержит дизайн-токены, CSS-компоненты и JS-утилиты в виде standalone-папки
для переиспользования в других проектах платформы.

---

## Быстрый старт

### 1. Подключение CSS

```html
<head>
  <!-- Шрифты (Inter + JetBrains Mono) -->
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800;900&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet" />

  <!-- Весь UI Kit одним файлом -->
  <link rel="stylesheet" href="path/to/aif-uikit/index.css" />
</head>
```

### 2. Подключение JS-утилит (для рендера LaTeX и кода)

```html
<!-- CDN зависимости (до utils/render-math.js) -->
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css" />
<script defer src="https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.js"></script>
<script defer src="https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/contrib/auto-render.min.js"></script>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.11.1/build/styles/github-dark-dimmed.min.css" />
<script defer src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.11.1/build/highlight.min.js"></script>

<!-- Утилиты UI Kit -->
<script defer src="path/to/aif-uikit/utils/render-math.js"></script>
```

### 3. Использование из C# Blazor

```csharp
// Рендер формул и подсветки кода в контейнере
await JS.InvokeVoidAsync("renderMath", ".aif-prose");
```

### 4. Демо

Откройте `demo/index.html` в браузере — визуальный каталог всех компонентов.

---

## Структура

```
AI Framework UI kit/
├── index.css                  ← Точка входа: @import всех слоёв
├── README.md                  ← Этот файл
│
├── tokens/
│   └── tokens.css             ← Все --aif-* CSS custom properties
│
├── base/
│   ├── reset.css              ← box-sizing, scrollbar, html/body
│   ├── typography.css         ← .aif-prose — Markdown/теория
│   └── animations.css         ← @keyframes (aif-spin, aif-fade-up...)
│
├── components/
│   ├── card.css               ← .aif-card, .aif-cards-grid
│   ├── icon-pill.css          ← .aif-icon-pill--{color}
│   ├── topbar.css             ← .aif-topbar, .aif-header, .aif-footer, .aif-brand
│   ├── badge.css              ← .aif-mono-badge, .aif-version-badge, .aif-chip
│   ├── button.css             ← .aif-btn--{color}, .aif-btn-ghost, .aif-seg
│   ├── choice-grid.css        ← .aif-choice-grid, .aif-choice-btn--{color}
│   ├── form-controls.css      ← .aif-slider, .aif-num-input, .aif-code-input
│   ├── tooltip.css            ← .aif-tip [data-tip="..."]
│   ├── overlay.css            ← .aif-overlay, .aif-chart-wrap
│   ├── empty-state.css        ← .aif-empty, .aif-404, .aif-error-block
│   ├── image-drop.css         ← .aif-img-drop, .aif-file-hidden
│   ├── error-banner.css       ← #blazor-error-ui (тёмная тема)
│   └── reconnect-modal.css    ← #components-reconnect-modal (тёмная тема)
│
├── utils/
│   └── render-math.js         ← window.renderMath(selector)
│
├── demo/
│   └── index.html             ← Визуальный каталог компонентов
│
└── docs/
    ├── tokens.md              ← Справочник токенов
    ├── colors.md              ← Цветовая система
    └── components.md          ← Каталог классов и примеры
```

---

## Дизайн-токены

Все переменные начинаются с `--aif-` (AIFramework).  
Исторически в оригинальном проекте использовался префикс `--cv-`
(от «Computer Vision»), который вводил в заблуждение — в UI Kit он переименован.

Основные категории:

| Категория | Пример |
|---|---|
| Фон | `--aif-bg`, `--aif-bg-void` |
| Поверхности | `--aif-card-bg`, `--aif-glass` |
| Границы | `--aif-border`, `--aif-border-accent` |
| Акцент | `--aif-accent`, `--aif-accent-light` |
| Текст | `--aif-text-1` … `--aif-text-6` |
| Радиусы | `--aif-r-xs` … `--aif-r-card`, `--aif-r-full` |
| Тени | `--aif-shadow-xs` … `--aif-shadow-accent-lg` |
| Шрифты | `--aif-font-sans`, `--aif-font-mono` |
| Макет | `--aif-max-w-wide`, `--aif-header-height` |
| Z-индексы | `--aif-z-topbar`, `--aif-z-overlay` |

Полный справочник: `docs/tokens.md`

---

## Цветовые модификаторы модулей

6 акцентных цветов применяются как CSS-суффикс к большинству компонентов:

```
sky | indigo | violet | emerald | amber | pink
```

Примеры:
```html
<div class="aif-card aif-card--indigo">
<button class="aif-btn aif-btn--emerald">
<div class="aif-icon-pill aif-icon-pill--violet">
<nav class="aif-topbar aif-topbar--sky">
```

Цвет задаётся на уровне C#-модуля через `ILibraryModule.Color` и является
**контрактом** между бэкендом и CSS. Подробнее: `docs/colors.md`

---

## Компоненты

| Файл | Что внутри |
|---|---|
| `card.css` | Навигационные карточки-ссылки + grid-сетки |
| `icon-pill.css` | Квадратные иконки с цветным градиентом |
| `topbar.css` | Sticky topbar, header, footer, brand, GitHub-кнопка |
| `badge.css` | API-badge, version badge, chip, gradient text |
| `button.css` | Run-кнопка, ghost, сегменты, close, аккордеон |
| `choice-grid.css` | Pill-выбор для дискретных параметров |
| `form-controls.css` | Слайдер + числовой input + textarea + code input |
| `tooltip.css` | CSS-тултип через `data-tip` |
| `overlay.css` | Fullscreen overlay, chart wrap, text result |
| `empty-state.css` | Пустой экран, 404, блок ошибки |
| `image-drop.css` | Зона загрузки файла |
| `error-banner.css` | Blazor error UI |
| `reconnect-modal.css` | Blazor reconnect modal (переписан под тёмную тему) |

Полный каталог с примерами разметки: `docs/components.md`

---

## JS-утилиты

### `utils/render-math.js`

Рендер LaTeX-формул (KaTeX) и подсветка кода (highlight.js) в DOM-элементе.

```javascript
// Глобальная функция
window.renderMath(selector);

// Примеры
renderMath('.aif-prose');   // рендер в конкретном контейнере
renderMath(null);            // рендер во всём document.body
```

Функции с polling (до 4 секунд ожидания CDN):
- `window.aifRenderKatex(element)` — только KaTeX
- `window.aifHighlight(element)` — только highlight.js

---

## Известные проблемы, исправленные в UI Kit

### 1. Конфликт `#blazor-error-ui`
В оригинальном проекте один и тот же ID определён дважды с противоречивыми стилями:
- `app.css` — жёлтый фон `#ffffe0` (светлая тема)
- `MainLayout.razor.css` — тёмно-красный `#431407` (тёмная тема)

**UI Kit**: оставлен тёмно-красный вариант (соответствует дизайну).

### 2. Префикс `--cv-` в токенах
Префикс `--cv-` (Computer Vision) использовался для **всех** глобальных токенов.

**UI Kit**: переименован в `--aif-` (AIFramework).

### 3. Светлый ReconnectModal
`ReconnectModal.razor.css` использовал белый фон (`background-color: white`)
и голубые кнопки `#6b9ed2`, что нарушало тёмный дизайн.

**UI Kit**: полностью переписан под тёмную тему с токенами.

### 4. Три разных max-width без системы
`90rem`, `78rem`, `72rem` хардкодились в 5+ файлах.

**UI Kit**: вынесены в токены `--aif-max-w-wide/default/narrow`.

### 5. Дублирование паттерна «тёмная карточка»
`background: linear-gradient(160deg, rgba(14,17,38,0.97)...)` дублировалось 5+ раз.

**UI Kit**: вынесено в `--aif-card-bg`, используется один раз в `card.css`.

---

## Источник

Проект: [AIFramework3Open](https://github.com/AIFramework/AIFramework3Open)  
Лицензия: Apache-2.0  
Стек: .NET 10, Blazor Server, SkiaSharp, Markdig, KaTeX, highlight.js
