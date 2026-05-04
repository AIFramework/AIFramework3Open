# Стратегия стилизации AI Framework UI Kit

UI Kit предлагает три независимых слоя кастомизации, которые можно использовать по отдельности или вместе.

---

## Уровень 1 — `@layer`: переопределение без `!important`

Используйте **`index.layer.css`** вместо `index.css` — тогда все стили кита окажутся внутри `@layer aif`, и стили **без слоя** автоматически побеждают:

```html
<!-- Вместо index.css: -->
<link rel="stylesheet" href="aif-uikit/index.layer.css" />
```

```css
/* ваш файл — просто переопределяйте без !important */
.aif-btn {
  border-radius: 9999px;
  letter-spacing: 0.05em;
}

.aif-card {
  border-width: 2px;
}
```

### Порядок приоритетов (от низшего к высшему)
```
@layer aif-reset  →  @layer aif  →  (без слоя — всегда побеждает)
```

### Совместимость
- **`index.css`** — работает во всех браузерах, без `@layer`
- **`index.layer.css`** — Chrome 99+, Firefox 97+, Safari 15.4+

---

## Уровень 2 — `[data-color]`: контекстный цвет

Поставьте атрибут `data-color` на любой родительский элемент —  
все компоненты внутри автоматически принимают этот цвет.

```html
<!-- Одним атрибутом меняем всё внутри -->
<section data-color="emerald">
  <div class="aif-card">...</div>          <!-- изумрудный hover -->
  <button class="aif-btn">Запустить</button> <!-- изумрудная кнопка -->
  <div class="aif-icon-pill">Icon</div>       <!-- изумрудная иконка -->
  <input class="aif-slider" type="range" /> <!-- изумрудный акцент -->
</section>
```

### Доступные значения `data-color`

| Значение  | Цвет        | Пример применения           |
|-----------|-------------|-----------------------------|
| `sky`     | Голубой     | Компьютерное зрение         |
| `indigo`  | Индиго      | Нейронные сети (по умолчанию) |
| `violet`  | Фиолетовый  | Трансформеры, NLP           |
| `emerald` | Изумрудный  | Генетические алгоритмы      |
| `amber`   | Янтарный    | Оптимизация, метаэвристика  |
| `pink`    | Розовый     | Диффузные модели            |

### Явные модификаторы имеют приоритет

```html
<section data-color="indigo">
  <!-- Кнопка остаётся изумрудной, несмотря на indigo-контекст: -->
  <button class="aif-btn aif-btn--emerald">Особая кнопка</button>
</section>
```

### Компоненты, реагирующие на `[data-color]`

- `.aif-card` — hover-подсветка и акцент-полоска сверху
- `.aif-btn` — цвет градиента и тень
- `.aif-icon-pill` — цвет иконки
- `.aif-topbar` — нижняя граница
- `.aif-theory-bar` — горизонтальная полоска заголовка
- `.aif-param-val` — значение параметра
- `.aif-choice-btn--on` — активная кнопка выбора
- `.aif-mono-badge` — API-бейдж
- `.aif-slider` — акцент слайдера

---

## Уровень 3 — Токены `--aif-*`: глобальная кастомизация

Переопределите CSS-переменные в `:root` — изменения распространятся на весь UI Kit.

```css
/* your-theme.css (подключить после index.css) */
:root {
  /* Смена основного цвета акцента */
  --aif-cur:          #2dd4bf;
  --aif-cur-btn:      linear-gradient(135deg, #0d9488, #14b8a6);
  --aif-cur-btn-shadow: rgba(13, 148, 136, 0.4);

  /* Смена скруглений */
  --aif-radius-sm:  2px;
  --aif-radius:     6px;
  --aif-radius-lg:  10px;

  /* Смена шрифтов */
  --aif-font-sans: "Inter", system-ui, sans-serif;
  --aif-font-mono: "JetBrains Mono", monospace;
}
```

### Ключевые контекстные токены (`--aif-cur-*`)

| Токен                    | Назначение                        |
|--------------------------|-----------------------------------|
| `--aif-cur`              | Основной цвет (текст, иконки)     |
| `--aif-cur-bg`           | Фоновый оттенок                   |
| `--aif-cur-border`       | Граница в нормальном состоянии    |
| `--aif-cur-hover-border` | Граница при hover                  |
| `--aif-cur-hover-shadow` | Тень при hover                    |
| `--aif-cur-top-border`   | Акцент-полоска сверху карточки    |
| `--aif-cur-bar`          | Градиент полоски теории           |
| `--aif-cur-btn`          | Градиент кнопки                   |
| `--aif-cur-btn-shadow`   | Тень кнопки                       |
| `--aif-cur-icon`         | Градиент иконки `.aif-icon-pill`  |
| `--aif-cur-icon-shadow`  | Тень иконки                       |
| `--aif-cur-topbar-border`| Нижняя граница топбара            |
| `--aif-cur-choice-active`| Активная кнопка выбора            |

