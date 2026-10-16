"""앰비언트 베드 루프 -> Docs/drafts/audio/ambient_loop.wav (D 프리지안 드론, 바람, B/C 패드, 심장박동 맥박)."""
import subprocess
import sys
from pathlib import Path

import numpy as np

import dsp
import drums
from dsp import LOOP_SAMPLES as N, BAR, SR

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "ambient_loop.wav"
t = np.arange(N) / SR


def lfo(bars):
    """주기 bars마디의 사인(0~1). bars는 32의 약수."""
    return 0.5 + 0.5 * np.sin(2 * np.pi * t / (bars * BAR))


def hump(first_bar, bars=8):
    """first_bar부터 bars마디에 걸쳐 0 -> 1 -> 0으로 오르내리는 raised-cosine. 구간 밖은 0."""
    x = (t / BAR - first_bar) / bars
    return np.where((x >= 0) & (x < 1), 0.5 * (1 - np.cos(2 * np.pi * x)), 0.0)


def cents(f, c):
    return dsp.loop_freq(f * 2 ** (c / 1200))


def stereo_pair(f, spread=3, shape=dsp.saw):
    """디튠 쌍을 좌우로 벌려 스테레오로."""
    return dsp.pan(shape(cents(f, -spread), N), -0.5) + dsp.pan(shape(cents(f, spread), N), 0.5)


def wobble(x, dark, bright, rate_bars, depth):
    """컷오프가 느리게 흔들리는 효과: 어두운/밝은 로우패스를 LFO로 크로스페이드."""
    w = (0.5 + depth * (lfo(rate_bars) - 0.5))[:, None]
    return (1 - w) * dsp.lowpass(x, dark, circular=True) + w * dsp.lowpass(x, bright, circular=True)


def ramp(bar_no):
    """bar_no 마디 경계 전후 반 마디에 걸쳐 0 -> 1로 오르는 raised-cosine."""
    x = np.clip((t / BAR - bar_no + 0.5), 0, 1)
    return 0.5 * (1 - np.cos(np.pi * x))


def fold(f, ref):
    """f를 ref에 가장 가까운 옥타브로."""
    return f * 2 ** round(np.log2(ref / f))


# 근음 이동: (시작 마디, D 위 반음 수). 마지막 구간은 루프 시작의 D로 복귀
PROGRESSION = [(0, 0), (4, 8), (8, 1), (16, 0), (20, 5), (24, 10), (28, 8), (31, 0)]
starts = [b for b, _ in PROGRESSION]

# 드론: 서브 + 근음 톱니 쌍 + 5도 톱니. 전환은 마디 경계 전후 반 마디 크로스페이드
sub = np.zeros((N, 2))
saws = np.zeros((N, 2))
sub_f = 36.71
for i, (b, pc) in enumerate(PROGRESSION):
    w = (ramp(b) if i else 1.0) - (ramp(starts[i + 1]) if i + 1 < len(starts) else 0.0)
    root = 73.42 * 2 ** (pc / 12)
    sub_f = next(f for f in sorted((36.71 * 2 ** (pc / 12) * 2 ** k for k in range(-1, 2)),
                                   key=lambda f: abs(np.log2(f / sub_f))) if 30 <= f <= 62)
    sub += dsp.pan(dsp.sine(dsp.loop_freq(sub_f), N), 0) * (0.35 * w)[:, None]
    saws += (stereo_pair(fold(root, 73.42)) + 0.7 * stereo_pair(fold(root * 2 ** (7 / 12), 110.0))) * w[:, None]

# 컷오프 흔들림은 A'에서 더 크게
depth = 0.5 + 0.8 * hump(16)
drone = sub + wobble(saws, 300, 500, 16, depth) * 0.5
drone *= (0.75 + 0.25 * lfo(8))[:, None]

# 바람: 좌우 비상관 대역통과 노이즈, 16마디 스웰
wind = np.stack([dsp.noise(N, dsp.rng(f"ambient/wind{c}")) for c in (0, 1)], axis=1)
wind = dsp.bandpass(wind, (200, 1200), circular=True) * (0.3 + 0.7 * lfo(16))[:, None] * 0.12

