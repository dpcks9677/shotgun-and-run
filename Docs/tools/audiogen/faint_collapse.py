"""기절 컷씬 효과음 생성. python faint_collapse.py -> Docs/drafts/audio/faint/faint_collapse.wav
털썩(쓰러짐) + 이명(서서히 커졌다 길게 줄어듦) + 먹먹한 느린 심장박동 2~3번, 끝으로 갈수록 저역 통과로 먹먹해짐.
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, gain_db, lowpass, noise, rng, sine, write_wav
from oneshots import finish

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "faint" / "faint_collapse.wav"
SECONDS = 3.3
BEATS = [(0.4, 1.0), (1.35, 0.7), (2.5, 0.4)]  # (시작 초, 세기) 점점 약하고 느려짐
TINNITUS_HZ = 4000
TINNITUS_DB = -20


def at(buf, x, sec):
    p = int(sec * SR)
    m = min(len(x), len(buf) - p)
    buf[p:p + m] += x[:m]


def thud(n, r):
    """저역 타격 + 옷 · 흙 노이즈."""
    t = np.arange(n) / SR
    body = sine(95 * 0.45 ** np.minimum(t / 0.1, 1), n) * exp_decay(n, 0.07)
    cloth = lowpass(noise(n, r), 1200) * exp_decay(n, 0.09)
    dirt = bandpass(noise(n, r), (150, 700)) * exp_decay(n, 0.2)
    x = body + 0.5 * cloth + 0.25 * dirt
    x[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))
    return x


def heartbeat(strength):
    """쿵-쿵 한 번(lub-dub)."""
    n = int(0.3 * SR)
    lub = sine(55, n) * exp_decay(n, 0.05)
    out = np.zeros(n)
    out += lub
    at(out, 0.6 * lub, 0.17)
    return strength * lowpass(out, 120, order=2)


def main():
    r = rng("faint_collapse/main")
    n = int(SECONDS * SR)
    t = np.arange(n) / SR

    body = np.zeros(n)
    at(body, 1.0 * thud(int(0.6 * SR), r), 0.0)
    for sec, s in BEATS:
        at(body, 0.45 * heartbeat(s), sec)

    # 끝으로 갈수록 먹먹: 원음과 저역 통과음을 크로스페이드
    muffled = lowpass(body, 300, order=4)
    mix = np.clip((t - 0.5) / (SECONDS - 0.5), 0, 1) ** 0.8
    body = body * (1 - mix) + muffled * mix

    # 이명: 0.9초까지 커졌다가 끝까지 길게 줄어듦
    env = np.where(t < 0.9, (t / 0.9) ** 2, ((SECONDS - t) / (SECONDS - 0.9)) ** 1.5)
    tone = sine(TINNITUS_HZ * (1 + 0.002 * np.sin(2 * np.pi * 5.5 * t)), n)
    tone *= 1 + 0.08 * sine(6.3, n)
    ring = gain_db(TINNITUS_DB) * tone * env

    write_wav(OUT, finish(body + ring))
    return check.main(["--oneshot", str(OUT)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
