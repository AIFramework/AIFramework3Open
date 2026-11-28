"""Выгрузка эталонных значений метрик качества (scikit-learn, statsmodels).

Запускается один раз вручную; тесты читают готовый JSON и Python не требуют.

    py Tools/MathGolden/export_metrics_golden.py

Источники эталонов:
  * R², MAE, RMSE, MAPE, accuracy — sklearn.metrics;
  * квадрат корреляции Пирсона — numpy.corrcoef (он не равен R², когда прогноз смещён);
  * MASE — определение Хайндмана и Кёлера (2006): MAE прогноза, делённая на MAE наивного
    сезонного прогноза на обучающем ряде; в sklearn её нет, считается numpy по формуле;
  * псевдо-R² Макфаддена — statsmodels Logit (prsquared = 1 − llf / llnull).

Результат: Tests/unit/AIFramework.UnitTests/Golden/metrics.json.
"""
import json
from pathlib import Path

import numpy as np
import sklearn
import statsmodels
import statsmodels.api as sm
from sklearn import metrics

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AIFramework.UnitTests' / 'Golden'


def regression_case(name, y, pred):
    y = np.asarray(y, dtype=float)
    pred = np.asarray(pred, dtype=float)
    item = {
        'name': name,
        'y': y.tolist(),
        'pred': pred.tolist(),
        'r2': float(metrics.r2_score(y, pred)),
        'mae': float(metrics.mean_absolute_error(y, pred)),
        'mse': float(metrics.mean_squared_error(y, pred)),
        'rmse': float(np.sqrt(metrics.mean_squared_error(y, pred))),
        'mape': float(metrics.mean_absolute_percentage_error(y, pred)),
    }
    if np.std(y) > 0 and np.std(pred) > 0:
        item['pearson_squared'] = float(np.corrcoef(y, pred)[0, 1] ** 2)
    return item


def mase(actual, forecast, training, period):
    actual, forecast, training = (np.asarray(v, dtype=float) for v in (actual, forecast, training))
    scale = np.mean(np.abs(training[period:] - training[:-period]))
    return float(np.mean(np.abs(actual - forecast)) / scale)


def main():
    rng = np.random.default_rng(31)
    x = rng.normal(0, 1, 60)
    y = 2.0 + 1.5 * x + rng.normal(0, 0.4, 60)

    regression = [
        regression_case('good_fit', y, 2.0 + 1.5 * x),
        # Прогноз 2y + 10: корреляция единица, а R² глубоко отрицателен
        regression_case('shifted_and_scaled', y, 2.0 * y + 10.0),
        regression_case('mean_prediction', y, np.full_like(y, y.mean())),
        regression_case('exact', [1.0, 2.0, 3.0, 4.0], [1.0, 2.0, 3.0, 4.0]),
        regression_case('worse_than_mean', [1.0, 2.0, 3.0, 4.0], [4.0, 3.0, 2.0, 1.0]),
        regression_case('with_zero_target', [0.0, 1.0, 2.0, 4.0], [0.5, 1.0, 1.5, 5.0]),
        regression_case('constant_target_exact', [3.0, 3.0, 3.0], [3.0, 3.0, 3.0]),
        regression_case('constant_target_missed', [3.0, 3.0, 3.0], [3.0, 3.5, 2.0]),
    ]

    series = (10 + 0.3 * np.arange(48) + 3 * np.sin(2 * np.pi * np.arange(48) / 12) + rng.normal(0, 0.5, 48)).tolist()
    train, test = series[:36], series[36:]
    naive = [train[-12 + (i % 12)] for i in range(12)]
    flat = [train[-1]] * 12
    mase_cases = [
        {'name': 'seasonal_naive_period_12', 'actual': test, 'forecast': naive, 'training': train, 'period': 12,
         'value': mase(test, naive, train, 12)},
        {'name': 'flat_period_1', 'actual': test, 'forecast': flat, 'training': train, 'period': 1,
         'value': mase(test, flat, train, 1)},
        {'name': 'in_sample_period_1', 'actual': train[1:], 'forecast': train[:-1], 'training': train, 'period': 1,
         'value': mase(train[1:], train[:-1], train, 1)},
    ]

    labels = rng.integers(0, 3, 80)
    guessed = np.where(rng.random(80) < 0.7, labels, rng.integers(0, 3, 80))
    classification = [{
        'name': 'three_classes',
        'y': labels.tolist(),
        'pred': guessed.tolist(),
        'accuracy': float(metrics.accuracy_score(labels, guessed)),
    }]

    design = sm.add_constant(rng.normal(0, 1, (200, 2)))
    outcome = (rng.random(200) < 1 / (1 + np.exp(-(0.3 + 1.2 * design[:, 1] - 0.8 * design[:, 2])))).astype(float)
    logit = sm.Logit(outcome, design).fit(disp=False)
    mcfadden = [{
        'name': 'logit_200',
        'log_likelihood': float(logit.llf),
        'null_log_likelihood': float(logit.llnull),
        'value': float(logit.prsquared),
    }]

    data = {
        'source': {'sklearn': sklearn.__version__, 'statsmodels': statsmodels.__version__, 'numpy': np.__version__},
        'regression': regression,
        'mase': mase_cases,
        'classification': classification,
        'mcfadden': mcfadden,
    }
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / 'metrics.json'
    path.write_text(json.dumps(data, indent=1), encoding='utf-8', newline='\n')
    print(path, path.stat().st_size, 'байт')
    for case in regression:
        print(' ', case['name'], 'r2', round(case['r2'], 4), 'mape', round(case['mape'], 4), 'r²Пирсона', case.get('pearson_squared'))


if __name__ == '__main__':
    main()
