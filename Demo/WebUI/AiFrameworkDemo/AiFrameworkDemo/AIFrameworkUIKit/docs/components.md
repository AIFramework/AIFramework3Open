# AI Framework UI Kit — Каталог компонентов

Все компоненты используют токены из `tokens/tokens.css`.  
Для работы требуется подключить `index.css`.

---

## Card (`components/card.css`)

Тёмная карточка-ссылка с цветным hover-эффектом.  
Объединяет `home-card`, `lp-card`, `cp-card` из оригинального проекта.

### Классы

| Класс | Описание |
|---|---|
| `.aif-card` | Базовая карточка (крупная, для главной страницы) |
| `.aif-card--sm` | Компактная (для вложенных категорий) |
| `.aif-card--{color}` | Цветовой hover-эффект (sky/indigo/violet/emerald/amber/pink) |
| `.aif-card--accent-top` | Акцентная полоска сверху + цветной модификатор |
| `.aif-card-body` | Flex-контейнер для title/desc/meta |
| `.aif-card-title` | Заголовок карточки |
| `.aif-card-title--sm` | Компактный заголовок |
| `.aif-card-desc` | Описание |
| `.aif-card-meta` | Строка метаданных (кол-во категорий, алгоритмов) |
| `.aif-card-arrow` | Иконка-стрелка навигации |
| `.aif-cards-grid` | Grid-сетка для главной (minmax 320px) |
| `.aif-cards-grid--sm` | Grid для LibraryPage (minmax 280px) |
| `.aif-cards-grid--xs` | Grid для CategoryPage (minmax 260px) |

### Пример разметки

```html
<a class="aif-card aif-card--indigo" href="/lib/nn">
  <div class="aif-icon-pill aif-icon-pill--indigo">
    <svg><!-- icon --></svg>
  </div>
  <div class="aif-card-body">
    <h2 class="aif-card-title">AI.NeuralNetworks</h2>
    <p class="aif-card-desc">Нейронные сети V2: MLP, GRU, автоэнкодер</p>
    <div class="aif-card-meta">
      <span>4 категории</span>
      <span class="aif-card-meta-sep">·</span>
      <span>8 алгоритмов</span>
    </div>
  </div>
  <svg class="aif-card-arrow"><!-- arrow --></svg>
</a>
```

---

## Icon Pill (`components/icon-pill.css`)

Квадратная иконка с градиентным фоном. Иконка конкретного модуля.

### Классы

| Класс | Описание |
|---|---|
| `.aif-icon-pill` | Базовый размер (3rem × 3rem) |
| `.aif-icon-pill--lg` | Крупный hero-вариант (3.5rem × 3.5rem) |
| `.aif-icon-pill--{color}` | Цветной градиент (6 вариантов) |

---

## Topbar (`components/topbar.css`)

Sticky-навигация второго уровня (под хедером).

### Классы

| Класс | Описание |
|---|---|
| `.aif-topbar` | Sticky-топбар |
| `.aif-topbar--wide` | Широкий вариант (max-width: 90rem) |
| `.aif-topbar--{color}` | Цветная нижняя граница |
| `.aif-topbar-inner` | Внутренний контейнер |
| `.aif-topbar-back` | Ссылка «назад» |
| `.aif-topbar-sep` | Разделитель `›` |
| `.aif-topbar-name` | Текущий раздел |
| `.aif-theory-bar` | 3px-полоска цвета (в заголовке карточки теории) |
| `.aif-theory-bar--{color}` | Цветной градиент полоски |
| `.aif-header` | Главный sticky-хедер (Header приложения) |
| `.aif-header-inner` | Внутренний контейнер хедера |
| `.aif-brand` | Логотип + название |
| `.aif-brand-logo` | Изображение логотипа |
| `.aif-brand-text` | Текст названия |
| `.aif-brand-prefix` | Акцентная часть «AI» |
| `.aif-footer` | Footer |
| `.aif-footer-inner` | Контейнер footer |
| `.aif-footer-sep` | Разделитель `·` |
| `.aif-footer-link` | Ссылка в footer |
| `.aif-gh-btn` | Кнопка GitHub в хедере |

### Пример разметки топбара

