"""플레이어 사망음: 낡은 교회 종 한 번. python death_bell.py [peak_db] -> Docs/drafts/audio/death/death_bell.wav"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, exp_decay, highpass, lowpass, noise, rng, sine, write_wav
from oneshots import PEAK_DB, finish, reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "death" / "death_bell.wav"
FUNDAMENTAL = 164.8  # E3
BODY_SECONDS = 4.2
# (배음비, 상대 진폭, 감쇠 시간 초): 교회 종의 허밍 · 프라임 · 단3도 · 5도 · 노미널 순
PARTIALS = [(0.5, 0.7, 4.5), (1.0, 1.0, 3.6), (1.2, 0.55, 2.6), (1.5, 0.5, 2.2),
            (2.0, 0.6, 1.8), (2.5, 0.3, 1.2), (3.0, 0.25, 0.9), (4.0, 0.2, 0.6), (5.4, 0.1, 0.4)]


def bell():
    r = rng("death/bell")
    n = int(BODY_SECONDS * SR)
    x = np.zeros(n)
    for ratio, amp, tau in PARTIALS:
        f = FUNDAMENTAL * ratio
        # 살짝 어긋난 쌍을 겹쳐 울렁이는 맥놀이를 만든다
        pair = sine(f, n) + sine(f + r.uniform(0.4, 1.0), n)
        x += amp * pair * exp_decay(n, tau)

    # 타격: 짧은 노이즈 클랭크 + 낮은 쿵
    hit = int(0.08 * SR)
    clank = bandpass(noise(hit, r), (900, 4000)) * exp_decay(hit, 0.015)
    x[:hit] += 1.2 * clank * x.std() / clank.std()
    thump = sine(FUNDAMENTAL / 2, int(0.3 * SR)) * exp_decay(int(0.3 * SR), 0.1)
    x[:len(thump)] += 0.8 * thump * x.std() / thump.std()

    x *= np.minimum(np.arange(n) / SR / 0.004, 1)
    x = highpass(lowpass(x, 6000), 50)
    return x


def main():
    peak = float(sys.argv[1]) if len(sys.argv) > 1 else PEAK_DB
    write_wav(OUT, finish(reverb(bell(), 1.6, 3500, -9, rng("death/bell/reverb")), peak))
    return check.main(["--oneshot", str(OUT)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
