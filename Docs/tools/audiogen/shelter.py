"""쉘터 BGM 루프 -> Docs/drafts/audio/shelter_loop.wav (미스터리 어쿠스틱 포크: 하프 + 플루트 + 뮤직박스 + 현 드론, D 도리안, 72 BPM, 16마디)."""
import sys
from pathlib import Path

import numpy as np

import check
import dsp
from dsp import SR

BPM = 72
BEAT_SAMPLES = SR * 60 // BPM  # 36750
BAR_SAMPLES = BEAT_SAMPLES * 4
BARS = 16
N = BAR_SAMPLES * BARS  # 2,352,000 (53.33초)
TARGET_RMS_DB = -20
OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "shelter_loop.wav"


def hz(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def at(beat):
    return int(round(beat * BEAT_SAMPLES))


def loop_freq(f):
    """루프 길이에 정수 주기가 들어가는 가장 가까운 주파수(dsp.loop_freq의 N 일반화)."""
    return round(f * N / SR) * SR / N


def circular_reverb(x, ir):
    X = np.fft.rfft(dsp.to_stereo(x), n=N, axis=0)
    H = np.fft.rfft(dsp.to_stereo(ir), n=N, axis=0)
    return np.fft.irfft(X * H, n=N, axis=0)


def harp(midi, vel):
    """가산 합성 뜯는 현. 고배음일수록 빨리 감쇠."""
    f = hz(midi)
    n = int(4.5 * SR)
    t = np.arange(n) / SR
    tau0 = float(np.clip(2.4 * (220 / f) ** 0.4, 0.8, 3.5))
    x = np.zeros(n)
    for k in range(1, 11):
        fk = k * f * (1 + 0.00008 * k * k)
        if fk > 9000:
            break
        x += k ** -1.15 * np.sin(2 * np.pi * fk * t) * np.exp(-t * (1 + 0.45 * (k - 1)) / tau0)
    x *= np.minimum(t / 0.002, 1)
    click = dsp.bandpass(dsp.noise(n, dsp.rng(f"title/pluck/{midi}")) * dsp.exp_decay(n, 0.006), (1500, 6000))
    return vel * (x + 0.15 * click)


def flute(midi, beats):
    f = hz(midi)
    n = int((beats * BEAT_SAMPLES) + 0.4 * SR)
    hold = int(beats * BEAT_SAMPLES)
    t = np.arange(n) / SR
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.0 * t) * np.minimum(t / 0.35, 1)
    env = dsp.adsr(n, 0.09, 0.12, 0.85, 0.35)
    env[hold:] = np.minimum(env[hold:], env[hold - 1] * np.linspace(1, 0, n - hold) ** 1.5)
    x = np.zeros(n)
    for k, a in enumerate([1.0, 0.32, 0.1, 0.04], start=1):
        x += a * dsp.sine(f * k * vib, n)
    breath = dsp.bandpass(dsp.noise(n, dsp.rng(f"title/breath/{midi}/{beats}")), (1800, 6500))
    return env * (x + 0.12 * breath)


def music_box(midi, vel):
    f = hz(midi)
    n = int(3.5 * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for ratio, a, tau in [(1.0, 1.0, 1.1), (2.01, 0.3, 0.55), (3.99, 0.14, 0.3)]:
        x += a * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t / tau)
    return vel * x * np.minimum(t / 0.001, 1)


# 4마디 코드 순환: Dm(add9) | G(add9, 도리안 IV) | Am(9) | C(add9). 하프는 이 순환을 16마디 내내 반복한다.
HARP_BARS = [
    [50, 57, 64, 69, 65, 69, 64, 57],
    [43, 50, 57, 59, 62, 59, 57, 50],
    [45, 52, 59, 64, 60, 64, 59, 52],
    [48, 55, 62, 64, 67, 64, 62, 55],
]
HARP_VEL = [1.0, 0.55, 0.8, 0.6, 0.85, 0.55, 0.75, 0.6]