```html
<nav class="aif-topbar aif-topbar--indigo">
  <div class="aif-topbar-inner">
    <a class="aif-topbar-back" href="/lib/nn">
      <svg><!-- arrow-left --></svg>
      Классификация
    </a>
    <span class="aif-topbar-sep">›</span>
    <span class="aif-topbar-name">MLP-классификатор</span>
    <code class="aif-mono-badge">AI.NeuralNetworks.V2.Nn.Sequential</code>
  </div>
</nav>
```

---

## Badge (`components/badge.css`)

### Классы

| Класс | Описание |
|---|---|
| `.aif-mono-badge` | Mono-шрифт, indigo-фон. Для API-классов |
| `.aif-mono-badge--md` | Чуть крупнее |
| `.aif-version-badge` | Фиолетовый градиент с текстом версии |
| `.aif-version-badge--float` | Floating поверх логотипа (повёрнут на -4°) |
| `.aif-chip` | Метаданные (лицензия, технологии) |
| `.aif-chip--link` | Кликабельный chip-ссылка |
| `.aif-gradient-text` | Текст с indigo-фиолетовым градиентом |

---

## Button (`components/button.css`)

### Классы

| Класс | Описание |
|---|---|
| `.aif-btn` | Основная кнопка действия (pill-shape) |
| `.aif-btn--{color}` | Цветной градиентный фон + тень |
| `.aif-btn:disabled` | Неактивное состояние (opacity 0.4) |
| `.aif-btn-spinner` | Анимированный спиннер внутри кнопки |
| `.aif-btn-meta` | Метаданные рядом (время выполнения) |
| `.aif-btn-ghost` | Ghost-кнопка (Сброс) |
| `.aif-btn-row` | Flex-строка с кнопками |
| `.aif-seg` | Сегментированный контейнер |
| `.aif-seg-btn` | Кнопка сегмента |
| `.aif-seg-btn--on` | Активный сегмент |
| `.aif-close-btn` | Круглая кнопка закрытия |
| `.aif-accordion-toggle` | Кнопка-переключатель аккордеона |
| `.aif-accordion-arrow` | Стрелка аккордеона |
| `.aif-accordion-arrow--open` | Развёрнутая стрелка (rotate 180°) |

---

## Choice Grid (`components/choice-grid.css`)

Pill-выбор для дискретных параметров (датасет, бэкенд алгоритма).

### Классы

| Класс | Описание |
|---|---|
| `.aif-choice-grid` | Grid-контейнер (auto-fit, minmax 7.5rem) |
| `.aif-choice-btn` | Кнопка варианта |
| `.aif-choice-btn--{color}` | Цветное активное состояние |
| `.aif-choice-btn--on` | Выбранный вариант |
| `.aif-choice-icon` | SVG-иконка внутри кнопки |
| `.aif-choice-label` | Текстовая метка |

---

## Form Controls (`components/form-controls.css`)

### Классы

| Класс | Описание |
|---|---|
| `.aif-param-row` | Строка параметра (label + controls) |
| `.aif-param-label-row` | Строка заголовка (label + unit + value) |
| `.aif-param-label` | Название параметра |
| `.aif-param-unit` | Единица измерения |
| `.aif-param-val` | Текущее значение (mono) |
| `.aif-param-val--{color}` | Цветной вариант значения |
| `.aif-param-controls` | Flex-строка слайдер + число |
| `.aif-slider` | Range input |
| `.aif-num-input` | Number input (5.5rem ширина) |
| `.aif-text-input` | Textarea для текстовых параметров |
| `.aif-code-input` | Textarea для выражений/скриптов |
| `.aif-params-panel` | Карточка-обёртка панели параметров |
| `.aif-params-header` | Заголовок панели («ПАРАМЕТРЫ») |
| `.aif-params-body` | Тело панели (flex-column, gap) |
| `.aif-settings-body` | Тело аккордеона настроек |
| `.aif-settings-row` | Строка настроек |
| `.aif-settings-label` | Метка настройки |

---

## Tooltip (`components/tooltip.css`)

CSS-тултип через `data-tip`. Появляется **слева** от элемента.

### Классы

