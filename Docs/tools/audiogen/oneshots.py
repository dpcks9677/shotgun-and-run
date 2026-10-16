"""던전 앰비언트 위에 뿌릴 모노 원샷 SFX 12종 생성. python oneshots.py -> Docs/drafts/audio/oneshots/"""
import sys
from pathlib import Path

import numpy as np
from scipy import signal

import check
from dsp import (SR, adsr, bandpass, exp_decay, gain_db, highpass, lowpass, make_ir, noise,
                 normalize_peak, rng, sine, write_wav)

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "oneshots"
PEAK_DB = -6
FADE = int(0.02 * SR)


def reverb(x, seconds, damp, wet_db, r):
    """모노 짧은 잔향. 드라이 + 웻, 꼬리만큼 길이 연장."""
    wet = signal.fftconvolve(x, make_ir(seconds, damp, r, stereo=False))
    out = np.zeros(len(wet))
    out[:len(x)] += x
    return out + gain_db(wet_db) * wet


def finish(x, peak=PEAK_DB):
    """끝 구간 페이드 + 시작 페이드 + 피크 정규화."""
    x = x.copy()
    x[:FADE] *= np.linspace(0, 1, FADE)
    x[-FADE:] *= np.linspace(1, 0, FADE)
    return normalize_peak(x, peak)


def grains(n, count, lo, hi, r):
    """길이 n의 불규칙 그레인 엔벨로프. 그레인 길이 lo~hi초, 진폭 랜덤."""
    env = np.zeros(n)
    for _ in range(count):
        g = int(r.uniform(lo, hi) * SR)
        p = int(r.uniform(0, n - g))
        env[p:p + g] += r.uniform(0.2, 1) * np.hanning(g)
    return env


def drip(i):
    r = rng(f"oneshot/drip_{i:02d}")
    f0 = [880, 698.5, 1046.5][i - 1] * r.uniform(0.97, 1.03)  # A5 · F5 · C6 근처
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    freq = f0 * 0.5 ** np.minimum(t / 0.06, 1)  # 급하강 후 f0/2 유지
    x = sine(freq, n) + 0.25 * sine(freq * 2.0, n)
    x *= adsr(n, 0.002, 0.0, 1, 0) * exp_decay(n, 0.09)
    x = np.concatenate([x, np.zeros(int(1.5 * SR))])
    x = lowpass(reverb(x, 1.5, 3000, -6, r), 4500)
    return finish(x)


def rustle(i):
    r = rng(f"oneshot/rustle_{i:02d}")
    n = int(r.uniform(0.8, 1.5) * SR)
    env = grains(n, int(r.integers(20, 41)), 0.008, 0.04, r)
    env *= np.hanning(n) ** 0.7  # 처음과 끝은 작게
    x = bandpass(noise(n, r), (2000, 7000)) * env
    return finish(reverb(x, 0.6, 5000, -10, r))


def metal(i):
    r = rng(f"oneshot/metal_{i:02d}")
    f0 = [220, 293.7][i - 1]  # A3 · D4
    n = int(r.uniform(3.0, 4.5) * SR)
    ratios = [1, 2.76, 5.4, 8.93, 13.34, 18.64][:int(r.integers(4, 7))]
    x = np.zeros(n)
    for k, ratio in enumerate(ratios):
        x += sine(f0 * ratio * r.uniform(0.998, 1.002), n) * exp_decay(n, 1.2 / (1 + 0.6 * k)) / (1 + k)
    x *= np.minimum(np.arange(n) / SR / 0.15, 1)  # 부드러운 어택
    x = lowpass(x, 2000, order=4)
    return finish(reverb(x, 1.2, 2500, -8, r))


def stone(i):
    r = rng(f"oneshot/stone_{i:02d}")
    n = int(r.uniform(1.0, 2.0) * SR)
    env = grains(n, int(r.integers(10, 25)), 0.01, 0.06, r) * np.hanning(n) ** 0.5
    x = bandpass(noise(n, r), (300, 1500)) * env
    for _ in range(int(r.integers(3, 7))):  # 낮은 클릭
        m = int(0.03 * SR)
        p = int(r.uniform(0.05, 0.9) * n)
        f = r.choice([73.42, 110, 146.8])  # D2 · A2 · D3
        x[p:p + m] += r.uniform(1, 2.5) * x.std() * sine(f, m) * exp_decay(m, 0.008)
    return finish(reverb(lowpass(x, 2500), 0.6, 2500, -9, r))


def wind(i):
    r = rng(f"oneshot/wind_{i:02d}")
    n = int(r.uniform(3.0, 4.5) * SR)
    centers = np.geomspace(400, 800, 6)  # 중심 주파수 이동: 대역통과 6개를 교차 페이드
    src = noise(n, r)
    pos = np.linspace(0, len(centers) - 1, n)
    x = np.zeros(n)
    for k, c in enumerate(centers):
        x += np.maximum(1 - np.abs(pos - k), 0) * bandpass(src, (c * 0.75, c * 1.3))
    x *= np.sin(np.pi * np.arange(n) / n) ** 2  # 스웰, 양 끝 0
    return finish(reverb(x, 1.0, 2500, -9, r))


def main():
    jobs = [(drip, 3, "drip"), (rustle, 3, "rustle"), (metal, 2, "metal"), (stone, 2, "stone"), (wind, 2, "wind")]
    paths = []
    for fn, count, name in jobs:
        for i in range(1, count + 1):
            path = OUT / f"sfx_{name}_{i:02d}.wav"
            write_wav(path, fn(i))
            paths.append(str(path))
    return check.main(["--oneshot"] + paths)


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
