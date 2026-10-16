"""청취용 시연 믹스: 앰비언트 -> 드럼 진입(적 카메라 안) -> 신스 진입(습격 10초 전) -> 습격 스템 -> 복귀. python preview_mix.py -> Docs/drafts/audio/preview_mix.wav"""
import numpy as np
import soundfile as sf

import dsp
from dsp import SR

D = "../../drafts/audio/"
amb, dr, gt, rd = (sf.read(D + f"{n}_loop.wav")[0] for n in ("ambient", "drums", "synth", "raid"))
shots = [sf.read(D + f"oneshots/{n}.wav")[0] for n in
         ("sfx_drip_01", "sfx_rustle_01", "sfx_metal_01", "sfx_stone_01", "sfx_wind_01", "sfx_drip_03", "sfx_rustle_02")]
total = 2 * dsp.LOOP_SAMPLES + dsp.LOOP_SAMPLES // 2  # 약 4분15초
t = np.arange(total)


def loop(x):
    return np.tile(x, (total // len(x) + 1, 1))[:total]


def ramp(start, up, hold_end, down):
    """start(초)에 up초 동안 올라가 hold_end(초)까지 유지, down초 동안 내려가는 0~1 곡선."""
    s = t / SR
    return np.clip((s - start) / up, 0, 1) * np.clip((hold_end + down - s) / down, 0, 1)


mix = loop(amb)
raid = ramp(160, 3, 192, 6)
mix += loop(dr) * ramp(105.6, 3, 192, 6)[:, None]   # 마디 33 경계(=다음 루프 첫 박 +3.2s)에서 드럼 진입
mix += loop(gt) * (ramp(150.0, 3, 192, 6) * (1 - (1 - dsp.gain_db(-6)) * raid))[:, None]  # 신스는 습격 10초 전 진입, 습격 구간에서는 -6dB
mix += loop(rd) * raid[:, None]
rng = dsp.rng("preview")
pos = 6.0
quiet = [(105.6, 200)]  # 전투 중에는 원샷 중단
while pos < total / SR - 6:
    if not any(a <= pos <= b for a, b in quiet):
        s = shots[rng.integers(len(shots))]
        i = int(pos * SR)
        mix[i:i + len(s)] += (s * 0.25)[:total - i, None] * np.array([1 - 0.3, 1 + 0.3])[None, :] * rng.uniform(0.6, 1.0)
    pos += rng.uniform(6, 18)
mix = dsp.normalize_peak(mix, -3)
dsp.write_wav(D + "preview_mix.wav", mix)
print("preview_mix.wav", round(total / SR), "s")
