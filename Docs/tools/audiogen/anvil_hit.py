"""대장간 모루 타격음 3종 생성. python anvil_hit.py -> Docs/drafts/audio/anvil/anvil_hit_01~03.wav
망치가 모루를 칠 때 한 번 나는 짧은 금속 쾅. 실제 모루 타격 녹음(freesound hammering-on-anvil, 19타)을 분석한 값에 맞췄다:
  - 에너지가 1.2~5kHz에 몰리고(스펙트럼 중심 약 4.6kHz), 주된 울림은 2385 · 3220Hz 부근 + 2625 · 1720Hz, 그 위 5.3~8.2kHz 잔울림
  - 감쇠가 빨라 -30dB까지 약 0.27초 (종처럼 길게 울리지 않음). 높은 대역일수록 빨리 꺼지고 저역 몸통음은 20dB 이상 아래
같은 소리가 3번 연달아 나오면 어색하므로 울림 높이를 조금씩 달리한 변형 3개를 돌려 쓴다.
구성: 망치 충격(딱 + 중고역 노이즈) + 모루 금속 울림(비정수 배음) + 저역 몸통음(참고 녹음보다 5~7dB 크게 — 묵직함) + 불똥 지글거림.
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, normalize_peak, rng, sine, write_wav

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "anvil"
PEAK_DB = -6
SECONDS = 0.6
# (주파수 Hz, 상대 크기, 감쇠 시간 초): 참고 녹음의 평균 스펙트럼 피크와 대역별 감쇠에서 뽑은 값
PARTIALS = [(230, 0.035, 0.10), (420, 0.04, 0.09), (778, 0.10, 0.10), (1720, 0.16, 0.07), (2385, 1.0, 0.085), (2625, 0.33, 0.08), (3182, 0.15, 0.08),
            (3220, 0.70, 0.08), (3390, 0.10, 0.055), (5362, 0.22, 0.06), (5600, 0.18, 0.06), (6658, 0.15, 0.05), (8178, 0.22, 0.04)]
PITCH = [1.0, 0.94, 1.06]  # 변형별 울림 높이 배율
HIT_GAIN = [1.0, 0.9, 1.1]  # 변형별 충격 세기


def anvil_hit(i):
    r = rng(f"anvil/hit_{i:02d}")
    n = int(SECONDS * SR)
    t = np.arange(n) / SR

    click = highpass(noise(n, r), 2400) * exp_decay(n, 0.004)  # 망치 면이 닿는 순간의 딱 (참고 녹음에서 가장 센 대역)
    strike = bandpass(noise(n, r), (600, 5000)) * exp_decay(n, 0.02)  # 중고역 충격 노이즈
    thump = sine(150 * (1 - 0.4 * np.minimum(t / 0.04, 1)), n) * exp_decay(n, 0.05)  # 저역 몸통음: 참고 녹음보다 약간 크게
    body = bandpass(noise(n, r), (100, 600)) * exp_decay(n, 0.04)
    impact = HIT_GAIN[i - 1] * (1.0 * click + 0.7 * strike + 0.05 * thump + 0.05 * body)

    ring = np.zeros(n)
    for freq, amp, tau in PARTIALS:
        f = freq * PITCH[i - 1] * r.uniform(0.99, 1.01)
        ring += amp * sine(f, n) * exp_decay(n, tau) * r.uniform(0.85, 1.0)
    ring *= np.minimum(t / 0.0005, 1) * 0.5

    sizzle = np.zeros(n)  # 튀는 불똥: 충격 직후 0.25초 안에 흩어진 고역 틱 (약하게)
    for _ in range(int(r.integers(5, 9))):
        m = int(0.004 * SR)
        p = int(r.uniform(0.01, 0.25) * SR)
        sizzle[p:p + m] += r.uniform(0.04, 0.12) * highpass(noise(m, r), 4000) * exp_decay(m, 0.0012)

    x = lowpass(impact + ring + sizzle, 12000)
    fade_in, fade_out = int(0.0005 * SR), int(0.03 * SR)  # 첫 딱을 살리려고 시작 페이드는 0.5ms만
    x[:fade_in] *= np.linspace(0, 1, fade_in)
    x[-fade_out:] *= np.linspace(1, 0, fade_out)
    return normalize_peak(x, PEAK_DB)


def main():
    paths = []
    for i in (1, 2, 3):
        path = OUT / f"anvil_hit_{i:02d}.wav"
        write_wav(path, anvil_hit(i))
        paths.append(str(path))
    return check.main(["--oneshot"] + paths)


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
