"""Tanker 십자 충격파 연출 스프라이트 — 계획서 Docs/tanker-shockwave-vfx-plan.md

python Docs/tools/spritegen/vfx/tanker_wave_vfx.py [시안.png] [--orc <Armored Orc.aseprite>] [--ref <vfx.aseprite>] [--gif]   # 시안(+GIF 2개)만 만든다 (기본: Docs/drafts/props/tanker-wave-draft.png)
python Docs/tools/spritegen/vfx/tanker_wave_vfx.py import [--ref <vfx.aseprite>]                                      # Assets/09.VFX/Enemy/TankerWave/ 에 시트 PNG를 쓴다 (SPRITEGEN_WRITE_ROOT로 리허설)

--orc는 시안에서 Tanker(Armored Orc Attack03)와 원본 fx를 보여 주는 용도다 (없으면 점으로 표시). 시트 생성에는 필요 없다.
--ref를 주면 파동을 불꽃(v06)이 아니라 **흙먼지 솟구침**(v07)으로 만든다: 사용자 레퍼런스 vfx.aseprite의 13~24프레임(대각선 돌 가시 + 연기)을 모양은 그대로 두고
흰색 · 베이지 6톤으로 바꾸고 모서리를 둥글려 연기처럼 만든 12프레임. 이 시트는 파동 하나가 이동하는 것이 아니라 **한 번의 솟구침**이라, 팔마다 3번(ERUPT_COUNT)만 벽(또는 30u 끝)까지 3등분한 거리에서 차례로 터뜨린다. 충격파 하나 = 같은 거리에서 함께 터지는 줄들(가로 2줄 · 세로 1줄).
방향: 왼쪽 = 좌우 반전, 위 · 아래 = **회전하지 않고 같은 스프라이트** (바닥에서 위로 솟는 모양이라 돌리면 어색하다). 가로 팔은 3u 폭을 2줄로 덮고 세로 팔은 한 줄이다.
이 모드에서는 불꽃색 Burst · WallHit를 쓰지 않는다 (시트에도 넣지 않음).

사용자 레퍼런스(폭발 컷): 밝은 핵에서 길게 뻗는 **불꽃 줄기**, 바닥에 남는 **검은 그을음**, 튀는 작은 파편.
요청 조건: 깔끔하게, **파동은 하나만**, 앞으로 나가며 **번쩍번쩍**. → 폭 3u(48px) 전체를 덮는 **불꽃 파동 하나**가 나가고, 16fps로 강 · 약 프레임이 교대해 번쩍인다.
파동 = 앞이 볼록한 밝은 호(앞 가장자리) + 호에서 뒤로 길게 흐르는 불꽃 줄기 7개 + 앞 중앙의 빛나는 핵. 파동이 지나간 자리에는 **그을음 띠** 한 줄이 남았다가 사라진다.
불꽃(파동 · 임팩트)은 **가산 합성**(`ShotgunAndRun/Sprite Additive`, 폭발 VFX와 같은 방식)이고, 그을음은 일반 합성(렌더러 알파 0.5)이다.
색은 폭발 VFX 팔레트(`pellet_vfx.py` / `grenade_vfx.py`의 FL_W · FL_Y · FL_O · FL_R + 가장 어두운 붉은색)를 그대로 쓴다. 원본 시트의 크림 색은 쓰지 않는다.

불꽃은 공중 · 바닥 평면에 흩어지는 빛이라 방향 구분 없이 **오른쪽 한 장만** 그리고, 왼쪽 = 좌우 반전, 위 · 아래 = 90° 회전(무손실)으로 쓴다.
바닥에 놓이는 링 · 타원은 세로 0.65배, 파동 폭 · 예고선은 판정 경계라 1:1. 길이 1u = 16px.
- Wave           48×48 4프레임  불꽃 파동 하나, 번쩍임 세기 1.0 · 0.4 · 0.8 · 0.3 (핵 = 캔버스 x 36 · y 24)
- Scorch_R(16×48) / Scorch_V(48×16) 1프레임  그을음 띠 1u 타일 (Tiled로 늘림, 일반 합성)
- Telegraph_R(32×48) / _U(48×32) 2프레임  예고선 타일 (2u 주기). 왼쪽 = R 좌우 반전, 아래 = U 상하 반전
- Burst     96×96 6프레임            시전 임팩트 (핵 + 십자 4방향 불꽃 줄기 다발)
- WallHit   48×48 5프레임            벽 도달 임팩트 (핵 + 사방으로 튀는 불꽃 줄기)
"""
import math
import os
import struct
import sys
import time
import zlib

from PIL import Image, ImageChops, ImageDraw, ImageFilter

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import grenade_vfx as gv  # noqa: E402  (같은 폴더: 폰트 · 바닥 합성 재사용)
import paths  # noqa: E402

VERSION = 'v07'