# A' 저역 럼블
rumble = np.stack([dsp.noise(N, dsp.rng(f"ambient/rumble{c}")) for c in (0, 1)], axis=1)
rumble = dsp.lowpass(rumble, 90, order=3, circular=True) * hump(16)[:, None] * 1.5

# B(E♭4), C(B♭3) 패드
padB = dsp.lowpass(stereo_pair(311.1, 4), 1200, circular=True) * hump(8)[:, None] * 0.12
padC = dsp.lowpass(stereo_pair(233.1, 4), 1000, circular=True) * hump(24)[:, None] * 0.14

dry = drone + wind + rumble + padB + padC
wet = dsp.circular_reverb(dry, dsp.make_ir(5, 3500, dsp.rng("ambient/ir")))
mix = 0.65 * dry + 0.35 * wet

# RMS -26 dBFS 우선, 피크가 -3을 넘으면 피크 기준으로 낮춤
mix *= dsp.gain_db(-26) / np.sqrt(np.mean(mix ** 2))
if np.max(np.abs(mix)) > dsp.gain_db(-3):
    mix = dsp.normalize_peak(mix, -3)

# 심장박동 맥박: 매 마디 첫 박 lub + 8분음표 뒤 dub. 전투 드럼 킥 격자와 맞고, 키트 드럼이 아니라 신호 구분을 해치지 않는다.
# 시안 D(승인본)와 같은 시드 · 레벨. 앰비언트 위에 얹고 재정규화하지 않는다.
pr = dsp.rng("calm/pulse-calm_D_pulse_every_bar")
pulse = np.zeros((N, 2))
for b in range(dsp.BARS):
    lub = dsp.lowpass(drums.kick(pr, -2.0), 110)
    dub = dsp.lowpass(drums.kick(pr, -8.0), 90) * 0.5
    dsp.add_wrapped(pulse, dsp.to_stereo(lub), dsp.bar(b))
    dsp.add_wrapped(pulse, dsp.to_stereo(dub), dsp.bar(b) + int(0.4 * SR))
pulse = 0.6 * pulse + 0.4 * dsp.circular_reverb(pulse, dsp.make_ir(2.5, 2500, dsp.rng("calm/ir")))
mix = mix + pulse * dsp.gain_db(-30.73) / np.sqrt(np.mean(pulse ** 2))

dsp.write_wav(OUT, mix, loop=True)
print(OUT)

# 직접 확인: 루프 2회 이어붙인 이음새 +-0.5초
r = np.sqrt(np.mean(mix ** 2))
two = np.concatenate([mix, mix])
w = SR // 2
before, after = two[N - w:N], two[N:N + w]
rms = lambda a: dsp.db(np.sqrt(np.mean(a ** 2)))
print(f"전체 RMS {dsp.db(r):.2f} dB, 피크 {dsp.db(np.max(np.abs(mix))):.2f} dB")
print(f"이음새 RMS 앞 0.5s {rms(before):.2f} dB / 뒤 0.5s {rms(after):.2f} dB")
for lo, hi in ((20, 150), (150, 1200), (1200, 8000)):
    band = lambda a: dsp.db(np.sqrt(np.mean(dsp.bandpass(a, (lo, hi), circular=True) ** 2)))
    print(f"  {lo}-{hi}Hz: 앞 {band(before):.2f} dB / 뒤 {band(after):.2f} dB")
print(f"DC 평균 L {mix[:, 0].mean():.2e} R {mix[:, 1].mean():.2e}")
print(f"최대 샘플 점프(이음새 포함) {np.max(np.abs(np.diff(two[N - 100:N + 100], axis=0))):.2e}, "
      f"루프 내부 {np.max(np.abs(np.diff(mix, axis=0))):.2e}")

sys.exit(subprocess.call([sys.executable, str(Path(__file__).with_name("check.py")), "--stem", str(OUT)]))
