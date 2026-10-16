"""프롤로그 4~7비트(쉘터에 들어선 뒤)의 BGM 루프 2개 -> Docs/drafts/audio/prologue_wary_loop.wav · prologue_hope_loop.wav
경계: 낮은 현 드론 + 느린 심장박동 + 첼로 몇 음, E♭로 부딪힘(D 프리지안). 희망: 같은 드론 위에서 E♭이 E로 풀리고 쉘터 모티프(A-F-G-B) 조각이 얹힘(D 도리안).
75 BPM, 8마디. python prologue_calm.py"""
import sys
from pathlib import Path

import numpy as np

import check
import drums
import dsp
from dsp import SR, BEAT_SAMPLES
from shelter import hz, music_box

BARS = 8
N = dsp.BAR_SAMPLES * BARS  # 1,128,960 (25.6초)
TARGET_RMS_DB, PEAK_DB = -23, -3  # 대사 아래로 깔리게 다른 BGM(-20)보다 낮춘다
OUT_DIR = Path(__file__).resolve().parents[2] / "drafts" / "audio"
MOTIF_BOX = [81, 77, 79, 83]  # 쉘터 BGM의 뮤직박스 모티프(A-F-G-B)

# 첼로 프레이즈: (미디 음, 시작 박, 길이 박, 세기). 박은 루프 시작 기준 32박
CELLO_WARY = [(50, 0, 5.5, 0.9), (51, 6, 2, 0.7), (50, 8, 6, 0.9), (45, 14.5, 1.5, 0.7), (48, 16, 4, 0.8), (50, 20, 2, 0.7), (51, 22, 2, 0.6), (50, 24, 8, 0.9)]
CELLO_HOPE = [(50, 0, 6, 0.9), (52, 6, 2, 0.7), (57, 8, 3, 0.9), (53, 11, 1, 0.7), (55, 12, 2, 0.8), (59, 14, 2, 0.8),
              (57, 16, 6, 0.9), (55, 22, 2, 0.7), (53, 24, 4, 0.8), (52, 28, 2, 0.7), (50, 30, 2, 0.8)]


def at(beat):
    return int(round(beat * BEAT_SAMPLES))


def loop_freq(f):
    """루프 길이에 정수 주기가 들어가는 가장 가까운 주파수(dsp.loop_freq의 N 일반화)."""
    return round(f * N / SR) * SR / N


def circular_reverb(x, ir):
    X = np.fft.rfft(dsp.to_stereo(x), n=N, axis=0)
    H = np.fft.rfft(dsp.to_stereo(ir), n=N, axis=0)
    return np.fft.irfft(X * H, n=N, axis=0)


def rms(x):
    return np.sqrt(np.mean(x ** 2))


def cello(midi, beats, vel):
    """톱니 2개(±6센트) + 사인 -> 로우패스, 느린 활 어택, 늦게 걸리는 비브라토. 길이 = beats + 0.5초 릴리스."""
    f = hz(midi)
    hold = int(beats * BEAT_SAMPLES)
    n = hold + int(0.5 * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.005 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.4) / 0.6, 0, 1)
    y = dsp.saw(f * vib * 2 ** (-6 / 1200), n) + dsp.saw(f * vib * 2 ** (6 / 1200), n) + 0.8 * dsp.sine(f * vib, n)
    y = dsp.lowpass(y, 1100)
    bow = dsp.bandpass(dsp.noise(n, dsp.rng(f"calm/bow/{midi}/{beats}")), (1500, 4000)) * 0.04
    return vel * (y + bow) * dsp.adsr(n, 0.22, 0.2, 0.85, 0.5)


def build_cello(phrase):
    out = np.zeros((N, 2))
    for midi, start, beats, vel in phrase:
        dsp.add_wrapped(out, dsp.pan(cello(midi, beats, vel), -0.15), at(start))
    return out


def saw_stack(freqs, pans, lp):
    """루프에 이음새 없는 톱니 쌍(±5센트) 합 -> 로우패스. 4마디 주기로 숨 쉬듯 부푼다."""
    t = np.arange(N) / SR
    out = np.zeros((N, 2))
    for f, amp, p in zip(freqs, (1.0, 0.6, 0.35, 0.3), pans):
        for cents, w in ((-5, -0.3), (5, 0.3)):
            out += amp * dsp.pan(dsp.saw(loop_freq(f * 2 ** (cents / 1200)), N), p + w)
    out = dsp.lowpass(out, lp, order=2, circular=True)
    swell = 0.6 + 0.4 * np.sin(2 * np.pi * t / (4 * dsp.BAR) - np.pi / 2)
    return out * swell[:, None]


def build_pulse(every):
    """심장박동: 로우패스한 부드러운 킥 두 번(lub-dub)을 every박마다."""
    r = dsp.rng("calm/pulse")
    out = np.zeros((N, 2))
    for beat in range(0, BARS * 4, every):
        for off, db in ((0, -6), (0.35, -12)):
            hit = dsp.lowpass(drums.kick(r, db), 180)
            dsp.add_wrapped(out, dsp.pan(hit, 0.0), at(beat + off))
    return out


def build_box():
    """쉘터 모티프를 한 옥타브 위 뮤직박스로 8분음표씩, 루프 6마디째(박 24)에 한 번."""
    out = np.zeros((N, 2))
    for i, midi in enumerate(MOTIF_BOX):
        dsp.add_wrapped(out, dsp.pan(music_box(midi, 1.0 - 0.1 * i), -0.5 if i % 2 else 0.5), at(24 + i * 0.5))
    return out + 0.4 * np.roll(out, at(0.75), axis=0)


def render(name, buses):
    """buses: (신호, 첼로 대비 dB, 리버브 여부). 첼로(첫 항목)가 기준."""
    ref = rms(buses[0][0])
    ir = dsp.make_ir(3.0, 4000, dsp.rng("calm/hall"))
    mix = np.zeros((N, 2))
    for sig, db, wet in buses:
        x = sig * (ref * dsp.gain_db(db) / rms(sig))
        mix += x + (0.3 * circular_reverb(x, ir) if wet else 0)
    ceil = dsp.gain_db(PEAK_DB)
    soft = lambda x: ceil * np.tanh(x / ceil)
    g = dsp.gain_db(TARGET_RMS_DB) / rms(mix)
    for _ in range(4):
        g *= dsp.gain_db(TARGET_RMS_DB) / rms(soft(mix * g))
    mix = soft(mix * g)
    path = OUT_DIR / f"{name}_loop.wav"
    dsp.write_wav(path, mix)
    print(f"{path} {N} samples {N / SR:.2f}s")
    return check.report(name, check.check(mix, None, False))


def main():
    drone = saw_stack([hz(38), hz(45), hz(50)], (-0.5, 0.0, 0.5), 500)
    ok = render("prologue_wary", [
        (build_cello(CELLO_WARY), 0, True),
        (drone, -8, True),
        (saw_stack([hz(50), hz(51)], (-0.4, 0.4), 700), -10, True),  # D3 · E♭3 단2도 부딪힘
        (build_pulse(1), -9, False),
    ])
    ok &= render("prologue_hope", [
        (build_cello(CELLO_HOPE), 0, True),
        (drone, -8, True),
        (saw_stack([hz(50), hz(52), hz(57)], (-0.4, 0.0, 0.4), 1200), -10, True),  # D3 · E3 · A3 (sus2)
        (build_pulse(2), -13, False),  # 심장박동이 느려진다
        (build_box(), -6, True),
    ])
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
