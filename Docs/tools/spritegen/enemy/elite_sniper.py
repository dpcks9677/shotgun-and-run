"""EliteSniper 스프라이트 — Skeleton Archer 시트를 흑철 + 금 테마로 바꾸고 장식을 입힌다.

원본: Assets/04.Sprites/Enemy/Skeleton Archer with shadows/Skeleton Archer.png
      900×600, 100×100 칸, 행 = Idle 6 · Walk 8 · Attack 9 · Revive 5 · Hurt 4 · Death 4 (36장)

방식
  1. 색 교체(LUT): 청강 투구 · 갑옷 → 흑철, 활(뼈색 몸통 · 갈색 외곽) → 금, 투구 꼭대기 장식도 활과 같은 색이라 금이 된다.
     그대로 두는 색: 외곽선 검정, 그림자 α100, 뼈 회색, 화살통, 붉은 천, 활시위, Revive 초록 이펙트, 발사 섬광.
  2. 장식: 프레임마다 투구 위치(OFFSET, Idle F0 기준 dx, dy)를 따라 템플릿을 붙인다.
     뿔 투구 · 투구 금띠 · 발광 눈 · 가시 견갑. 견갑은 몸이 무너진 프레임(쓰러짐 · 일어남)에는 붙이지 않는다.
  3. Hurt F1 · F2는 원본이 Hurt F0에 틴트(+ 붉은 테두리)를 씌운 것이다.
     원본 쌍(F0 → F1/F2)으로 색 변환을 선형 근사해 엘리트 F0에 다시 씌우고, 붉은 테두리는 새 실루엣 바깥선에 다시 그린다.

사용법
  python elite_sniper.py                 # 시안: Docs/drafts/props/elite-sniper-draft.png (+ .gif)
  python elite_sniper.py import          # Assets에 Elite Skeleton Archer.png + .meta 쓰기 (원본 슬라이스 복사, 새 guid)
"""

import os
import random
import re
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402

SRC_DIR = os.path.join(paths.SPRITES, 'Enemy', 'Skeleton Archer with shadows')
SRC = os.path.join(SRC_DIR, 'Skeleton Archer.png')
DST = os.path.join(SRC_DIR, 'Elite Skeleton Archer.png')
DRAFT = paths.repo('Docs', 'drafts', 'props', 'elite-sniper-draft.png')

CELL = 100
ROWS = [('Idle', 6), ('Walk', 8), ('Attack', 9), ('Revive', 5), ('Hurt', 4), ('Death', 4)]

# 투구 위치 (Idle F0 기준). 투구 템플릿 맞춤으로 구했고, Hurt F1 · F2는 틴트 프레임이라 F0 값을 쓴다.
OFFSET = {
    'Idle': [(0, 0), (0, 0), (0, -1), (0, -1), (0, 0), (0, 0)],
    'Walk': [(0, 0), (0, -1), (0, 0), (0, 1), (0, 0), (0, -1), (0, 0), (0, 1)],
    'Attack': [(0, 0)] * 5 + [(0, 1), (0, 1), (0, 0), (0, 0)],
    'Revive': [(2, 6), (2, 6), (1, 2), (0, 0), (0, 0)],
    'Hurt': [(-2, 0)] * 4,
    'Death': [(0, 0), (1, 2), (2, 6), (2, 6)],
}
# 몸이 서 있는 프레임만 견갑을 붙인다 (dx, dy가 기준과 같은 프레임)
def has_body(row, i):
    dx, dy = OFFSET[row][i]
    return dx in (0, -2) and abs(dy) <= 1


OUT = (0, 0, 0, 255)

# ── 팔레트 ──
# 흑철: 원본 청강 11단계를 명도 순서대로 옮긴다 (살짝 푸른 보라 기)
IRON = {
    (23, 47, 66): (24, 22, 30),
    (40, 68, 90): (36, 33, 44),
    (66, 92, 107): (50, 47, 60),
    (83, 109, 124): (62, 58, 72),
    (96, 117, 129): (72, 68, 84),
    (111, 130, 141): (86, 82, 98),
    (127, 144, 154): (100, 96, 112),
    (143, 161, 174): (116, 112, 128),
    (155, 173, 183): (132, 128, 144),
    (197, 208, 213): (176, 172, 188),
    (201, 213, 217): (184, 180, 196),
}
G0, G1, G2, G3, G4 = (70, 44, 16), (138, 92, 30), (194, 142, 48), (232, 190, 86), (252, 230, 148)
GOLD = {
    (49, 41, 39): G0,      # 활 외곽(갈색)
    (151, 139, 134): G1,
    (177, 169, 166): G2,
    (210, 206, 204): G3,
}
LUT = {**IRON, **GOLD}

