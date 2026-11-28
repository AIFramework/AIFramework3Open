"""Выгрузка эталонных значений для тестов AI.Econometrics.

Запускается один раз вручную; тесты читают готовый JSON и Python не требуют.

    py Tools/EconGolden/export_econ_golden.py

Источники эталонов:
  * устойчивость VAR — numpy.linalg.eigvals сопровождающей матрицы;
  * оценка VAR по данным — numpy.linalg.lstsq по тем же лагам, затем eigvals;
  * интервалы прогноза моделей локального уровня и локального линейного тренда —
    statsmodels.tsa.UnobservedComponents при заданных дисперсиях (smooth, get_forecast).

Результат: Tests/unit/AI.Economics.UnitTests/Golden/econometrics.json.
"""
import json
from pathlib import Path

import numpy as np
import statsmodels
import statsmodels.api as sm

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AI.Economics.UnitTests' / 'Golden'


def companion(lags):
    """Сопровождающая матрица VAR(p): lags — список матриц A_1 … A_p размера k×k."""
    k = lags[0].shape[0]
    p = len(lags)
    c = np.zeros((k * p, k * p))
    c[:k, :] = np.hstack(lags)
    c[k:, :-k] = np.eye(k * (p - 1))
    return c


def var_case(name, lags):
    lags = [np.asarray(a, dtype=float) for a in lags]
    k = lags[0].shape[0]
    eig = np.linalg.eigvals(companion(lags))
    # Коэффициенты в раскладке VarResult.Coefficients: константа, затем лаги подряд
    coefficients = np.hstack([np.zeros((k, 1))] + lags)
    return {
        'name': name,
        'order': len(lags),
        'coefficients': coefficients.tolist(),
        'spectral_radius': float(np.max(np.abs(eig))),
        'eigenvalues': sorted([[float(z.real), float(z.imag)] for z in eig], reverse=True),
    }


def var_fit_case(name, lags, n, seed, noise):
    """Данные из VAR(p) и оценка МНК теми же регрессорами, что в VectorAutoregression.Fit."""
    rng = np.random.default_rng(seed)
    lags = [np.asarray(a, dtype=float) for a in lags]
    k, p = lags[0].shape[0], len(lags)
    y = np.zeros((n, k))
    y[:p] = rng.normal(size=(p, k))
    for t in range(p, n):
        y[t] = sum(lags[i] @ y[t - 1 - i] for i in range(p)) + noise * rng.normal(size=k)

    rows = n - p
    design = np.ones((rows, 1 + k * p))
    for i in range(rows):
        for lag in range(1, p + 1):
            design[i, 1 + (lag - 1) * k: 1 + lag * k] = y[i + p - lag]
    beta, *_ = np.linalg.lstsq(design, y[p:], rcond=None)
    estimated = [beta[1 + i * k: 1 + (i + 1) * k].T for i in range(p)]
    eig = np.linalg.eigvals(companion(estimated))
    return {
        'name': name,
        'order': p,
        'data': y.tolist(),
        'spectral_radius': float(np.max(np.abs(eig))),
    }


def state_space_case(name, spec, n, seed, true_variances, variances, horizon):
    """Ряд из модели и прогноз statsmodels при заданных дисперсиях."""
    rng = np.random.default_rng(seed)
    obs, lvl, slp = true_variances
    level, slope = 10.0, 0.3 if spec == 'local linear trend' else 0.0
    y = np.zeros(n)
    for t in range(n):
        y[t] = level + rng.normal(scale=np.sqrt(obs))
        level += slope + rng.normal(scale=np.sqrt(lvl))
        if spec == 'local linear trend':
            slope += rng.normal(scale=np.sqrt(slp))

    model = sm.tsa.UnobservedComponents(y, spec)
    params = list(variances) if spec == 'local linear trend' else list(variances[:2])
    result = model.smooth(params)
    forecast = result.get_forecast(horizon)
    return {
        'name': name,
        'model': 'LocalLinearTrend' if spec == 'local linear trend' else 'LocalLevel',
        'series': y.tolist(),
        'observation_variance': variances[0],
        'level_variance': variances[1],
        'slope_variance': variances[2] if spec == 'local linear trend' else 0.0,
        'horizon': horizon,
        'forecast': np.asarray(forecast.predicted_mean).tolist(),
        'forecast_se': np.asarray(forecast.se_mean).tolist(),
        'filtered_state_variance': float(result.filtered_state_cov[0, 0, -1]),
        'log_likelihood': float(result.llf),
    }


def main():
    data = {
        'source': {'numpy': np.__version__, 'statsmodels': statsmodels.__version__},
        'var_stability': [
            # Старт степенного метода (1/size, …) — собственный вектор для 0.30, доминирующий корень 1.05 ему ортогонален
            var_case('symmetric_unstable', [[[0.675, -0.375], [-0.375, 0.675]]]),
            var_case('stable_real', [[[0.5, 0.1], [0.2, 0.3]]]),
            var_case('complex_pair', [[[0.6, -0.7], [0.7, 0.6]]]),
            var_case('var2_complex', [[[0.5, 0.2], [-0.3, 0.4]], [[-0.3, 0.1], [0.2, -0.45]]]),
            var_case('var3_three_series', [
                [[0.4, 0.1, 0.0], [0.0, 0.3, 0.2], [0.1, 0.0, 0.5]],
                [[0.2, 0.0, 0.1], [0.1, 0.2, 0.0], [0.0, 0.1, 0.2]],
                [[0.1, 0.0, 0.0], [0.0, 0.1, 0.0], [0.0, 0.0, 0.35]],
            ]),
            var_case('unit_root', [[[1.0, 0.0], [0.0, 0.5]]]),
        ],
        'var_fit': [
            var_fit_case('unstable_by_data', [[[0.675, -0.375], [-0.375, 0.675]]], 70, 11, 0.05),
            var_fit_case('stable_var2', [[[0.5, 0.2], [-0.3, 0.4]], [[-0.3, 0.1], [0.2, -0.45]]], 300, 12, 1.0),
        ],
        'state_space': [
            state_space_case('local_level', 'local level', 120, 21, (2.0, 0.3, 0.0), (1.9, 0.32, 0.0), 8),
            state_space_case('local_level_noisy_level', 'local level', 60, 22, (0.5, 1.5, 0.0), (0.6, 1.4, 0.0), 5),
            state_space_case('local_linear_trend', 'local linear trend', 150, 23, (1.0, 0.2, 0.01), (1.1, 0.25, 0.012), 10),
            state_space_case('local_linear_trend_lively_slope', 'local linear trend', 100, 24, (0.5, 0.1, 0.2), (0.5, 0.1, 0.2), 6),
        ],
    }

    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / 'econometrics.json'
    path.write_text(json.dumps(data, indent=1), encoding='utf-8', newline='\n')
    print(path, path.stat().st_size, 'байт')


if __name__ == '__main__':
    main()
