"""던전 BGM 합성 공통 모듈. 신호는 float(-1~1), 모노 (N,) 또는 스테레오 (N,2)."""
import zlib
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy import signal

SR = 44100
BPM = 75
BEAT = 0.8  # 초
BAR = 3.2
BARS = 32
LOOP_SAMPLES = 4515840  # SR * 102.4
assert SR * BAR * BARS == LOOP_SAMPLES and isinstance(LOOP_SAMPLES, int)

BAR_SAMPLES = int(SR * BAR)
BEAT_SAMPLES = int(SR * BEAT)


def rng(name):
    """이름으로 시드한 Generator. 같은 이름이면 항상 같은 결과."""
    return np.random.default_rng(zlib.crc32(name.encode()))


def loop_freq(f):
    """루프 길이에 정수 주기가 들어가는 가장 가까운 주파수. 루프 전체에 걸친 지속음 오실레이터는 이 값을 써야 이음새 위상이 맞는다."""
    cycles = round(f * LOOP_SAMPLES / SR)
    return cycles * SR / LOOP_SAMPLES


def bar(n):
    """0 기반 마디 n의 시작 샘플."""
    return n * BAR_SAMPLES


def beat(n):
    """루프 전체 기준 0 기반 박 n의 시작 샘플."""
    return n * BEAT_SAMPLES


# 오실레이터: freq는 스칼라 또는 길이 n 배열(위상 누적으로 연속)
def _phase(freq, n):
    f = np.broadcast_to(np.asarray(freq, dtype=np.float64), (n,))
    return np.cumsum(f) / SR


def sine(freq, n):
    return np.sin(2 * np.pi * _phase(freq, n))


def saw(freq, n):
    return 2 * (_phase(freq, n) % 1.0) - 1


def square(freq, n):
    return np.where(_phase(freq, n) % 1.0 < 0.5, 1.0, -1.0)


def noise(n, rng):
    return rng.uniform(-1, 1, n)


# 필터: circular=True면 3번 이어붙여 필터한 뒤 가운데만 취함(루프 신호용)
def _filt(x, btype, cutoff, order, circular):
    sos = signal.butter(order, cutoff, btype=btype, fs=SR, output="sos")
    if not circular:
        return signal.sosfilt(sos, x, axis=0)
    n = len(x)
    return signal.sosfilt(sos, np.concatenate([x, x, x]), axis=0)[n:2 * n]


def lowpass(x, cutoff, order=2, circular=False):
    return _filt(x, "lowpass", cutoff, order, circular)


def highpass(x, cutoff, order=2, circular=False):
    return _filt(x, "highpass", cutoff, order, circular)


def bandpass(x, cutoff, order=2, circular=False):
    """cutoff = (저역, 고역) Hz."""
    return _filt(x, "bandpass", cutoff, order, circular)


# 블록 단위 가변 컷오프 공진 로우패스
BLOCK = 64


def resonant_lowpass(x, fc, q):
    """블록(BLOCK 샘플)마다 컷오프 fc[k]가 바뀌는 2극 RBJ 로우패스. 필터 상태는 블록 사이에 이어간다."""
    w0 = 2 * np.pi * fc / SR
    alpha = np.sin(w0) / (2 * q)
    a0 = 1 + alpha
    c = np.cos(w0)
    sos = np.stack([(1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, np.ones_like(a0), -2 * c / a0, (1 - alpha) / a0], axis=1)
    y = np.empty_like(x)
    zi = np.zeros((1, x.shape[1], 2))
    for k, s in enumerate(sos):
        i = k * BLOCK
        y[i:i + BLOCK], zi = signal.sosfilt(s[None], x[i:i + BLOCK], axis=0, zi=zi)
    return y


# 엔벨로프(시간 인자는 초). adsr의 r은 n의 끝 r초 구간에서 s → 0
def adsr(n, a, d, s, r):
    t = np.arange(n) / SR
    nr = int(r * SR)
    env = np.interp(t, [0, a, a + d], [0, 1, s])
    if nr:
        env[n - nr:] = np.minimum(env[n - nr:], s * np.linspace(1, 0, nr))
    return env


def exp_decay(n, tau):
    return np.exp(-np.arange(n) / SR / tau)


def make_ir(seconds, damp, rng, stereo=True):
    """지수 감쇠 노이즈 IR(-60dB가 seconds초). damp = 로우패스 컷오프 Hz. 에너지 1로 정규화."""
    n = int(seconds * SR)
    shape = (n, 2) if stereo else (n,)
    ir = rng.standard_normal(shape) * exp_decay(n, seconds / 6.9).reshape((n,) + (1,) * (len(shape) - 1))
    ir = lowpass(ir, damp)
    return ir / np.sqrt(np.sum(ir ** 2) / (2 if stereo else 1))


def circular_reverb(x, ir):
    """길이 LOOP_SAMPLES 신호의 원형 합성곱(꼬리가 루프 시작으로 감김). 웻만 반환, 항상 (N,2)."""
    assert len(x) == LOOP_SAMPLES and len(ir) <= LOOP_SAMPLES
    X = np.fft.rfft(to_stereo(x), n=LOOP_SAMPLES, axis=0)
    H = np.fft.rfft(to_stereo(ir), n=LOOP_SAMPLES, axis=0)
    return np.fft.irfft(X * H, n=LOOP_SAMPLES, axis=0)


def circular_delay(x, time, feedback, mix):
    """x + mix * (피드백 반복 탭들). time은 초. 꼬리는 원형으로 감김."""
    d = int(time * SR)
    wet = np.zeros_like(x)
    k, g = 1, feedback
    while g > 1e-3:
        wet += g * np.roll(x, k * d, axis=0)
        k, g = k + 1, g * feedback
    return x + mix * wet


def add_wrapped(buf, event, pos, gain=1.0):
    """buf((N,2))의 pos에 event를 더하되 끝을 넘으면 앞으로 감는다. event는 모노/스테레오."""
    idx = (pos + np.arange(len(event))) % len(buf)
    buf[idx] += gain * (event if event.ndim == 2 else event[:, None])


def soft_clip(x, drive):
    return np.tanh(drive * x) / np.tanh(drive)


def to_stereo(x):
    return x if x.ndim == 2 else np.stack([x, x], axis=1)


def pan(x, p):
    """p: -1(왼)~1(오른), 등파워. 모노 입력 -> (N,2)."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def db(x):
    """선형 진폭 -> dB."""
    return 20 * np.log10(np.maximum(x, 1e-12))


def gain_db(d):
    """dB -> 선형 배율."""
    return 10 ** (d / 20)


def normalize_peak(x, peak_db):
    return x * (gain_db(peak_db) / np.max(np.abs(x)))


def write_wav(path, x, loop=False):
    """16bit PCM 44100 WAV 저장. loop=True면 길이 LOOP_SAMPLES 확인."""
    if loop:
        assert len(x) == LOOP_SAMPLES, f"loop length {len(x)} != {LOOP_SAMPLES}"
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(path), x, SR, subtype="PCM_16")