H0, H1, H2, H3 = (92, 80, 70), (150, 138, 122), (204, 194, 176), (238, 232, 218)   # 뿔(상아)
E1, E2 = (196, 24, 20), (255, 96, 64)                                             # 발광 눈
S1, S2, S3 = (100, 96, 112), (156, 152, 170), (232, 230, 240)                     # 견갑 가시(흑철 → 날 끝)
GREEN = (123, 201, 79, 255)                                                       # Revive 발광 외곽선(원본 색)

# ── 장식 템플릿 (Idle F0 칸 좌표) ──
# 값: 색 또는 OUT(검정 외곽선 — 투명이 아닌 칸 위에도 강제로 찍을 때). 자동 외곽선은 투명 칸에 닿는 곳만 두른다.
HORN_L = {
    (45, 38): H3,
    (45, 39): H2, (44, 39): H1,
    (44, 40): H1, (45, 40): H2,
    (44, 41): H0, (45, 41): H2, (46, 41): H1,
    (45, 42): H0, (46, 42): H2, (47, 42): H1,
    (46, 43): G1, (47, 43): G2,           # 뿌리 금테 — 투구 금띠(44줄) 바로 위에 붙는다
}
HORN_R = {(105 - x, y): c for (x, y), c in HORN_L.items()}
# 금띠: 투구 가운데 어두운 띠 (원본 색 → 금)
BAND_ROW = 44
BAND = {(23, 47, 66): G1, (40, 68, 90): G1, (66, 92, 107): G2, (96, 117, 129): G3}
EYES = {(51, 47): E1, (52, 47): E2, (55, 47): E2}
# 견갑: 어깨 판 아랫단 금테 + 바깥 위로 뻗은 가시. 왼쪽 가시는 화살통 위라 외곽선을 강제로 찍는다.
PAULDRON = {(48, 51): G3, (49, 51): G2, (55, 51): G2}
SPIKE_R = {(55, 50): S1, (56, 50): S1, (56, 49): S1, (57, 49): S2, (57, 48): S2, (58, 48): S2,
           (58, 47): S3, (59, 46): S3}
SPIKE_L = {(48, 50): S1, (47, 50): S1, (47, 49): S2, (46, 49): S1, (46, 48): S2, (45, 47): S3, (44, 46): S3,
           (48, 49): OUT, (47, 48): OUT, (46, 47): OUT, (45, 46): OUT,
           (45, 48): OUT, (44, 47): OUT, (45, 49): OUT, (46, 50): OUT, (47, 51): OUT, (43, 46): OUT, (44, 45): OUT}


def rgba(c):
    return tuple(c) + (255,) if len(c) == 3 else tuple(c)


def is_clear(px):
    """투명이거나 바닥 그림자(α100) — 장식 외곽선을 둘러도 되는 칸"""
    return px[3] < 255


def recolor(cell):
    out = cell.copy()
    h, w = cell.shape[:2]
    for y in range(h):
        for x in range(w):
            p = tuple(int(v) for v in cell[y, x])
            if p[3] == 255 and p[:3] in LUT:
                out[y, x] = rgba(LUT[p[:3]])
    return out


def stamp(dst, parts, dx, dy, line=OUT):
    """parts를 (dx, dy)만큼 옮겨 찍고, 새로 찍은 픽셀 둘레의 투명 칸에 외곽선(line)을 두른다."""
    drawn = []
    for (x, y), c in parts.items():
        X, Y = x + dx, y + dy
        dst[Y, X] = line if c == OUT else rgba(c)
        drawn.append((X, Y))
    mine = set(drawn)
    for X, Y in drawn:
        for nx, ny in ((X - 1, Y), (X + 1, Y), (X, Y - 1), (X, Y + 1)):
            if (nx, ny) not in mine and is_clear(dst[ny, nx]):
                dst[ny, nx] = line


