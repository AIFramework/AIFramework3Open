/**
 * AIFramework UIKit — Theme Manager
 * Управление тёмной/светлой темой через cookie + localStorage + data-theme на <html>.
 *
 * Cookie «aif-theme» читается сервером (Blazor SSR) → нет вспышки при загрузке.
 * localStorage — фоллбэк для статических демо-страниц UIKit.
 *
 * API:
 *   aifTheme.get()               → 'dark' | 'light'
 *   aifTheme.set(t)              → сохраняет тему, применяет data-theme
 *   aifTheme.toggle()            → переключает тему и возвращает новую
 *   aifTheme.getContrast()       → 'normal' | 'high'
 *   aifTheme.setContrast(level)  → сохраняет контраст, применяет data-contrast
 *   aifTheme.toggleContrast()    → normal ↔ high
 *   aifTheme.apply()             → читает сохранённые theme/contrast и применяет
 */

window.aifTheme = (function () {
  var THEME_COOKIE_KEY      = 'aif-theme';
  var THEME_STORAGE_KEY     = 'aif-theme';
  var CONTRAST_COOKIE_KEY   = 'aif-contrast';
  var CONTRAST_STORAGE_KEY  = 'aif-contrast';
  var DEFAULT_THEME         = 'dark';
  var DEFAULT_CONTRAST      = 'normal';
  var COOKIE_TTL            = 365 * 24 * 60 * 60; // 1 год в секундах

  function readCookie(key) {
    var m = document.cookie.match(new RegExp('(?:^|;\\s*)' + key + '=([^;]+)'));
    return m ? decodeURIComponent(m[1]) : null;
  }

  function writeCookie(key, value) {
    document.cookie =
      key + '=' + encodeURIComponent(value) +
      '; path=/' +
      '; max-age=' + COOKIE_TTL +
      '; SameSite=Lax';
  }

  function normalizeTheme(theme) {
    return theme === 'light' ? 'light' : 'dark';
  }

  function normalizeContrast(contrast) {
    return contrast === 'high' ? 'high' : 'normal';
  }

  function get() {
    // Куки приоритетнее: сервер тоже читает их
    return normalizeTheme(readCookie(THEME_COOKIE_KEY) || localStorage.getItem(THEME_STORAGE_KEY) || DEFAULT_THEME);
  }

  function getContrast() {
    return normalizeContrast(readCookie(CONTRAST_COOKIE_KEY) || localStorage.getItem(CONTRAST_STORAGE_KEY) || DEFAULT_CONTRAST);
  }

  function set(theme) {
    var next = normalizeTheme(theme);
    writeCookie(THEME_COOKIE_KEY, next);
    localStorage.setItem(THEME_STORAGE_KEY, next);
    document.documentElement.setAttribute('data-theme', next);
  }

  function toggle() {
    var next = document.documentElement.getAttribute('data-theme') === 'light'
      ? 'dark'
      : 'light';
    set(next);
    return next;
  }

  function setContrast(contrast) {
    var next = normalizeContrast(contrast);
    writeCookie(CONTRAST_COOKIE_KEY, next);
    localStorage.setItem(CONTRAST_STORAGE_KEY, next);
    document.documentElement.setAttribute('data-contrast', next);
    return next;
  }

  function toggleContrast() {
    var current = document.documentElement.getAttribute('data-contrast') || getContrast();
    var next = current === 'high' ? 'normal' : 'high';
    return setContrast(next);
  }

  function apply() {
    var theme = get();
    var contrast = getContrast();
    document.documentElement.setAttribute('data-theme', theme);
    document.documentElement.setAttribute('data-contrast', contrast);
    return { theme: theme, contrast: contrast };
  }

  return { get, set, toggle, getContrast, setContrast, toggleContrast, apply };
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
