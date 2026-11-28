"""Выгрузка эталонных значений спецфункций и хвостов распределений (mpmath, 50 знаков).

Запускается один раз вручную; тесты читают готовый JSON и Python не требуют.

    py Tools/MathGolden/export_special_golden.py

Источник эталонов — mpmath: loggamma, gamma, gammainc (регуляризованная), betainc (регуляризованная),
erf, erfc, factorial; квантили и хвосты распределений Стьюдента, хи-квадрат и Фишера получены из тех же
функций обращением (findroot) при 50 знаках.

Результат: Tests/unit/AIFramework.UnitTests/Golden/special_functions.json.
"""
import json
from pathlib import Path

import mpmath as mp

mp.mp.dps = 50

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AIFramework.UnitTests' / 'Golden'


def f(value):
    """Число в JSON: ближайший double (repr сохраняет его без потерь)."""
    return float(value)


def log_gamma():
    points = ['1e-300', '1e-8', '1e-3', '0.1', '0.3', '0.49', '0.5', '0.75', '0.999', '0.9999999', '1', '1.0000001',
              '1.001', '1.2', '1.4616321449683623', '1.5', '1.9', '1.999999', '2', '2.000001', '2.1', '2.5', '3', '7.5',
              '10', '25.5', '100', '171.5', '1000', '1e5', '1e10', '1e15', '1e300']
    return [{'x': f(mp.mpf(p)), 'value': f(mp.loggamma(mp.mpf(f(mp.mpf(p)))))} for p in points]


def gamma():
    points = ['-2.5', '-0.5', '1e-10', '0.1', '0.5', '1', '1.5', '2', '5', '6', '10.5', '20', '50.25', '100', '170', '171.5']
    return [{'x': f(mp.mpf(p)), 'value': f(mp.gamma(mp.mpf(f(mp.mpf(p)))))} for p in points]


def incomplete_gamma():
    cases = []
    for a in ['0.5', '1', '2.5', '10', '50', '200.5', '1000', '100000']:
        av = mp.mpf(a)
        for ratio in ['1e-6', '0.01', '0.3', '0.9', '0.99', '1', '1.01', '1.1', '2', '5']:
            x = av * mp.mpf(ratio)
            xd = mp.mpf(f(x))
            try:
                p = mp.gammainc(av, 0, xd, regularized=True)
                q = mp.gammainc(av, xd, mp.inf, regularized=True)
            except mp.libmp.libhyper.NoConvergence:
                # a = 1e5, x >= 2a: ряд mpmath не сходится, а сам хвост меньше 1e-13000 — в double это ровно 0 и 1
                assert av >= 1e5 and xd >= 2 * av
                p, q = mp.mpf(1), mp.mpf(0)
            cases.append({'a': f(av), 'x': f(xd), 'p': f(p), 'q': f(q)})
    for a, x in [('0.5', '1e-30'), ('0.5', '50'), ('0.5', '300'), ('3', '1e-10'), ('1e-3', '1e-3'), ('1e-3', '5'), ('30', '100')]:
        av, xd = mp.mpf(a), mp.mpf(f(mp.mpf(x)))
        cases.append({'a': f(av), 'x': f(xd),
                      'p': f(mp.gammainc(av, 0, xd, regularized=True)),
                      'q': f(mp.gammainc(av, xd, mp.inf, regularized=True))})
    return cases


def incomplete_beta():
    cases = []
    shapes = [('0.5', '0.5'), ('1', '1'), ('2', '3'), ('0.5', '20'), ('20', '0.5'), ('5.5', '0.5'), ('50', '80'),
              ('1000', '1500'), ('1e-2', '3'), ('2.3', '1e4')]
    for a, b in shapes:
        av, bv = mp.mpf(a), mp.mpf(b)
        for x in ['1e-12', '1e-4', '0.05', '0.3', '0.5', '0.7', '0.95', '0.9999']:
            xd = mp.mpf(f(mp.mpf(x)))
            cases.append({'a': f(av), 'b': f(bv), 'x': f(xd), 'value': f(mp.betainc(av, bv, 0, xd, regularized=True)),
                          # Дополнение — через симметрию I_x(a, b) = 1 − I_{1−x}(b, a): интеграл по [x, 1]
                          # mpmath берёт разностью и теряет хвосты меньше 1e-50
                          'complement': f(mp.betainc(bv, av, 0, 1 - xd, regularized=True))})
    return cases


def error_function():
    points = ['0', '1e-300', '1e-200', '1e-154', '1e-20', '1e-8', '1e-3', '0.1', '0.5', '0.9', '1', '1.2', '1.3', '2', '3',
              '5', '6', '10', '26', '27', '-0.5', '-3', '-10']
    return [{'x': f(mp.mpf(p)), 'erf': f(mp.erf(mp.mpf(f(mp.mpf(p))))), 'erfc': f(mp.erfc(mp.mpf(f(mp.mpf(p)))))}
            for p in points]


def factorial():
    return [{'n': n, 'value': f(mp.factorial(n))} for n in [0, 1, 2, 5, 10, 13, 14, 20, 21, 22, 23, 25, 50, 100, 150, 170]]


