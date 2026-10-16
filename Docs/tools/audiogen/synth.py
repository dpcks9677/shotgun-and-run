"""전투 다크 신스 리프 + 서브 스템 -> Docs/drafts/audio/synth_loop.wav. python synth.py"""
import sys
import time
from pathlib import Path

import numpy as np

import check
import dsp
from dsp import SR

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "synth_loop.wav"
EIGHTH = int(0.4 * SR)  # 17640

D2, EB2, C2, BB2, A2 = 73.42, 77.78, 65.41, 116.54, 110.0
# 마디별 (리드 근음, 서브 근음)
D_ROOT = (D2, D2 / 2)
RIFF = {}
for b in range(32):
    if b < 12 or 16 <= b < 24:
        RIFF[b] = D_ROOT
    elif b < 16:
        RIFF[b] = (EB2, EB2 / 2)
    else:
        RIFF[b] = {0: (C2, C2), 1: (BB2, BB2 / 2), 2: (A2, A2 / 2), 3: D_ROOT}[(b - 24) % 4]

DRIVE = 2.5
SUB_DB = -4
LEVEL_RMS_DB, LEVEL_PEAK_DB = -23, -3
OPEN_LONG, OPEN_SHORT, PLUCK, PLUCK_RAISED = 1.5, 0.9, 0.5, 0.45  # 8분음표(0.4초)보다 길게 겹쳐 레가토
SEMI = 2 ** (1 / 12)
DETUNE = (-10, -4, 4, 10)  # 센트
SPREAD = (-0.8, -0.3, 0.3, 0.8)
DRIFT_CENTS = 3
BASE_CUTOFF = 220  # 8마디 LFO로 약 150~300Hz
Q_RES, Q_FLAT = 1.8, 0.7
# (게이트 초, 필터 피크 Hz, 필터 감쇠 tau 초, 음량)
ACCENT = (OPEN_LONG, 1800, 0.45, 1.0)
MID = (OPEN_SHORT, 1300, 0.30, 0.9)
PLUCK_P = (PLUCK, 900, 0.20, 0.7)
PLUCK_UP = (PLUCK_RAISED, 900, 0.20, 0.7)

_drift_rng = dsp.rng("synth/drift")


def base_cutoff(pos):
    """루프 위치(샘플)의 컷오프 베이스라인. 8마디 주기 LFO."""
    return BASE_CUTOFF * 2 ** (0.45 * np.sin(2 * np.pi * pos / (8 * dsp.BAR_SAMPLES)))


def note(freq, kind, pos):
    """신스 한 음 (n,2). 톱니 유니즌 -> 공진 필터(엔벨로프 + LFO) -> 소프트 클립. pos는 루프 내 시작 샘플."""
    gate, peak, tau, _ = kind
    n = int((gate + 0.2) * SR)
    n += -n % dsp.BLOCK
    t = np.arange(n) / SR
    y = np.zeros((n, 2))
    for cents, p in zip(DETUNE, SPREAD):
        drift = DRIFT_CENTS * np.sin(2 * np.pi * _drift_rng.uniform(0.3, 1.2) * t + _drift_rng.uniform(0, 2 * np.pi))
        y += dsp.pan(dsp.saw(freq * 2 ** ((cents + drift) / 1200), n), p) / len(DETUNE)
    tb = t[::dsp.BLOCK]
    base = base_cutoff(pos + np.arange(0, n, dsp.BLOCK))
    fc = base + (peak - base) * np.exp(-tb / tau)
    y = dsp.resonant_lowpass(dsp.resonant_lowpass(y, fc, Q_RES), fc, Q_FLAT)
    y = dsp.soft_clip(y, DRIVE)
    return y * dsp.adsr(n, 0.03, 0.25, 0.85, 0.2)[:, None]


def lead_bus(human):
    buf = np.zeros((dsp.LOOP_SAMPLES, 2))
    i = 0
    for b in range(dsp.BARS):
        root = RIFF[b][0] * 2  # 근음 옥타브 위
        for e in range(8):
            if e == 0:
                kind, f = ACCENT, root
            elif e == 4:
                kind, f = MID, root
            elif e == 7 and b % 4 == 3:
                kind, f = PLUCK_UP, root * SEMI
            else:
                kind, f = PLUCK_P, root
            shift, vel = human[i]
            i += 1
            pos = dsp.bar(b) + e * EIGHTH + shift
            dsp.add_wrapped(buf, note(f, kind, pos), pos, kind[3] * dsp.gain_db(vel))
    return buf


def chorus(x, delay=0.014, depth=0.004, rate=0.35, mix=0.5):
    """변조 딜레이 코러스. LFO는 루프에 정수 주기, 좌우 위상이 반대."""
    n = np.arange(dsp.LOOP_SAMPLES)
    lfo = 2 * np.pi * dsp.loop_freq(rate) * n / SR
    out = np.empty_like(x)
    for ch, ph in enumerate((0, np.pi)):
        src = (n - (delay + depth * np.sin(lfo + ph)) * SR) % dsp.LOOP_SAMPLES
        i = np.floor(src).astype(int)
        f = src - i
        out[:, ch] = (1 - f) * x[i, ch] + f * x[(i + 1) % dsp.LOOP_SAMPLES, ch]
    return x + mix * out


def sub_note(freq, n=int(1.65 * SR)):
    return dsp.sine(freq, n) * dsp.adsr(n, 0.04, 0.2, 0.8, 0.12)


def main():
    t0 = time.time()
    r = dsp.rng("synth/human")
    human = [(int(r.uniform(-0.004, 0.004) * SR), r.uniform(-1, 1)) for _ in range(dsp.BARS * 8)]
    lead = dsp.highpass(lead_bus(human), 100)
    lead = chorus(lead)
    ir = dsp.make_ir(1.0, 3000, dsp.rng("synth/ir"))
    lead = 0.85 * lead + 0.15 * dsp.circular_reverb(lead, ir)

    sub = np.zeros((dsp.LOOP_SAMPLES, 2))
    for b in range(dsp.BARS):
        ev = sub_note(RIFF[b][1])
        for k in (0, 2):  # 2박씩 이어지는 서브
            dsp.add_wrapped(sub, ev, dsp.bar(b) + k * dsp.BEAT_SAMPLES)
    rms = lambda x: np.sqrt(np.mean(x ** 2))
    mix = lead + sub * (rms(lead) * dsp.gain_db(SUB_DB) / rms(sub))
    mix *= dsp.gain_db(LEVEL_RMS_DB) / rms(mix)
    if dsp.db(np.max(np.abs(mix))) > LEVEL_PEAK_DB:
        mix = dsp.normalize_peak(mix, LEVEL_PEAK_DB)
    dsp.write_wav(OUT, mix, loop=True)
    print(f"wrote {OUT} ({time.time() - t0:.1f}s)")
    return check.main(["--stem", str(OUT)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
