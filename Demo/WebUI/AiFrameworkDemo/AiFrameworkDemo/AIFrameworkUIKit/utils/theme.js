/**
 * AIFramework UIKit — Theme Manager
 * Управление тёмной/светлой темой через cookie + localStorage + data-theme на <html>.
 *
 * Cookie «aif-theme» читается сервером (Blazor SSR) → нет вспышки при загрузке.
 * localStorage — фоллбэк для статических демо-страниц UIKit.
 *
 * API:
 *   aifTheme.get()    → 'dark' | 'light'
 *   aifTheme.set(t)   → сохраняет в cookie + localStorage, применяет data-theme
 *   aifTheme.toggle() → переключает и возвращает новую тему
 *   aifTheme.apply()  → читает сохранённую тему и применяет
 */

window.aifTheme = (function () {
  var COOKIE_KEY   = 'aif-theme';
  var STORAGE_KEY  = 'aif-theme';
  var DEFAULT      = 'dark';
  var COOKIE_TTL   = 365 * 24 * 60 * 60; // 1 год в секундах

  function getCookie() {
    var m = document.cookie.match(/(?:^|;\s*)aif-theme=([^;]+)/);
    return m ? m[1] : null;
  }

  function setCookie(theme) {
    document.cookie =
      COOKIE_KEY + '=' + theme +
      '; path=/' +
      '; max-age=' + COOKIE_TTL +
      '; SameSite=Lax';
  }

  function get() {
    // Куки приоритетнее: сервер тоже читает их
    return getCookie() || localStorage.getItem(STORAGE_KEY) || DEFAULT;
  }

  function set(theme) {
    setCookie(theme);
    localStorage.setItem(STORAGE_KEY, theme);
    document.documentElement.setAttribute('data-theme', theme);
  }

  function toggle() {
    var next = document.documentElement.getAttribute('data-theme') === 'light'
      ? 'dark'
      : 'light';
    set(next);
    return next;
  }

  function apply() {
    var theme = get();
    document.documentElement.setAttribute('data-theme', theme);
    return theme;
  }

  return { get, set, toggle, apply };
})();

// Применяем при каждой загрузке скрипта
aifTheme.apply();

// Blazor enhanced navigation: восстанавливаем после перехода
document.addEventListener('blazor:afterNavigation', function () {
  aifTheme.apply();
});

/**
 * aifUI — утилиты для Blazor JSInterop
 */
window.aifUI = {
  scrollToId: function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
};
