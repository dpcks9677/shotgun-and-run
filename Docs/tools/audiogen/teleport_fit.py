"""다운로드한 순간이동 효과음을 컷씬 타이밍에 맞춰 두 벌로 만든다. python teleport_fit.py 원본.mp3 -> Assets/06.Sounds/Cutscene/
재생 속도를 SPEED배로 올려(피치도 같이 올라감) 정점을 앞당기고, 도착용은 앞 TRIM_IN_SECONDS를 잘라 정점이 빛줄기 직후에 오게 한다.
teleport_out.wav : 퇴장(시작과 함께 재생, 정점이 빛줄기가 솟는 1초 무렵)
teleport_in.wav  : 도착(시작과 함께 재생, 정점이 시작 지연 0.3초 직후)"""
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy import signal

OUT_DIR = Path(__file__).resolve().parents[3] / "Assets" / "06.Sounds" / "Cutscene"
SPEED_UP, SPEED_DOWN = 7, 5  # 속도 1.4배, 피치 약 +5.8반음
TRIM_IN_SECONDS = 0.8  # 빠르게 한 소리 기준
FADE_IN_SECONDS = 0.1


def main(src):
    x, sr = sf.read(src)
    fast = signal.resample_poly(x, SPEED_DOWN, SPEED_UP, axis=0)  # 샘플 수를 줄인 채 같은 sr로 재생 = 빠르게 + 높게
    trim = int(TRIM_IN_SECONDS * sr)
    arrive = fast[trim:].copy()
    fade = int(FADE_IN_SECONDS * sr)
    arrive[:fade] *= np.linspace(0, 1, fade)[:, None]
    for name, data in (("teleport_out", fast), ("teleport_in", arrive)):
        peak = np.abs(data).max()
        assert peak < 1, f"{name} clipping: {peak}"
        sf.write(str(OUT_DIR / f"{name}.wav"), data, sr, subtype="PCM_16")
        print(f"{name}: {len(data) / sr:.2f} s, peak {20 * np.log10(peak):.1f} dBFS")


if __name__ == "__main__":
    main(sys.argv[1])