| Класс | Описание |
|---|---|
| `.aif-tip` | Элемент с тултипом, `data-tip="текст"` |
| `.aif-tip--right` | Тултип справа |

### Пример

```html
<span class="aif-tip" data-tip="Суммарный объём обучающей выборки">
  <svg width="13" height="13"><!-- info icon --></svg>
</span>
```

---

## Overlay (`components/overlay.css`)

Полноэкранный тёмный overlay для просмотра графика.

### Классы

| Класс | Описание |
|---|---|
| `.aif-overlay` | Полноэкранный overlay (fixed, blur backdrop) |
| `.aif-overlay-box` | Модальный контейнер |
| `.aif-overlay-header` | Хедер модального окна |
| `.aif-overlay-title` | Заголовок |
| `.aif-overlay-img-wrap` | Область изображения |
| `.aif-overlay-img` | Само изображение |
| `.aif-chart-wrap` | Обёртка графика (cursor: zoom-in) |
| `.aif-chart-img` | Изображение графика |
| `.aif-chart-expand` | Иконка «развернуть» |
| `.aif-text-result` | Блок текстового вывода |
| `.so-cmd` | Строка команды |
| `.so-result` | Строка результата |
| `.so-error` | Строка ошибки |

---

## Empty State (`components/empty-state.css`)

### Классы

| Класс | Описание |
|---|---|
| `.aif-empty` | Базовый пустой экран (5rem padding, centered) |
| `.aif-empty--dashed` | С пунктирной рамкой (ожидание действия) |
| `.aif-error-block` | Строка ошибки выполнения (красная) |
| `.aif-404` | Страница 404 |
| `.aif-404-code` | Большая цифра «404» |
| `.aif-404-title` | Заголовок 404 |
| `.aif-404-link` | Ссылка «на главную» |
| `.aif-error-page` | Страница ошибки |
| `.aif-error-page-icon` | Иконка ошибки |
| `.aif-error-page-title` | Заголовок |
| `.aif-error-page-msg` | Сообщение |
| `.aif-error-page-link` | Ссылка |

---

## Image Drop (`components/image-drop.css`)

Зона загрузки изображения (file input).

### Классы

| Класс | Описание |
|---|---|
| `.aif-img-drop` | Зона загрузки (dashed border) |
| `.aif-img-drop-text` | Основной текст |
| `.aif-img-drop-hint` | Подсказка (форматы) |
| `.aif-img-drop-change` | «Нажмите чтобы сменить» |
| `.aif-img-preview` | Превью загруженного изображения |
| `.aif-file-hidden` | Скрытый `<input type="file">` |

---

## Error Banner (`components/error-banner.css`)

| Класс / ID | Описание |
|---|---|
| `#blazor-error-ui` | Нижняя Blazor-ошибка (dark, position: fixed) |
| `.blazor-error-boundary` | Boundary-обёртка с красной рамкой |

---

## Reconnect Modal (`components/reconnect-modal.css`)

Модальное окно переподключения Blazor Server. Полностью переписано
под тёмную тему (оригинал использовал светлую).

Стандартные Blazor-классы сохранены без изменений для совместимости:
- `#components-reconnect-modal`
- `.components-reconnect-container`
- `.components-rejoining-animation`
- `.components-reconnect-*-visible` (state selectors)

---

## Typography / Prose (`base/typography.css`)

Стили для Markdown-контента (теория алгоритмов).

Применяется к контейнеру `.aif-prose`.  
В Blazor: замена `Scoped CSS :deep()` на глобальный класс.

Поддерживает: заголовки h1–h6, параграфы, ссылки, inline-код,
блоки кода (highlight.js), списки, task lists, цитаты, таблицы,
горизонтальный разделитель, изображения, KaTeX-математику,
сноски (Markdig footnotes).

---

## Animations (`base/animations.css`)

| Keyframe | Применение |
|---|---|
| `aif-spin` | Спиннер в кнопке |
| `aif-pulse-ring` | Пульсирующее кольцо |
| `aif-shimmer` | Скелетон-загрузка |
| `aif-float` | Парение логотипа |
| `aif-fade-up` | Появление карточек и результатов |

Вспомогательный класс: `.aif-fade-up { animation: aif-fade-up 0.25s ease both; }`
