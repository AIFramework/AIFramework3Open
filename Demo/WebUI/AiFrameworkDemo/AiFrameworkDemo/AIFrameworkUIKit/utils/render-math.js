/**
 * AI Framework UI Kit — Math & Code Rendering Utilities
 * Версия: 1.0.0
 *
 * Источник: Demo/WebUI/AiFrameworkDemo/Components/App.razor (строки 40–98, inline <script>)
 * Вынесено в отдельный файл без изменений логики, добавлена документация.
 *
 * Зависимости (CDN, должны быть подключены ДО этого файла):
 *   - KaTeX:       https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.js
 *   - KaTeX auto:  https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/contrib/auto-render.min.js
 *   - highlight.js: https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.11.1/build/highlight.min.js
 *
 * Интеграция с Blazor:
 *   Вызов из C#: await JS.InvokeVoidAsync("renderMath", ".ap-theory-body");
 *   window.renderMath экспортируется глобально для совместимости с Blazor IJSRuntime.
 *
 * Интеграция с Markdig (C#-бэкенд):
 *   Markdig.UseMathematics() создаёт:
 *     $$...$$ → <div class="math math-display">
 *     $...$   → <span class="math math-inline">
 *   Сервер также заменяет \[..\] → $$ и \(..\) → $ до передачи в Markdig.
 */

/**
 * Рендерит KaTeX-формулы в указанном DOM-элементе.
 * Обрабатывает:
 *   - элементы с классами .math.math-display (блочные формулы)
 *   - элементы с классами .math.math-inline (строчные формулы)
 *   - fallback: авторендер через renderMathInElement для оставшихся разделителей
 *
 * @param {Element} root — корневой DOM-элемент для поиска формул
 * @returns {boolean} true если KaTeX загружен и обработка выполнена, false если ещё не готов
 */
function aifRenderKatex(root) {
  if (typeof katex === 'undefined') return false;

  root.querySelectorAll('.math.math-display').forEach(function (e) {
    if (e.dataset.katexDone) return;
    try {
      katex.render(e.textContent.trim(), e, {
        displayMode: true,
        throwOnError: false,
        strict: 'ignore'
      });
    } catch (_) {}
    e.dataset.katexDone = '1';
  });

  root.querySelectorAll('.math.math-inline').forEach(function (e) {
    if (e.dataset.katexDone) return;
    try {
      katex.render(e.textContent.trim(), e, {
        displayMode: false,
        throwOnError: false,
        strict: 'ignore'
      });
    } catch (_) {}
    e.dataset.katexDone = '1';
  });

  // Fallback: авторендер для необработанных разделителей $...$ и \[...\]
  if (typeof renderMathInElement === 'function') {
    try {
      renderMathInElement(root, {
        delimiters: [
          { left: '$$',  right: '$$',  display: true  },
          { left: '\\[', right: '\\]', display: true  },
          { left: '\\(', right: '\\)', display: false },
          { left: '$',   right: '$',   display: false }
        ],
        throwOnError: false,
        ignoredTags: ['script', 'noscript', 'style', 'textarea', 'pre', 'code', 'option'],
        ignoredClasses: ['hljs']
      });
    } catch (_) {}
  }

  return true;
}

/**
 * Подсвечивает блоки кода через highlight.js.
 * Обрабатывает элементы pre > code, пропуская уже обработанные.
 *
 * @param {Element} root — корневой DOM-элемент
 * @returns {boolean} true если hljs загружен и обработка выполнена
 */
function aifHighlight(root) {
  if (typeof hljs === 'undefined') return false;

    root.querySelectorAll('pre code').forEach(function (block) {
    if (block.dataset.hljsDone) return;
    
    // Пропускаем mermaid блоки, их обработает aifRenderMermaid
    if (block.classList.contains('language-mermaid') || block.parentElement.classList.contains('language-mermaid')) {
      return;
    }

    try { hljs.highlightElement(block); } catch (_) {}
    block.dataset.hljsDone = '1';
  });

  return true;
}

/**
 * Рендерит Mermaid-диаграммы.
 * Ищет блоки pre.language-mermaid или code.language-mermaid.
 *
 * @param {Element} root — корневой DOM-элемент
 * @returns {boolean} true если mermaid загружен и обработка выполнена
 */
function aifRenderMermaid(root) {
  if (typeof mermaid === 'undefined') return false;

  var blocks = root.querySelectorAll('pre.language-mermaid, code.language-mermaid, .mermaid');
  if (blocks.length === 0) return true;

  // Инициализация Mermaid при первом вызове
  if (!window._mermaidInitialized) {
    var theme = document.documentElement.getAttribute('data-theme') === 'light' ? 'default' : 'dark';
    mermaid.initialize({
      startOnLoad: false,
      theme: theme,
      securityLevel: 'loose',
      fontFamily: 'Inter, system-ui, sans-serif'
    });
    window._mermaidInitialized = true;
  }

  blocks.forEach(function (block, index) {
    if (block.dataset.mermaidDone) return;
    
    var content = block.textContent.trim();
    var id = 'mermaid-' + Date.now() + '-' + index;
    
    // Создаем контейнер для рендера
    var container = document.createElement('div');
    container.className = 'aif-mermaid-wrap';
    
    // Заменяем оригинальный блок на контейнер
    block.parentNode.insertBefore(container, block);
    block.style.display = 'none';

    try {
      mermaid.render(id, content).then(function(result) {
        container.innerHTML = result.svg;
        block.dataset.mermaidDone = '1';

        // Добавляем zoom/pan для Mermaid SVG
        var svgEl = container.querySelector('svg');
        if (svgEl && typeof svgPanZoom === 'function') {
          svgEl.style.width = '100%';
          svgEl.style.height = '100%';
          svgEl.style.minHeight = '300px';
          svgPanZoom(svgEl, {
            zoomEnabled: true,
            controlIconsEnabled: true,
            fit: true,
            center: true,
            minZoom: 0.1,
            maxZoom: 10
          });
        }
      });
    } catch (err) {
      console.error('Mermaid render error:', err);
      block.style.display = 'block'; // Показываем текст при ошибке
    }
  });

  return true;
}

/**
 * Основная точка входа — рендерит KaTeX, highlight.js и Mermaid в указанном контейнере.
 * Использует polling (до 40 попыток × 100ms = 4 секунды) для ожидания
 * загрузки CDN-библиотек, подключённых через defer.
 *
 * @param {string|null} selector — CSS-селектор контейнера (по умолчанию document.body)
 *
 * @example
 * // Из JavaScript
 * renderMath('.aif-prose');
 *
 * @example
 * // Из C# Blazor (IJSRuntime)
 * await JS.InvokeVoidAsync("renderMath", ".aif-prose");
 */
function renderMath(selector) {
  var el = selector ? document.querySelector(selector) : document.body;
  if (!el) return;

  var attempts = 0;
  function tick() {
    var katexReady   = aifRenderKatex(el);
    var hljsReady    = aifHighlight(el);
    var mermaidReady = aifRenderMermaid(el);
    
    if ((!katexReady || !hljsReady || !mermaidReady) && attempts++ < 40) {
      setTimeout(tick, 100);
    }
  }
  tick();
}

// Экспорт в глобальную область — обратная совместимость с Blazor IJSRuntime.InvokeVoidAsync
window.renderMath     = renderMath;
window.aifRenderKatex   = aifRenderKatex;
window.aifHighlight     = aifHighlight;
window.aifRenderMermaid = aifRenderMermaid;
