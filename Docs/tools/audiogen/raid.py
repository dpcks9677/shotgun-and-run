"""습격 단계 스템: 저음 브라스 + 서스테인 파워코드 + 디스토션 베이스 처킹 + 킥 갤럽 드럼 -> Docs/drafts/audio/raid_loop.wav. python raid.py"""
import sys
import time
from pathlib import Path

import numpy as np
from scipy import signal

import check
import drums
import dsp
from dsp import SR

OUT = Path(__file__).resolve().parents[2] / "drafts" / "audio" / "raid_loop.wav"

D2, EB2, C2, BB2, A2 = 73.42, 77.78, 65.41, 116.54, 110.0
# 마디별 파워코드 근음 / 브라스 근음(55~80Hz로 통일: B♭, A는 한 옥타브 아래)
CHORD = {}
BRASS = {}
for b in range(32):
    if b < 12 or 16 <= b < 24:
        CHORD[b] = BRASS[b] = D2
    elif b < 16:
        CHORD[b] = BRASS[b] = EB2
    else:
        CHORD[b], BRASS[b] = {0: (C2, C2), 1: (BB2, BB2 / 2), 2: (A2, A2 / 2), 3: (D2, D2)}[(b - 24) % 4]

LEVEL_RMS_DB, LEVEL_PEAK_DB = -21, -3
POWER_DB = -6  # 서스테인 파워코드(브라스 대비)
BASS_CHUG_DB = -2  # 디스토션 베이스 처킹(브라스 대비)
DRUM_DB = -2  # 브라스 대비
INTRO_DB = -3  # 마디 0~3 스네어 · 킥 · 베이스 처킹
WET = 0.15
CHUG_WET = 0.12
Q_BRASS = 2.0
BASE_CUTOFF = 300
UNISON = (-5, 0, 5)  # 센트
VOICES = ((1.0, 1.0), (1.5, 0.7), (2.0, 0.5))  # (근음 배수, 음량)
HAT_DOWNBEAT_DB = 3  # 마디 첫 스텝 하이햇 추가 악센트
DOWNBEAT_KICK_DB = -3  # 마디 첫 박 킥 강세
HAT_DB = (0, -8, -4, -8)  # 스텝 % 4 별 악센트: 박 앞 강 · 8분 중간 · 나머지 약
CHUG_LEN, ACCENT_LEN, GALLOP_LEN, SIXTEENTH_LEN = 0.13, 0.25, 0.15, 0.08  # 처그 길이(초)
VIB_DELAY, VIB_FADE, VIB_RATE, VIB_CENTS = 0.6, 1.0, 5, 8


def peaking(x, f, gain, q=1.0):
    """RBJ 피킹 EQ(완만한 대역 강조). gain은 dB."""
    A = dsp.gain_db(gain / 2)
    w0 = 2 * np.pi * f / SR
    alpha = np.sin(w0) / (2 * q)
    c = np.cos(w0)
    sos = np.array([[1 + alpha * A, -2 * c, 1 - alpha * A, 1 + alpha / A, -2 * c, 1 - alpha / A]])
    sos /= sos[0, 3]
    return signal.sosfilt(sos, x, axis=0)


def brass_note(root, gate, open_t, fc_end, rel=0.4, att=0.25):
    """저음 브라스 한 음 (n,2). 톱니 유니즌 3음(근음·5도·옥타브) -> 컷오프가 열리는 공진 필터 -> 포먼트. 길이 = gate + rel."""
    n = int((gate + rel) * SR)
    n += -n % dsp.BLOCK
    t = np.arange(n) / SR
    vib = VIB_CENTS * np.clip((t - VIB_DELAY) / VIB_FADE, 0, 1) * np.sin(2 * np.pi * VIB_RATE * t)
    y = np.zeros((n, 2))
    for mult, vol in VOICES:
        for cents, p in zip(UNISON, (-0.3, 0, 0.3)):
            y += vol * dsp.pan(dsp.saw(root * mult * 2 ** ((cents + vib) / 1200), n), p)
    y /= 6
    tb = t[::dsp.BLOCK]
    fc = BASE_CUTOFF * (fc_end / BASE_CUTOFF) ** np.clip(tb / open_t, 0, 1)
    y = dsp.resonant_lowpass(y, fc, Q_BRASS)
    y = peaking(peaking(y, 500, 4), 1200, 3)
    env = np.interp(t, [0, att, open_t, open_t + 0.6], [0, 0.5, 1, 0.7])  # 크레센도 후 70% 서스테인
    nr = int(rel * SR)
    env[n - nr:] = np.minimum(env[n - nr:], 0.7 * np.linspace(1, 0, nr))
    return y * env[:, None]


