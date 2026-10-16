"""매복 컷씬 경보음 생성. python ambush_alarm.py -> Docs/drafts/audio/ambush/ambush_alarm.wav
녹슨 경종을 다급하게 4번 치는 소리 + 첫 타에 깔리는 낮은 붐 + 던전 잔향. 뿔나팔(습격 경보)과 겹치지 않게 금속 종의 비정수 배음 위주.
종 음정은 D · Eb(D 프리지안)에서 타마다 조금씩 달라진다. 2초 이후엔 여운만 남는다.
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, normalize_peak, rng, sine, write_wav
from oneshots import reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "ambush"
PEAK_DB = -1.5
SECONDS = 2.3
# (타격 시각 초, 종 기음 Hz, 세기): D5 · Eb5 · D5(살짝 낮게) · Eb5(살짝 높게)
STRIKES = [(0.0, 587.3, 1.0), (0.2, 622.3, 0.85), (0.4, 584.0, 0.9), (0.59, 626.0, 0.8)]
# (기음 배율, 상대 크기, 감쇠 시간 초): 종의 hum · prime · tierce · quint · nominal 등 비정수 배음
PARTIALS = [(0.5, 0.5, 0.7), (1.0, 1.0, 0.5), (1.19, 0.6, 0.4), (1.5, 0.5, 0.3), (2.0, 0.45, 0.25), (2.51, 0.3, 0.18), (3.03, 0.2, 0.12), (4.07, 0.15, 0.08)]


def bell(freq, n, r):
    t = np.arange(n) / SR
    x = np.zeros(n)
    for ratio, amp, tau in PARTIALS:
        f = freq * ratio * r.uniform(0.995, 1.005)
        x += amp * sine(f, n) * exp_decay(n, tau) * r.uniform(0.85, 1.0)
        x += 0.5 * amp * sine(f * 1.007, n) * exp_decay(n, tau)  # 녹슨 종의 거친 맥놀이
    clank = bandpass(noise(n, r), (1500, 6000)) * exp_decay(n, 0.012)  # 쇠 부딪히는 딱
    rattle = highpass(noise(n, r), 3000) * exp_decay(n, 0.06) * (0.5 + 0.5 * np.sin(2 * np.pi * 70 * t))  # 녹 가루 떨리는 지글거림
    return x * np.minimum(t / 0.0004, 1) + 2.5 * clank + 0.12 * rattle


def main():
    r = rng("ambush_alarm/bells")
    n = int(SECONDS * SR)
    m = int(1.6 * SR)
    mix = np.zeros(n + int(1.5 * SR))
    for start, freq, gain in STRIKES:
        p = int(start * SR)
        mix[p:p + m] += gain * bell(freq, m, r)

    t = np.arange(m) / SR
    boom = sine(48 * (1 - 0.35 * np.minimum(t / 0.15, 1)), m) * exp_decay(m, 0.3)  # 바닥 울림: 첫 타에만
    boom += 0.5 * lowpass(noise(m, r), 120) * exp_decay(m, 0.15)
    mix[:m] += 0.9 * boom * np.minimum(t / 0.002, 1)

    x = reverb(lowpass(mix, 9000), 1.8, 3500, -8, r)[:n]
    tail = np.clip((SECONDS - np.arange(n) / SR) / 0.5, 0, 1)  # 2초 이후 여운만 남게 줄이고 끝은 0으로
    late = np.where(np.arange(n) / SR > 1.1, np.exp(-(np.arange(n) / SR - 1.1) / 0.5), 1.0)
    x = normalize_peak(x * tail * late, PEAK_DB)
    path = OUT / "ambush_alarm.wav"
    write_wav(path, x)
    first = int(np.argmax(np.abs(x[:int(0.1 * SR)])))
    print(f"first hit peak: {first / SR * 1000:.1f} ms, rms {20 * np.log10(np.sqrt(np.mean(x ** 2))):.1f} dBFS")
    return check.main(["--oneshot", str(path)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
