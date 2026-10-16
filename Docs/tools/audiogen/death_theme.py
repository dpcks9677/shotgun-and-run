"""사망 종소리 뒤에 이어지는 약 2초 장송 스팅어. python death_theme.py [peak_db] -> Docs/drafts/audio/death/death_theme.wav
낮은 팀파니 두 번("두둥") + 낮은 금관 드론 위에 G -> F# -> E로 내려가는 느린 선율. 끝은 짧은 잔향으로 사라진다."""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, adsr, bandpass, exp_decay, lowpass, noise, rng, saw, sine, soft_clip, write_wav
from oneshots import finish, reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "death" / "death_theme.wav"
BODY_SECONDS = 2.5
E2, B2, E3, Fs3, G3 = 82.41, 123.47, 164.81, 185.0, 196.0
DETUNE = (-10, 10)  # 센트
DRIVE = 2.0

HITS = [(0.0, 1.0), (0.75, 1.15)]  # (시작 초, 세기)
DRONE = [E2, B2]
MELODY = [(0.0, 0.75, G3), (0.75, 0.75, Fs3), (1.5, 1.0, E3)]  # (시작 초, 길이 초, 주파수)


def timpani(r):
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    freq = 60 + 55 * np.exp(-t / 0.05)  # 때리는 순간 높았다가 내려앉는 음높이
    body = (sine(freq, n) + 0.3 * sine(freq * 1.5, n)) * exp_decay(n, 0.28)
    click = bandpass(noise(n, r), (100, 500)) * exp_decay(n, 0.015)
    return body + 0.5 * click


def brass(freq, seconds, cutoff):
    n = int(seconds * SR)
    x = sum(saw(freq * 2 ** (c / 1200), n) for c in DETUNE) / len(DETUNE)
    return lowpass(x, cutoff, order=2) * adsr(n, 0.15, 0.0, 1.0, 0.3)


def place(buf, event, start, gain):
    i = int(start * SR)
    m = min(len(event), len(buf) - i)
    buf[i:i + m] += gain * event[:m]


def theme():
    r = rng("death/theme")
    n = int(BODY_SECONDS * SR)
    x = np.zeros(n)
    for start, power in HITS:
        place(x, timpani(r), start, power)
    for f in DRONE:
        place(x, brass(f, BODY_SECONDS, 350), 0.0, 0.35)
    for start, seconds, f in MELODY:
        place(x, brass(f, seconds, 700), start, 0.3)
    return x


def main():
    peak = float(sys.argv[1]) if len(sys.argv) > 1 else -1.5
    x = reverb(theme(), 2.2, 2500, -4, rng("death/theme/reverb"))
    x = soft_clip(x / np.abs(x).max(), DRIVE)  # 순간 피크를 눌러 평균 음량을 키운다
    write_wav(OUT, finish(x, peak))
    return check.main(["--oneshot", str(OUT)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
