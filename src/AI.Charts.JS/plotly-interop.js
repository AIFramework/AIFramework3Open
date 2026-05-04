/**
 * AI.Charts.JS — Plotly interop + context menu (FFT, derivative, integral, histogram).
 * Equivalent to WinForms ChartVisual context menu.
 */
(function () {
    'use strict';

    /* ──────── CSS (injected once) ──────── */
    var styleId = '__aicharts_ctx_style';
    if (!document.getElementById(styleId)) {
        var css = document.createElement('style');
        css.id = styleId;
        css.textContent = [
            '.aichart-ctx { position:fixed; z-index:99999; min-width:200px;',
            '  background:#1e2538; border:1px solid #3a4560; border-radius:6px;',
            '  box-shadow:0 6px 24px rgba(0,0,0,.45); padding:4px 0; font:13px/1.4 "Segoe UI",sans-serif;',
            '  color:#d0d8e8; display:none; }',
            '.aichart-ctx.light { background:#fff; border-color:#d0d5dd; color:#1a252f;',
            '  box-shadow:0 6px 24px rgba(0,0,0,.15); }',
            '.aichart-ctx-item { padding:6px 16px; cursor:pointer; display:flex; align-items:center; gap:8px; }',
            '.aichart-ctx-item:hover { background:#2a3a55; }',
            '.aichart-ctx.light .aichart-ctx-item:hover { background:#eef1f6; }',
            '.aichart-ctx-sep { height:1px; margin:4px 8px; background:#3a4560; }',
            '.aichart-ctx.light .aichart-ctx-sep { background:#e2e5ea; }',
            '.aichart-ctx-icon { width:16px; text-align:center; opacity:.7; font-size:14px; }',
            /* popup overlay for transform results */
            '.aichart-popup { position:fixed; z-index:99998; top:0; left:0; width:100%; height:100%;',
            '  background:rgba(0,0,0,.5); display:flex; align-items:center; justify-content:center; }',
            '.aichart-popup-inner { background:#1e2538; border-radius:8px; width:80vw; max-width:900px;',
            '  height:70vh; position:relative; overflow:hidden; box-shadow:0 8px 32px rgba(0,0,0,.6); }',
            '.aichart-popup.light .aichart-popup-inner { background:#fff; box-shadow:0 8px 32px rgba(0,0,0,.2); }',
            '.aichart-popup-close { position:absolute; top:8px; right:12px; z-index:10;',
            '  background:transparent; border:none; color:#d0d8e8; font-size:22px; cursor:pointer; line-height:1; }',
            '.aichart-popup.light .aichart-popup-close { color:#1a252f; }',
        ].join('\n');
        document.head.appendChild(css);
    }

    /* ──────── Math utilities ──────── */

    function fftReal(re) {
        var n = re.length;
        if (n < 2) return { re: new Float64Array(n), im: new Float64Array(n) };
        var n2 = 1 << Math.ceil(Math.log2(n));
        if (n2 !== n) {
            var padded = new Float64Array(n2);
            padded.set(re);
            re = padded;
            n = n2;
        }
        var log2n = Math.round(Math.log2(n));
        var rr = new Float64Array(n), ii = new Float64Array(n);
        for (var i = 0; i < n; i++) {
            var j = 0, x = i;
            for (var b = 0; b < log2n; b++) { j = (j << 1) | (x & 1); x >>= 1; }
            rr[j] = re[i];
        }
        for (var s = 1; s <= log2n; s++) {
            var m = 1 << s, half = m >> 1;
            var wR = Math.cos(2 * Math.PI / m), wI = -Math.sin(2 * Math.PI / m);
            for (var k = 0; k < n; k += m) {
                var uR = 1, uI = 0;
                for (var jj = 0; jj < half; jj++) {
                    var tR = uR * rr[k + jj + half] - uI * ii[k + jj + half];
                    var tI = uR * ii[k + jj + half] + uI * rr[k + jj + half];
                    rr[k + jj + half] = rr[k + jj] - tR;
                    ii[k + jj + half] = ii[k + jj] - tI;
                    rr[k + jj] += tR;
                    ii[k + jj] += tI;
                    var tmpR = uR * wR - uI * wI;
                    uI = uR * wI + uI * wR;
                    uR = tmpR;
                }
            }
        }
        return { re: rr, im: ii };
    }

    function computeSpectrum(x, y) {
        var n = y.length;
        if (n < 4) return null;
        var windowed = new Float64Array(n);
        for (var i = 0; i < n; i++) {
            var w = 0.54 - 0.46 * Math.cos(2 * Math.PI * i / (n - 1)); // Hamming
            windowed[i] = y[i] * w;
        }
        var f = fftReal(windowed);
        var nn = f.re.length;
        var half = Math.floor(nn / 2);
        var dx = (x.length > 1) ? Math.abs(x[1] - x[0]) : 1;
        var fs = 1.0 / dx;
        var mag = new Float64Array(half);
        var freq = new Float64Array(half);
        for (var i = 0; i < half; i++) {
            mag[i] = Math.sqrt(f.re[i] * f.re[i] + f.im[i] * f.im[i]) * 2 / n;
            freq[i] = i * fs / nn;
        }
        return { x: freq, y: mag };
    }

    function computeDerivative(x, y) {
        var n = y.length;
        if (n < 2) return null;
        var dx = new Float64Array(n), dy = new Float64Array(n);
        for (var i = 0; i < n - 1; i++) {
            var h = x[i + 1] - x[i];
            dx[i] = (x[i] + x[i + 1]) / 2;
            dy[i] = h !== 0 ? (y[i + 1] - y[i]) / h : 0;
        }
        return { x: dx.subarray(0, n - 1), y: dy.subarray(0, n - 1) };
    }

    function computeIntegral(x, y) {
        var n = y.length;
        if (n < 2) return null;
        var ix = new Float64Array(n), iy = new Float64Array(n);
        ix[0] = x[0];
        iy[0] = 0;
        for (var i = 1; i < n; i++) {
            ix[i] = x[i];
            iy[i] = iy[i - 1] + (y[i] + y[i - 1]) * (x[i] - x[i - 1]) / 2;
        }
        return { x: ix, y: iy };
    }

    function computeHistogram(y, bins) {
        var n = y.length;
        if (n < 2) return null;
        bins = bins || Math.max(10, Math.min(100, Math.round(Math.sqrt(n))));
        var mn = y[0], mx = y[0];
        for (var i = 1; i < n; i++) { if (y[i] < mn) mn = y[i]; if (y[i] > mx) mx = y[i]; }
        if (mn === mx) mx = mn + 1;
        var step = (mx - mn) / bins;
        var counts = new Float64Array(bins);
        var centers = new Float64Array(bins);
        for (var i = 0; i < bins; i++) centers[i] = mn + step * (i + 0.5);
        for (var i = 0; i < n; i++) {
            var b = Math.floor((y[i] - mn) / step);
            if (b >= bins) b = bins - 1;
            if (b < 0) b = 0;
            counts[b]++;
        }
        for (var i = 0; i < bins; i++) counts[i] /= (n * step);
        return { x: centers, y: counts };
    }

    /* ──────── Popup chart ──────── */

    function showPopupChart(title, xLabel, yLabel, xData, yData, traceType, dark) {
        var overlay = document.createElement('div');
        overlay.className = 'aichart-popup' + (dark ? '' : ' light');
        var inner = document.createElement('div');
        inner.className = 'aichart-popup-inner';
        var closeBtn = document.createElement('button');
        closeBtn.className = 'aichart-popup-close';
        closeBtn.textContent = '\u00d7';
        closeBtn.onclick = function () { Plotly.purge(plotDiv); document.body.removeChild(overlay); };
        overlay.onclick = function (e) { if (e.target === overlay) closeBtn.onclick(); };
        var plotDiv = document.createElement('div');
        plotDiv.style.cssText = 'width:100%;height:100%;';
        inner.appendChild(closeBtn);
        inner.appendChild(plotDiv);
        overlay.appendChild(inner);
        document.body.appendChild(overlay);

        var bg    = dark ? '#1a1f2e' : '#ffffff';
        var fg    = dark ? '#d0d8e8' : '#000000';
        var grid  = dark ? '#3a4560' : '#b4b4b4';
        var frame = dark ? '#4a5568' : '#cbd5e1';

        var trace = {
            x: Array.from(xData),
            y: Array.from(yData),
            type: traceType === 'bar' ? 'bar' : 'scatter',
            mode: traceType === 'bar' ? undefined : 'lines',
            line: traceType === 'bar' ? undefined : { width: 2, color: '#0078D7' },
            marker: traceType === 'bar' ? { color: '#0078D7' } : undefined,
        };
        var layout = {
            title: { text: '<b>' + title + '</b>', font: { size: 13, color: fg } },
            paper_bgcolor: bg, plot_bgcolor: bg,
            font: { color: fg, size: 11 },
            margin: { l: 88, r: 22, t: 36, b: 56 },
            xaxis: { title: { text: xLabel, font: { size: 11 } }, gridcolor: grid, zeroline: false, showline: true, mirror: true, linecolor: frame },
            yaxis: { title: { text: yLabel, font: { size: 11 } }, gridcolor: grid, zeroline: false, showline: true, mirror: true, linecolor: frame },
        };
        Plotly.newPlot(plotDiv, [trace], layout, { responsive: true, displaylogo: false });
    }

    /* ──────── Context menu ──────── */

    var _ctxMenu = null;
    var _ctxTarget = null;
    var _ctxDark = true;
    var _ctxDotNetRef = null;

    function hideCtx() {
        if (_ctxMenu) _ctxMenu.style.display = 'none';
    }

    function ensureCtxMenu() {
        if (_ctxMenu) return _ctxMenu;
        _ctxMenu = document.createElement('div');
        _ctxMenu.className = 'aichart-ctx';

        var items = [
            { icon: '\u{1f4c8}', text: '\u0421\u043f\u0435\u043a\u0442\u0440 (FFT)', action: 'fft' },
            { icon: '\u{1f4ca}', text: '\u0413\u0438\u0441\u0442\u043e\u0433\u0440\u0430\u043c\u043c\u0430', action: 'hist' },
            { icon: '\u2199', text: '\u041f\u0440\u043e\u0438\u0437\u0432\u043e\u0434\u043d\u0430\u044f', action: 'diff' },
            { icon: '\u222b', text: '\u0418\u043d\u0442\u0435\u0433\u0440\u0430\u043b', action: 'integ' },
            { sep: true },
            { icon: '\u{1f50d}', text: '\u0410\u0432\u0442\u043e\u043c\u0430\u0441\u0448\u0442\u0430\u0431', action: 'autoscale' },
            { sep: true },
            { icon: '\u{1f4be}', text: '\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c PNG', action: 'save' },
            { icon: '\u{1f4cb}', text: '\u041a\u043e\u043f\u0438\u0440\u043e\u0432\u0430\u0442\u044c', action: 'copy' },
        ];

        items.forEach(function (it) {
            if (it.sep) {
                var s = document.createElement('div');
                s.className = 'aichart-ctx-sep';
                _ctxMenu.appendChild(s);
                return;
            }
            var d = document.createElement('div');
            d.className = 'aichart-ctx-item';
            d.innerHTML = '<span class="aichart-ctx-icon">' + it.icon + '</span>' + it.text;
            d.dataset.action = it.action;
            d.addEventListener('click', function (ev) { ev.stopPropagation(); onCtxAction(it.action); });
            _ctxMenu.appendChild(d);
        });

        document.body.appendChild(_ctxMenu);
        document.addEventListener('click', function () { hideCtx(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') hideCtx(); });
        return _ctxMenu;
    }

    function getFirstTraceData(el) {
        if (!el || !el.data || el.data.length === 0) return null;
        for (var i = 0; i < el.data.length; i++) {
            var t = el.data[i];
            if (t.x && t.y && t.x.length > 1 && t.y.length > 1) {
                var xArr = (t.x instanceof Float64Array || t.x instanceof Float32Array)
                    ? Array.from(t.x) : t.x;
                var yArr = (t.y instanceof Float64Array || t.y instanceof Float32Array)
                    ? Array.from(t.y) : t.y;
                return {
                    x: xArr.map(Number).filter(function (v) { return isFinite(v); }),
                    y: yArr.map(Number).filter(function (v) { return isFinite(v); }),
                    name: t.name || ''
                };
            }
        }
        return null;
    }

    function renderPlotlyPopup(jsonStr, dark) {
        if (!jsonStr) return;
        var spec = JSON.parse(jsonStr);
        var traces = spec.traces || [];
        if (traces.length === 0) return;

        var bg   = dark ? '#1a1f2e' : '#ffffff';
        var fg   = dark ? '#d0d8e8' : '#000000';
        var grid = dark ? '#3a4560' : '#b4b4b4';
        var frame = dark ? '#4a5568' : '#cbd5e1';

        var overlay = document.createElement('div');
        overlay.className = 'aichart-popup' + (dark ? '' : ' light');
        var inner = document.createElement('div');
        inner.className = 'aichart-popup-inner';
        var closeBtn = document.createElement('button');
        closeBtn.className = 'aichart-popup-close';
        closeBtn.textContent = '\u00d7';
        closeBtn.onclick = function () { Plotly.purge(plotDiv); document.body.removeChild(overlay); };
        overlay.onclick = function (e) { if (e.target === overlay) closeBtn.onclick(); };
        var plotDiv = document.createElement('div');
        plotDiv.style.cssText = 'width:100%;height:100%;';
        inner.appendChild(closeBtn);
        inner.appendChild(plotDiv);
        overlay.appendChild(inner);
        document.body.appendChild(overlay);

        var layout = {
            autosize: true,
            paper_bgcolor: bg, plot_bgcolor: bg,
            font: { color: fg, family: "'Segoe UI', 'Helvetica Neue', Arial, sans-serif", size: 11 },
            title: { text: '<b>' + (spec.title || '') + '</b>', font: { size: 13, color: fg }, x: 0.5, xanchor: 'center' },
            showlegend: traces.length > 1 && traces.length <= 12,
            legend: { bgcolor: dark ? 'rgba(26,31,46,0.92)' : 'rgba(252,252,253,0.96)', bordercolor: dark ? '#4a5568' : '#BEC0C6', borderwidth: 1, font: { size: 11 } },
            margin: { l: 88, r: 22, t: 36, b: 56 },
            xaxis: { title: { text: spec.axisX || '', font: { size: 11 } }, gridcolor: grid, zeroline: false, showline: true, mirror: true, linecolor: frame, tickfont: { size: 11 } },
            yaxis: { title: { text: spec.axisY || '', font: { size: 11 } }, gridcolor: grid, zeroline: false, showline: true, mirror: true, linecolor: frame, tickfont: { size: 11 } },
        };
        Plotly.newPlot(plotDiv, traces, layout, { responsive: true, displaylogo: false });
    }

    function onCtxAction(action) {
        hideCtx();
        var el = _ctxTarget;
        if (!el) return;

        if (action === 'autoscale') {
            Plotly.relayout(el, { 'xaxis.autorange': true, 'yaxis.autorange': true });
            return;
        }
        if (action === 'save') {
            Plotly.downloadImage(el, { format: 'png', width: 1200, height: 800, filename: 'chart' });
            return;
        }
        if (action === 'copy') {
            Plotly.toImage(el, { format: 'png', width: 1200, height: 800 }).then(function (url) {
                fetch(url).then(function (r) { return r.blob(); }).then(function (blob) {
                    try {
                        navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
                    } catch (e) { window.open(url, '_blank'); }
                });
            });
            return;
        }

        if (_ctxDotNetRef) {
            _ctxDotNetRef.invokeMethodAsync('ComputeTransform', action).then(function (json) {
                if (json) renderPlotlyPopup(json, _ctxDark);
            });
        }
    }

    /* ──────── Main render ──────── */

    window.renderPlotly = function (elementId, jsonStr, darkTheme, dotNetRef) {
        var el = document.getElementById(elementId);
        if (!el || typeof Plotly === 'undefined') return;

        if (el._plotlyInitialized) {
            try { Plotly.purge(el); } catch (e) { }
            el._plotlyInitialized = false;
        }

        var spec = JSON.parse(jsonStr);
        var traces = spec.traces || [];
        var is3d = !!spec.is3d;
        var isPolar = !!spec.isPolar;
        var isLogY = !!spec.isLogY;
        var isGraph = !!spec.isGraph;

        var bg   = darkTheme ? '#1a1f2e' : '#ffffff';
        var fg   = darkTheme ? '#d0d8e8' : '#000000';
        var grid = darkTheme ? '#3a4560' : '#b4b4b4';
        var frame = darkTheme ? '#4a5568' : '#cbd5e1';

        var layout = {
            autosize: true,
            paper_bgcolor: bg,
            plot_bgcolor: bg,
            font: { color: fg, family: "'Segoe UI', 'Helvetica Neue', Arial, sans-serif", size: 11 },
            title: { text: '<b>' + (spec.title || '') + '</b>', font: { size: 13, color: fg }, x: 0.5, xanchor: 'center' },
            showlegend: traces.length > 1 && traces.length <= 12,
            legend: {
                bgcolor: darkTheme ? 'rgba(26,31,46,0.92)' : 'rgba(252,252,253,0.96)',
                bordercolor: darkTheme ? '#4a5568' : '#BEC0C6',
                borderwidth: 1,
                font: { size: 11 }
            },
            hoverlabel: {
                bgcolor: darkTheme ? '#2a3348' : '#ffffff',
                bordercolor: darkTheme ? '#4a5568' : '#cbd5e0',
                font: { size: 11, color: fg }
            },
        };

        if (isGraph) {
            layout.margin = { l: 10, r: 10, t: 40, b: 10 };
            layout.xaxis = {
                visible: false, showgrid: false, zeroline: false,
                showline: false, showticklabels: false,
                fixedrange: false
            };
            layout.yaxis = {
                visible: false, showgrid: false, zeroline: false,
                showline: false, showticklabels: false,
                fixedrange: false
            };
            layout.showlegend = false;
            layout.hovermode = 'closest';
            layout.dragmode = 'pan';
            if (spec.shapes) layout.shapes = spec.shapes;
            if (spec.annotations) layout.annotations = spec.annotations;
        } else if (is3d) {
            layout.margin = { l: 0, r: 0, t: 36, b: 0 };
            layout.scene = {
                xaxis: { title: { text: spec.axisX || '', font: { size: 11 } }, gridcolor: grid, zeroline: false, backgroundcolor: bg, showbackground: true, gridwidth: 1 },
                yaxis: { title: { text: spec.axisY || '', font: { size: 11 } }, gridcolor: grid, zeroline: false, backgroundcolor: bg, showbackground: true, gridwidth: 1 },
                zaxis: { title: { text: spec.axisZ || '', font: { size: 11 } }, gridcolor: grid, zeroline: false, backgroundcolor: bg, showbackground: true, gridwidth: 1 },
                bgcolor: bg,
                camera: spec.camera || { eye: { x: 1.5, y: 1.5, z: 1.2 } },
                aspectmode: 'auto',
                domain: { x: [0, 0.82], y: [0, 1] }
            };
            layout.legend.x = 0;
            layout.legend.y = 1;
            layout.legend.xanchor = 'left';
            layout.legend.yanchor = 'top';
        } else if (isPolar) {
            layout.margin = { l: 56, r: 14, t: 36, b: 46 };
            layout.polar = {
                bgcolor: bg,
                angularaxis: { gridcolor: grid, linecolor: fg, linewidth: 1.5, tickfont: { size: 11 } },
                radialaxis: { title: { text: spec.axisY || '', font: { size: 11 } }, gridcolor: grid, linecolor: fg, tickfont: { size: 11 } }
            };
        } else {
            layout.margin = { l: 88, r: 22, t: 36, b: 56 };
            layout.xaxis = {
                title: { text: spec.axisX || '', font: { size: 11 }, standoff: 6 },
                gridcolor: grid, gridwidth: 1,
                zeroline: false,
                linecolor: frame, linewidth: 1, tickfont: { size: 11 },
                showline: true, mirror: true
            };
            layout.yaxis = {
                title: { text: spec.axisY || '', font: { size: 11 }, standoff: 6 },
                gridcolor: grid, gridwidth: 1,
                zeroline: false,
                linecolor: frame, linewidth: 1, tickfont: { size: 11 },
                showline: true, mirror: true
            };
            if (isLogY) layout.yaxis.type = 'log';
        }

        var config = {
            responsive: false,
            displayModeBar: true,
            modeBarButtonsToRemove: ['sendDataToCloud', 'lasso2d', 'select2d'],
            displaylogo: false,
            scrollZoom: is3d ? 'gl3d' : false
        };

        var doPlot = function () {
            var w = el.offsetWidth, h = el.offsetHeight;
            if (w < 200) w = 680;
            if (h < 200) h = 520;
            layout.autosize = false;
            layout.width  = w;
            layout.height = h;

            try {
                Plotly.newPlot(el, traces, layout, config).then(function () {
                    el._plotlyInitialized = true;
                });
            } catch (e) { }

            if (el._plotlyResizeObserver) {
                try { el._plotlyResizeObserver.disconnect(); } catch (e) { }
            }
            if (typeof ResizeObserver !== 'undefined') {
                var ro = new ResizeObserver(function () {
                    if (!el._plotlyInitialized) return;
                    var nw = el.offsetWidth, nh = el.offsetHeight;
                    if (nw > 100 && nh > 100) {
                        try { Plotly.relayout(el, { width: nw, height: nh }); } catch (e) { }
                    }
                });
                ro.observe(el);
                el._plotlyResizeObserver = ro;
            }
        };

        var prevW = -1, stable = 0;
        var waitForLayout = function () {
            var w = el.offsetWidth;
            if (w === prevW) stable++;
            else { stable = 0; prevW = w; }

            if (stable >= 2 && w > 0 && el.offsetParent !== null) {
                doPlot();
            } else if (stable < 30) {
                requestAnimationFrame(waitForLayout);
            } else {
                doPlot();
            }
        };
        requestAnimationFrame(waitForLayout);

        if (el._ctxBound) {
            el.removeEventListener('contextmenu', el._ctxBound, true);
        }
        var ctxHandler = function (e) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            var menu = ensureCtxMenu();
            _ctxTarget = el;
            _ctxDark = !!darkTheme;
            _ctxDotNetRef = dotNetRef || null;
            menu.className = 'aichart-ctx' + (darkTheme ? '' : ' light');
            menu.style.display = 'block';
            var mx = e.clientX, my = e.clientY;
            menu.style.left = '-9999px';
            menu.style.top = '-9999px';
            menu.style.visibility = 'hidden';
            requestAnimationFrame(function () {
                var mw = menu.offsetWidth, mh = menu.offsetHeight;
                if (mx + mw > window.innerWidth) mx = window.innerWidth - mw - 4;
                if (my + mh > window.innerHeight) my = window.innerHeight - mh - 4;
                if (mx < 0) mx = 0;
                if (my < 0) my = 0;
                menu.style.left = mx + 'px';
                menu.style.top = my + 'px';
                menu.style.visibility = 'visible';
            });
            return false;
        };
        el.addEventListener('contextmenu', ctxHandler, true);
        el._ctxBound = ctxHandler;
    };

    window.destroyPlotly = function (elementId) {
        var el = document.getElementById(elementId);
        if (el) {
            if (el._ctxBound) {
                el.removeEventListener('contextmenu', el._ctxBound, true);
                el._ctxBound = null;
            }
            if (el._plotlyResizeObserver) {
                try { el._plotlyResizeObserver.disconnect(); } catch (e) { }
                el._plotlyResizeObserver = null;
            }
            if (typeof Plotly !== 'undefined') Plotly.purge(el);
            el._plotlyInitialized = false;
        }
    };

    /* ──────── BFCache: force reload when page restored from back-forward cache ──────── */
    window.addEventListener('pageshow', function (e) {
        if (e.persisted) {
            window.location.reload();
        }
    });
})();