def brass_bus():
    buf = np.zeros((dsp.LOOP_SAMPLES, 2))
    for b in range(0, 24, 2):  # 2마디마다 롱 스웰(다음 스웰과 겹침)
        dsp.add_wrapped(buf, brass_note(BRASS[b], 6.4, 1.8, 2200), dsp.bar(b))
    for b in range(24, 32):  # 마디마다 짧은 스웰
        dsp.add_wrapped(buf, brass_note(BRASS[b], 3.0, 1.0, 1800), dsp.bar(b))
    for b in range(1, 24, 2):  # 스웰이 없는 마디 첫 박의 스탭 악센트
        dsp.add_wrapped(buf, brass_note(BRASS[b], 0.35, 0.15, 1500, rel=0.15, att=0.02), dsp.bar(b), 0.6)
    return buf


def chord_take(root, bars, cents, pan, pos, wobble):
    """파워코드 한 테이크 (n,2): 근음·5도·옥타브 톱니 유니즌 -> 디스토션 2단 -> 캐비닛. 서스테인만, 피킹 노이즈 없음."""
    n = int((bars * dsp.BAR + 0.3) * SR)
    t = np.arange(n) / SR
    y = np.zeros(n)
    for mult in (1.0, 1.5, 2.0):
        for c in (-6, 0, 6):
            y += dsp.saw(root * mult * 2 ** ((cents + c) / 1200), n)
    y = dsp.soft_clip(dsp.soft_clip(y / 9, 8), 3)
    y = dsp.highpass(y, 70)
    y = dsp.lowpass(y, 4500)
    y = peaking(y, 1500, 4, 0.8)
    f, ph = wobble
    y *= dsp.gain_db(np.sin(2 * np.pi * dsp.loop_freq(f) * (pos + np.arange(n)) / SR + ph))  # ±1dB
    y *= dsp.adsr(n, 0.05, 0.05, 1.0, 0.3)
    return dsp.pan(y, pan)


def power_bus():
    buf = np.zeros((dsp.LOOP_SAMPLES, 2))
    r = dsp.rng("raid/guitar")
    b = 0
    while b < 32:
        bars = 1 if b >= 24 else 2
        for shift, cents, p in ((-0.007, -6, -0.7), (0.007, 6, 0.7)):
            pos = dsp.bar(b) + int(shift * SR)
            wobble = (r.uniform(0.2, 0.5), r.uniform(0, 2 * np.pi))
            dsp.add_wrapped(buf, chord_take(CHORD[b], bars, cents, p, pos, wobble), pos)
        b += bars
    return buf


def kick_events(b, gallop):
    """기본 드럼 위에 얹는 킥 · 스네어 추가분 (종류, 스텝, dB): 킥 갤럽. 기본 드럼의 킥 0 · 10, 스네어 8은 제외."""
    ev = [("k", 2, -4), ("k", 3, -6), ("k", 6, -4), ("k", 7, -6), ("k", 14, -4), ("k", 15, -6), ("s", 5, -14), ("s", 13, -14)]
    if gallop:
        ev.append(("k", 12, -4))
    if b >= 24:
        ev.append(("k", 11, -5))
    return sorted(ev, key=lambda e: e[1])


def hihat(r):
    """16분 클로즈드 하이햇: 5~9kHz 노이즈 짧은 감쇠 -> 7kHz 로우패스. 타격마다 대역 · 감쇠 ±15%."""
    n = int(0.06 * SR)
    band = (5000 * r.uniform(0.85, 1.15), 9000 * r.uniform(0.85, 1.15))
    return dsp.lowpass(dsp.bandpass(dsp.noise(n, r), band), 7000) * dsp.exp_decay(n, 0.05 / 4.6 * r.uniform(0.85, 1.15))