def decorate(src_cell, dst, row, i):
    dx, dy = OFFSET[row][i]
    # 금띠: 원본 기준으로 띠 줄의 청강 픽셀만
    for x in range(48, 58):
        p = tuple(int(v) for v in src_cell[BAND_ROW + dy, x + dx])
        if p[3] == 255 and p[:3] in BAND:
            dst[BAND_ROW + dy, x + dx] = rgba(BAND[p[:3]])
    # 눈: 원본이 검정 눈구멍일 때만 (Revive F0 · F1은 초록 발광이라 그대로)
    for (x, y), c in EYES.items():
        if tuple(int(v) for v in src_cell[y + dy, x + dx]) == OUT:
            dst[y + dy, x + dx] = rgba(c)
    # Revive F0~F3은 원본 실루엣이 초록으로 빛나므로 장식 외곽선도 초록
    line = GREEN if row == 'Revive' and i < 4 else OUT
    stamp(dst, HORN_L, dx, dy, line)
    stamp(dst, HORN_R, dx, dy, line)
    if has_body(row, i):
        for (x, y), c in PAULDRON.items():
            if src_cell[y + dy, x + dx][3] == 255:
                dst[y + dy, x + dx] = rgba(c)
        stamp(dst, SPIKE_R, dx, dy, line)
        stamp(dst, SPIKE_L, dx, dy, line)


# ── Hurt 틴트 ──
def fit_tint(base, tinted):
    """base → tinted 색 변환을 3×4 선형으로 근사 (검정 · 그림자 · 붉은 테두리 제외)"""
    a = (base[:, :, 3] == 255) & (tinted[:, :, 3] == 255)
    a &= base[:, :, :3].sum(axis=2) > 0
    a &= ~((tinted[:, :, 0] == 237) & (tinted[:, :, 1] == 13))
    X = base[a][:, :3].astype(float)
    Y = tinted[a][:, :3].astype(float)
    X1 = np.hstack([X, np.ones((len(X), 1))])
    M, *_ = np.linalg.lstsq(X1, Y, rcond=None)
    return M


def apply_tint(cell, M, red_edge):
    out = cell.copy()
    a = (cell[:, :, 3] == 255) & (cell[:, :, :3].sum(axis=2) > 0)
    X = cell[a][:, :3].astype(float)
    Y = np.clip(np.hstack([X, np.ones((len(X), 1))]) @ M, 0, 255).round().astype(int)
    out[a, :3] = Y
    if red_edge:
        # 실루엣 바깥선(투명 · 그림자에 닿는 불투명 픽셀)을 붉게
        solid = cell[:, :, 3] == 255
        clear = ~solid
        edge = np.zeros_like(solid)
        edge[1:, :] |= clear[:-1, :]
        edge[:-1, :] |= clear[1:, :]
        edge[:, 1:] |= clear[:, :-1]
        edge[:, :-1] |= clear[:, 1:]
        edge &= solid
        dark = cell[:, :, :3].sum(axis=2) < 150          # 원본도 외곽선(검정 · 갈색)만 붉게 바꿨다
        out[edge & dark] = (237, 13, 13, 255)
    return out


def build():
    src = np.array(Image.open(SRC).convert('RGBA')).astype(int)
    dst = src.copy()
    hurt_tint = {}
    for r, (row, n) in enumerate(ROWS):
        for i in range(n):
            ys, xs = r * CELL, i * CELL
            s = src[ys:ys + CELL, xs:xs + CELL]
            if row == 'Hurt' and i in (1, 2):
                hurt_tint[i] = fit_tint(src[ys:ys + CELL, 0:CELL], s)
                continue
            d = recolor(s)
            decorate(s, d, row, i)
            dst[ys:ys + CELL, xs:xs + CELL] = d
    r = [k for k, _ in ROWS].index('Hurt')
    ys = r * CELL
    base = dst[ys:ys + CELL, 0:CELL]
    for i, M in hurt_tint.items():
        dst[ys:ys + CELL, i * CELL:(i + 1) * CELL] = apply_tint(base, M, red_edge=(i == 1))
    return src.astype(np.uint8), dst.astype(np.uint8)


# ── 시안 ──
BG = (38, 40, 46, 255)
CROP = (30, 30, 75, 66)   # 칸 안에서 캐릭터가 들어가는 범위
Z = 5


