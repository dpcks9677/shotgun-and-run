"""피격 핏자국(총알에 맞고 튀어나가는 피) — 8방향 × 변형 3종 × 8프레임

python Docs/tools/spritegen/vfx/blood_vfx.py --preview   # 확대 시안만 (Docs/drafts/props/blood_vfx_preview.png)
python Docs/tools/spritegen/vfx/blood_vfx.py             # Assets에 PNG 쓰기 (Blood.png)

- 시트: 가로 8프레임, 세로는 (변형 * 8 + 방향) 행. 한 칸 57×57, 피벗 = 정중앙 픽셀 (피격 지점).
  행 번호 = 변형 * 8 + 방향. 방향 d는 +x(동)에서 반시계로 45° 단위(0=동 1=북동 2=북 … 7=남동).
- 회전하지 않는다(STYLE 11장). 방향마다 같은 입자 명세를 그 각도로 풀어서 픽셀을 직접 찍는다:
  점은 가장 가까운 픽셀, 줄기는 끝점을 반올림한 Bresenham 선. 그래서 대각선도 계단이 깨끗하다.
- 모양은 총알 관통 직후 피가 진행 방향으로 튀는 흔적: 맞은 자리의 얼룩 + 방향을 따라 뻗는 줄기 + 멀수록 작아지는 방울.
  프레임이 갈수록 입자가 바깥으로 날아가 제자리에 선다(마지막 프레임 = 정지 상태, 클립은 그대로 유지하며 색만 페이드).
- 변형 0 부채형(줄기 3 + 방울 많음) · 변형 1 가는 제트형(길고 좁은 줄기 + 점선처럼 끊기는 방울) · 변형 2 덩어리형(굵고 짧은 줄기 + 큰 덩어리).
- 색은 pellet_vfx.py의 피 팔레트에 중간 단계 둘을 더해 5단. 외곽선 · 반투명 없음(전부 불투명).
"""
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402

CELL = 57                       # 홀수: 정중앙 픽셀 = 피격 지점
C0 = CELL // 2
FRAMES = 8
DIRS = 8
VARIANTS = 3

# 밝음 → 어두움
HI = (240, 96, 92)
BR = (216, 44, 48)
MD = (190, 32, 40)
DK = (140, 22, 30)
DD = (100, 16, 24)
RAMP = [HI, BR, MD, DK, DD]


def ease(t):
    return 1 - (1 - t) ** 2.2


# ---------------------------------------------------------------- 입자 명세 (진행 방향 +u, 옆 방향 +v, 단위 px)

def spec(variant):
    """한 변형의 입자 명세. 방향과 무관 — 방향 8개가 같은 명세를 쓴다."""
    r = random.Random(7000 + variant)
    pool, streaks, drops = [], [], []
    # 맞은 자리의 얼룩: 작은 불규칙 덩어리 (프레임 0부터 있다)
    pool_r = (2.2, 1.6, 3.0)[variant]
    for dv in range(-3, 4):
        for du in range(-3, 4):
            d = math.hypot(du * 0.9, dv)
            if d <= pool_r + r.uniform(-0.6, 0.4):
                pool.append((du, dv, DD if d > pool_r - 1 else DK))
    if variant == 0:
        for ang, start, length, spd in ((-18, 4, 11, 1.0), (1, 3, 17, 1.0), (19, 5, 12, 0.92)):
            streaks.append(dict(ang=ang, start=start, length=length, spd=spd, gap=(3, 1), wide=0.45))
        cone, count, reach = 38, 26, (7, 25)
        clots = 3
    elif variant == 1:
        for ang, start, length, spd, gap in ((0, 3, 22, 1.0, (2, 2)), (-8, 6, 12, 0.9, (2, 1))):
            streaks.append(dict(ang=ang, start=start, length=length, spd=spd, gap=gap, wide=0.3))
        cone, count, reach = 14, 16, (9, 26)
        clots = 1
        for i in range(5):                                   # 맞은 쪽(뒤)으로 튀는 작은 방울
            drops.append(dict(u=-r.uniform(3, 8), v=r.uniform(-3.5, 3.5), size=1, col=DK, spd=0.8))
        for u in (13, 17, 20, 23, 25):                       # 진행 축 위에 점선처럼 늘어선 구슬 방울
            drops.append(dict(u=u, v=r.uniform(-1, 1), size=1, col=BR, spd=1.0))
    else:
        for ang, start, length, spd in ((-38, 3, 8, 1.0), (-10, 3, 10, 1.0), (14, 3, 9, 0.95), (40, 4, 7, 0.9)):
            streaks.append(dict(ang=ang, start=start, length=length, spd=spd, gap=(0, 0), wide=0.6))
        cone, count, reach = 46, 10, (6, 21)
        clots = 6
    for i in range(count):
        u = r.uniform(*reach)
        v = r.uniform(-1, 1) * math.tan(math.radians(cone)) * u * 0.9
        far = (u - reach[0]) / (reach[1] - reach[0])
        col = RAMP[min(4, 2 + int(far * 2.5) + r.randint(0, 1))] if far > 0.3 else r.choice((MD, DK))
        drops.append(dict(u=u, v=v, size=1, col=col, spd=r.uniform(0.85, 1.15)))
    for i in range(clots):                                   # 2×2 덩어리: 중간 거리, 밝은 색 하이라이트 1px
        u = r.uniform(reach[0] + 2, reach[1] * 0.75)
        v = r.uniform(-1, 1) * math.tan(math.radians(cone)) * u * 0.6
        drops.append(dict(u=u, v=v, size=2, col=MD, spd=r.uniform(0.9, 1.05)))
    return dict(pool=pool, streaks=streaks, drops=drops)


# ---------------------------------------------------------------- 그리기

