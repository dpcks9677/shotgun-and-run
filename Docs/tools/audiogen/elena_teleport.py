"""엘레나 순간이동 컷씬의 밧줄 풀림 SFX. python elena_teleport.py -> Docs/drafts/audio/elena_teleport/rope_release.wav
순간이동음은 플레이어와 같은 teleport_out.wav를 쓴다 (teleport_fit.py).
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, rng, sine, write_wav
from oneshots import finish, reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "elena_teleport"
ROPE_SECONDS = 1.0


def rope_release():
    r = rng("elena_teleport/rope")
    n = int(ROPE_SECONDS * SR)
    t = np.arange(n) / SR

    # 섬유 마찰: 거친 노이즈의 대역이 올라갔다 내려가는 스윕 + 불규칙한 떨림
    slide_n = int(0.6 * SR)
    st = np.linspace(0, 1, slide_n)
    centers = np.geomspace(700, 2600, 6)
    src = noise(slide_n, r)
    pos = np.sin(np.pi * st) * (len(centers) - 1)
    slide = np.zeros(slide_n)
    for k, c in enumerate(centers):
        slide += np.maximum(1 - np.abs(pos - k), 0) * bandpass(src, (c * 0.7, c * 1.4))
    scrape = lowpass(noise(slide_n, r), 60)
    slide *= np.sin(np.pi * st) ** 0.7 * (0.5 + 3 * np.abs(scrape) / np.abs(scrape).max())

    # 매듭 풀림 탁: 짧은 중역 클릭
    knot_n = int(0.06 * SR)
    knot = bandpass(noise(knot_n, r), (1200, 3500)) * exp_decay(knot_n, 0.008)
    knot_pos = int(0.12 * SR)

    # 낙하음: 낮은 툭 + 약한 섬유 소리
    thud_pos = int(0.62 * SR)
    m = n - thud_pos
    thud = sine(120 * 0.6 ** np.minimum(np.arange(m) / SR / 0.05, 1), m) * exp_decay(m, 0.035)
    thud += 0.6 * lowpass(noise(m, r), 700) * exp_decay(m, 0.03)

    x = np.zeros(n)
    x[:slide_n] += 0.7 * slide
    x[knot_pos:knot_pos + knot_n] += 1.6 * knot
    x[thud_pos:] += 1.3 * thud
    x = highpass(x, 80)
    return finish(reverb(x, 0.3, 3500, -16, r)[:n])


def main():
    path = OUT / "rope_release.wav"
    write_wav(path, rope_release())
    return check.main(["--oneshot", str(path)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
