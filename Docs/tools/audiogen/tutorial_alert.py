"""몬스터 경보 포효 생성. python tutorial_alert.py -> Docs/drafts/audio/tutorial_alert/monster_alert.wav
오크 · 고블린 2~4마리가 알아채는 순간의 짧은 "크아악". 낮은 톱니 성대 + 포먼트 ㅏ→ㅓ 이동 + 그르렁 변조 + 숨 노이즈, 3겹을 어긋나게 겹친다.
"""
import sys
from pathlib import Path

import numpy as np

import check
from dsp import SR, bandpass, highpass, lowpass, noise, rng, saw, soft_clip, write_wav
from oneshots import finish, reverb

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "tutorial_alert"
SECONDS = 1.1
FORMANTS_A = [(750, 1.0), (1200, 0.7), (2500, 0.3)]   # ㅏ
FORMANTS_EO = [(550, 1.0), (950, 0.7), (2400, 0.3)]   # ㅓ


def formant_bank(x, formants):
    return sum(g * bandpass(x, (f * 0.85, f * 1.18)) for f, g in formants)


def voice(name, base, delay, length, gain):
    """포효 한 마리. base = 기본음 Hz, delay/length 초."""
    r = rng(f"tutorial_alert/{name}")
    n = int(length * SR)
    t = np.linspace(0, 1, n)
    ts = t * length

    # 기본음: 시작에 치솟았다가 내려가고 끝에서 더 처짐 + 불안정한 떨림
    jitter = np.convolve(noise(n, r), np.ones(400) / 400, "same") * 6
    f0 = base * (1.25 - 0.35 * np.minimum(t * 3, 1) - 0.25 * t) * (1 + 0.02 * np.sin(2 * np.pi * 7 * ts)) + jitter * base * 0.15
    src = saw(f0, n) + 0.6 * np.maximum(saw(f0, n) - 0.4, 0) * 2  # 톱니 + 펄스 느낌의 날카로움

    # 그르렁 진폭 변조 (후반으로 갈수록 거칠게)
    grr_f = 38 + 14 * r.uniform()
    grr = 1 - (0.35 + 0.35 * t) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * grr_f * ts + 1.5 * np.sin(2 * np.pi * 5 * ts))))
    src = src * grr

    # 포먼트 ㅏ→ㅓ 크로스페이드
    w = np.clip((t - 0.3) / 0.5, 0, 1)
    body = (1 - w) * formant_bank(src, FORMANTS_A) + w * formant_bank(src, FORMANTS_EO)
    body += 0.25 * lowpass(src, 400)

    # 숨 노이즈 (초반 강한 어택 + 지속)
    breath = bandpass(noise(n, r), (1200, 5000)) * (0.25 + 0.9 * np.exp(-ts / 0.08))

    env = np.minimum(ts / 0.015, 1) * np.where(ts < 0.35, 0.7 + 0.3 * ts / 0.35, np.exp(-(ts - 0.35) / 0.28))
    env *= np.minimum((length - ts) / 0.1, 1)
    y = soft_clip((body + 0.6 * breath) * env, 2.0)

    out = np.zeros(int(SECONDS * SR))
    p = int(delay * SR)
    out[p:p + n] = y[:len(out) - p] * gain
    return out


def alert():
    x = voice("a", 85, 0.0, 1.05, 1.0)
    x += voice("b", 105, 0.035, 0.95, 0.8)
    x += voice("c", 68, 0.07, 1.0, 0.7)
    x = highpass(x, 50)
    x = lowpass(x, 7000)
    r = rng("tutorial_alert/reverb")
    return finish(reverb(x, 0.25, 3500, -16, r))


def main():
    path = OUT / "monster_alert.wav"
    write_wav(path, alert())
    return check.main(["--oneshot", str(path)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