def to_px(u, v, theta):
    c, s = math.cos(theta), math.sin(theta)
    return C0 + u * c - v * s, C0 - (u * s + v * c)          # 화면 y는 아래로 늘어난다


def line(pts):
    """Bresenham 선분의 픽셀들 (끝점 포함)"""
    (x0, y0), (x1, y1) = pts
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err, out = dx + dy, []
    while True:
        out.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return out
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def frame(sp, d, f):
    im = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    theta = math.radians(45 * d)

    def put(x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < CELL and 0 <= y < CELL:
            im.putpixel((x, y), (*c, 255))

    t = f / (FRAMES - 1)
    e = ease(t)
    for du, dv, c in sp['pool']:
        put(*to_px(du, dv, theta), c)
    for s in sp['streaks']:
        k = min(1.0, e * s['spd'])
        if k <= 0:
            continue
        a = math.radians(s['ang'])
        head = (s['start'] + s['length']) * k
        tail = s['start'] * k
        if head - tail < 1:
            continue
        ends = []
        for u in (tail, head):
            x, y = to_px(u * math.cos(a), u * math.sin(a), theta)
            ends.append((int(round(x)), int(round(y))))
        px = line(ends)
        ddx, ddy = ends[1][0] - ends[0][0], ends[1][1] - ends[0][1]
        pw = (-1 if ddy > 0 else 1, 0) if abs(ddy) >= abs(ddx) else (0, 1)   # 줄기에 거의 수직인 한 칸
        on, off = s['gap']
        n = len(px)
        for i, (x, y) in enumerate(px):                      # 머리(바깥)에서 꼬리(안쪽)로 어두워진다
            if off and i % (on + off) >= on:
                continue
            p = i / max(1, n - 1)
            col = BR if p > 0.85 else MD if p > 0.45 else DK
            put(x, y, col)
            if p < s['wide']:                                # 뿌리(안쪽)는 한 줄 더 굵게
                put(x + pw[0], y + pw[1], DK if p < s['wide'] * 0.5 else MD)
    for dr in sp['drops']:
        k = min(1.0, e * dr['spd'])
        if f == 0 and dr['u'] > 0:
            continue
        x, y = to_px(dr['u'] * k, dr['v'] * k, theta)
        x, y = int(round(x)), int(round(y))
        if dr['size'] == 2:
            for ox, oy in ((0, 0), (1, 0), (0, 1), (1, 1)):
                put(x + ox, y + oy, dr['col'])
            put(x, y, HI if k >= 1 else BR)                  # 한 픽셀 하이라이트
        else:
            put(x, y, dr['col'])
    return im


def sheet():
    out = Image.new('RGBA', (CELL * FRAMES, CELL * DIRS * VARIANTS), (0, 0, 0, 0))
    for v in range(VARIANTS):
        sp = spec(v)
        for d in range(DIRS):
            for f in range(FRAMES):
                out.paste(frame(sp, d, f), (f * CELL, (v * DIRS + d) * CELL))
    return out


BLOOD_SHEET = paths.repo('Assets', '04.Sprites', 'Player', 'Damaged', 'Blood.png')
DRAFT = paths.repo('Docs', 'drafts', 'props', 'blood_vfx_preview.png')


def preview(scale=4):
    """위: 변형별 8방향 마지막 프레임을 바닥색 위에 + 오크(피격 대상 크기 비교). 아래: 변형별 동 · 북동 8프레임 진행."""
    bg = (38, 38, 42, 255)
    orc = Image.open(paths.SPRITES + '/Enemy/Orc with shadows/Orc.png').convert('RGBA').crop((0, 0, 100, 100))
    bb = orc.getbbox()
    orc = orc.crop(bb)
    gap = 6
    cell = CELL * scale
    sh = sheet()
    top = Image.new('RGBA', (DIRS * (cell + gap), VARIANTS * (cell + gap)), bg)
    for v in range(VARIANTS):
        for d in range(DIRS):
            fr = sh.crop(((FRAMES - 1) * CELL, (v * DIRS + d) * CELL, FRAMES * CELL, (v * DIRS + d + 1) * CELL))
            base = Image.new('RGBA', (CELL, CELL), bg)
            base.alpha_composite(orc, (C0 - orc.width // 2, C0 - orc.height // 2))
            base.alpha_composite(fr)
            top.alpha_composite(base.resize((cell, cell), Image.NEAREST), (d * (cell + gap), v * (cell + gap)))
    s2 = scale * 2 // 3 or 1
    c2 = CELL * s2
    rows = []
    for v in range(VARIANTS):
        for d in (0, 1):
            strip = sh.crop((0, (v * DIRS + d) * CELL, FRAMES * CELL, (v * DIRS + d + 1) * CELL))
            base = Image.new('RGBA', strip.size, bg)
            base.alpha_composite(strip)
            rows.append(base.resize((strip.width * s2, c2), Image.NEAREST))
    W = max(top.width, rows[0].width)
    H = top.height + sum(r.height + gap for r in rows)
    full = Image.new('RGBA', (W, H), bg)
    full.alpha_composite(top, (0, 0))
    y = top.height
    for r in rows:
        full.alpha_composite(r, (0, y))
        y += r.height + gap
    os.makedirs(os.path.dirname(DRAFT), exist_ok=True)
    full.convert('RGB').save(DRAFT)
    print('draft', os.path.relpath(DRAFT, paths.REPO))


if __name__ == '__main__':
    if '--preview' in sys.argv:
        preview()
    else:
        sheet().save(paths.out(BLOOD_SHEET))
        print('wrote', os.path.relpath(BLOOD_SHEET, paths.REPO), (CELL * FRAMES, CELL * DIRS * VARIANTS))