# 플루트 멜로디: (미디 음, 시작 박, 길이 박). 4마디(16박) 프레이즈 4개, 박은 루프 시작 기준.
FLUTE = [
    # 제시: 모티프 A-F-G-B
    (69, 0, 3), (65, 3, 1), (67, 4, 2), (71, 6, 2), (72, 8, 3), (71, 11, 1), (69, 12, 4),
    # 응답: 한 단계 위
    (74, 16, 2), (72, 18, 1), (69, 19, 1), (71, 20, 1.5), (69, 21.5, .5), (67, 22, 2), (76, 24, 3), (74, 27, 1), (72, 28, 2), (67, 30, 2),
    # 고조: 가장 넓은 음역
    (69, 32, 1), (74, 33, 1), (77, 34, 2), (76, 36, 1.5), (74, 37.5, .5), (71, 38, 2), (72, 40, 1), (76, 41, 1), (81, 42, 2), (79, 44, 2), (76, 46, 2),
    # 하강: 비우며 복귀
    (77, 48, 2), (74, 50, 2), (71, 52, 3), (69, 55, 1), (72, 56, 2), (69, 58, 2), (67, 60, 2),
]

# 뮤직박스: 모티프(A-F-G-B)를 한 옥타브 위로 8분음표씩, 각 순환의 마지막 마디 3박부터
MOTIF_BOX = [81, 77, 79, 83]


def build_harp():
    out = np.zeros((N, 2))
    r = dsp.rng("title/harp")
    for bar in range(BARS):
        for i, midi in enumerate(HARP_BARS[bar % 4]):
            pos = at(bar * 4 + i * 0.5) + int(r.uniform(-0.006, 0.006) * SR)
            vel = HARP_VEL[i] * r.uniform(0.9, 1.1)
            p = float(np.clip((midi - 56) / 30 - 0.4, -0.5, 0.5))
            dsp.add_wrapped(out, dsp.pan(harp(midi, vel), p), pos % N, 0.22)
    return out


def build_flute():
    out = np.zeros((N, 2))
    for midi, start, beats in FLUTE:
        dsp.add_wrapped(out, dsp.pan(flute(midi, beats), 0.2), at(start), 0.3)
    return out


def build_box():
    out = np.zeros((N, 2))
    r = dsp.rng("title/box")
    for cycle in range(4):
        for i, midi in enumerate(MOTIF_BOX):
            pos = at(cycle * 16 + 14 + i * 0.5) + int(r.uniform(-0.004, 0.004) * SR)
            dsp.add_wrapped(out, dsp.pan(music_box(midi, 1.0 - 0.1 * i), -0.5 if i % 2 else 0.5), pos, 0.3)
    return out


def build_drone():
    """D2 + A2 + D3 톱니 쌍을 로우패스, 4마디 주기로 숨 쉬듯 진폭 변화. 정수 주기 주파수라 이음새가 맞는다."""
    t = np.arange(N) / SR
    out = np.zeros((N, 2))
    for midi, amp in [(38, 1.0), (45, 0.6), (50, 0.35)]:
        for cents, p in [(-4, -0.6), (4, 0.6)]:
            f = loop_freq(hz(midi) * 2 ** (cents / 1200))
            out += dsp.pan(amp * dsp.saw(f, N), p)
    out = dsp.lowpass(out, 700, order=2, circular=True)
    swell = 0.7 + 0.3 * np.sin(2 * np.pi * t / (4 * BAR_SAMPLES / SR) - np.pi / 2)
    return out * swell[:, None]


def main():
    r = dsp.rng("title/reverb")
    hall = dsp.make_ir(3.4, 5000, r)
    box = build_box()
    box = box + 0.45 * np.roll(box, at(0.75), axis=0) + 0.2 * np.roll(box, at(1.5), axis=0)  # 점 8분 에코
    dry = build_harp() + build_flute() + box + 0.09 * build_drone()
    wet = circular_reverb(dry, hall)
    mix = dry + 0.3 * wet
    mix *= dsp.gain_db(TARGET_RMS_DB) / np.sqrt(np.mean(mix ** 2))
    peak = dsp.db(np.max(np.abs(mix)))
    if peak > -1:
        mix *= dsp.gain_db(-1 - peak)
    dsp.write_wav(OUT, mix)
    ok = check.report("shelter_loop", check.check(mix, None, False))
    print(f"{OUT} {N} samples {N / SR:.2f}s")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