def drum_bus():
    """습격 드럼 레이어 (LOOP_SAMPLES,2): 기본 하프타임 드럼과 합쳐 킥이 갤럽으로 얹히는 더블타임 + 16분 하이햇 · 탐 필인.
    킥 · 스네어 추가분은 kick_events(시드 raid/drums3 · raid/vary3), 마디 b%4==2는 하이햇 32분 롤(시드 raid/drums2 · raid/vary2)."""
    r = dsp.rng("raid/drums")
    v = dsp.rng("raid/vary")
    r2 = dsp.rng("raid/drums2")
    v2 = dsp.rng("raid/vary2")
    r3 = dsp.rng("raid/drums3")
    v3 = dsp.rng("raid/vary3")
    ra = dsp.rng("raid/accent")  # 첫 박 강세 전용 시드(다른 타격의 난수열을 건드리지 않는다)
    va = dsp.rng("raid/accent_vary")
    hat = lambda g: hihat(v)
    tom = lambda hz: lambda g: drums.tom(hz, v, g)
    hat2 = lambda g: hihat(v2)
    buf = np.zeros((dsp.LOOP_SAMPLES, 2))

    def hit(synth, b, step, vel_db=0.0, p=0.0, jitter=0.004, r=r, half=False):
        pos = dsp.bar(b) + step * drums.STEP + half * drums.STEP // 2 + round(r.uniform(-jitter, jitter) * SR)
        g = vel_db + r.uniform(-1.5, 1.5)
        dsp.add_wrapped(buf, dsp.pan(synth(g), p), pos, dsp.gain_db(g))

    for b in range(32):
        roll = b % 8 == 7
        taken = range(8, 16) if roll else range(12, 16) if b % 4 == 3 else ()  # 필인이 차지한 스텝
        intro = INTRO_DB if b < 4 else 0
        for step in range(16):
            if step not in taken:
                hit(hat, b, step, -6 + HAT_DB[step % 4] + (HAT_DOWNBEAT_DB if step == 0 else 0), r.uniform(-0.4, 0.4), 0.003)
        gallop = b % 4 == 1  # 홀수 마디의 비필인 마디: 처킹 마지막 박이 12 · 14 · 15
        for kind, step, db in kick_events(b, gallop):
            if step not in taken:
                if kind == "k":
                    hit(lambda g: drums.kick(v3, g), b, step, db + intro, r=r3)
                else:
                    hit(lambda g: drums.snare(v3, g), b, step, db + intro, r3.uniform(-0.1, 0.1), r=r3)
        hit(lambda g: drums.kick(va, g), b, 0, DOWNBEAT_KICK_DB + intro, r=ra)  # 마디 첫 박 강세: 기본 드럼의 스텝 0 킥에 겹침(리듬 불변)
        if b % 4 == 2:  # 마지막 박 16분 하이햇 사이에 32분 타를 끼워 롤
            for i, step in enumerate(range(12, 16)):
                hit(hat2, b, step, -10 + 8 * i / 3, r2.uniform(-0.4, 0.4), 0.002, r2, True)
        if roll:
            for i, hz in enumerate(np.geomspace(drums.TOM_HZ[0], drums.TOM_HZ[-1], 8)):
                hit(tom(hz), b, 8 + i, -14 + 12 * i / 7, drums.tom_pan(hz) + r.uniform(-0.1, 0.1))
        elif taken:
            for i, hz in enumerate(drums.TOM_HZ):
                hit(tom(hz), b, 12 + i, drums.TOM_DB[i], drums.tom_pan(hz) + r.uniform(-0.1, 0.1))
    return 0.8 * buf + 0.2 * dsp.circular_reverb(buf, dsp.make_ir(1.0, 4000, dsp.rng("raid/drum_ir")))


