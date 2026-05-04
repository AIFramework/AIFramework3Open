using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace AiFrameworkDemo.Core;

/// <summary>
/// Загрузка Markdown-теории с препроцессингом LaTeX.
/// Поддерживает: $..$, $$..$$, \(..\), \[..\] как inline/display math.
/// Markdig конвертирует их в span.math.math-inline / div.math.math-display,
/// затем JavaScript-функция window.renderMath() пропускает через KaTeX.
/// </summary>
public static class TheoryLoader
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    // Полный набор расширений + математика + автолинки + emoji + task lists + footnotes + таблицы (pipe + grid)
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseMathematics()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAutoLinks()
        .UseEmojiAndSmiley()
        .UseTaskLists()
        .UseGridTables()
        .UsePipeTables()
        .UseFootnotes()
        .UseCitations()
        .UseAbbreviations()
        .UseDefinitionLists()
        .UseFigures()
        .UseGenericAttributes()
        .Build();

    // Конвертируем \[..\] -> $$..$$ и \(..\) -> $..$ ДО Markdig,
    // чтобы он всегда создавал <span class="math"> или <div class="math math-display">.
    private static readonly Regex _reDisplayMath =
        new(@"\\\[(.+?)\\\]", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex _reInlineMath =
        new(@"\\\((.+?)\\\)", RegexOptions.Singleline | RegexOptions.Compiled);

    private static string PreprocessLatex(string md)
    {
        md = _reDisplayMath.Replace(md, m => "\n\n$$" + m.Groups[1].Value.Trim() + "$$\n\n");
        md = _reInlineMath.Replace(md, m => "$" + m.Groups[1].Value + "$");
        return md;
    }

    private static string? _docsRoot;

    /// <summary>Вызывается из Program.cs с ContentRootPath приложения.</summary>
    public static void Configure(string contentRootPath)
    {
        var dir = contentRootPath;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "Docs", "Tutorials");
            if (Directory.Exists(candidate))
            {
                _docsRoot = candidate;
                _cache.Clear();
                return;
            }
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
    }

    private static string DocsRoot
    {
        get
        {
            if (_docsRoot is not null) return _docsRoot;
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10; i++)
            {
                var candidate = Path.Combine(dir, "Docs", "Tutorials");
                if (Directory.Exists(candidate))
                {
                    _docsRoot = candidate;
                    return _docsRoot;
                }
                var parent = Directory.GetParent(dir);
                if (parent is null) break;
                dir = parent.FullName;
            }
            _docsRoot = Path.Combine(AppContext.BaseDirectory, "Docs", "Tutorials");
            return _docsRoot;
        }
    }

    public static string LoadHtml(string tutorialFolder, string theoryFile)
    {
        if (string.IsNullOrEmpty(tutorialFolder) || string.IsNullOrEmpty(theoryFile))
            return Placeholder();

        string cacheKey = $"{tutorialFolder}/{theoryFile}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            string path = Path.Combine(DocsRoot, tutorialFolder, theoryFile);
            if (!File.Exists(path))
                return Placeholder();
            string md = PreprocessLatex(File.ReadAllText(path));
            return Markdown.ToHtml(md, _pipeline);
        });
    }

    private static readonly Regex _reMdSyntax =
        new(@"[#*`>\[\]()!_~\-|$\\{}=+]|\d+\.", RegexOptions.Compiled);

    /// <summary>
    /// Загружает сырой текст теории (.md файл) для полнотекстовой индексации.
    /// Возвращает пустую строку, если файл не найден.
    /// </summary>
    public static string LoadText(string tutorialFolder, string theoryFile)
    {
        if (string.IsNullOrEmpty(tutorialFolder) || string.IsNullOrEmpty(theoryFile))
            return string.Empty;

        string path = Path.Combine(DocsRoot, tutorialFolder, theoryFile);
        if (!File.Exists(path)) return string.Empty;

        string raw = File.ReadAllText(path);
        // Убираем разметку Markdown/LaTeX, оставляем только слова
        raw = _reMdSyntax.Replace(raw, " ");
        return raw;
    }

    public static void ClearCache() => _cache.Clear();

    /// <summary>
    /// Возвращает сырой Markdown для редактирования в UI.
    /// </summary>
    public static string LoadRawMarkdown(string tutorialFolder, string theoryFile)
    {
        if (string.IsNullOrEmpty(tutorialFolder) || string.IsNullOrEmpty(theoryFile))
            return "";
        string path = Path.Combine(DocsRoot, tutorialFolder, theoryFile);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>
    /// Сохраняет Markdown на диск, сбрасывает кэш и возвращает готовый HTML.
    /// </summary>
    public static string SaveAndRender(string tutorialFolder, string theoryFile, string markdown)
    {
        string dir = Path.Combine(DocsRoot, tutorialFolder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, theoryFile), markdown);
        _cache.TryRemove($"{tutorialFolder}/{theoryFile}", out _);
        return LoadHtml(tutorialFolder, theoryFile);
    }

    /// <summary>
    /// Конвертирует Markdown в HTML без сохранения (предпросмотр).
    /// </summary>
    public static string RenderPreview(string markdown)
    {
        return Markdown.ToHtml(PreprocessLatex(markdown), _pipeline);
    }

    /// <summary>
    /// Возвращает путь к файлу теории относительно Docs/Tutorials.
    /// </summary>
    public static string GetRelativePath(string tutorialFolder, string theoryFile)
        => $"Docs/Tutorials/{tutorialFolder}/{theoryFile}";

    private static string Placeholder() =>
        "<p class='th-placeholder'>Теория в разработке. Отредактируйте соответствующий .md файл в Docs/Tutorials/.</p>";
}