def draft(src, dst, path):
    cw, ch = (CROP[2] - CROP[0]) * Z, (CROP[3] - CROP[1]) * Z
    gap, label_w = 6, 70
    rows_h = sum(2 * ch + gap * 3 for _ in ROWS)
    W = label_w + 9 * (cw + gap)
    img = Image.new('RGBA', (W, rows_h + 40 + 140), (24, 25, 30, 255))
    dr = ImageDraw.Draw(img)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype(paths.font_path(True), 16)
    except Exception:
        font = None
    dr.text((8, 8), 'EliteSniper v02 — 흑철 + 금 / 뿔 투구 · 발광 눈 · 가시 견갑 · 금 활   (위: 원본, 아래: 엘리트, ×5)', fill=(230, 230, 230), font=font)
    y = 40
    s_im, d_im = Image.fromarray(src), Image.fromarray(dst)
    for r, (row, n) in enumerate(ROWS):
        dr.text((8, y + ch // 2), row, fill=(200, 200, 200), font=font)
        for k, im in enumerate((s_im, d_im)):
            for i in range(n):
                c = im.crop((i * CELL + CROP[0], r * CELL + CROP[1], i * CELL + CROP[2], r * CELL + CROP[3]))
                bg = Image.new('RGBA', c.size, BG)
                bg.alpha_composite(c)
                img.paste(bg.resize((cw, ch), Image.NEAREST), (label_w + i * (cw + gap), y + k * (ch + gap)))
        y += 2 * ch + gap * 3
    # 실제 크기 비교 (×2, 엘리트는 1.2배): 원본 · 엘리트 Idle F0
    dr.text((8, y + 8), '게임 크기 ×2 (엘리트는 스케일 1.2)', fill=(200, 200, 200), font=font)
    y += 34
    x = label_w
    for im, scale in ((s_im, 2), (d_im, 2.4), (s_im, 2), (d_im, 2.4)):
        c = im.crop((CROP[0], CROP[1], CROP[2], CROP[3]))
        bg = Image.new('RGBA', c.size, BG)
        bg.alpha_composite(c)
        bg = bg.resize((round(c.width * scale), round(c.height * scale)), Image.NEAREST)
        img.paste(bg, (x, y + 100 - bg.height))
        x += bg.width + 10
    img.save(path)
    # GIF: Idle · Walk · Attack, 원본 | 엘리트
    frames = []
    for r, (row, n) in enumerate(ROWS[:3]):
        for i in range(n):
            f = Image.new('RGBA', (2 * cw + gap, ch), BG)
            for k, im in enumerate((s_im, d_im)):
                c = im.crop((i * CELL + CROP[0], r * CELL + CROP[1], i * CELL + CROP[2], r * CELL + CROP[3]))
                bg = Image.new('RGBA', c.size, BG)
                bg.alpha_composite(c)
                f.paste(bg.resize((cw, ch), Image.NEAREST), (k * (cw + gap), 0))
            frames.append(f.convert('RGB'))
    frames[0].save(os.path.splitext(path)[0] + '.gif', save_all=True, append_images=frames[1:], duration=110, loop=0)


# ── 임포트 ──
def new_guid():
    """저장소에 없는 guid (SystemRandom)"""
    rng = random.SystemRandom()
    used = set()
    for root, _, files in os.walk(paths.repo('Assets')):
        for f in files:
            if f.endswith('.meta'):
                with open(os.path.join(root, f), encoding='utf-8', errors='ignore') as fh:
                    m = re.search(r'^guid: ([0-9a-f]{32})', fh.read(), re.M)
                    if m:
                        used.add(m.group(1))
    while True:
        g = '%032x' % rng.getrandbits(128)
        if g not in used:
            return g


def write_import(dst):
    png = paths.out(DST)
    meta_path = png + '.meta'
    if os.path.exists(DST + '.meta'):
        with open(DST + '.meta', encoding='utf-8') as fh:
            guid = re.search(r'^guid: ([0-9a-f]{32})', fh.read(), re.M).group(1)    # 다시 만들 때 guid 재사용
    else:
        guid = new_guid()
    with open(SRC + '.meta', encoding='utf-8') as fh:
        meta = fh.read()
    meta = re.sub(r'^guid: [0-9a-f]{32}', 'guid: ' + guid, meta, count=1, flags=re.M)
    meta = meta.replace('Skeleton Archer_', 'Elite Skeleton Archer_')
    Image.fromarray(dst).save(png)
    with open(meta_path, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(meta)
    print('wrote', png, 'guid', guid)


if __name__ == '__main__':
    src, dst = build()
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        write_import(dst)
    else:
        path = sys.argv[1] if len(sys.argv) > 1 else DRAFT
        os.makedirs(os.path.dirname(path), exist_ok=True)
        draft(src, dst, path)
        print('draft', path)
