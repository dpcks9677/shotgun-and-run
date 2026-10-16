"""WAV 검증: python check.py [--oneshot | --stem] file.wav ... / python check.py --selftest"""
import sys

import numpy as np
import soundfile as sf

from dsp import LOOP_SAMPLES, SR, db


def check(x, info, oneshot, stem=False):
    """(이름, 통과, 설명) 목록. info는 sf.info 결과 또는 None(길이 검사 생략). stem이면 첫 박에 타격이 오는 리듬 스템이라 seam_rms는 참고값."""
    res = []
    if info is not None:
        res.append(("format", info.samplerate == SR and info.subtype == "PCM_16",
                    f"{info.samplerate} Hz {info.subtype}"))
    n = len(x)
    x = x.reshape(n, -1)
    peak = db(np.max(np.abs(x)))
    res.append(("peak", peak <= -1, f"{peak:.1f} dBFS (<= -1)"))
    res.append(("rms", True, f"{db(np.sqrt(np.mean(x ** 2))):.1f} dBFS"))
    if oneshot:
        ends = max(np.max(np.abs(x[0])), np.max(np.abs(x[-1])))
        res.append(("ends", ends < 0.01, f"start/end {ends:.4f} (< 0.01)"))
        res.append(("duration", 0.2 <= n / SR <= 6, f"{n / SR:.2f} s (0.2~6)"))
        return res
    if info is not None:
        res.append(("length", n == LOOP_SAMPLES, f"{n} (== {LOOP_SAMPLES})"))
    jump = np.max(np.abs(x[0] - x[-1]))
    limit = 3 * np.percentile(np.abs(np.diff(x, axis=0)), 99.9)
    res.append(("seam", jump <= limit, f"jump {jump:.5f} (<= {limit:.5f})"))
    w = int(0.05 * SR)
    ratio = (np.sqrt(np.mean(x[:w] ** 2)) + 1e-9) / (np.sqrt(np.mean(x[-w:] ** 2)) + 1e-9)
    res.append(("seam_rms", stem or 0.5 <= ratio <= 2, f"first/last 50ms RMS ratio {ratio:.2f} (0.5~2{', 참고' if stem else ''})"))
    return res


def report(label, res):
    for name, ok, msg in res:
        print(f"{'PASS' if ok else 'FAIL'} {label} {name}: {msg}")
    return all(ok for _, ok, _ in res)


def selftest():
    n = 4 * SR
    t = np.arange(n)
    good = 0.5 * np.sin(2 * np.pi * 100 * t / SR)  # 정수 주기(400)
    bad = 0.5 * np.sin(2 * np.pi * 100.125 * t / SR + np.pi / 2)  # 반 주기 어긋남
    ok_good = report("selftest/continuous", check(good, None, False))
    ok_bad = report("selftest/offset", check(bad, None, False))
    ok = ok_good and not ok_bad
    print("selftest", "OK" if ok else "FAILED")
    return ok


def main(args):
    if "--selftest" in args:
        return selftest()
    oneshot = "--oneshot" in args
    stem = "--stem" in args
    ok = True
    for path in (a for a in args if not a.startswith("--")):
        x, _ = sf.read(path, dtype="float64")
        ok &= report(path, check(x, sf.info(path), oneshot, stem))
    return ok


if __name__ == "__main__":
    sys.exit(0 if main(sys.argv[1:]) else 1)
