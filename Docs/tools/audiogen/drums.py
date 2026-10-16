"""전투 드럼 스템: 하프타임 킥 · 스네어 · 플로어탐 심장박동 + 8마디 탐 필인. 심벌 · 하이햇 없음."""
import numpy as np

import check
import dsp
from dsp import SR, LOOP_SAMPLES, bar, gain_db

OUT = "../../drafts/audio/drums_loop.wav"
STEP = SR // 5  # 16분음표 = 0.2초 = 8820샘플
FILL_BARS = (7, 15, 23, 31)
TOM_HZ = (190, 150, 115, 85)  # 하이 · 미드 · 로우 · 플로어
TOM_DB = (-8, -6, -4, -2)


def vel01(vel_db, lo=-14, hi=0):
    """벨로시티 dB -> 0(약)~1(강)."""
    return np.clip((vel_db - lo) / (hi - lo), 0, 1)


def click(r, fc, tau):
    """스틱 어택 클릭: fc 이상 고역 노이즈의 짧은 감쇠."""
    n = int(8 * tau * SR)
    return dsp.highpass(dsp.noise(n, r), fc) * dsp.exp_decay(n, tau)


def kick(r, vel_db=0.0):
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    v = vel01(vel_db)
    f0 = 120 * r.uniform(0.95, 1.05)
    body = dsp.sine(45 + (f0 - 45) * np.exp(-t / 0.04), n) * dsp.exp_decay(n, 0.35 / 4.6 * r.uniform(0.92, 1.08))
    c = click(r, 2000, 0.004)
    body[:len(c)] += 0.25 * r.uniform(0.7, 1.3) * (0.7 + 0.3 * v) * c
    return body


def snare(r, vel_db=0.0):
    n = int(0.5 * SR)
    v = vel01(vel_db)
    lo, hi = 1000 * r.uniform(0.85, 1.15), 6000 * r.uniform(0.85, 1.15)
    hiss = dsp.bandpass(dsp.noise(n, r), (lo, hi)) * dsp.exp_decay(n, 0.25 / 4.6 * r.uniform(0.85, 1.15))
    body = dsp.sine(190 * r.uniform(0.97, 1.03), n) * dsp.exp_decay(n, 0.12 / 4.6)
    return 1.2 * r.uniform(0.85, 1.15) * (0.8 + 0.2 * v) * hiss + 0.6 * r.uniform(0.85, 1.15) * body


def tom_pan(hz):
    """하이탐 왼쪽 -> 플로어탐 오른쪽 경향."""
    return np.interp(hz, (TOM_HZ[-1], TOM_HZ[0]), (0.25, -0.25))


def tom(hz, r, vel_db=0.0):
    """hz가 클수록 작은 탐: 감쇠 짧고 배음 비율 높고 피치 벤드 얕다. 센 타격일수록 밝고(클릭 · 벤드 ↑) 감쇠 약간 짧다."""
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    s = (hz - TOM_HZ[-1]) / (TOM_HZ[0] - TOM_HZ[-1])  # 0 = 플로어탐, 1 = 하이탐
    v = vel01(vel_db, -14, -2)
    f = hz * r.uniform(0.975, 1.025)
    decay = (0.4 - 0.18 * s) * (1.1 - 0.2 * v) * r.uniform(0.9, 1.1)
    bend = (0.5 - 0.2 * s) * (0.6 + 0.8 * v)
    fall = f * (1 + bend * np.exp(-t / 0.05))
    env = dsp.exp_decay(n, decay / 4.6)
    over = (0.1 + 0.1 * s) * dsp.sine(fall * (1.5 + 0.1 * s), n) * env ** 2  # 몸통 배음은 기본음보다 빨리 죽는다
    body = (dsp.sine(fall, n) * env + over) * r.uniform(0.9, 1.1)
    c = click(r, 2500 * r.uniform(0.8, 1.25) * (0.7 + 0.6 * v), 0.004)
    body[:len(c)] += 0.12 * r.uniform(0.7, 1.3) * (0.3 + 1.4 * v) * c
    return body


def main():
    r = dsp.rng("drums/human")
    v = dsp.rng("drums/vary")
    buf = np.zeros((LOOP_SAMPLES, 2))

    def hit(synth, b, step, vel_db=0.0, p=0.0):
        pos = bar(b) + step * STEP + round(r.uniform(-0.005, 0.005) * SR)
        g = vel_db + r.uniform(-1.5, 1.5) + (-3 if b < 4 else 0)
        dsp.add_wrapped(buf, dsp.pan(synth(g), p), pos, gain_db(g))

    k = lambda g: kick(v, g)
    s = lambda g: snare(v, g)
    toms = [lambda g, hz=hz: tom(hz, v, g) for hz in TOM_HZ]

    for b in range(32):
        fill = b in FILL_BARS
        hit(k, b, 0)
        hit(k, b, 10, -2)
        hit(s, b, 8)
        if fill:
            for i, ev in enumerate(toms):
                hit(ev, b, 12 + i, TOM_DB[i], tom_pan(TOM_HZ[i]) + r.uniform(-0.1, 0.1))
        else:
            hit(toms[3], b, 12, -10, tom_pan(TOM_HZ[3]) + r.uniform(-0.1, 0.1))
            hit(toms[3], b, 14, -13, tom_pan(TOM_HZ[3]) + r.uniform(-0.1, 0.1))
        if b >= 24 and not fill:
            hit(k, b, 14, -4)
            hit(s, b, 15, -12)

    ir = dsp.make_ir(1.2, 4000, dsp.rng("drums/ir"))
    x = buf + 0.25 * dsp.circular_reverb(buf, ir)
    x = dsp.soft_clip(dsp.normalize_peak(x, -6), 2.0)
    x *= gain_db(-23) / np.sqrt(np.mean(x ** 2))
    dsp.write_wav(OUT, x, loop=True)
    return x


if __name__ == "__main__":
    import sys
    main()
    sys.exit(0 if check.main(["--stem", OUT]) else 1)
