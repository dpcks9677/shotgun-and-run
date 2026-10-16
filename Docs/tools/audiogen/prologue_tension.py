"""프롤로그 쉘터 진입 전 긴장 BGM 루프 -> Docs/drafts/audio/prologue_tension_loop.wav
(D 프리지안, 75 BPM, 8마디. 드럼은 킥 · 스네어가 16분음표로 엇갈리는 더블타임의 더블 = 300 BPM 느낌). python prologue_tension.py"""
import sys
from pathlib import Path

import numpy as np

import check
import drums
import dsp
from dsp import SR
from raid import hihat

BARS = 8
N = dsp.BAR_SAMPLES * BARS  # 1,128,960 (25.6초)
STEP = drums.STEP  # 16분음표 8820샘플
TARGET_RMS_DB, PEAK_DB = -20, -3
OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "prologue_tension_loop.wav"

D2, EB2 = 73.42, 77.78
ROOTS = [D2, D2, D2, EB2]  # 4마디 순환: 마지막 마디에서 단2도로 긴장을 올린다
CLUSTER = (146.83, 155.56, 220.0)  # D3 · E♭3 · A3 (단2도 부딪힘)
LFO_HZ = 2.5  # 8분음표 박동. 루프에 정수 주기(64)가 들어간다


def at(bar, step):
    """step은 16분음표 단위(0.5 = 32분음표)."""
    return bar * dsp.BAR_SAMPLES + round(step * STEP)


def loop_freq(f):
    """루프 길이에 정수 주기가 들어가는 가장 가까운 주파수(dsp.loop_freq의 N 일반화)."""
    return round(f * N / SR) * SR / N


def circular_reverb(x, ir):
    X = np.fft.rfft(dsp.to_stereo(x), n=N, axis=0)
    H = np.fft.rfft(dsp.to_stereo(ir), n=N, axis=0)
    return np.fft.irfft(X * H, n=N, axis=0)


def bass_pulse(f, accent):
    """8분 펄스 베이스 한 발: 톱니 + 서브 사인 -> 가벼운 클립 -> 로우패스. 첫 박은 악센트."""
    n = int(0.3 * SR)
    y = dsp.saw(f, n) + 0.8 * dsp.sine(f / 2, n)
    y = dsp.lowpass(dsp.soft_clip(y / 1.8, 2.5), 420 if accent else 320)
    return y * dsp.exp_decay(n, 0.07) * np.minimum(np.arange(n) / SR / 0.004, 1) * (1.0 if accent else 0.7)


def build_bass():
    out = np.zeros((N, 2))
    for b in range(BARS):
        for step in range(0, 16, 2):
            dsp.add_wrapped(out, bass_pulse(ROOTS[b % 4], step == 0), at(b, step))
    return out


def build_pad():
    """D · E♭ · A 톱니 클러스터를 로우패스하고 8분음표 박동으로 떨게 한다. 4마디 주기로 숨 쉬듯 부푼다."""
    t = np.arange(N) / SR
    out = np.zeros((N, 2))
    for f, p in zip(CLUSTER, (-0.4, 0.0, 0.4)):
        for cents, w in ((-5, -0.3), (5, 0.3)):
            out += dsp.pan(dsp.saw(loop_freq(f * 2 ** (cents / 1200)), N), p + w)
    out = dsp.lowpass(out, 800, order=2, circular=True)
    swell = 0.55 + 0.45 * np.sin(2 * np.pi * t / (4 * dsp.BAR) - np.pi / 2)
    tremolo = 0.65 + 0.35 * np.sin(2 * np.pi * LFO_HZ * t)
    return out * (swell * tremolo)[:, None]


def build_drums():
    """킥 · 스네어가 16분음표로 엇갈리는 더블타임의 더블(300 BPM 느낌) + 32분 하이햇. 필인 없이 처음부터 끝까지 같은 밀도."""
    r = dsp.rng("prologue/drums")
    v = dsp.rng("prologue/vary")
    out = np.zeros((N, 2))

    def hit(synth, b, step, vel_db=0.0, jitter=0.004, p=0.0):
        pos = at(b, step) + round(r.uniform(-jitter, jitter) * SR)
        g = vel_db + r.uniform(-1.5, 1.5)
        dsp.add_wrapped(out, dsp.pan(synth(g), p), pos, dsp.gain_db(g))

    for b in range(BARS):
        for step in np.arange(0, 16, 0.5):
            accent = 3 if step % 4 == 0 else -3 if step % 1 else 0
            hit(lambda g: hihat(v), b, step, -12 + accent, 0.003, r.uniform(-0.4, 0.4))
        for step in range(0, 16, 2):
            hit(lambda g: drums.kick(v, g), b, step, -3)
        for step in range(1, 16, 2):
            hit(lambda g: drums.snare(v, g), b, step, -9, p=r.uniform(-0.1, 0.1))
    return out


def main():
    rms = lambda x: np.sqrt(np.mean(x ** 2))
    bass = build_bass()
    pad = build_pad()
    drum = build_drums()
    room = dsp.make_ir(1.0, 4000, dsp.rng("prologue/room"))
    hall = dsp.make_ir(2.4, 3500, dsp.rng("prologue/hall"))
    mix = bass + 0.22 * rms(bass) / rms(pad) * pad
    mix = mix + 0.15 * circular_reverb(mix, hall)
    drum = 0.85 * drum + 0.15 * circular_reverb(drum, room)
    mix = mix + drum * (rms(bass) * dsp.gain_db(-1) / rms(drum))
    ceil = dsp.gain_db(PEAK_DB)
    soft = lambda x: ceil * np.tanh(x / ceil)  # 드럼 트랜지언트 피크만 눌러 RMS를 유지
    g = dsp.gain_db(TARGET_RMS_DB) / rms(mix)
    for _ in range(4):
        g *= dsp.gain_db(TARGET_RMS_DB) / rms(soft(mix * g))
    mix = soft(mix * g)
    dsp.write_wav(OUT, mix)
    ok = check.report("prologue_tension", check.check(mix, None, False, stem=True))
    print(f"{OUT} {N} samples {N / SR:.2f}s")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
