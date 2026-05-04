using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Components.Pages;

public partial class AlgoPage : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string LibId   { get; set; } = "";
    [Parameter] public string AlgoKey { get; set; } = "";

    private ILibraryModule? _mod;
    private CategoryDef?    _cat;
    private AlgoDef?        _algo;
    private string          _theoryHtml = "";

    private Dictionary<string, double> _numericParams = new();
    private Dictionary<string, string> _textParams    = new();
    private DemoSettings _settings = new();
    private bool    _needsImage;
    private string? _imageBase64;
    private string? _imagePreview;

    private bool    _settingsOpen;
    private bool    _overlayOpen;
    private bool    _busy;
    private string? _error;
    private string? _pngDataUrl;
    private string? _plotlyJson;
    private string? _pendingPlotlyJson;
    private string  _plotlyDivId = "plotly_" + Guid.NewGuid().ToString("N")[..8];
    private string? _textOutput;
    private long    _elapsedMs;
    private string  _activeKey = "";
    private AI.Charts.ChartView? _sourceChart;
    private DotNetObjectReference<AlgoPage>? _dotNetRef;

    private bool _isLive;
    private bool _destroyPending;
    private CancellationTokenSource? _debounceCts;

    private enum TheoryMode { View, Edit, Preview }
    private TheoryMode _theoryMode = TheoryMode.View;
    private string _rawMarkdown = "";
    private string _previewHtml = "";
    private bool   _needsMathRender;

    protected override void OnParametersSet()
    {
        var compositeKey = LibId + "/" + AlgoKey;
        if (compositeKey == _activeKey) return;

        _mod = LibraryRegistry.Get(LibId);
        if (_mod is null) return;

        foreach (var c in _mod.Categories)
            foreach (var a in c.Algorithms)
                if (a.Key == AlgoKey) { _algo = a; _cat = c; break; }

        if (_algo is null) return;

        _theoryHtml = TheoryLoader.LoadHtml(_mod.TutorialFolder, _algo.TheoryFile);
        _needsImage = _algo.Params.Any(p => p.Key == "_needsImage");
        InitParams();
        _destroyPending = !string.IsNullOrEmpty(_plotlyJson);
        _pngDataUrl = _plotlyJson = _textOutput = _error = null;
        _elapsedMs = 0;
        _isLive = false;
        _theoryMode = TheoryMode.View;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_destroyPending)
        {
            _destroyPending = false;
            try { await JS.InvokeVoidAsync("destroyPlotly", _plotlyDivId); } catch { }
        }

        if (_pendingPlotlyJson is not null)
        {
            var json = _pendingPlotlyJson;
            _pendingPlotlyJson = null;
            await Task.Delay(150);
            try
            {
                _dotNetRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("renderPlotly", _plotlyDivId, json, _settings.DarkTheme, _dotNetRef);
            }
            catch { }
        }

        if (_needsMathRender)
        {
            _needsMathRender = false;
            await Task.Delay(50);
            await JS.InvokeVoidAsync("renderMath", ".aif-prose");
        }

        var key = LibId + "/" + AlgoKey;
        if (key == _activeKey) return;
        _activeKey = key;

        await JS.InvokeVoidAsync("renderMath", ".aif-prose");

        if (!_needsImage)
            await RunAsync();
    }

    private void InitParams()
    {
        _numericParams = new(StringComparer.Ordinal);
        _textParams    = new(StringComparer.Ordinal);
        if (_algo is null) return;
        foreach (var p in _algo.Params)
        {
            if (p.Key.StartsWith("_")) { if (p.Key != "_needsImage") _textParams[p.Key] = p.TextDefault; }
            else _numericParams[p.Key] = p.Default;
        }
    }

    private async void ResetParams()
    {
        if (!string.IsNullOrEmpty(_plotlyJson))
            try { await JS.InvokeVoidAsync("destroyPlotly", _plotlyDivId); } catch { }

        InitParams();
        _pngDataUrl = _plotlyJson = _pendingPlotlyJson = _textOutput = _error = null;
        _elapsedMs = 0;
        _imageBase64 = _imagePreview = null;
    }

    private void SetNumParam(string key, string? val)
    {
        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            _numericParams[key] = v;
        if (_isLive && !_busy) ScheduleLiveRun();
    }

    private void ScheduleLiveRun()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, token);
                if (!token.IsCancellationRequested)
                    await InvokeAsync(RunAsync);
            }
            catch (TaskCanceledException) { }
        });
    }

    private void SetTextParam(string key, string val) => _textParams[key] = val;

    private async Task OnImageSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null) return;
        var buffer = new byte[file.Size];
        await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        await stream.ReadExactlyAsync(buffer);
        _imageBase64  = Convert.ToBase64String(buffer);
        _imagePreview = $"data:{file.ContentType};base64,{_imageBase64}";
    }

    private async Task RunAsync()
    {
        if (_mod is null || _algo is null || _busy) return;
        _busy = true; _error = null;
        StateHasChanged();
        try
        {
            var np = new Dictionary<string, double>(_numericParams);
            var tp = new Dictionary<string, string>(_textParams);
            if (_needsImage && !string.IsNullOrEmpty(_imageBase64))
                tp["_imageBase64"] = _imageBase64;

            var sw     = System.Diagnostics.Stopwatch.StartNew();
            var result = await Task.Run(() => _mod.RunDemo(_algo.Key, np, tp, _settings));
            sw.Stop();
            _elapsedMs = sw.ElapsedMilliseconds;

            _pngDataUrl  = result.PngDataUrl;
            _plotlyJson  = result.PlotlyJson;
            _textOutput  = result.TextOutput;
            _sourceChart = result.SourceChart;
            _isLive      = _elapsedMs < 100 && !_needsImage;

            if (result.Error is not null) _error = result.Error;
            if (result.NeedsImageUpload && string.IsNullOrEmpty(_imageBase64))
                _error = "Загрузите изображение для этого алгоритма.";
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _pngDataUrl = _plotlyJson = _textOutput = null;
            _sourceChart = null;
        }
        finally
        {
            _busy = false;
            if (!string.IsNullOrEmpty(_plotlyJson))
                _pendingPlotlyJson = _plotlyJson;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public Task<string?> ComputeTransform(string action)
    {
        if (_sourceChart is null) return Task.FromResult<string?>(null);
        try { return Task.FromResult(AI.Charts.JS.PlotlyChartRenderer.ComputeTransform(_sourceChart, action)); }
        catch  { return Task.FromResult<string?>(null); }
    }

    private static string FormatScriptOutput(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var line in raw.Split('\n'))
        {
            var esc = System.Net.WebUtility.HtmlEncode(line);
            if      (line.StartsWith(">> "))  sb.AppendLine($"<span class=\"so-cmd\">{esc}</span>");
            else if (line.StartsWith("=> "))  sb.AppendLine($"<span class=\"so-result\">{esc}</span>");
            else if (line.StartsWith("!!! ") || line.StartsWith("ВЫПОЛНЕНИЕ ПРЕРВАНО"))
                                              sb.AppendLine($"<span class=\"so-error\">{esc}</span>");
            else                              sb.AppendLine(esc);
        }
        return sb.ToString();
    }

    private void StartEditing()
    {
        _rawMarkdown = TheoryLoader.LoadRawMarkdown(_mod!.TutorialFolder, _algo!.TheoryFile);
        _theoryMode = TheoryMode.Edit;
    }

    private void ShowPreview()
    {
        _previewHtml = TheoryLoader.RenderPreview(_rawMarkdown);
        _theoryMode = TheoryMode.Preview;
        _needsMathRender = true;
    }

    private async Task SaveTheory()
    {
        _theoryHtml = TheoryLoader.SaveAndRender(_mod!.TutorialFolder, _algo!.TheoryFile, _rawMarkdown);
        _theoryMode = TheoryMode.View;
        _needsMathRender = true;
        await Task.CompletedTask;
    }

    private void CancelEditing()
    {
        _theoryMode = TheoryMode.View;
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        if (!string.IsNullOrEmpty(_plotlyJson))
            _ = JS.InvokeVoidAsync("destroyPlotly", _plotlyDivId);
        _dotNetRef?.Dispose();
    }
}
