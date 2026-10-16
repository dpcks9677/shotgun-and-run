"""구역 방어 퀘스트 시작 시 묵직한 쇠창살 문이 떨어져 닫히는 소리. python gate_close.py -> Docs/drafts/audio/gate/gate_close.wav
무거운 쇠사슬 덜컹(0~0.16초) -> 쇠창살이 석재 문턱에 쿵(0.16초, 문 그림 착지와 맞춤) -> 무게에 눌려 한 번 더 쿵(+0.11초)
-> 쇠가 낮게 울며 가라앉음 -> 걸쇠 철컥(0.62초) -> 석실 되울림.
쇠 배음은 레퍼런스 금속 타격 녹음(freesound metal-hit-echo)의 스펙트럼 분석값을 쓰되, 무거운 창살로 들리게
중역 배음을 0.6배로 낮추고(441 -> 265Hz 등) 44 · 55Hz 저역 질량을 길게 남긴다. 고역 "챙"은 최소로 줄인다.
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, normalize_peak, rng, sine, write_wav
from oneshots import reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "gate"
PEAK_DB = -1.2
SECONDS = 2.0
SLAM = 0.16     # 첫 쿵 시각 초
SETTLE = 0.11   # 눌려서 한 번 더 쿵 하기까지 초
LOCK = 0.62     # 걸쇠 철컥 시각 초
PRE_RMS_DB = -6  # 소프트 클립 전 RMS: 올릴수록 두껍고 커짐
WEIGHT = 0.6    # 쇠 배음 주파수 배율: 낮출수록 무거운 쇠로 들림
# (주파수 Hz, 상대 레벨 dB): 레퍼런스 0.27~0.5초 구간 피크 + 오래 남는 저역 질량(44 · 55Hz)
PARTIALS = [(44, 0), (55, -1), (67, 0), (87, -2), (129, -1.5), (169, -4.9), (212, -7.2), (261, -9.1), (441, -9.1), (490, -8.9),
            (741, -13), (810, -13.4), (1342, -14)]


def decay_tau(f):
    """저역은 길게, 고역은 짧게 (레퍼런스: 2kHz 이상은 0.3초에 -15dB)."""
    return 0.9 if f < 100 else 0.55 if f < 300 else 0.3 if f < 800 else 0.12


def place(buf, ev, t0):
    p = int(t0 * SR)
    m = min(len(ev), len(buf) - p)
    buf[p:p + m] += ev[:m]


def strike(r, length, level):
    """무거운 쇠가 석재에 내려앉는 한 번: 저역 몸통 + 둔탁한 타격 + 낮춘 쇠 배음. 날카로운 고역은 최소."""
    m = int(length * SR)
    t = np.arange(m) / SR
    ring = np.zeros(m)
    for f, d in PARTIALS:
        ring += 10 ** (d / 20) * sine(f * (WEIGHT if f >= 300 else 1) * r.uniform(0.99, 1.01), m) * exp_decay(m, decay_tau(f)) * r.uniform(0.8, 1.0)
    thud = bandpass(noise(m, r), (70, 600)) * exp_decay(m, 0.09)
    body = lowpass(noise(m, r), 220) * exp_decay(m, 0.16)
    knock = bandpass(noise(m, r), (500, 2200)) * exp_decay(m, 0.012)
    crack = highpass(noise(m, r), 1800) * exp_decay(m, 0.005)
    clang = bandpass(noise(m, r), (1200, 5000)) * exp_decay(m, 0.035)
    hit = 0.9 * ring + 1.2 * thud + 0.9 * body + 1.0 * knock + 0.8 * crack + 0.9 * clang
    return level * hit * np.minimum(t / 0.0008, 1)


def main():
    r = rng("gate_close/heavy")
    n = int(SECONDS * SR)
    mix = np.zeros(n + int(1.5 * SR))

    # 떨어지기 직전: 무거운 쇠사슬 고리가 덜컹, 낮은 쇠 스치는 소리
    for t0 in np.sort(r.uniform(0.0, SLAM - 0.02, 5)):
        m = int(0.05 * SR)
        clank = bandpass(noise(m, r), (250, 2500)) * exp_decay(m, 0.012)
        clank += 0.5 * sine(r.uniform(180, 320), m) * exp_decay(m, 0.03)
        place(mix, r.uniform(0.15, 0.3) * clank, t0)
    m = int(SLAM * SR)
    place(mix, 0.2 * bandpass(noise(m, r), (80, 900)) * np.linspace(0.2, 1, m), 0.0)

    # 쿵: 서브 저역 + 몸통 + 낮은 쇠 배음, 눌려서 한 번 더, 쇠가 가라앉으며 낮게 울림
    m = int(1.0 * SR)
    t = np.arange(m) / SR
    sub = sine(78 * 0.45 ** np.minimum(t / 0.2, 1), m) * exp_decay(m, 0.28)
    punch = sine(130 * 0.5 ** np.minimum(t / 0.08, 1), m) * exp_decay(m, 0.1)
    place(mix, 1.6 * sub + 1.0 * punch, SLAM)
    place(mix, strike(r, 1.4, 1.0), SLAM)
    place(mix, 0.4 * (1.5 * sine(60 * 0.6 ** np.minimum(t / 0.15, 1), m) * exp_decay(m, 0.2)), SLAM + SETTLE)
    place(mix, strike(r, 1.0, 0.45), SLAM + SETTLE)
    for k, t0 in enumerate([SLAM + 0.28, SLAM + 0.36, SLAM + 0.47]):  # 창살이 낮게 덜그럭, 점점 약하게
        m2 = int(0.09 * SR)
        rat = bandpass(noise(m2, r), (r.uniform(150, 350), r.uniform(900, 1800))) * exp_decay(m2, 0.025)
        place(mix, 0.45 * 0.65 ** k * rat, t0)

    # 걸쇠: 묵직한 쇠 빗장이 걸리는 둔한 철컥
    for dt, g, f in [(0.0, 1.0, 420), (0.07, 0.8, 310)]:
        m3 = int(0.15 * SR)
        click = bandpass(noise(m3, r), (200, 2500)) * exp_decay(m3, 0.015)
        click += 0.8 * sine(f, m3) * exp_decay(m3, 0.05) + 0.4 * sine(f * 1.53, m3) * exp_decay(m3, 0.03)
        place(mix, 0.7 * g * click, LOCK + dt)

    # 되울림: 어두운 석실 반향 + 짧은 잔향
    dry = lowpass(mix, 7000)
    echo = dry.copy()
    for delay, db in [(0.19, -8), (0.37, -14)]:
        p = int(delay * SR)
        echo[p:] += 10 ** (db / 20) * lowpass(dry[:-p], 2500)
    x = reverb(echo, 1.4, 2500, -8, r)[:n]
    x = x * (10 ** (PRE_RMS_DB / 20) / np.sqrt(np.mean(x ** 2)))
    x = np.tanh(x)
    s = np.arange(n) / SR
    x *= np.clip((SECONDS - s) / 0.4, 0, 1)
    x = normalize_peak(x, PEAK_DB)
    path = OUT / "gate_close.wav"
    write_wav(path, x)
    peak = int(np.argmax(np.abs(x)))
    print(f"main hit peak: {peak / SR * 1000:.1f} ms, peak {20 * np.log10(np.max(np.abs(x))):.1f} dBFS, rms {20 * np.log10(np.sqrt(np.mean(x ** 2))):.1f} dBFS")
    return check.main(["--oneshot", str(path)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