def t_sf(t, df):
    """Правый хвост Стьюдента P(T > t), t >= 0."""
    return mp.betainc(df / 2, mp.mpf(1) / 2, 0, df / (df + t * t), regularized=True) / 2


def chi2_sf(x, df):
    return mp.gammainc(df / 2, x / 2, mp.inf, regularized=True)


def f_sf(x, d1, d2):
    return mp.betainc(d2 / 2, d1 / 2, 0, d2 / (d2 + d1 * x), regularized=True)


def tails():
    out = {'normal_sf': [], 't_sf': [], 'chi2_sf': [], 'f_sf': []}
    for x in ['0', '0.5', '1.96', '5', '8.3', '10', '20', '37']:
        xd = mp.mpf(f(mp.mpf(x)))
        out['normal_sf'].append({'x': f(xd), 'value': f(mp.erfc(xd / mp.sqrt(2)) / 2)})
    for df in ['1', '2', '4.6', '10', '30', '1000']:
        for t in ['0', '0.5', '2', '10', '50', '1e3', '1e6']:
            dv, tv = mp.mpf(df), mp.mpf(f(mp.mpf(t)))
            out['t_sf'].append({'t': f(tv), 'df': f(dv), 'value': f(t_sf(tv, dv))})
    for df in ['1', '2', '5', '30', '200']:
        for ratio in ['1e-6', '0.1', '1', '3', '10', '40']:
            dv = mp.mpf(df)
            xv = mp.mpf(f(dv * mp.mpf(ratio)))
            out['chi2_sf'].append({'x': f(xv), 'df': f(dv), 'value': f(chi2_sf(xv, dv)),
                                   'cdf': f(mp.gammainc(dv / 2, 0, xv / 2, regularized=True))})
    for d1, d2 in [('1', '10'), ('3', '40'), ('5', '200'), ('20', '20')]:
        for x in ['0.01', '1', '4', '30', '300']:
            a, b, xv = mp.mpf(d1), mp.mpf(d2), mp.mpf(f(mp.mpf(x)))
            out['f_sf'].append({'x': f(xv), 'df1': f(a), 'df2': f(b), 'value': f(f_sf(xv, a, b))})
    return out


def solve(function, low, high):
    """Корень монотонной функции на отрезке делением пополам: 220 шагов дают 50 знаков на любой скобке."""
    f_low = function(low)
    assert f_low * function(high) < 0, 'корень не в скобке'
    for _ in range(220):
        middle = (low + high) / 2
        if function(middle) * f_low > 0:
            low = middle
        else:
            high = middle
    return (low + high) / 2


def quantiles():
    out = {'t': [], 'chi2': []}
    for df in ['1', '2', '3', '4.6', '10', '30', '500']:
        dv = mp.mpf(df)
        for p in ['1e-300', '1e-12', '1e-10', '1e-4', '0.025', '0.3', '0.5', '0.9', '0.975', '0.999999']:
            pd = mp.mpf(f(mp.mpf(p)))
            if pd == mp.mpf('0.5'):
                value = mp.mpf(0)
            else:
                tail = pd if pd < mp.mpf('0.5') else 1 - pd
                # Обращение хвоста в логарифмической шкале: при p = 1e-300 квантиль доходит до 1e300
                root = solve(lambda s: mp.log(t_sf(mp.exp(s), dv)) - mp.log(tail), mp.log(mp.mpf('1e-30')), mp.log(mp.mpf('1e305')))
                value = -mp.exp(root) if pd < mp.mpf('0.5') else mp.exp(root)
            out['t'].append({'p': f(pd), 'df': f(dv), 'value': f(value)})
    for df in ['1', '2', '5', '30', '200']:
        dv = mp.mpf(df)
        for p in ['1e-300', '1e-10', '1e-4', '0.05', '0.5', '0.95', '0.9999', '0.999999999']:
            pd = mp.mpf(f(mp.mpf(p)))
            low, high = mp.log(mp.mpf('1e-700')), mp.log(mp.mpf('1e5'))
            if pd < mp.mpf('0.5'):
                root = solve(lambda s: mp.log(mp.gammainc(dv / 2, 0, mp.exp(s) / 2, regularized=True)) - mp.log(pd), low, high)
            else:
                root = solve(lambda s: mp.log(chi2_sf(mp.exp(s), dv)) - mp.log(1 - pd), low, high)
            out['chi2'].append({'p': f(pd), 'df': f(dv), 'value': f(mp.exp(root))})
    return out


def main():
    data = {
        'source': {'mpmath': mp.__version__, 'digits': mp.mp.dps},
        'log_gamma': log_gamma(),
        'gamma': gamma(),
        'incomplete_gamma': incomplete_gamma(),
        'incomplete_beta': incomplete_beta(),
        'erf': error_function(),
        'factorial': factorial(),
        'tails': tails(),
        'quantiles': quantiles(),
    }
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / 'special_functions.json'
    path.write_text(json.dumps(data, indent=1), encoding='utf-8', newline='\n')
    print(path, path.stat().st_size, 'байт')
    for key, value in data.items():
        if isinstance(value, list):
            print(' ', key, len(value))


if __name__ == '__main__':
    main()
