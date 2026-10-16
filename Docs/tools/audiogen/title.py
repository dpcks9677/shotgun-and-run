"""타이틀 BGM 루프 -> Docs/drafts/audio/title_loop.wav (묵직하고 스산한 금관: 튜바 드론 + 트롬본 코랄 + 호른 콜, D 프리지안, 54 BPM, 16마디)."""
import sys
from pathlib import Path

import numpy as np

import check
import dsp
from dsp import SR

BPM = 54
BEAT_SAMPLES = SR * 60 // BPM  # 49000
BAR_SAMPLES = BEAT_SAMPLES * 4
BARS = 16
N = BAR_SAMPLES * BARS  # 3,136,000 (71.11초)
TARGET_RMS_DB = -20
HORN_RELEASE = 0.4  # 호른 음 끝 여운(초)
REVERB_SECONDS = 3.2
OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "title_loop.wav"


def hz(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def at(beat):
    return int(round(beat * BEAT_SAMPLES))


def circular_reverb(x, ir):
    X = np.fft.rfft(dsp.to_stereo(x), n=N, axis=0)
    H = np.fft.rfft(dsp.to_stereo(ir), n=N, axis=0)
    return np.fft.irfft(X * H, n=N, axis=0)


def brass(midi, env, tilt, cents=0, vib=None, scoop=0):
    """가산 합성 금관. 소리가 클수록(env가 클수록) 고배음이 살아나 밝아진다. env: 0~1 진폭, tilt: 작을수록 밝다.
    vib: 비브라토 변조(길이 n, 1 근처), scoop: 시작에서 아래서 올라오는 음정 보정(cent)."""
    n = len(env)
    f = hz(midi) * 2 ** (cents / 1200) * np.ones(n)
    if vib is not None:
        f = f * vib
    if scoop:
        f = f * 2 ** (-scoop / 1200 * np.exp(-np.arange(n) / SR / 0.07))
    phase = np.cumsum(f) / SR
    x = np.zeros(n)
    for k in range(1, 15):
        if k * f[0] > 9000:
            break
        x += env ** (tilt * (k - 1)) / k * np.sin(2 * np.pi * k * phase)
    return env * x


def hump(n, rise, fall):
    """raised-cosine 오르내림 엔벨로프(샘플 수 rise, fall)."""
    e = np.ones(n)
    e[:rise] = 0.5 * (1 - np.cos(np.pi * np.arange(rise) / rise))
    e[n - fall:] = 0.5 * (1 + np.cos(np.pi * np.arange(fall) / fall))
    return e


# 4마디마다 바뀌는 코드: Dm | B♭ | Gm | E♭ (마지막 E♭ -> 루프 시작 Dm이 프리지안 종지)
# (튜바 근음, 트롬본 3성)
CHORDS = [
    (38, (45, 50, 53)),
    (34, (46, 50, 53)),
    (31, (46, 50, 55)),
    (39, (46, 51, 55)),
]

# 호른 콜: (미디 음, 시작 박, 길이 박). 4마디(16박) 프레이즈 4개. 모티프 A-F-G-B♭를 계승한다.
HORN = [
    # 제시: 모티프 -> D
    (57, 0, 2), (53, 2, 1), (55, 3, 1), (58, 4, 3), (57, 7, 1), (50, 8, 8),
    # 응답: 한 옥타브 가까이 올라감
    (62, 16, 2), (58, 18, 1), (60, 19, 1), (65, 20, 3), (63, 23, 1), (62, 24, 8),
    # 고조: 가장 높고 센 구간
    (67, 32, 2), (63, 34, 1), (65, 35, 1), (70, 36, 3), (69, 39, 1), (67, 40, 2), (65, 42, 2), (63, 44, 4),
    # 하강: 비우며 처음으로 복귀
    (65, 48, 2), (62, 50, 1), (63, 51, 1), (67, 52, 2), (63, 54, 2), (58, 56, 4), (57, 60, 2),
]


def breathing(t, rate_bars=2, depth=0.18):
    return 1 - depth + depth * np.sin(2 * np.pi * t / (rate_bars * BAR_SAMPLES / SR))


def build_tuba():
    """4마디 구간마다 근음 하나. 앞뒤 구간과 한 마디 겹쳐 크로스페이드, 숨 쉬듯 진폭이 변한다."""
    out = np.zeros((N, 2))
    L = 5 * BAR_SAMPLES
    t = np.arange(L) / SR
    for c, (root, _) in enumerate(CHORDS):
        start = (4 * c - 0.5) * BAR_SAMPLES
        env = hump(L, BAR_SAMPLES, BAR_SAMPLES) * breathing(t + start / SR, depth=0.2)
        for cents, p in [(-5, -0.35), (5, 0.35)]:
            dsp.add_wrapped(out, dsp.pan(brass(root, env, tilt=0.55, cents=cents), p), int(start) % N, 0.5)
    return out


def build_trombones():
    out = np.zeros((N, 2))
    L = 5 * BAR_SAMPLES
    t = np.arange(L) / SR
    for c, (_, voices) in enumerate(CHORDS):
        start = (4 * c - 0.5) * BAR_SAMPLES
        env = hump(L, BAR_SAMPLES, BAR_SAMPLES) * breathing(t + start / SR + 1.0, depth=0.25)
        for v, midi in enumerate(voices):
            for cents, p in [(-6, -0.5 + 0.35 * v), (6, -0.2 + 0.35 * v)]:
                dsp.add_wrapped(out, dsp.pan(brass(midi, env, tilt=0.42, cents=cents), p), int(start) % N, 0.22)
    return out


def build_horn():
    out = np.zeros((N, 2))
    for midi, start, beats in HORN:
        hold = int(beats * BEAT_SAMPLES)
        n = hold + int(HORN_RELEASE * SR)
        t = np.arange(n) / SR
        env = dsp.adsr(n, 0.35, 0.3, 0.85, HORN_RELEASE)
        env[:hold] *= np.linspace(1, max(0.45, 1 - 0.07 * beats), hold)  # 긴 음은 점점 줄어든다
        env[hold:] = np.minimum(env[hold:], env[hold - 1] * np.linspace(1, 0, n - hold) ** 1.3)
        vib = 1 + 0.005 * np.sin(2 * np.pi * 4.6 * t) * np.minimum(np.maximum(t - 0.5, 0) / 0.8, 1)
        dsp.add_wrapped(out, dsp.pan(brass(midi, env, tilt=0.3, vib=vib, scoop=40), 0.15), at(start))
    return out


def main():
    hall = dsp.make_ir(REVERB_SECONDS, 3500, dsp.rng("title/brass/reverb"))
    horn = build_horn()
    echo = dsp.lowpass(np.roll(horn, at(2), axis=0), 1400, circular=True)  # 먼 데서 되돌아오는 메아리
    dry = 0.38 * build_tuba() + 0.5 * build_trombones() + 0.4 * horn + 0.1 * echo
    wet = circular_reverb(dry, hall)
    mix = dry + 0.38 * wet
    mix *= dsp.gain_db(TARGET_RMS_DB) / np.sqrt(np.mean(mix ** 2))
    peak = dsp.db(np.max(np.abs(mix)))
    if peak > -1:
        mix *= dsp.gain_db(-1 - peak)
    dsp.write_wav(OUT, mix)
    ok = check.report("title_loop", check.check(mix, None, False))
    print(f"{OUT} {N} samples {N / SR:.2f}s")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
