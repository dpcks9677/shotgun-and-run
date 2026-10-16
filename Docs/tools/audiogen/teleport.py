"""순간이동 SFX 2종 생성. python teleport.py -> Docs/drafts/audio/teleport/
teleport_charge.wav : 하얗게 물드는 1초 동안 깔리는 충전음 (에너지가 모이며 올라감)
teleport_burst.wav  : 빛줄기가 솟는 순간의 폭발 + 차임 + 여운
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, rng, sine, write_wav
from oneshots import finish, reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "teleport"
CHARGE_SECONDS = 1.0
BURST_SECONDS = 1.3
CHIME_NOTES = [1174.7, 1568.0, 1760.0, 2349.3]  # D6 · G6 · A6 · D7


def charge():
    r = rng("teleport/charge")
    n = int(CHARGE_SECONDS * SR)
    t = np.linspace(0, 1, n)
    rise = t ** 2  # 끝으로 갈수록 가파르게 커짐

    hum = sine(110, n) + 0.5 * sine(165, n)  # 낮은 험 + 5도
    hum *= 0.3 + 0.7 * rise

    sweep_freq = 300 * (1800 / 300) ** t * (1 + 0.01 * np.sin(2 * np.pi * 9 * t))  # 지수 상승 + 떨림
    sweep = (sine(sweep_freq, n) + 0.3 * sine(sweep_freq * 2, n)) * rise

    centers = 2000 * (6000 / 2000) ** t  # 위로 올라가는 반짝임 노이즈
    shimmer = np.zeros(n)
    src = noise(n, r)
    for k, c in enumerate(np.geomspace(2000, 6000, 6)):
        w = np.maximum(1 - np.abs(np.log(centers / c) / np.log(6000 / 2000) * 5), 0)
        shimmer += w * bandpass(src, (c * 0.8, c * 1.25))
    shimmer *= rise

    x = 0.6 * hum + 0.5 * sweep + 0.5 * shimmer
    return finish(reverb(x, 0.5, 4000, -12, r))


def burst():
    r = rng("teleport/burst")
    n = int(BURST_SECONDS * SR)
    t = np.arange(n) / SR

    # 공기 스윕: 밝게 시작해 낮아지는 노이즈
    sweep_n = int(0.6 * SR)
    st = np.linspace(0, 1, sweep_n)
    centers = np.geomspace(7000, 900, 8)
    src = noise(sweep_n, r)
    pos = st * (len(centers) - 1)
    whoosh = np.zeros(sweep_n)
    for k, c in enumerate(centers):
        whoosh += np.maximum(1 - np.abs(pos - k), 0) * bandpass(src, (c * 0.7, c * 1.4))
    whoosh *= np.minimum(np.arange(sweep_n) / SR / 0.01, 1) * exp_decay(sweep_n, 0.18)

    # 서브 붐
    boom_freq = 90 * 0.5 ** np.minimum(t / 0.25, 1)
    boom = sine(boom_freq, n) * exp_decay(n, 0.22)

    # 차임 글리터: 짧은 간격으로 흩뿌린 종소리
    chime = np.zeros(n)
    for k, f in enumerate(CHIME_NOTES * 2):
        p = int((0.02 + 0.045 * k + r.uniform(0, 0.015)) * SR)
        m = n - p
        tone = sine(f, m) + 0.3 * sine(f * 2.76, m) + 0.15 * sine(f * 5.4, m)
        chime[p:] += tone * exp_decay(m, 0.35 * (1 - 0.06 * k)) * (1 - 0.07 * k)

    x = np.zeros(n)
    x[:sweep_n] += 0.9 * whoosh
    x += 0.8 * boom + 0.25 * chime
    x = highpass(x, 40)
    return finish(reverb(lowpass(x, 9000), 1.2, 5000, -7, r))


def main():
    jobs = [(charge, "teleport_charge"), (burst, "teleport_burst")]
    paths = []
    for fn, name in jobs:
        path = OUT / f"{name}.wav"
        write_wav(path, fn())
        paths.append(str(path))
    return check.main(["--oneshot"] + paths)


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