# 폭발 VFX 팔레트 (grenade_vfx.LAVA = 어두운 붉은색 · FL_R · FL_O · FL_Y · FL_W). 불꽃 파동 · 임팩트는 이 5톤만 쓴다
FIRE = gv.LAVA
FL_DK, FL_R, FL_O, FL_Y, FL_W = FIRE
SOOT = (24, 17, 15)            # 그을음 띠 (바닥 평균 휘도 약 38보다 어둡게)
SOOT_E = (32, 24, 21)          # 그을음 가장자리 한 단계
RED = (178, 52, 40)            # 예고선 전용 붉은색 (불꽃과 겹치지 않게 어둡고 채도 낮은 계열)
RED_DK = (98, 27, 15)
# 흙먼지 솟구침 (--ref): 어두운 흙색 → 베이지 → 크림 → 흰색 6톤. 원본 명도가 DUST_EDGES 경계로 나뉘어 톤이 된다
DUST_TONES = [(112, 98, 82), (150, 132, 108), (190, 170, 140), (222, 206, 176), (240, 230, 208), (255, 255, 255)]
DUST_EDGES = (70, 100, 125, 165, 200)
ERUPT_FRAMES = range(12, 24)   # vfx.aseprite의 13~24프레임 (0부터 센 번호)
SMOOTH_SIGMA, SMOOTH_CORE, SMOOTH_FRINGE, TONE_SIGMA = 1.3, 0.40, 0.18, 1.2   # 윤곽을 둥글리는 정도: 덩어리 경계 · 가장자리 잔연기 문턱 · 톤 번짐
ERUPT_FPS = 18                 # 원본 프레임 간격 55ms
ERUPT_COUNT = 3                # 팔마다 터지는 충격파 수. 벽(또는 30u 끝)까지 3등분한 거리에서 차례로 터지고 마지막이 끝에 닿는다
ERUPT_LANES = 2                # 가로 팔이 3u 폭을 덮는 줄 수 (세로 팔은 한 줄). 같은 거리의 줄들이 함께 터져 충격파 하나가 된다
ERUPT_HOLD, ERUPT_FADE = 0.15, 0.45   # 마지막 프레임 유지 · 사라지는 시간 (초)
PALETTE = set(FIRE) | {SOOT, SOOT_E, RED, RED_DK} | set(DUST_TONES)

GROUND_V = gv.GROUND_V         # 바닥에 놓이는 링의 세로 비율 (수류탄 범위 표시와 같은 0.65)
BAND = 48                      # 충격파 폭 3u
TELE_W, TELE_H = 32, 48
BURST = 96
WALLHIT = 48
FRAMES = {'Wave': 4, 'Telegraph': 2, 'Burst': 6, 'WallHit': 5}
WAVE = 48                      # 파동 캔버스 (정사각)
FRONT = 36                     # 파동 핵의 캔버스 x (왼쪽으로 불꽃 줄기가 흐른다)
SCORCH_ALPHA = 0.5
WAVE_FPS = 16                  # 번쩍임 재생 속도 (강 · 약 · 중 · 약 교대)
FLASH = (1.0, 0.4, 0.8, 0.3)   # 프레임별 번쩍임 세기
WAVES = 1                      # 파동 수 (사용자: 하나만)
WAVE_GAP = 0.1                 # 파동이 여럿일 때 출발 간격 (초)
WAVE_ALPHA = (1.0,)              # 파동별 렌더러 알파
SPEED = 40 * 16                # 전파 속도 40u/초 (px/초) — 초안
TELEGRAPH_TIME = 1.2           # 초안
REF = None                     # --ref: 사용자 레퍼런스 vfx.aseprite 경로. 주면 파동이 흙먼지 솟구침이 된다


def canvas(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c + (255,))


def hnoise(ix, iy, seed):
    return gv._hash(ix, iy, seed)


def ease_out(t):
    return gv.ease_out(t)


def transpose_up(im):
    """H(오른쪽으로 진행) → V(위로 진행). 90° 전치 — 격자가 깨지지 않는다 (바닥에 그린 예고선 전용)"""
    w, h = im.size
    out = canvas(h, w)
    for yv in range(w):
        for xv in range(h):
            out.putpixel((xv, yv), im.getpixel((w - 1 - yv, xv)))
    return out


# ================================================================ 불꽃 파동

def flame(im, ox, oy, dx, dy, length, w0, dim=0):
    """(ox, oy)에서 방향 (dx, dy)로 길게 뻗는 끝이 가는 불꽃 줄기. 색: 머리 크림-노랑 → 노랑 → 주황 → 붉은 꼬리 끝. dim = 색을 그만큼 어둡게(번쩍임의 약한 프레임)"""
    steps = max(2, int(length * 2))
    px, py = -dy, dx
    for k in range(steps + 1):
        t = k / steps
        w = max(0.5, w0 * (1 - t) ** 0.9)
        idx = 4 if t < 0.14 else 3 if t < 0.42 else 2 if t < 0.75 else 1 if t < 0.93 else 0
        c = FIRE[max(0, idx - dim)]
        cx, cy = ox + dx * length * t, oy + dy * length * t
        for j in range(-int(w), int(w) + 1):
            put(im, round(cx + px * j), round(cy + py * j), c)


def disc(im, cx, cy, r, c):
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if x * x + y * y <= r * r + r // 2:
                put(im, cx + x, cy + y, c)