---

## Добавление нового цветового контекста

Чтобы добавить свой `data-color="teal"`, скопируйте блок из `themes/custom-example.css`:

```css
/* your-theme.css */
[data-color="teal"] {
  --aif-cur:               #2dd4bf;
  --aif-cur-bg:            rgba(13, 148, 136, 0.1);
  --aif-cur-border:        rgba(13, 148, 136, 0.25);
  --aif-cur-hover-border:  rgba(20, 184, 166, 0.35);
  --aif-cur-hover-shadow:  rgba(20, 184, 166, 0.15);
  --aif-cur-top-border:    rgba(13, 148, 136, 0.45);
  --aif-cur-bar:           linear-gradient(90deg, #0d9488, #2dd4bf);
  --aif-cur-btn:           linear-gradient(135deg, #0d9488, #14b8a6);
  --aif-cur-btn-shadow:    rgba(13, 148, 136, 0.4);
  --aif-cur-icon:          linear-gradient(145deg, #2dd4bf, #0d9488);
  --aif-cur-icon-shadow:   rgba(13, 148, 136, 0.45);
  --aif-cur-topbar-border: rgba(13, 148, 136, 0.2);
  --aif-cur-choice-active: linear-gradient(150deg, rgba(20,184,166,0.95), rgba(15,118,110,0.95));
}
```

---

## Светлая тема

Подключите `themes/light.css` после `index.css`:

```html
<link rel="stylesheet" href="aif-uikit/index.css" />
<!-- Всегда светлая: -->
<link rel="stylesheet" href="aif-uikit/themes/light.css" />
<!-- Или по системным настройкам: -->
<link rel="stylesheet" href="aif-uikit/themes/light.css"
      media="(prefers-color-scheme: light)" />
```

Или переключение через JS:
```js
document.documentElement.classList.toggle("aif-theme-light");
```

---

## Применение в Blazor

### 1. `[data-color]` через атрибут Razor

```razor
<section data-color="@Module.Color">
    <div class="aif-card">...</div>
    <button class="aif-btn">Запустить</button>
</section>

@code {
    // Module.Color = "emerald", "sky", "indigo" и т.д.
}
```

### 2. Переопределение токенов через inline-стиль

```razor
<div style="--aif-cur: #2dd4bf; --aif-cur-btn: linear-gradient(135deg, #0d9488, #14b8a6);">
    <button class="aif-btn">Кнопка с кастомным цветом</button>
</div>
```

### 3. Scoped CSS — переопределение для конкретного компонента

```css
/* MyComponent.razor.css */
/* Без @layer — автоматически имеет приоритет над китом */
.my-panel .aif-btn {
  font-size: var(--aif-text-xs);
  padding: 4px 10px;
}
```

---

## Структура файлов темизации

```
AI Framework UI Kit/
├── index.css              ← Точка входа (@layer aif)
├── tokens/
│   ├── tokens.css         ← Все токены + --aif-cur-* (дефолт: indigo)
│   └── themes.css         ← [data-color] → --aif-cur-* маппинги
└── themes/
    ├── custom-example.css ← Шаблоны для кастомизации
    └── light.css          ← Светлая тема
```

---

## Быстрый рецепт: новый модуль с цветом

```html
<!-- 1. Укажите цвет один раз на корневом элементе страницы -->
<div data-color="violet">

  <!-- 2. Используйте стандартные компоненты — цвет применится автоматически -->
  <nav class="aif-topbar">
    <span class="aif-topbar-name">Трансформеры</span>
  </nav>

  <div class="aif-theory-bar">Теория</div>

  <div class="aif-cards-grid">
    <div class="aif-card aif-card--accent-top">
      <div class="aif-icon-pill">Icon</div>
      <div class="aif-card-body">
        <div class="aif-card-title">BERT</div>
        <button class="aif-btn">Демо</button>
      </div>
    </div>
  </div>

</div>
```

**Результат:** весь модуль — фиолетовый. Ноль повторяющихся классов-модификаторов.
