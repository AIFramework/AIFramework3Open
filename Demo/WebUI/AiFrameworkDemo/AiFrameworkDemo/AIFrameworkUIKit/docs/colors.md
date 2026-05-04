# AI Framework UI Kit — Цветовая система

## Концепция

Дизайн-система построена на **тёмной базе** с **6 акцентными цветами модулей**.  
Каждый модуль/библиотека получает один цвет, который применяется последовательно:
- иконка на главной странице
- полоска в заголовке карточки теории
- верхняя граница панели параметров
- градиент основной кнопки «Запустить»
- badge значения параметра
- hover-эффект карточки на навигационных страницах

---

## Базовая палитра фона

```
#02030a  --aif-bg-void   ####  Void — фон body
#050710  --aif-bg-deep   ####  Deep — тёмный фон страниц
#080b17  --aif-bg        ####  Base — основной фон
```

---

## Текстовая шкала (6 ступеней)

```
#f8fafc  --aif-text-1  ####  Primary   — заголовки
#e2e8f0  --aif-text-2  ####  Secondary — подзаголовки
#94a3b8  --aif-text-3  ####  Muted     — контент
#64748b  --aif-text-4  ####  Faint     — метки
#475569  --aif-text-5  ####  Ghost     — placeholder
#334155  --aif-text-6  ####  Dim       — стрелки, разделители
```

---

## Акцентный цвет системы — Indigo

Indigo (#6366f1) — основной акцент всего интерфейса (не привязан к конкретному модулю):
- ссылки, фокус-кольца, активные состояния сегментов
- scrollbar-thumb теории
- bullet-points в markdown
- `accent-color` для range-слайдера
- hover-подсветка таблиц в теории

```
#6366f1  --aif-accent        ####  Основной
#818cf8  --aif-accent-light  ####  Светлый (ссылки, API-badge)
#a5b4fc  --aif-accent-bright ####  Яркий (hover)
```

---

## 6 цветов модулей

### Sky (Небесный)
Применяется для модуля: DSP, ComputerVision или других «синих» библиотек.

```
#38bdf8  --aif-c-sky         ####  Base
         --aif-c-sky-bg      rgba(14,165,233, 0.1)
         --aif-c-sky-border  rgba(14,165,233, 0.22)
```

CSS-модификаторы: `--sky` (`.aif-card--sky`, `.aif-btn--sky`, `.aif-icon-pill--sky`...)

---

### Indigo (Индиго)
Применяется для AI/NeuralNetworks модулей.

```
#818cf8  --aif-c-indigo         ####  Base (светлый, для текста)
         --aif-c-indigo-bg      rgba(79,70,229, 0.1)
         --aif-c-indigo-border  rgba(79,70,229, 0.25)
```

Кнопка при indigo:
```css
background: linear-gradient(135deg, #4f46e5, #6366f1);
box-shadow: 0 4px 20px -4px rgba(99,102,241, 0.4);
```

---

### Violet (Фиолетовый)
Применяется для ML, классификации.

```
#a78bfa  --aif-c-violet         ####  Base
         --aif-c-violet-bg      rgba(124,58,237, 0.1)
         --aif-c-violet-border  rgba(124,58,237, 0.25)
```

Кнопка:
```css
background: linear-gradient(135deg, #7c3aed, #8b5cf6);
```

---

### Emerald (Изумрудный)
Применяется для классической математики, алгоритмов оптимизации.

```
#34d399  --aif-c-emerald         ####  Base
         --aif-c-emerald-bg      rgba(5,150,105, 0.1)
         --aif-c-emerald-border  rgba(5,150,105, 0.22)
```

Кнопка:
```css
background: linear-gradient(135deg, #059669, #10b981);
```

---

### Amber (Янтарный)
Применяется для систем управления (Control Systems).

```
#fbbf24  --aif-c-amber         ####  Base
         --aif-c-amber-bg      rgba(217,119,6, 0.1)
         --aif-c-amber-border  rgba(217,119,6, 0.25)
```

Кнопка:
```css
background: linear-gradient(135deg, #b45309, #d97706);
```

---

### Pink (Розовый)
Применяется для Charts, визуализации данных.

```
#f472b6  --aif-c-pink         ####  Base
         --aif-c-pink-bg      rgba(219,39,119, 0.1)
         --aif-c-pink-border  rgba(219,39,119, 0.25)
```

Кнопка:
```css
background: linear-gradient(135deg, #be185d, #ec4899);
```

---

## Семантические цвета (ошибки)

```
#f43f5e  --aif-error         ####  Красный — ошибки
#fb7185  --aif-error-text    ####  Текст ошибки (светлее)
         --aif-error-bg      rgba(244,63,94, 0.08)
         --aif-error-border  rgba(244,63,94, 0.25)
```

---

## Цвет версии (Version Badge)

Version badge и brand badge используют фиксированный фиолетово-indigo градиент,
независимо от текущего цвета модуля:

```css
background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
box-shadow: 0 2px 8px -2px rgba(99,102,241, 0.55);
```

---

## Контракт именования CSS-модификаторов

Имена цветов (sky, indigo, violet, emerald, amber, pink) являются контрактом
между C#-кодом и CSS. В Blazor они передаются через свойство:

```csharp
public interface ILibraryModule {
    string Color { get; }  // Возвращает: "sky" | "indigo" | "violet" | "emerald" | "amber" | "pink"
}
```

И подставляются в разметку:
```html
<div class="aif-card aif-card--@lib.Color">
```

**Важно:** изменение значений `Color` в C# ломает CSS без изменения стилей.