def wave_frame(i):
    """오른쪽으로 나가는 불꽃 파동 하나: 앞이 볼록한 밝은 호 + 호에서 뒤로 흐르는 줄기 7개(가운데가 가장 길다) + 앞 중앙의 핵과 십자 반짝임.
    프레임마다 번쩍임 세기(FLASH)가 달라 강 · 약이 교대한다: 강한 프레임은 핵 · 반짝임 · 줄기가 크고 밝고, 약한 프레임은 작고 한 단계 어둡다"""
    im = canvas(WAVE, WAVE)
    f = FLASH[i]
    dim = 0 if f > 0.7 else 1
    for k in range(7):
        v = 9 + k * (30 / 6)
        dv = (v - 23.5) / 24
        s_ = 1 - dv * dv
        ln = (12 + 22 * s_ ** 0.8) * (0.55 + 0.45 * f)
        tilt = dv * 0.3                                # 양 끝 줄기는 바깥쪽으로 살짝 벌어진다
        flame(im, FRONT - 12 * dv * dv, v, -math.cos(tilt), math.sin(tilt), ln, 1.2 + 1.6 * s_, dim)
    for y in range(2, WAVE - 2):                        # 앞 가장자리 호: 폭 3u의 경계
        dv = (y - 23.5) / 24
        x = round(FRONT - 12 * dv * dv)
        put(im, x, y, FIRE[4 - dim])
        put(im, x - 1, y, FIRE[3 - dim])
    r = 2 + round(3 * f)
    disc(im, FRONT, 24, r + 2, FIRE[3 - dim])
    disc(im, FRONT, 24, r, FIRE[4 - dim])
    for n in range(r + 3, min(r + 3 + round(3 + 6 * f), WAVE - 2 - FRONT)):    # 십자 반짝임: 강한 프레임에서 길게 (캔버스 안)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            put(im, FRONT + dx * n, 24 + dy * n, FIRE[4 - dim] if n < r + 6 else FIRE[3 - dim])
    return im


