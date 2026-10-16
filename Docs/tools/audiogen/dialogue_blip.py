"""대화 타이핑 SFX: 글자가 찍힐 때마다 울리는 짧은 블립. python dialogue_blip.py -> Docs/drafts/audio/ui/dialogue_blip.wav
매 블립을 같은 소리로 반복 재생하므로 한 개만 만든다. 원샷 최소 길이(0.2초) 검사는 제외한다.
"""
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

import check
from dsp import SR, exp_decay, highpass, lowpass, normalize_peak, sine, square, write_wav

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "ui" / "dialogue_blip.wav"
PEAK_DB = -6
SECONDS = 0.07
FUNDAMENTAL = 293.7  # D4 (BGM의 D 프리지안과 맞춤)


def blip():
    n = int(SECONDS * SR)
    t = np.arange(n) / SR
    freq = FUNDAMENTAL * (1 + 0.04 * np.exp(-t / 0.008))  # 살짝 위에서 떨어지며 시작
    x = sine(freq, n) + 0.12 * lowpass(square(freq, n), 2500)  # 둥근 사인 + 약한 레트로 배음
    x *= np.minimum(t / 0.002, 1) * exp_decay(n, 0.018)
    fade = int(0.01 * SR)
    x[-fade:] *= np.linspace(1, 0, fade)
    return normalize_peak(highpass(x, 120), PEAK_DB)


def main():
    write_wav(OUT, blip())
    x, _ = sf.read(str(OUT), dtype="float64")
    res = [r for r in check.check(x, sf.info(str(OUT)), oneshot=True) if r[0] != "duration"]
    return check.report(str(OUT), res)


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
