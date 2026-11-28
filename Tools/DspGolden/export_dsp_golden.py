"""Выгрузка эталонных значений для тестов AI.DSP (многоскоростная обработка и БИХ-фильтры).

Запускается один раз вручную; тесты читают готовые JSON и Python не требуют.

    py Tools/DspGolden/export_dsp_golden.py --experiments <каталог experiments диссертации>

Источники эталонов:
  * многоскоростная обработка — код экспериментов главы 3 (resampling_compare.py, motion_compensation.py,
    methodology_experiments.py) и потоковая модель tools/srcsynth/resampler_mpy.py;
  * интерполяция Акимы — scipy.interpolate.Akima1DInterpolator;
  * БИХ-фильтры — scipy.signal (butter, cheby1, cheby2, ellip, bessel, *ord, zpk2sos, sosfilt, sosfiltfilt).

Результат: Tests/unit/AIFramework.UnitTests/Dsp/Golden/multirate.json и iir.json.
"""
import argparse
import json
import math
import os
import sys
from fractions import Fraction
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'Tests' / 'unit' / 'AIFramework.UnitTests' / 'Dsp' / 'Golden'


def arr(v):
    return [float(x) for x in np.asarray(v, dtype=np.float64).ravel()]


def clean(v):
    """JSON без Infinity и NaN: бесконечные значения (ОСШ при Q = 1) записываются как null."""
    if isinstance(v, float):
        return v if math.isfinite(v) else None
    if isinstance(v, dict):
        return {k: clean(x) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [clean(x) for x in v]
    return v


def iarr(v):
    return [int(x) for x in np.asarray(v).ravel()]


# ---------------------------------------------------------------------------------------------------------------
# Многоскоростная обработка
# ---------------------------------------------------------------------------------------------------------------
def export_multirate(exp_dir: Path):
    os.environ['MUL_MODE'] = 'floor'          # methodology_experiments не подменяет умножение автора
    sys.path.insert(0, str(exp_dir))
    sys.path.insert(0, str(exp_dir.parent / 'tools' / 'srcsynth'))
    import resampling_compare as rc
    import methodology_experiments as me
    import resampler_mpy as mpy

    g = {}

    # --- Q16: умножение автора (вниз) и с округлением (к ближайшему)
    pairs = [(0.9, 0.9), (-0.9, 0.9), (0.5, -0.25), (-1.75, -2.5), (3.141592, 0.333333), (-0.0001, 0.7),
             (100.5, -0.01), (1.0, 1.0), (-1.0, -1.0), (0.999985, 0.999985)]
    rows = []
    for a, b in pairs:
        ia, ib = int(rc.to_int(a)), int(rc.to_int(b))
        rows.append(dict(a=a, b=b, ia=ia, ib=ib, floor=int(rc.mul(ia, ib)), nearest=int(me.mul_round(ia, ib))))
    g['q16_mul'] = rows

    # --- Кайзер: порядок, бета, окно, ФНЧ
    g['kaiser_beta'] = [dict(a=a, beta=rc.kaiser_beta(a)) for a in (10, 21, 30, 40, 50, 60, 80, 100)]
    g['kaiser_order'] = [dict(a=a, dw=dw, n=rc.kaiser_order(a, dw)) for a, dw in
                         ((80, 0.01), (60, 0.05), (40, 0.1), (80, 4000 / 384000), (80, 2050 / 6585600))]
    g['kaiser_window'] = dict(n=31, beta=5.0, w=arr(np.kaiser(31, 5.0)))
    P, Q, h = rc.std_filter(384000, 48000, rc.BAND, rc.ATTEN)
    g['std_filter_384_48'] = dict(P=P, Q=Q, h=arr(h))
    P, Q, h = rc.std_filter(48000, 44100, rc.BAND, rc.ATTEN)
    g['std_filter_48_441'] = dict(P=P, Q=Q, L=len(h), h_head=arr(h[:64]), h_center=arr(h[len(h) // 2 - 32:len(h) // 2 + 33]))

    # --- Корректирующий фильтр (2.5)-(2.8)
    cf = []
    for (N, K, R, O, band_norm) in ((8, 4, 8, 31, (20000 + 22050) / 2 / 48000), (8, 4, 8, 31, (20000 + 24000) / 2 / 48000),
                                     (2, 4, 2, 31, (20000 + 22050) / 2 / 44100), (9, 4, 9, 31, (20000 + 22050) / 2 / 44100),
                                     (8, 2, 8, 15, 0.3)):
        cf.append(dict(N=N, K=K, R=R, O=O, band=band_norm, h=arr(rc.correction_filter(N, K, R, O, band_norm))))
    g['correction'] = cf

    # --- Матрица Лагранжа для Фэрроу
    g['lagrange'] = {str(p): dict(nodes=me.lagrange_matrix(p)[0], C=[arr(r) for r in me.lagrange_matrix(p)[1]])
                     for p in (1, 2, 3, 5)}
    g['farrow_cost'] = {str(p): list(me.farrow_cost(p)) for p in (1, 3, 5)}

    # --- Фазы тестовых сигналов (генератор NumPy в C# не воспроизводится)
    rng = np.random.default_rng(rc.SEED)
    g['phases_wide'] = arr(np.random.default_rng(rc.SEED).uniform(0, 2 * np.pi, len(rc.TONES)))
    g['phases_narrow'] = arr(np.random.default_rng(rc.SEED).uniform(0, 2 * np.pi, len(rc.TONES_NB)))
    r2 = np.random.default_rng(rc.SEED)
    r2.uniform(0, 2 * np.pi, len(rc.TONES))
    g['phase_alias_extra'] = float(r2.uniform(0, 2 * np.pi))
    g['tones_wide'] = rc.TONES
    g['tones_narrow'] = rc.TONES_NB

    # --- Короткие сигналы: выходы схем побитно (double и Q16 автора)
    short = []
    for Fs, Ft in ((384000, 48000), (384000, 44100), (48000, 44100), (44100, 48000)):
        x, _ = rc.test_signal(Fs, 0.02, rc.TONES, 0.18, np.random.default_rng(rc.SEED))
        P, Q, h = rc.std_filter(Fs, Ft, rc.BAND, rc.ATTEN)
        std_d = rc.polyphase_resample(x, P, Q, h)
        std_q = rc.polyphase_resample(x, P, Q, h, fixed=True) if len(h) < 20000 else None
        syn_d, ym, yo, info = rc.syn_resample(x, Fs, Ft, 4, 31, rc.BAND)
        syn_q, *_ = rc.syn_resample(x, Fs, Ft, 4, 31, rc.BAND, fixed=True)
        short.append(dict(Fs=Fs, Ft=Ft, x=arr(x), std_double=arr(std_d), std_q16=None if std_q is None else arr(std_q),
                          syn_double=arr(syn_d), syn_q16=arr(syn_q), syn_mults=ym, syn_ops=yo, info=info))
    g['short'] = short

    # --- Фэрроу отдельно: 48 -> 44,1 кГц (таблица 3.5) — выход на коротком сигнале
    x, _ = rc.test_signal(48000, 0.01, [3000], 0.9, np.random.default_rng(rc.SEED))
    g['farrow_short'] = dict(x=arr(x), y_double=arr(rc.farrow(x, Fraction(48000, 44100))),
                             y_q16=iarr(rc.farrow(rc.to_int(x), Fraction(48000, 44100), fixed=True)))

    # --- Потоковая целочисленная модель resampler_mpy (округление к ближайшему, накопитель 64 бит)
    rs = np.random.default_rng(7)
    xi = [int(v) for v in rs.integers(-30000, 30000, 300)]
    hq = [int(round(v * 65536)) for v in rc.kaiser_lowpass(31, 0.3, 5.0, gain=3)]
    nodes, C = me.lagrange_matrix(3)
    Cq = [[int(round(float(v) * 65536)) for v in r] for r in C]
    inc = int(round(48000 / 44100 * (1 << 32)))
    g['mpy'] = dict(
        x=xi, h=hq,
        cic_dec_8_4=mpy.CicDecimator(8, 4).process(xi),
        cic_dec_6_3=mpy.CicDecimator(6, 3).process(xi),
        cic_int_8_4=mpy.CicInterpolator(8, 4).process(xi),
        cic_int_9_4=mpy.CicInterpolator(9, 4).process(xi),
        fir_3_7=mpy.PolyphaseFir(3, 7, hq).process(xi),
        fir_1_1=mpy.PolyphaseFir(1, 1, hq).process(xi),
        fir_2_1=mpy.PolyphaseFir(2, 1, hq).process(xi),
        farrow_inc=inc, farrow_C=Cq, farrow_nodes=nodes,
        farrow=mpy.Farrow(nodes, Cq, inc).process(xi),
    )

    # --- Итоговые числа главы 3
    cmp_ = json.loads((exp_dir / 'results' / 'compare.json').read_text(encoding='utf-8'))
    g['compare_main'] = cmp_['main']
    g['compare_main_nb'] = cmp_['main_nb']
    g['compare_alias'] = cmp_['alias']
    g['farrow_vs_f'] = cmp_['farrow_vs_f']
    g['author_polyphase'] = cmp_['author_polyphase']

    # --- Движущийся источник
    import motion_compensation as mc
    mot = json.loads((exp_dir / 'results' / 'motion.json').read_text(encoding='utf-8'))
    mot['phases'] = arr(mc.PH)
    mot['tones'] = mc.TONES
    ints, mus = mc.tau_recursive(2000, 32, 256)
    mot['tau_recursive_32_256_head'] = dict(ints=iarr(ints), mus=iarr(mus))
    g['motion'] = mot

    # --- Акима (scipy)
    from scipy.interpolate import Akima1DInterpolator
    ra = np.random.default_rng(11)
    xs = np.arange(40, dtype=float)
    ys = np.cumsum(ra.normal(size=40))
    ys[20:24] = ys[20]            # плато: проверка случая нулевых весов
    pts = np.linspace(3, 35, 257)
    g['akima'] = dict(y=arr(ys), t=arr(pts), v=arr(Akima1DInterpolator(xs, ys)(pts)))

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'multirate.json').write_text(json.dumps(clean(g), ensure_ascii=False, allow_nan=False), encoding='utf-8')
    print('multirate.json:', (OUT / 'multirate.json').stat().st_size, 'байт')


# ---------------------------------------------------------------------------------------------------------------
# БИХ-фильтры
# ---------------------------------------------------------------------------------------------------------------
def export_iir():
    from scipy import signal as ss

    g = {}
    fs = 600.0
    designs = []

    def add(kind, order, wn, btype, rp=None, rs=None, norm=None):
        kw = dict(btype=btype, output='sos', fs=fs)
        if kind == 'butter':
            sos = ss.butter(order, wn, **kw)
            z, p, k = ss.butter(order, wn, btype=btype, output='zpk', fs=fs)
        elif kind == 'cheby1':
            sos = ss.cheby1(order, rp, wn, **kw)
            z, p, k = ss.cheby1(order, rp, wn, btype=btype, output='zpk', fs=fs)
        elif kind == 'cheby2':
            sos = ss.cheby2(order, rs, wn, **kw)
            z, p, k = ss.cheby2(order, rs, wn, btype=btype, output='zpk', fs=fs)
        elif kind == 'ellip':
            sos = ss.ellip(order, rp, rs, wn, **kw)
            z, p, k = ss.ellip(order, rp, rs, wn, btype=btype, output='zpk', fs=fs)
        else:
            sos = ss.bessel(order, wn, norm=norm, **kw)
            z, p, k = ss.bessel(order, wn, btype=btype, output='zpk', fs=fs, norm=norm)
        designs.append(dict(kind=kind, order=order, wn=wn if np.isscalar(wn) else list(wn), btype=btype, rp=rp, rs=rs,
                            norm=norm, sos=[arr(r) for r in sos],
                            z=[[float(v.real), float(v.imag)] for v in np.atleast_1d(z)],
                            p=[[float(v.real), float(v.imag)] for v in np.atleast_1d(p)], k=float(k)))

    for order in (1, 2, 3, 4, 5, 8, 10, 13, 16, 20):
        add('butter', order, 50.0, 'lowpass')
        add('cheby1', order, 50.0, 'lowpass', rp=1.0)
        add('cheby2', order, 55.0, 'lowpass', rs=40.0)
        add('ellip', order, 50.0, 'lowpass', rp=1.0, rs=40.0)
        add('bessel', order, 50.0, 'lowpass', norm='phase')
    for order in (2, 5, 12):
        add('bessel', order, 50.0, 'lowpass', norm='mag')
        add('bessel', order, 50.0, 'lowpass', norm='delay')
    for kind, extra in (('butter', {}), ('cheby1', dict(rp=0.5)), ('cheby2', dict(rs=60.0)),
                        ('ellip', dict(rp=0.5, rs=60.0)), ('bessel', dict(norm='phase'))):
        for order in (3, 6, 11):
            add(kind, order, 120.0, 'highpass', **extra)
            add(kind, order, [80.0, 160.0], 'bandpass', **extra)
            add(kind, order, [80.0, 160.0], 'bandstop', **extra)
    g['designs'] = designs

    # --- Порядок по ТЗ
    orders = []
    specs = [('lowpass', 45.0, 55.0, 1.0, 40.0), ('lowpass', 100.0, 110.0, 0.5, 80.0), ('highpass', 120.0, 100.0, 1.0, 50.0),
             ('bandpass', [80.0, 160.0], [60.0, 190.0], 1.0, 45.0), ('bandstop', [60.0, 190.0], [80.0, 160.0], 0.5, 40.0),
             ('lowpass', 30.0, 31.0, 0.1, 100.0), ('bandpass', [100.0, 110.0], [95.0, 120.0], 0.2, 70.0)]
    for btype, wp, ws, gp, gs in specs:
        row = dict(btype=btype, wp=wp, ws=ws, gpass=gp, gstop=gs)
        for name, f in (('butter', ss.buttord), ('cheby1', ss.cheb1ord), ('cheby2', ss.cheb2ord), ('ellip', ss.ellipord)):
            n, wn = f(wp, ws, gp, gs, fs=fs)
            row[name] = dict(n=int(n), wn=arr(np.atleast_1d(wn)))
        orders.append(row)
    g['orders'] = orders

    # --- Фильтрация
    rs_ = np.random.default_rng(5)
    x = rs_.normal(size=400)
    sos = ss.ellip(6, 1.0, 40.0, 50.0, output='sos', fs=fs)
    g['filtering'] = dict(x=arr(x), sos=[arr(r) for r in sos], sosfilt=arr(ss.sosfilt(sos, x)),
                          sosfiltfilt=arr(ss.sosfiltfilt(sos, x)))

    # --- Частотные характеристики
    w, hresp = ss.sosfreqz(sos, worN=64, fs=fs)
    _, gd = ss.group_delay(ss.sos2tf(sos), w=w, fs=fs)
    g['response'] = dict(f=arr(w), re=arr(hresp.real), im=arr(hresp.imag), gd=arr(gd))

    # --- zpk2sos отдельно (пары нулей и полюсов)
    z, p, k = ss.ellip(7, 0.5, 50.0, [70.0, 150.0], btype='bandpass', output='zpk', fs=fs)
    g['zpk2sos'] = dict(z=[[float(v.real), float(v.imag)] for v in z], p=[[float(v.real), float(v.imag)] for v in p], k=float(k),
                        sos=[arr(r) for r in ss.zpk2sos(z, p, k)])

    # --- Эллиптические функции (scipy.special)
    from scipy import special as sp
    g['ellipk'] = [dict(m=m, k=float(sp.ellipk(m))) for m in (0.0, 0.1, 0.5, 0.9, 0.99, 0.999999)]
    g['ellipj'] = [dict(u=u, m=m, sn=float(v[0]), cn=float(v[1]), dn=float(v[2]))
                   for u, m in ((0.3, 0.2), (1.2, 0.7), (2.0, 0.99), (0.7, 0.0))
                   for v in [sp.ellipj(u, m)]]

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'iir.json').write_text(json.dumps(clean(g), ensure_ascii=False, allow_nan=False), encoding='utf-8')
    print('iir.json:', (OUT / 'iir.json').stat().st_size, 'байт')


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--experiments', default=str(Path.home() / 'Desktop' / 'Дисертация' / 'experiments'))
    ap.add_argument('--only', choices=['multirate', 'iir'])
    a = ap.parse_args()
    if a.only != 'iir':
        export_multirate(Path(a.experiments))
    if a.only != 'multirate':
        export_iir()