def chug_steps(b):
    """마디 b의 베이스 처킹 (스텝, 길이): 8분 처킹, 홀수 마디는 마지막 박을 갤럽(12 · 14 · 15), 탐 필인 마디는 마지막 박을 쉰다."""
    steps = [(s, ACCENT_LEN if s == 0 else CHUG_LEN) for s in range(0, 12, 2)]
    if b % 4 == 3:
        return steps
    if b % 2 == 1:
        return steps + [(12, GALLOP_LEN), (14, SIXTEENTH_LEN), (15, SIXTEENTH_LEN)]
    return steps + [(12, CHUG_LEN), (14, CHUG_LEN)]


def bass_chug(f, dur, vel_db, r):
    """디스토션 베이스 처그 한 발(모노): 톱니 + 사각파 + 약한 사인 서브 + 어택 클릭 -> 디스토션 2단 -> 110Hz 하이패스 · 3.2kHz 로우패스 · 800Hz 그라인드 -> 게이트.
    피치 ±2센트, 감쇠 ±15%, 클릭 ±30%."""
    n = int((dur + 0.03) * SR)
    t = np.arange(n) / SR
    f *= 2 ** (r.uniform(-2, 2) / 1200)
    y = dsp.saw(f, n) + 0.5 * dsp.square(f, n) + 0.2 * dsp.sine(f, n)
    y /= np.max(np.abs(y))
    y += 0.2 * r.uniform(0.7, 1.3) * dsp.highpass(dsp.noise(n, r), 3000) * dsp.exp_decay(n, 0.003)
    y = dsp.soft_clip(dsp.soft_clip(y, 7), 3)
    y = peaking(dsp.lowpass(dsp.highpass(y, 110), 3200), 800, 4)
    gate = dsp.exp_decay(n, dur / 3 * r.uniform(0.85, 1.15)) * np.clip(t / 0.002, 0, 1) * np.clip((dur + 0.03 - t) / 0.03, 0, 1)
    return y * gate * dsp.gain_db(vel_db)


def bass_chug_bus(bars=range(32)):
    """디스토션 베이스 처킹 버스(센터): 8분 처킹 리듬 · 브라스 근음(chug_steps · BRASS). 타격마다 개별 합성, 타이밍 ±4ms."""
    buf = np.zeros((dsp.LOOP_SAMPLES, 2))
    r = dsp.rng("raid/bass")
    for b in bars:
        for step, dur in chug_steps(b):
            vel = r.uniform(-1.5, 1.5) + (2 if step == 0 else 0) + (INTRO_DB if b < 4 else 0)
            pos = dsp.bar(b) + step * drums.STEP + round(r.uniform(-0.004, 0.004) * SR)
            dsp.add_wrapped(buf, bass_chug(BRASS[b], dur, vel, r), pos)
    return buf


def main():
    t0 = time.time()
    brass = brass_bus()
    power = dsp.highpass(power_bus(), 100)  # 100Hz 이하는 브라스 근음만
    rms = lambda x: np.sqrt(np.mean(x ** 2))
    mix = brass + power * (rms(brass) * dsp.gain_db(POWER_DB) / rms(power))
    ir = dsp.make_ir(1.2, 3500, dsp.rng("raid/ir"))
    mix = (1 - WET) * mix + WET * dsp.circular_reverb(mix, ir)
    bass = bass_chug_bus()
    chug = bass * (rms(brass) * dsp.gain_db(BASS_CHUG_DB) / rms(bass))
    ir_chug = dsp.make_ir(0.8, 3500, dsp.rng("raid/chug_ir"))
    mix += (1 - CHUG_WET) * chug + CHUG_WET * dsp.circular_reverb(chug, ir_chug)
    drum = drum_bus()
    mix += drum * (rms(brass) * dsp.gain_db(DRUM_DB) / rms(drum))
    ceil = dsp.gain_db(LEVEL_PEAK_DB)
    soft = lambda x: ceil * np.tanh(x / ceil)  # 드럼 트랜지언트 피크만 눌러 RMS를 유지
    g = dsp.gain_db(LEVEL_RMS_DB) / rms(mix)
    for _ in range(4):
        g *= dsp.gain_db(LEVEL_RMS_DB) / rms(soft(mix * g))
    mix = soft(mix * g)
    dsp.write_wav(OUT, mix, loop=True)
    print(f"wrote {OUT} ({time.time() - t0:.1f}s)")
    return check.main(["--stem", str(OUT)])


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
