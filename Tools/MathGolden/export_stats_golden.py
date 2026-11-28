"""Выгрузка эталонных значений описательной статистики (numpy, scipy, pandas).

Запускается один раз вручную; тесты читают готовый JSON и Python не требуют.

    py Tools/MathGolden/export_stats_golden.py

Источники эталонов:
  * дисперсия, СКО, ковариация, корреляция, квантили всех девяти типов Хайндмана — Фана,
    медиана, гистограмма — numpy;
  * асимметрия и эксцесс (смещённые и несмещённые оценки), средние ранги, z-оценки,
    расстояния — scipy;
  * те же дисперсия и квантиль проверены pandas (совпадают с numpy при ddof = 1 и типе 7).

Результат: Tests/unit/AIFramework.UnitTests/Golden/descriptive_statistics.json.
"""
import json
from pathlib import Path

import numpy as np
import pandas as pd
import scipy
from scipy import stats
from scipy.spatial import distance

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AIFramework.UnitTests' / 'Golden'

# Имена типов квантиля в numpy в порядке Хайндмана — Фана (типы 1…9)
QUANTILE_METHODS = ['inverted_cdf', 'averaged_inverted_cdf', 'closest_observation', 'interpolated_inverted_cdf',
                    'hazen', 'weibull', 'linear', 'median_unbiased', 'normal_unbiased']
LEVELS = [0.0, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 1.0]


def datasets():
    rng = np.random.default_rng(2026)
    return {
        'eleven': [float(v) for v in range(1, 12)],
        'even_ten': [3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0, 5.0, 3.5],
        'two': [2.0, 5.0],
        'ties': [1.0, 2.0, 2.0, 2.0, 3.0, 7.0, 7.0, 10.0],
        'normal_200': rng.normal(3.0, 2.0, 200).tolist(),
        'skewed_150': rng.lognormal(0.0, 0.8, 150).tolist(),
        # Сдвиг 1e9: формула E[x²] − E[x]² теряет здесь все знаки
        'shifted': (1e9 + np.array([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0,
                                    17.0, 18.0, 19.0])).tolist(),
        'negative': [-5.5, -1.0, 0.0, 2.5, 3.0, -7.25, 4.0],
    }


def describe(name, values):
    x = np.asarray(values, dtype=float)
    n = len(x)
    item = {
        'name': name,
        'values': [float(v) for v in x],
        'mean': float(np.mean(x)),
        'variance_sample': float(np.var(x, ddof=1)) if n > 1 else None,
        'variance_population': float(np.var(x, ddof=0)),
        'std_sample': float(np.std(x, ddof=1)) if n > 1 else None,
        'std_population': float(np.std(x, ddof=0)),
        'median': float(np.median(x)),
        'quantiles': {str(kind + 1): [float(np.quantile(x, q, method=method)) for q in LEVELS]
                      for kind, method in enumerate(QUANTILE_METHODS)},
        'ranks_average': [float(v) for v in stats.rankdata(x, method='average')],
        'zscore_sample': [float(v) for v in stats.zscore(x, ddof=1)] if n > 1 else None,
        'zscore_population': [float(v) for v in stats.zscore(x, ddof=0)],
        'minmax': [float(v) for v in (x - x.min()) / (x.max() - x.min())],
    }
    if n > 3:
        item['skewness_biased'] = float(stats.skew(x, bias=True))
        item['skewness_unbiased'] = float(stats.skew(x, bias=False))
        item['excess_kurtosis_biased'] = float(stats.kurtosis(x, fisher=True, bias=True))
        item['excess_kurtosis_unbiased'] = float(stats.kurtosis(x, fisher=True, bias=False))
    # pandas как второй эталон умолчаний: var/std с ddof = 1, quantile — линейная интерполяция (тип 7)
    series = pd.Series(x)
    if n > 1:
        assert abs(series.var() - item['variance_sample']) <= 1e-9 * max(1.0, abs(item['variance_sample']))
    assert abs(series.quantile(0.25) - item['quantiles']['7'][3]) <= 1e-12 * max(1.0, abs(item['quantiles']['7'][3]))
    return item


def pairs():
    rng = np.random.default_rng(7)
    x = rng.normal(0.0, 1.0, 120)
    y = 0.6 * x + rng.normal(0.0, 0.8, 120)
    cases = [
        ('linear_noise', x, y),
        ('exact_negative', np.arange(1.0, 9.0), -2.0 * np.arange(1.0, 9.0) + 3.0),
        ('shifted', 1e9 + np.arange(1.0, 20.0), 5e8 + np.arange(1.0, 20.0) ** 1.5),
        ('short', np.array([1.0, 2.0, 4.0]), np.array([2.0, 1.0, 5.0])),
    ]
    out = []
    for name, a, b in cases:
        out.append({
            'name': name,
            'x': [float(v) for v in a],
            'y': [float(v) for v in b],
            'covariance_sample': float(np.cov(a, b, ddof=1)[0, 1]),
            'covariance_population': float(np.cov(a, b, ddof=0)[0, 1]),
            'pearson': float(np.corrcoef(a, b)[0, 1]),
            'euclidean': float(distance.euclidean(a, b)),
            'cosine_similarity': float(1.0 - distance.cosine(a, b)),
            'cosine_distance': float(distance.cosine(a, b)),
            'rss': float(np.sum((a - b) ** 2)),
        })
    return out


def histograms():
    rng = np.random.default_rng(11)
    x = rng.normal(0.0, 1.0, 500)
    out = []
    for name, values, bins in [('normal_500_10', x, 10), ('eleven_5', np.arange(1.0, 12.0), 5), ('ties_4', np.array([1.0, 2.0, 2.0, 2.0, 3.0, 7.0, 7.0, 10.0]), 4)]:
        counts, edges = np.histogram(values, bins=bins)
        density, _ = np.histogram(values, bins=bins, density=True)
        out.append({'name': name, 'values': [float(v) for v in values], 'bins': bins,
                    'edges': [float(v) for v in edges], 'counts': [int(v) for v in counts],
                    'density': [float(v) for v in density]})
    return out


def main():
    data = {
        'source': {'numpy': np.__version__, 'scipy': scipy.__version__, 'pandas': pd.__version__},
        'levels': LEVELS,
        'samples': [describe(name, values) for name, values in datasets().items()],
        'pairs': pairs(),
        'histograms': histograms(),
    }
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / 'descriptive_statistics.json'
    path.write_text(json.dumps(data, indent=1), encoding='utf-8', newline='\n')
    print(path, path.stat().st_size, 'байт')


if __name__ == '__main__':
    main()
