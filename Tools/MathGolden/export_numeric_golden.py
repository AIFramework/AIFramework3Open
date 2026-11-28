"""Выгрузка эталонов численных методов (scipy).

Запускается один раз вручную; тесты читают готовый JSON и Python не требуют.

    py Tools/MathGolden/export_numeric_golden.py

Источники эталонов:
  * корни скалярных уравнений — scipy.optimize.brentq с предельной точностью;
  * решения ОДУ — scipy.integrate.solve_ivp (DOP853, rtol = atol = 1e-13);
  * матричная экспонента — scipy.linalg.expm;
  * разложение Холецкого, решение системы, логарифм определителя — scipy.linalg / numpy.linalg;
  * трёхдиагональная система — scipy.linalg.solve_banded;
  * нелинейные наименьшие квадраты — scipy.optimize.least_squares(method='lm').

Результат: Tests/unit/AIFramework.UnitTests/Golden/numeric.json.
"""
import json
import math
from pathlib import Path

import numpy as np
import scipy
from scipy import integrate, linalg, optimize

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AIFramework.UnitTests' / 'Golden'

EPS = np.finfo(float).eps


def roots():
    cases = {
        # имя: (функция, левый конец, правый конец)
        'cubic': (lambda x: x ** 3 - 2 * x - 5, 2.0, 3.0),
        'cos_minus_x': (lambda x: math.cos(x) - x, 0.0, 1.0),
        'tiny_scale': (lambda x: 1e-12 * (x - 0.3), 0.0, 1.0),
        'exp_minus_million': (lambda x: math.exp(x) - 1e6, 0.0, 20.0),
        'flat_near_root': (lambda x: (x - 1.5) ** 3, 0.0, 4.0),
        'root_far_from_one': (lambda x: x - 1e9 / 3, 0.0, 1e9),
    }
    out = []
    for name, (f, a, b) in cases.items():
        root = optimize.brentq(f, a, b, xtol=1e-300, rtol=4 * EPS, maxiter=500)
        out.append({'name': name, 'a': a, 'b': b, 'root': root})
    return out


def odes():
    cases = {
        'growth': (lambda x, y: y, 0.0, [1.0], 1.0),
        'gaussian': (lambda x, y: -2 * x * y, 0.0, [1.0], 2.0),
        'forced': (lambda x, y: np.sin(x) - y, 0.0, [0.5], 3.0),
        'oscillator': (lambda x, y: [y[1], -y[0]], 0.0, [1.0, 0.0], 5.0),
    }
    out = []
    for name, (f, x0, y0, x1) in cases.items():
        solution = integrate.solve_ivp(f, (x0, x1), y0, method='DOP853', rtol=1e-13, atol=1e-13)
        out.append({'name': name, 'x0': x0, 'y0': y0, 'x1': x1, 'y1': solution.y[:, -1].tolist()})
    return out


def exponentials():
    rng = np.random.default_rng(20261129)
    matrices = {
        'zero': np.zeros((3, 3)),
        'rotation_generator': np.array([[0.0, -1.0], [1.0, 0.0]]),
        'nilpotent': np.array([[0.0, 1.0, 2.0], [0.0, 0.0, 3.0], [0.0, 0.0, 0.0]]),
        'stable_system': np.array([[-0.5, 1.0, 0.0], [0.0, -0.3, 2.0], [0.1, 0.0, -1.2]]),
        'random_4x4': rng.normal(size=(4, 4)),
        'large_norm': np.array([[-12.0, 30.0], [-8.0, 9.0]]),
        'stiff_diagonal': np.diag([-50.0, -1.0, 0.5]),
    }
    return [{'name': name, 'matrix': a.tolist(), 'exp': linalg.expm(a).tolist()} for name, a in matrices.items()]


def cholesky():
    rng = np.random.default_rng(7)
    b = rng.normal(size=(5, 5))
    spd = b @ b.T + 0.5 * np.eye(5)
    rhs = rng.normal(size=5)
    lower = linalg.cholesky(spd, lower=True)
    sign, logdet = np.linalg.slogdet(spd)

    # Вырожденная ковариация: два одинаковых ряда. Обычное разложение обязано отказать
    series = rng.normal(size=20)
    singular = np.cov(np.vstack([series, series, rng.normal(size=20)]))

    return {
        'matrix': spd.tolist(),
        'rhs': rhs.tolist(),
        'lower': lower.tolist(),
        'solution': linalg.solve(spd, rhs, assume_a='pos').tolist(),
        'inverse': linalg.inv(spd).tolist(),
        'logdet': float(logdet),
        'singular': singular.tolist(),
        'indefinite': [[1.0, 2.0], [2.0, 1.0]],
    }


def tridiagonal():
    rng = np.random.default_rng(11)
    n = 9
    lower = rng.uniform(-1, 1, size=n)
    upper = rng.uniform(-1, 1, size=n)
    diagonal = 3 + rng.uniform(0, 1, size=n)
    rhs = rng.normal(size=n)

    banded = np.zeros((3, n))
    banded[0, 1:] = upper[:-1]
    banded[1, :] = diagonal
    banded[2, :-1] = lower[1:]

    return {
        'lower': lower.tolist(),
        'diagonal': diagonal.tolist(),
        'upper': upper.tolist(),
        'rhs': rhs.tolist(),
        'solution': linalg.solve_banded((1, 1), banded, rhs).tolist(),
    }


def least_squares():
    # Затухающая экспонента с шумом: y = a·exp(−k·t) + c
    rng = np.random.default_rng(3)
    t = np.linspace(0, 5, 25)
    y = 2.5 * np.exp(-1.3 * t) + 0.4 + rng.normal(scale=0.02, size=t.size)

    def residuals(p):
        return p[0] * np.exp(-p[1] * t) + p[2] - y

    fit = optimize.least_squares(residuals, [1.0, 1.0, 0.0], method='lm', xtol=1e-15, ftol=1e-15, gtol=1e-15)

    return {
        't': t.tolist(),
        'y': y.tolist(),
        'start': [1.0, 1.0, 0.0],
        'parameters': fit.x.tolist(),
        'cost': float(2 * fit.cost),
    }


def spline():
    # Естественный кубический сплайн: вторые производные на концах равны нулю
    from scipy.interpolate import CubicSpline

    x = np.array([0.0, 0.7, 1.5, 2.1, 3.4, 4.0, 5.2])
    y = np.sin(x) + 0.1 * x
    probes = np.array([0.0, 0.35, 1.0, 1.5, 2.9, 3.99, 5.2])

    return {'x': x.tolist(), 'y': y.tolist(), 'probes': probes.tolist(),
            'values': CubicSpline(x, y, bc_type='natural')(probes).tolist()}


def main():
    data = {
        'meta': {'scipy': scipy.__version__, 'numpy': np.__version__},
        'roots': roots(),
        'odes': odes(),
        'expm': exponentials(),
        'cholesky': cholesky(),
        'tridiagonal': tridiagonal(),
        'least_squares': least_squares(),
        'spline': spline(),
    }
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'numeric.json').write_text(json.dumps(data, indent=1), encoding='utf-8')
    print('ok', OUT / 'numeric.json')


if __name__ == '__main__':
    main()