def scorch_tile(direction):
    """파동이 지나간 자리에 남는 그을음 띠 1u 타일: 한 톤 + 가장자리 한 단계, 위아래 가장자리만 들쭉날쭉 (길이 방향 16px 주기). 잡티 없음"""
    im = canvas(16, BAND)
    for a in range(16):
        top = 2 + round(hnoise(a // 2, 1, 951) * 2)
        bot = BAND - 3 - round(hnoise(a // 2, 2, 952) * 2)
        for v in range(top, bot + 1):
            put(im, a, v, SOOT_E if v < top + 2 or v > bot - 1 else SOOT)
    return im if direction == 'R' else im.transpose(Image.TRANSPOSE)


# ================================================================ 예고선 (바닥 표시 — 판정 경계라 눌리지 않은 1:1)

def telegraph_frame(i):
    """점선 테두리 + 앞으로 향한 화살표(진행 방향, 점 장식 없음). 2프레임: 점선 위상과 화살표 밝기가 바뀌며 점멸. 렌더러 알파 0.7로 쓴다"""
    im = canvas(TELE_W, TELE_H)
    ph = 8 * i
    for x in range(TELE_W):
        if ((x + ph) % 16) < 8:
            for y in (0, 1, TELE_H - 2, TELE_H - 1):
                put(im, x, y, RED)
    arrow = RED if i == 0 else RED_DK
    for dy in range(-10, 11):
        x = 22 - round(abs(dy) * 0.85)
        for k in range(3):
            put(im, x - k, 24 + dy, arrow)
    return im


# ================================================================ 임팩트 (섬광 · 링 · 속도선 · 파편)

def burst_frame(i):
    """착지 순간: 밝은 핵 + 십자 4방향으로 뻗는 불꽃 줄기 다발(방향마다 5줄, 가운데가 가장 길다). 핵은 점점 작아지고 줄기는 끝에서 짧아진다"""
    im = canvas(BURST, BURST)
    t = (i + 1) / FRAMES['Burst']
    e = ease_out(min(1.0, t * 1.2)) * (1.0 if t < 0.7 else 0.75)
    ci = BURST // 2
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for ang in (-0.3, -0.15, 0.0, 0.15, 0.3):
            ca, sa = math.cos(ang), math.sin(ang)
            ux, uy = dx * ca - dy * sa, dx * sa + dy * ca
            flame(im, ci + ux * 6, ci + uy * 6, ux, uy, (22 + 14 * ca ** 3) * e, 2.4 if ang == 0 else 1.5)
    r = max(2, round(9 * (1 - 0.7 * t)))
    disc(im, ci, ci, r + 3, FL_Y)
    disc(im, ci, ci, r, FL_W)
    for n in range(r + 4, r + 4 + round(14 * (1 - t))):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            put(im, ci + dx * n, ci + dy * n, FL_W)
    return im


def wallhit_frame(i):
    """벽에 닿아 터짐: 밝은 핵 → 사방으로 뻗는 불꽃 줄기 8개 → 짧아지며 사라짐"""
    im = canvas(WALLHIT, WALLHIT)
    t = (i + 1) / FRAMES['WallHit']
    e = ease_out(min(1.0, t * 1.3))
    ci = WALLHIT // 2
    for k in range(8):
        a = (k + 0.3 * hnoise(k, 1, 901)) * math.tau / 8
        flame(im, ci + math.cos(a) * 2, ci + math.sin(a) * 2, math.cos(a), math.sin(a), (6 + 10 * e) * (1 - 0.45 * t) * (0.8 + 0.4 * hnoise(k, 2, 902)), 1.4)
    disc(im, ci, ci, max(2, 6 - i), FL_Y if i < 3 else FL_O)       # 중앙 핵: 점점 작아지고 식는다
    if i < 3:
        disc(im, ci, ci, max(1, 4 - i), FL_W)
    return im


def hstrip(frames):
    fw, fh = frames[0].size
    im = canvas(fw * len(frames), fh)
    for i, f in enumerate(frames):
        im.paste(f, (i * fw, 0))
    return im


def sheets():
    """{파일 이름: 프레임 목록}"""
    out = {}
    out['TankerWave'] = erupt_frames() if REF else [wave_frame(i) for i in range(FRAMES['Wave'])]
    out['TankerWaveScorch_R'] = [scorch_tile('R')]
    out['TankerWaveScorch_V'] = [scorch_tile('V')]
    tele = [telegraph_frame(i) for i in range(FRAMES['Telegraph'])]
    out['TankerWaveTelegraph_R'] = tele
    out['TankerWaveTelegraph_U'] = [transpose_up(f) for f in tele]
    if not REF:
        out['TankerWaveBurst'] = [burst_frame(i) for i in range(FRAMES['Burst'])]
        out['TankerWaveWallHit'] = [wallhit_frame(i) for i in range(FRAMES['WallHit'])]
    return out


def build():
    d = paths.repo('Assets', '09.VFX', 'Enemy', 'TankerWave')
    return {os.path.join(d, name + '.png'): hstrip(fr) for name, fr in sheets().items()}


# ================================================================ 검사

def check():
    problems = []
    for name, frames in sheets().items():
        w, h = frames[0].size
        if w % 16 or h % 16:
            problems.append(f'{name}: 캔버스 {w}×{h}이 16 배수가 아님')
        for i, f in enumerate(frames):
            if f.size != (w, h):
                problems.append(f'{name} {i}: 프레임 크기가 다름')
            if not set(a for *_, a in f.get_flattened_data()) <= {0, 255}:
                problems.append(f'{name} {i}: 반투명 픽셀 (불투명 도트만 쓴다)')
            for px in f.get_flattened_data():
                if px[3] and px[:3] not in PALETTE:
                    problems.append(f'{name} {i}: 팔레트 밖 색 {px[:3]}')
                    break
            bb = f.getbbox()
            if bb is None:
                problems.append(f'{name} {i}: 빈 프레임')
                continue
            if 'Telegraph' not in name and 'Scorch' not in name and (bb[0] < 1 or bb[1] < 1 or bb[2] > w - 1 or bb[3] > h - 1):
                problems.append(f'{name} {i}: 캔버스 가장자리에 닿음 {bb}')
    return problems


# ================================================================ 시안 합성

GAME = {
    'W': 560, 'H': 400, 'O': (260, 200),
    'wall': {'R': 11 * 16, 'L': 9 * 16, 'U': 6 * 16, 'D': None},      # 위 · 좌 · 우는 벽이 가깝고, 아래는 벽이 없다(화면 밖까지)
}
ORC_SCALE = 1.8


def read_ase(path):
    """RGBA .aseprite → (캔버스 크기, 레이어 [(이름, 보이는가)], 셀 {(프레임, 레이어): (x, y, 이미지)}). 연결 셀은 원본 셀을 그대로 가리킨다"""
    d = open(path, 'rb').read()
    nframes, cw0, ch0 = struct.unpack_from('<HHH', d, 6)
    off = 128
    layers, cels = [], {}
    for f in range(nframes):
        fsize = struct.unpack_from('<I', d, off)[0]
        n = struct.unpack_from('<I', d, off + 12)[0] or struct.unpack_from('<H', d, off + 6)[0]
        pos = off + 16
        for _ in range(n):
            csize, ctype = struct.unpack_from('<IH', d, pos)
            b = pos + 6
            if ctype == 0x2004:
                nl = struct.unpack_from('<H', d, b + 16)[0]
                layers.append((d[b + 18:b + 18 + nl].decode('utf8'), bool(struct.unpack_from('<H', d, b)[0] & 1)))
            elif ctype == 0x2005:
                li, x, y, _op, ct = struct.unpack_from('<HhhBH', d, b)
                if ct in (0, 2):
                    cw, ch = struct.unpack_from('<HH', d, b + 16)
                    raw = d[b + 20:pos + csize]
                    raw = zlib.decompress(raw) if ct == 2 else raw
                    cels[(f, li)] = (x, y, Image.frombytes('RGBA', (cw, ch), raw))
                elif ct == 1:
                    cels[(f, li)] = cels[(struct.unpack_from('<H', d, b + 16)[0], li)]
            pos += csize
        off += fsize
    return (cw0, ch0), layers, cels


def merge_ase(ase, f, keep):
    """프레임 f를 keep(레이어 이름, 보이는가)을 만족하는 레이어만 아래부터 합친다"""
    size, layers, cels = ase
    fr = canvas(*size)
    for li, (name, visible) in enumerate(layers):
        if keep(name, visible) and (f, li) in cels:
            x, y, im = cels[(f, li)]
            fr.alpha_composite(im, (x, y))
    return fr


def read_orc(path):
    """Armored Orc.aseprite → (Attack03 29~37 body+shadow 9프레임, 원본 fx 18 · 19 · 26~28 5프레임). fx는 이 연출이 대신하므로 Tanker에는 넣지 않는다"""
    ase = read_ase(path)
    return ([merge_ase(ase, f, lambda n, v: n in ('shadow', 'body')) for f in range(29, 38)],
            [merge_ase(ase, f, lambda n, v: n == 'fx') for f in (18, 19, 26, 27, 28)])


# ================================================================ 흙먼지 솟구침 (--ref)

def to_dust(frame):
    """원본 프레임 → 흙먼지: 명도로 DUST_TONES 6톤에 대응시키고, 윤곽 · 면 줄무늬를 블러로 풀어 연기 덩어리로 둥글린다.
    알파 마스크를 블러해 문턱(SMOOTH_CORE)으로 다시 자르고, 문턱 아래 SMOOTH_FRINGE까지는 성긴 디더링 잔연기로 남긴다"""
    w, h = frame.size
    px = list(frame.get_flattened_data())
    tone = [sum((0.299 * r + 0.587 * g + 0.114 * b) >= e for e in DUST_EDGES) if a else 0 for r, g, b, a in px]
    alpha = Image.new('L', (w, h))
    alpha.putdata([255 if p[3] else 0 for p in px])
    tone_a = Image.new('L', (w, h))
    tone_a.putdata([t * 51 if p[3] else 0 for t, p in zip(tone, px)])         # 톤 5 → 255
    ab = list(alpha.filter(ImageFilter.GaussianBlur(SMOOTH_SIGMA)).get_flattened_data())
    tb = list(tone_a.filter(ImageFilter.GaussianBlur(TONE_SIGMA)).get_flattened_data())
    wb = list(alpha.filter(ImageFilter.GaussianBlur(TONE_SIGMA)).get_flattened_data())
    bayer = ((0, 2), (3, 1))
    out = canvas(w, h)
    for i in range(w * h):
        x, y = i % w, i // w
        a = ab[i] / 255
        if a < SMOOTH_FRINGE:
            continue
        t = tb[i] / max(wb[i], 1) * 5                                          # 알파 가중 평균 톤 (0~5)
        t = max(0, min(5, round(t - (1 - min(1, a / 0.9)) * 1.2)))              # 바깥쪽일수록 어둡게
        if a >= SMOOTH_CORE:
            out.putpixel((x, y), DUST_TONES[t] + (255,))
        elif (a - SMOOTH_FRINGE) / (SMOOTH_CORE - SMOOTH_FRINGE) > bayer[y % 2][x % 2] / 4:
            out.putpixel((x, y), DUST_TONES[min(t, 1)] + (255,))
    return out


def erupt_frames():
    """--ref의 13~24프레임(back과 숨김 레이어 제외)을 흙먼지 12프레임으로"""
    ase = read_ase(REF)
    return [to_dust(merge_ase(ase, f, lambda n, v: v and n != 'back')) for f in ERUPT_FRAMES]


def erupt_anchor(frame):
    """솟구침이 서는 자리(바닥 중심): 맨 아래 10줄의 평균 x, 바닥선은 맨 아래에서 3칸 위"""
    x0, y0, x1, y1 = frame.getbbox()
    xs = [x for y in range(y1 - 10, y1) for x in range(x0, x1) if frame.getpixel((x, y))[3]]
    return round(sum(xs) / len(xs)), y1 - 3


def font(size, bold=True):
    return gv.font(size, bold)


def fade(im, k):
    if k >= 1:
        return im
    im = im.copy()
    im.putalpha(im.getchannel('A').point(lambda a: int(a * k)))
    return im


def mirrored(f, flip):
    return f.transpose(Image.FLIP_LEFT_RIGHT) if flip else f


_CACHE = {}


def sheets_cached():
    if 'S' not in _CACHE:
        _CACHE['S'] = sheets()
    return _CACHE['S']


def additive_paste(scene, sp, x, y, mul=1.0):
    """가산 합성: 장면 위에 스프라이트 색 × 알파 × mul을 더한다 (ShotgunAndRun/Sprite Additive와 같은 결과)"""
    box = (max(0, x), max(0, y), min(scene.width, x + sp.width), min(scene.height, y + sp.height))
    if box[0] >= box[2] or box[1] >= box[3]:
        return
    layer = Image.new('RGBA', sp.size, (0, 0, 0, 0))
    layer.paste(sp, mask=sp.getchannel('A').point(lambda a: int(a * mul)))
    layer = layer.crop((box[0] - x, box[1] - y, box[2] - x, box[3] - y))
    region = ImageChops.add(scene.crop(box), layer)
    scene.paste(region, box)


def oriented(im, dr):
    """오른쪽 기준 이미지를 방향에 맞게: 왼쪽 = 좌우 반전, 위 = 반시계 90°, 아래 = 시계 90° (모두 무손실)"""
    return {'R': im, 'L': im.transpose(Image.FLIP_LEFT_RIGHT), 'U': im.rotate(90, expand=True), 'D': im.rotate(-90, expand=True)}[dr]


def erupt_items(frames, T_, dr, Ln, origin):
    """한 팔의 충격파 ERUPT_COUNT개 [(바닥 y, -거리, 이미지, 붙일 x, y)]. 벽까지 3등분한 거리에서 거리/SPEED 뒤에 차례로 12프레임을 재생하고, 끝나면 잠시 유지했다 사라진다.
    가로 팔은 충격파마다 ERUPT_LANES줄(1u 간격)이 같은 거리에서 함께 터져 3u 폭을 채우고 세로 팔은 한 줄. 왼쪽만 좌우 반전하고 위 · 아래는 회전하지 않는다"""
    ox, oy = origin
    ax, ay = erupt_anchor(frames[0])
    flip = dr == 'L'
    if flip:
        ax = frames[0].width - 1 - ax
    vx, vy = {'R': (1, 0), 'L': (-1, 0), 'U': (0, -1), 'D': (0, 1)}[dr]
    horiz = dr in 'RL'
    lanes = range(ERUPT_LANES) if horiz else (0,)
    items = []
    for lane in lanes:
        side = round((lane - (ERUPT_LANES - 1) / 2) * 16) if horiz else 0
        for k in range(ERUPT_COUNT):
            dist = Ln * (k + 1) // ERUPT_COUNT
            t = T_ - dist / SPEED - hnoise(k, lane, 971) * 0.03
            if t < 0:
                continue
            i = int(t * ERUPT_FPS)
            alpha = 1.0
            if i >= len(frames):
                alpha = 1 - (t - len(frames) / ERUPT_FPS - ERUPT_HOLD) / ERUPT_FADE
                if alpha <= 0:
                    continue
                i = len(frames) - 1
            cx, cy = ox + vx * dist, oy + vy * dist + side
            items.append((cy, -dist, fade(mirrored(frames[i], flip), min(1.0, alpha)), cx - ax, cy - ay))
    return items


def compose(T_, orc=None, game=GAME):
    """T_초 시점의 십자 파동 합성. T_ < 0이면 예고, T_ = 0이 착지"""
    Sh = sheets_cached()
    W, H = game['W'], game['H']
    ox, oy = game['O']
    scene = gv.floor(W, H, seed=5).convert('RGBA')
    d = ImageDraw.Draw(scene)
    walls = game['wall']
    lens = {dr: min(30 * 16 if dist is None else dist, 30 * 16) for dr, dist in walls.items()}
    vec = {'R': (1, 0), 'L': (-1, 0), 'U': (0, -1), 'D': (0, 1)}
    core = FRONT - WAVE // 2                           # 파동 캔버스 중심에서 핵까지 (진행 방향)
    over = []
    erupts = []
    for dr in [x for x in 'ULRD' if x in game.get('arms', 'RLUD')]:
        Ln = lens[dr]
        horiz = dr in 'RL'
        if T_ < 0:
            tf = Sh['TankerWaveTelegraph_R' if horiz else 'TankerWaveTelegraph_U'][int((T_ + TELEGRAPH_TIME) * 6) % 2]
            tf = mirrored(tf, dr == 'L') if horiz else (tf.transpose(Image.FLIP_TOP_BOTTOM) if dr == 'D' else tf)
            tl = tf.width if horiz else tf.height
            for k in range((Ln + tl - 1) // tl):
                fr = (k + 1) * tl
                x = ox + (fr - tl if dr == 'R' else -fr) if horiz else ox - 24
                y = oy - 24 if horiz else (oy - fr if dr == 'U' else oy + fr - tl)
                scene.alpha_composite(fade(tf, 0.7), (x, y))
            continue
        t_arr = Ln / SPEED
        reach0 = int(min(Ln, SPEED * T_))
        ks = 1.0 if T_ < t_arr + 0.6 else max(0.0, 1 - (T_ - t_arr - 0.6) / 0.6)      # 그을음: 도달 뒤 잠시 남았다가 사라진다
        sc = Sh['TankerWaveScorch_R' if horiz else 'TankerWaveScorch_V'][0]
        for fr in range(16, reach0 - 8 + 1, 16):
            if ks > 0:
                x, y = ((ox + fr - 16 if dr == 'R' else ox - fr), oy - 24) if horiz else (ox - 24, oy - fr if dr == 'U' else oy + fr - 16)
                scene.alpha_composite(fade(sc, SCORCH_ALPHA * ks), (x, y))
        if REF:
            erupts += erupt_items(Sh['TankerWave'], T_, dr, Ln, (ox, oy))
            continue
        for w in reversed(range(WAVES)):                # 뒤 파동부터 가산
            tw = T_ - WAVE_GAP * w
            if tw <= 0:
                continue
            reach = int(min(Ln, SPEED * tw))
            k = 1.0 if tw < t_arr else max(0.0, 1 - (tw - t_arr) / 0.15)       # 벽에 닿으면 빠르게 사라진다
            if k <= 0:
                continue
            im = oriented(Sh['TankerWave'][int(tw * WAVE_FPS) % FRAMES['Wave']], dr)
            cx, cy = ox + vec[dr][0] * (reach - core) , oy + vec[dr][1] * (reach - core)
            additive_paste(scene, im, cx - im.width // 2, cy - im.height // 2, k * WAVE_ALPHA[w])
            if w == 0 and reach >= Ln and walls[dr] is not None:                # 벽 임팩트는 첫 파동만
                ti = int((tw - t_arr) * 20)
                if ti < FRAMES['WallHit']:
                    over.append((Sh['TankerWaveWallHit'][ti], ox + vec[dr][0] * (Ln - 8) - 24, oy + vec[dr][1] * (Ln - 8) - 24))
    for _y, _d, im, x, y in sorted(erupts, key=lambda e: e[:2]):     # 화면 아래(= 카메라에 가까운) 것이 위에 오게, 같은 높이는 먼 것부터
        scene.paste(im.convert('RGB'), (x, y), im.getchannel('A'))
    for dr, dist in walls.items():                          # 벽은 충격파 뒤에 다시 칠해 끝에서 잘라 낸다 (임시 면)
        if dist is None:
            continue
        box = {'R': (ox + dist, 0, W, H), 'L': (0, 0, ox - dist, H), 'U': (0, 0, W, oy - dist)}[dr]
        d.rectangle(box, fill=(34, 36, 48))
        edge = {'R': (box[0], 0, box[0], H), 'L': (box[2], 0, box[2], H), 'U': (0, box[3], W, box[3])}[dr]
        d.line(edge, fill=(72, 76, 96))
    if orc:
        idx = 0 if T_ < -0.9 else 1 if T_ < -0.6 else 2 if T_ < -0.3 else 3 if T_ < -0.1 else 4 if T_ < 0 else 5 if T_ < 0.2 else 6 if T_ < 0.4 else 7 if T_ < 0.6 else 8
        big = orc[idx].resize((int(100 * ORC_SCALE),) * 2, Image.NEAREST)
        scene.alpha_composite(big, (ox - big.width // 2, oy - big.height // 2))
    else:
        d.ellipse((ox - 4, oy - 4, ox + 4, oy + 4), fill=(255, 255, 255))
    if not REF and T_ >= 0 and int(T_ * 20) < FRAMES['Burst']:
        additive_paste(scene, Sh['TankerWaveBurst'][int(T_ * 20)], ox - 48, oy - 48)
    for im, x, y in over:
        additive_paste(scene, im, x, y)
    return scene.convert('RGB')


def tiled(frames, n, horizontal=True):
    """몸통 이음 확인용: 같은 프레임을 n번 이어 붙인다"""
    out = []
    for f in frames:
        w, h = f.size
        im = canvas(w * n, h) if horizontal else canvas(w, h * n)
        for k in range(n):
            im.alpha_composite(f, (k * w, 0) if horizontal else (0, k * h))
        out.append(im)
    return out


def strip(frames, scale, bg=(38, 40, 46), pad=6, gap=6):
    fw, fh = max(f.width for f in frames), max(f.height for f in frames)
    out = Image.new('RGB', (len(frames) * (fw * scale + gap) + pad, fh * scale + pad * 2), (16, 14, 18))
    for i, f in enumerate(frames):
        tile = Image.new('RGBA', (fw * scale, fh * scale), bg + (255,))
        tile.alpha_composite(f.resize((f.width * scale, f.height * scale), Image.NEAREST))
        out.paste(tile.convert('RGB'), (pad + i * (fw * scale + gap), pad))
    return out


def make_draft(dst, orc_path=None):
    orc, ref = read_orc(orc_path) if orc_path else (None, None)
    Sh = sheets_cached()
    BG, FG, DIM = gv.BG, gv.FG, gv.DIM
    pad = 20
    W = 2 * GAME['W'] + 3 * pad
    secs = []

    def head(text, sub=None):
        im = Image.new('RGB', (W, 64 if sub else 40), BG)
        d = ImageDraw.Draw(im)
        d.text((pad, 4), text, fill=FG, font=font(26))
        if sub:
            d.text((pad, 38), sub, fill=DIM, font=font(16, False))
        return im

    title = Image.new('RGB', (W, 100), BG)
    d = ImageDraw.Draw(title)
    d.text((pad, 12), 'Tanker 십자 충격파 VFX 시안', fill=FG, font=font(36))
    d.text((pad, 62), '탑다운 3/4 시점(오브젝트와 같은 투영) · 1배 = 게임 도트 크기(16px = 1u), 폭 3u = 48px, 최대 30u = 480px · 어두운 던전 바닥 위 합성(벽은 임시 면) · Tanker = Armored Orc Attack03(1.8배)', fill=DIM, font=font(17, False))
    d.rounded_rectangle((W - 130 - pad, 20, W - pad, 66), 10, fill=(210, 206, 204))
    d.text((W - 130 - pad + 30, 24), VERSION, fill=(40, 30, 28), font=font(32))
    secs.append(title)

    if ref:
        crop = [f.crop(f.getbbox()) for f in ref]
        secs.append(head('0. 원본 fx 레이어 (Armored Orc Attack01 18 · 19 · Attack02 26~28, 4배)', '참고용 원본 fx. 이번 시안은 사용자 레퍼런스(밝은 핵 + 불꽃 줄기 + 그을음)를 따라 불꽃 색의 번쩍이는 파동 하나로 그림'))
        secs.append(strip(crop, 4, pad=pad))
    secs.append(head('1. 시간별 합성 (1배)', '위 · 왼쪽 · 오른쪽은 벽이 가까워 짧게 끊기고, 아래쪽은 벽이 없어 최대 길이로 나간다(화면 밖). 예고 1.2초 → 착지 → ' + ('충격파 3번이 차례로 솟는 흙먼지(가로 2줄 · 세로 1줄, 위 · 아래는 회전 없음) → 벽에서 멈춤 → 사라짐' if REF else '불꽃 파동 전파 → 벽 도달 → 그을음이 사라짐')))
    shots = [(-0.8, '예고 (T = -0.8초): 벽까지 점멸'), (0.12, '착지 직후 (0.12초): 임팩트 + 선두 발사'),
             (0.30, '전파 중 (0.30초)'), (0.55, '벽 도달 (0.55초): 위 · 왼쪽 도달, 오른쪽 · 아래 진행')]
    ims = [compose(t, orc) for t, _ in shots]
    grid = Image.new('RGB', (W, 2 * (GAME['H'] + 28) + pad), BG)
    d = ImageDraw.Draw(grid)
    for k, (im, (_, lb)) in enumerate(zip(ims, shots)):
        x = pad + (k % 2) * (GAME['W'] + pad)
        y = (k // 2) * (GAME['H'] + 28 + pad / 2)
        d.text((x, y), lb, fill=FG, font=font(18))
        grid.paste(im, (int(x), int(y) + 26))
    secs.append(grid)

    secs.append(head('2. 최대 길이 30u (1배)', '벽이 없으면 10u · 20u · 30u 지점에서 충격파 3번이 터진다. 지나간 자리에는 그을음 띠가 남는다' if REF else '벽이 없으면 파동이 480px 끝까지 번쩍이며 나간다. 지나간 자리에는 그을음 띠만 남는다'))
    far = {'W': 30 * 16 + 80, 'H': 96, 'O': (20, 64), 'arms': 'R', 'wall': {'R': None}}
    longrow = Image.new('RGB', (W, 2 * 104), BG)
    for k, t in enumerate((0.35, 0.80)):
        longrow.paste(compose(t, orc=None, game=far), (pad, k * 104))
    secs.append(longrow)

    secs.append(head('3. 부품 프레임 (4배 · 임팩트 3배)'))

    def blocks(items):
        bs = []
        for lb, im in items:
            t = Image.new('RGB', (im.width, im.height + 26), BG)
            ImageDraw.Draw(t).text((0, 2), lb, fill=FG, font=font(16))
            t.paste(im, (0, 26))
            bs.append(t)
        row = Image.new('RGB', (W, max(b.height for b in bs)), BG)
        x = pad
        for b in bs:
            row.paste(b, (x, 0))
            x += b.width + 30
        return row

    if REF:
        secs.append(blocks([(f'Wave 96×96 · 12프레임 @{ERUPT_FPS}fps 흙먼지 솟구침 (일반 합성, 바닥 중심 = x {erupt_anchor(Sh["TankerWave"][0])[0]} · y {erupt_anchor(Sh["TankerWave"][0])[1]})', strip(Sh['TankerWave'], 2, bg=(38, 36, 40)))]))
    else:
        secs.append(blocks([('Wave 48×48 · 4프레임 @16fps 번쩍임 강 · 약 교대 (가산, 핵 = x 36 · y 24)', strip(Sh['TankerWave'], 5, bg=(20, 18, 22)))]))
    secs.append(blocks([('Scorch_R 16×48 (4번 이은 모습, 알파 0.5)', strip(tiled(Sh['TankerWaveScorch_R'], 4), 5)),
                        ('Scorch_V 48×16 (세로로 4번)', strip(tiled(Sh['TankerWaveScorch_V'], 4, False), 5))]))
    secs.append(blocks([('Telegraph_R 32×48 · 2프레임 (알파 0.7)', strip(Sh['TankerWaveTelegraph_R'], 4)),
                        ('Telegraph_U 48×32', strip(Sh['TankerWaveTelegraph_U'], 4))]))
    if not REF:
        secs.append(blocks([('Burst 96×96 · 6프레임 @20fps (가산)', strip(Sh['TankerWaveBurst'], 3, bg=(20, 18, 22)))]))
        secs.append(blocks([('WallHit 48×48 · 5프레임 @20fps (가산)', strip(Sh['TankerWaveWallHit'], 4, bg=(20, 18, 22)))]))

    H = sum(s.height for s in secs) + 16 * len(secs)
    out = Image.new('RGB', (W, H), BG)
    y = 0
    for s in secs:
        out.paste(s, (0, y))
        y += s.height + 16
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    for attempt in range(5):
        try:
            out.save(dst)
            break
        except OSError:
            if attempt == 4:
                raise
            time.sleep(1.0)
    return out.size


def make_gifs(folder, orc_path=None, fps=20, scale=2):
    """시간 순서대로 합성한 GIF 2개: 전체 흐름(예고 -1.2초 ~ 페이드, 벽 가까운 팔 포함) · 벽이 없는 30u 최대 길이. 끝에 잠깐 멈춘다"""
    orc = read_orc(orc_path)[0] if orc_path else None
    far = {'W': 30 * 16 + 80, 'H': 160, 'O': (40, 80), 'arms': 'R', 'wall': {'R': None}}
    out = []
    for name, game, t0, t1, sc, o in (('tanker-wave', GAME, -TELEGRAPH_TIME, 1.5, scale, orc), ('tanker-wave-30u', far, -TELEGRAPH_TIME, 1.9, 2, orc)):
        n = int(round((t1 - t0) * fps))
        frames = []
        for k in range(n + 1):
            im = compose(t0 + k / fps, o, game)
            frames.append(im.resize((im.width * sc, im.height * sc), Image.NEAREST))
        pal = [f.quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
        durs = [round(1000 / fps)] * len(pal)
        durs[-1] += 600
        dst = os.path.join(folder, name + '.gif')
        pal[0].save(dst, save_all=True, append_images=pal[1:], duration=durs, loop=0, disposal=2)
        out.append(dst)
    return out


if __name__ == '__main__':
    argv = sys.argv[1:]
    orc = None
    for flag in ('--ref', '--orc'):                                  # 값을 받는 인자를 먼저 떼어 낸다 (남은 첫 인자가 시안 경로)
        if flag in argv:
            k = argv.index(flag)
            if flag == '--ref':
                REF = argv[k + 1]
            else:
                orc = argv[k + 1]
            del argv[k:k + 2]
    if 'import' in argv:
        problems = check()
        if problems:
            sys.exit('검사 실패: ' + '; '.join(problems))
        for path, im in build().items():
            im.save(paths.out(path))
            print('wrote', os.path.relpath(path, paths.REPO), im.size)
        sys.exit(0)
    args = [a for a in argv if not a.startswith('--')]
    dst = args[0] if args else paths.repo('Docs', 'drafts', 'props', 'tanker-wave-draft.png')
    problems = check()
    for p in problems:
        print('검사:', p)
    print('검사 문제', len(problems), '건')
    print('시안', dst, make_draft(dst, orc))
    if '--gif' in argv:
        for g in make_gifs(os.path.dirname(dst), orc):
            print('gif', g, os.path.getsize(g) // 1024, 'KB')
