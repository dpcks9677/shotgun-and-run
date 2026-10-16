"""보스룸 문 (Z4 → 보스룸 세로 통로 입구, 2026-10-10 사용자 안) — 시안.

Z4-Boss 통로가 꺾여 북쪽으로 올라가는 세로 통로의 입구(통로 바닥 y97)에 서는 돌 쌍여닫이 문.
- 사용자 레퍼런스: 아치 돌문 + 짐승 얼굴 돋을새김 + 양옆 벽돌 기둥 위 푸른 불꽃 화로. 보석은 아치에 3개(해금 조건 하나마다 1개 점등)
- 크기 128x128 = 8x8칸. 가로 = 통로 6칸 + 양쪽 벽 선 1칸씩 (x347~354), 맨 아래 줄 = 가로 통로 북쪽 벽 정면 아랫변(y97)
- 기둥 앞면 = 그 벽의 원본 타일 픽셀(위 58 처마 · 86_2 벽돌 · 86 걸레받이)을 그대로 이어 붙여 벽 높이(48px)와 줄을 맞춤
- 문짝은 틀보다 4px 안쪽(북쪽)에 있어 4px 위로 보이고, 그 아래 4px는 문턱 윗면
- 열림: 문짝이 경첩을 축으로 안쪽(북쪽)으로 돌아감. 문짝 위 점 (u, h)는 화면 x = 경첩 ± u cosθ, 화면 y = 바닥 − h − u sinθ
  (탑다운 3/4: 북쪽 깊이 1px = 화면 위 1px). 왼쪽 문짝은 동쪽을 보게 돌아 어두워지고, 오른쪽은 서쪽(빛 쪽)을 봐서 조금 밝아짐
- 보석 · 불꽃은 따로 겹쳐 놓는 스프라이트 (보석 점등은 애니메이션 없이 켜기만, 불꽃은 4프레임 반복)

    python boss_door.py <시안.png> <씬 캡처.png>      (씬 캡처 = unity/capture_area.cs, 칸 x336~365 · y86~115, PPU 16)"""
import os as _os, sys as _sys, math, hashlib, random
HERE = _os.path.dirname(_os.path.abspath(__file__))
_sys.path[:0] = [_os.path.abspath(_os.path.join(HERE, '..', '..'))]
from PIL import Image, ImageDraw
from bl import H, load_font

VERSION = 'v01'
W, HT = 128, 128
BASE = HT - 1                      # 맨 아래 줄 (벽 정면 아랫변)
CX = 63.5                          # 문 가운데
SPRING = 72                        # 아치가 시작되는 줄 (앞면)
RI, RO = 32, 43                    # 아치 안 · 바깥 반지름
TOPD = 6                           # 아치 틀이 벽에서 튀어나온 깊이 → 바깥 곡선 위 윗면 띠
RECESS = 4                         # 문짝이 틀보다 들어간 깊이
LEAF_B = BASE - RECESS             # 문짝 바닥 줄
DOOR_X0, DOOR_X1 = 32, 95          # 문 개구부 (문짝 두 장, 32px씩)
JAMB = ((21, 31), (96, 106))       # 문설주
PILLAR = ((0, 19), (108, 127))     # 기둥
PILLAR_TOP = 80                    # 기둥 앞면 윗줄 (벽 정면 3줄 = 48px)
PILLAR_D = 8                       # 기둥 윗면 깊이
ANGLES = (0, 10, 22, 36, 50, 64, 76, 85)   # 열림 프레임 각도 (도)

# 돌 (DungeonDark 벽 실측 191a1c ~ 45484f에서 한두 단계 밝게 이어 붙임)
K = [H('141518'), H('1a1c1f'), H('202226'), H('27292d'), H('2e3034'), H('383b40'), H('45484f'), H('555963'), H('666b75')]
# 문짝 (같은 돌에 푸른 기를 조금 더)
L = [H('16191e'), H('1d2127'), H('252a31'), H('2e333b'), H('383e47'), H('444b55'), H('535a65'), H('646c78')]
GEM_OFF = [H('1b262b'), H('243439'), H('30454c'), H('445f67')]
GEM_ON = [H('0f5f7a'), H('1b8fb0'), H('39c3e0'), H('8ff0ff'), H('e8ffff')]
HALO = (110, 230, 255)
FIRE = [H('123a6e'), H('1e63b5'), H('2f95e8'), H('6fcfff'), H('c8f4ff')]
VOIDC = H('1e1611')


def _rng(tag):
    return random.Random(int(hashlib.md5(tag.encode()).hexdigest()[:8], 16))


# ── 원본 벽 타일 ──
_TILES = {}


# 페인터가 Z4-보스 통로 북쪽 벽에 칠한 조각: 타일 이름 → (시트, Unity rect x, y). 스프라이트 이름이 타일 이름과 달라 rect로 찾는다.
WALL_RECT = {'DungeonDark_Wall_58': ('DungeonDark.png', 128, 464),        # 처마 (y99)
             'DungeonDark_Wall_86_2': ('DungeonWallMid.png', 0, 0),       # 가운데 벽돌 (y98)
             'DungeonDark_Wall_86': ('DungeonDark.png', 128, 448)}        # 걸레받이 (y97)


def wall_tile(name):
    """원본 시트에서 16x16 잘라 옴 (Unity rect는 아래 기준)"""
    if name not in _TILES:
        root = _os.path.abspath(_os.path.join(HERE, '..', '..', '..', '..', '..'))
        sheet, x, y = WALL_RECT[name]
        w = h = 16
        png = _os.path.join(root, 'Assets', '04.Sprites', 'Tile', 'Dungeon', sheet)
        im = Image.open(png).convert('RGBA')
        _TILES[name] = im.crop((x, im.height - y - h, x + w, im.height - y))
    return _TILES[name]


class Canvas:
    def __init__(s, w=W, h=HT):
        s.im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        s.p = s.im.load()
        s.w, s.h = w, h

    def px(s, x, y, c):
        if 0 <= x < s.w and 0 <= y < s.h:
            if len(c) == 4 and c[3] < 255:
                s.im.alpha_composite(Image.new('RGBA', (1, 1), c), (x, y))
            else:
                s.p[x, y] = c[:3] + (255,)

    def get(s, x, y):
        return s.p[x, y] if 0 <= x < s.w and 0 <= y < s.h else (0, 0, 0, 0)


def _shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)


# ── 문짝 무늬 (문 좌표: X 0~63 왼→오, Hh 0~ 아래→위) ──
def htop(xd):
    """문 좌표 X열의 문짝 높이 (아치 윗변, 문짝 면 기준)"""
    d = xd + 0.5 - 32
    return LEAF_B - (SPRING - RECESS) + math.sqrt(max(0.0, RI * RI - d * d))


SKULL = [  # 왼쪽 반 (12열, 오른쪽 끝이 가운데). h 뿔, # 얼굴, o 눈 · 코 구멍, t 이빨
    'h...........',
    'hh..........',
    '.hh.........',
    '.hhh........',
    '..hhh...####',
    '...hhh######',
    '....hh######',
    '....########',
    '...#########',
    '...##ooo####',
    '...#ooooo###',
    '...#ooooo###',
    '...##ooo####',
    '....#######o',
    '.....######o',
    '.....#######',
    '......t.t.t.',
    '......######',
    '.......#####',
]
SKULL_TOP = 64                     # 해골 맨 윗줄의 Hh


def _skull(xd, hh):
    r = SKULL_TOP - hh
    if not 0 <= r < len(SKULL):
        return None
    c = xd - 20
    if not 0 <= c < 24:
        return None
    ch = SKULL[r][c if c < 12 else 23 - c]
    return None if ch == '.' else ch


def leaf_texture():
    """64 x 88 문짝 무늬. tex[(xd, hh)] = 색"""
    hmax = int(math.ceil(htop(31))) + 1
    hm, kind = {}, {}
    for xd in range(64):
        top = htop(xd)
        xl = xd if xd < 32 else xd - 32
        for hh in range(hmax):
            if hh >= top:
                continue
            t = top - 1 - hh
            edge = min(xl, 31 - xl, hh, t)
            lv = 1
            if edge < 3:
                lv = 2                                                    # 테 (돋음)
            elif 6 <= xl <= 25 and (8 <= hh <= 30 or (36 <= hh and t >= 8)):
                lv = 0                                                    # 판 (오목)
            sk = _skull(xd, hh)
            if sk:
                lv = 3
                kind[(xd, hh)] = sk
            hm[(xd, hh)] = lv
    for xd, hh in ((1, 12), (1, 32), (1, 52), (62, 12), (62, 32), (62, 52), (30, 20), (33, 20), (30, 44), (33, 44)):
        for dx in (0, 1):
            for dy in (0, 1):
                if (xd + dx, hh + dy) in hm:
                    hm[(xd + dx, hh + dy)] = 3                            # 쇠못 머리
                    kind[(xd + dx, hh + dy)] = 'n'
    r = _rng('bossdoor-leaf')
    tex = {}
    for (xd, hh), lv in hm.items():
        base = {0: 2, 1: 3, 2: 4, 3: 5}[lv]
        up, lf, rt = hm.get((xd, hh + 1), -9), hm.get((xd - 1, hh), -9), hm.get((xd + 1, hh), -9)
        dn = hm.get((xd, hh - 1), -9)
        i = base
        if up < lv:
            i += 2                                                        # 돋은 것의 윗변 = 윗면 (빛)
        elif lf < lv:
            i += 1
        if rt < lv:
            i -= 1
        if dn < lv and lv >= 2:
            i -= 1
        if up > lv:
            i -= 2                                                        # 돋은 것 바로 아래 그늘
        elif hm.get((xd - 1, hh + 1), -9) > lv:
            i -= 1
        if lv == 1 and i == base and r.random() < 0.08:
            i += r.choice((-1, 1))                                        # 돌 결
        col = L[max(0, min(7, i))]
        k = kind.get((xd, hh))
        if k == 'o':
            col = L[0]
            if hm.get((xd, hh + 1)) == 3 and kind.get((xd, hh + 1)) != 'o':
                col = K[0]
        elif k == 'n':
            col = L[7] if (up < 3 or lf < 3) else L[2]
        if xd == 31:
            col = K[0]                                                    # 두 문짝 사이 틈
        tex[(xd, hh)] = col
    for x0, h0, path in ((9, 70, ((1, -1), (1, -1), (0, -1), (1, -1), (1, -2), (0, -1))),     # 균열
                         (44, 14, ((-1, 1), (-1, 1), (0, 1), (-1, 1), (-1, 1))),
                         (52, 60, ((1, -1), (0, -1), (1, -1)))):
        x, h = x0, h0
        for dx, dh in path:
            x += dx; h += dh
            if (x, h) in tex:
                tex[(x, h)] = L[1]
                if (x + 1, h - 1) in tex and hm.get((x + 1, h - 1)) == hm.get((x, h)):
                    tex[(x + 1, h - 1)] = L[min(7, {0: 2, 1: 3, 2: 4, 3: 5}[hm[(x, h)]] + 1)]
    return tex


_TEX = None


def draw_leaves(c, ang):
    global _TEX
    if _TEX is None:
        _TEX = leaf_texture()
    th = math.radians(ang)
    co, si = math.cos(th), math.sin(th)
    tops = {}
    for side in (0, 1):
        hinge = DOOR_X0 if side == 0 else DOOR_X1 + 1
        # 돌아가며 빛을 덜 받는 왼쪽 문짝(동쪽을 봄) / 더 받는 오른쪽 문짝(서쪽을 봄), 안쪽 그늘 조금
        f = (1.0 - 0.38 * si) if side == 0 else (1.0 + 0.10 * si - 0.10 * si * si)
        for x in range(DOOR_X0, DOOR_X1 + 1):
            u = ((x + 0.5 - hinge) if side == 0 else (hinge - x - 0.5)) / co
            if not 0 <= u < 32:
                continue
            xd = int(u) if side == 0 else 63 - int(u)
            z = u * si
            for y in range(HT):
                hh = int(LEAF_B + 0.5 - (y + 0.5) - z + 0.5)
                col = _TEX.get((xd, hh))
                # 개구부 밖(아치 위로 솟는 문짝 끝)은 그리지 않음: 실제로는 보이지만 뿔처럼 읽혀서 아치 뒤로 숨김
                if col is None or not opening(x, y):
                    continue
                c.px(x, y, _shade(col, f))
                tops[x] = min(tops.get(x, 999), y)
    if ang > 0:
        for x, y in tops.items():                                         # 문짝 윗면 두께 (4px 판의 윗변)
            th_px = 1 if ang < 40 else 2
            for k in range(1, th_px + 1):
                if opening(x, y - k):
                    c.px(x, y - k, L[5] if k == 1 else L[4])
    return tops


def opening(x, y):
    """앞면 기준 문 개구부 (아치 안 + 문턱 위)"""
    if not DOOR_X0 <= x <= DOOR_X1 or y > LEAF_B:
        return False
    d = x + 0.5 - (CX + 0.5)
    return y >= SPRING or (SPRING - (y + 0.5)) <= math.sqrt(max(0.0, RI * RI - d * d))


def ring_front(x, y):
    dx, dy = x + 0.5 - (CX + 0.5), SPRING - (y + 0.5)
    if dy < 0:
        return False
    r2 = dx * dx + dy * dy
    return RI * RI < r2 <= RO * RO


def draw_frame(c):
    nv = 9
    joint_a = [180.0 * j / nv for j in range(1, nv)]
    # 1) 아치 윗면 (초승달): 바깥 곡선 위 TOPD px
    for x in range(W):
        for y in range(HT):
            if ring_front(x, y):
                continue
            k = next((kk for kk in range(1, TOPD + 1) if ring_front(x, y + kk)), 0)
            if not k:
                continue
            dx = x + 0.5 - (CX + 0.5)
            col = K[5] if dx < -10 else K[4]
            if k == 1:
                col = K[7] if dx < 12 else K[6]
            elif k == 2:
                col = K[6] if dx < 0 else K[5]
            elif k == TOPD:
                col = K[2]
            ang = math.degrees(math.atan2(SPRING - (y + k + 0.5), -dx))
            if any(abs(ang - a) < 1.6 for a in joint_a) and 1 < k < TOPD:
                col = K[3]
            c.px(x, y, col)
    # 2) 홍예 앞면
    r = _rng('bossdoor-ring')
    for x in range(W):
        for y in range(HT):
            if not ring_front(x, y):
                continue
            dx, dy = x + 0.5 - (CX + 0.5), SPRING - (y + 0.5)
            rr = math.sqrt(dx * dx + dy * dy)
            ang = math.degrees(math.atan2(dy, -dx))
            kf = ang / (180.0 / nv)
            fk = kf - math.floor(kf)
            col = K[4] if int(kf) % 2 == 0 else K[3]
            if any(abs(ang - a) < 1.5 for a in joint_a):
                col = K[1]
            elif rr > RO - 1.2:
                col = K[6] if dx < 10 else K[5]                           # 바깥 모서리 (윗면과 만나는 빛선)
            elif rr < RI + 1.3:
                col = K[1] if dy > 2 else K[2]                            # 안쪽 모서리 → 개구부 그늘
            elif rr < RI + 2.4:
                col = K[2]
            elif fk < 0.13:
                col = K[5]
            elif fk > 0.88:
                col = K[2]
            elif r.random() < 0.06:
                col = K[2]
            c.px(x, y, col)
    # 3) 문설주: 13px 돌 4단, 윗변 빛 · 왼변 빛 · 오른변 그늘
    for a, b in JAMB:
        for y in range(SPRING, HT):
            blk = (y - SPRING) // 14
            yy = (y - SPRING) % 14
            for x in range(a, b + 1):
                col = K[4] if blk % 2 == 0 else K[3]
                if yy == 13:
                    col = K[1]
                elif yy == 0 and blk > 0:
                    col = K[6]
                elif x == a:
                    col = K[5]
                elif x == b:
                    col = K[2]
                c.px(x, y, col)
    # 4) 문턱 윗면 (개구부 바닥 4px)
    for x in range(JAMB[0][1] + 1, JAMB[1][0]):
        for y in range(LEAF_B + 1, HT):
            k = y - LEAF_B
            c.px(x, y, K[2] if k == 1 else K[5] if k == 2 else K[4] if k == 3 else K[6])
    for x in range(36, 92, 9):
        c.px(x, LEAF_B + 2, K[3])
    # 5) 이맛돌: 앞면 + 더 깊은 윗면, 보석 자리
    kx0, kx1 = 55, 72
    ky0, ky1 = SPRING - RO - 2, SPRING - RI + 5
    kd = TOPD + 3
    for y in range(ky0 - kd, ky0):
        k = ky0 - y
        for x in range(kx0, kx1 + 1):
            c.px(x, y, K[7] if k == 1 else K[6] if k <= 3 else K[2] if k == kd else K[5] if x < CX else K[4])
    for y in range(ky0, ky1 + 1):
        for x in range(kx0, kx1 + 1):
            col = K[5]
            if y == ky0:
                col = K[7]
            elif x == kx0:
                col = K[6]
            elif x == kx1 or y == ky1:
                col = K[2]
            c.px(x, y, col)
    for y in range(ky0 - kd, ky1 + 1):
        c.px(kx0 - 1, y, K[1]); c.px(kx1 + 1, y, K[1])
    for y in range(ky0 + 1, ky1 + 2):                                     # 이맛돌 오른쪽 그림자
        c.px(kx1 + 2, y, (0, 0, 0, 90))
    c.px(kx0 + 3, ky1 - 3, K[3]); c.px(kx0 + 4, ky1 - 2, K[3])           # 깨진 자국


def draw_pillar(c, a, b):
    t58, t862, t86 = wall_tile('DungeonDark_Wall_58'), wall_tile('DungeonDark_Wall_86_2'), wall_tile('DungeonDark_Wall_86')
    for y in range(PILLAR_TOP, HT):
        t = t58 if y < 96 else t862 if y < 112 else t86
        for x in range(a, b + 1):
            col = t.getpixel((x % 16, y % 16))
            if col[:3] == VOIDC[:3]:
                col = K[2]
            c.px(x, y, col[:3] + (255,))
        c.px(a, y, K[5] if y % 4 else K[4])
        c.px(b, y, K[1])
    for x in range(a, b + 1):
        c.px(x, PILLAR_TOP, K[6])
    for y in range(PILLAR_TOP - PILLAR_D, PILLAR_TOP):                    # 기둥 윗면
        k = PILLAR_TOP - y
        for x in range(a, b + 1):
            col = K[4] if x > (a + b) // 2 else K[5]
            if k == 1:
                col = K[7] if x < b - 3 else K[6]
            elif k == 2:
                col = K[5]
            elif k == PILLAR_D:
                col = K[2]
            if x == a and 1 < k < PILLAR_D:
                col = K[6]
            c.px(x, y, col)
    r = _rng('bossdoor-pillar%d' % a)
    for _ in range(4):
        c.px(r.randint(a + 2, b - 2), r.randint(PILLAR_TOP - PILLAR_D + 2, PILLAR_TOP - 3), K[3])
    # 화로: 돌 사발 (윗면 타원 테 + 앞면 띠), 안에 푸른 숯
    bx = (a + b) / 2.0
    rim_y, rx, ry = PILLAR_TOP - 7, 7.5, 2.5
    for y in range(rim_y - 3, PILLAR_TOP - 1):
        for x in range(a, b + 1):
            dx = x + 0.5 - (bx + 0.5)
            dy = y + 0.5 - rim_y
            if (dx / rx) ** 2 + (dy / ry) ** 2 <= 1.0:                    # 사발 윗면 (테 + 속)
                inner = (dx / (rx - 1.6)) ** 2 + (dy / (ry - 0.9)) ** 2 <= 1.0
                if inner:
                    col = K[0]
                    if dy > -0.5 and (x * 7 + y * 3) % 5 < 2:
                        col = FIRE[1] if (x + y) % 2 else FIRE[2]
                else:
                    col = K[7] if dy > 0 and dx < 2 else K[6] if dy > 0 else K[5]
                c.px(x, y, col)
            elif y > rim_y and abs(dx) <= rx - (y - rim_y) * 0.55:       # 사발 앞면 (아래로 좁아짐)
                col = K[5] if dx < -2 else K[4] if dx < 3 else K[3]
                if y == PILLAR_TOP - 2:
                    col = K[2]
                c.px(x, y, col)


GEM_KEY = (1, 3, 5, 7, 7, 7, 5, 3, 1)                                  # 이맛돌 보석 (가로 7 x 세로 9)
GEM_SIDE = (1, 3, 5, 5, 5, 3, 1)                                       # 홍예 보석 (5 x 7)


def gem_slots():
    """보석 3개: (가운데 x, 윗줄 y, 모양). 0 = 왼쪽, 1 = 이맛돌, 2 = 오른쪽 (점등 순서와 무관한 자리 번호)"""
    rm = (RI + RO) / 2.0
    d = rm * math.cos(math.radians(45))
    return [(CX - d, int(SPRING - d) - 3, GEM_SIDE), (CX, SPRING - RO + 1, GEM_KEY), (CX + d, int(SPRING - d) - 3, GEM_SIDE)]


def gem_cells(cx, y0, shape):
    cells = {}
    for j, wdt in enumerate(shape):
        x0 = int(round(cx - wdt / 2.0 + 0.5))
        for i in range(wdt):
            cells[(x0 + i, y0 + j)] = (i, j, wdt)
    return cells


def draw_gem(c, slot, lit):
    cx, y0, shape = gem_slots()[slot]
    cells = gem_cells(cx, y0, shape)
    n = len(shape)
    if lit:
        halo = {}                                                         # 빛 번짐 (테두리 밖 2~4px)
        for (x, y) in cells:
            for dx in range(-4, 5):
                for dy in range(-4, 5):
                    q = (x + dx, y + dy)
                    if q in cells:
                        continue
                    dd = max(abs(dx), abs(dy))
                    if dd >= 2:
                        halo[q] = min(halo.get(q, 9), dd)
        for (x, y), dd in halo.items():
            c.px(x, y, HALO + ({2: 95, 3: 55, 4: 25}[dd],))
    for (x, y) in cells:                                                  # 박힌 자리 (1px 어두운 테)
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            if (x + dx, y + dy) not in cells:
                c.px(x + dx, y + dy, K[0] if not lit else GEM_ON[0])
    pal = GEM_ON if lit else GEM_OFF
    for (x, y), (i, j, wdt) in cells.items():
        upper = j < n // 2
        left = i < wdt // 2
        if i == 0 or i == wdt - 1 or j == n - 1:
            col = pal[0] if not left or j == n - 1 else pal[1]
        elif upper and left:
            col = pal[3]
        elif upper or left:
            col = pal[2]
        else:
            col = pal[1]
        c.px(x, y, col)
    hx, hy = min(cells, key=lambda p: (p[1] + p[0] * 0.6))
    c.px(hx + 1, hy + 1, GEM_ON[4] if lit else GEM_OFF[3])


def flame(f):
    """화로 불꽃 16 x 24 (아래 가운데 = 사발 속 가운데), 4프레임"""
    c = Canvas(16, 24)
    r = _rng('bossdoor-flame%d' % f)
    sway = (0, 1, 0, -1)[f]
    hgt = (17, 19, 16, 18)[f]
    for y in range(24):
        t = (23 - y) / float(hgt)                                         # 0 = 바닥, 1 = 끝
        if t > 1:
            continue
        half = 5.2 * (1 - t) ** 0.8 * (0.75 + 0.25 * math.sin(t * 3.1)) + 0.4
        mid = 7.5 + sway * t * 1.6 + 0.8 * math.sin(t * 6 + f * 1.7) * t
        for x in range(16):
            d = abs(x + 0.5 - (mid + 0.5)) / max(half, 0.6)
            if d > 1:
                continue
            q = d + t * 0.65
            col = FIRE[4] if q < 0.35 else FIRE[3] if q < 0.62 else FIRE[2] if q < 0.9 else FIRE[1]
            c.px(x, y, col)
    for _ in range(3):                                                    # 불티
        x, y = r.randint(4, 11), r.randint(1, 23 - hgt + 3)
        c.px(x, y, FIRE[3])
    return c.im


def door_frame(ang, gems=(False, False, False), flames=None):
    c = Canvas()
    tops = draw_leaves(c, ang) if ang < 89 else {}
    if ang > 0:                                                           # 열린 개구부: 아치 밑 그늘 (투명 위 반투명)
        for x in range(DOOR_X0, DOOR_X1 + 1):
            for y in range(HT):
                if not opening(x, y) or c.get(x, y)[3] == 255:
                    continue
                d = x + 0.5 - (CX + 0.5)
                arc = SPRING - math.sqrt(max(0.0, RI * RI - d * d))
                dist = y - arc
                a = int(max(0, 150 - dist * 9)) if dist < 17 else 0
                side = min(x - DOOR_X0, DOOR_X1 - x)
                a = max(a, int(max(0, 70 - side * 14)))
                if a:
                    c.px(x, y, (0, 0, 0, a))
        if ang <= 36:                                                     # 문짝이 문턱을 긁으며 이는 먼지
            r = _rng('bossdoor-dust%d' % ang)
            for _ in range(10 + ang // 3):
                x = r.randint(DOOR_X0 + 2, DOOR_X1 - 2)
                y = LEAF_B - r.randint(0, 6 + ang // 6)
                c.px(x, y, (120, 124, 132, r.choice((70, 110, 150))))
    draw_frame(c)
    for a, b in PILLAR:
        draw_pillar(c, a, b)
    for i, lit in enumerate(gems):
        draw_gem(c, i, lit)
    if flames is not None:
        fl = flame(flames)
        for a, b in PILLAR:
            c.im.alpha_composite(fl, (int((a + b) / 2.0) - 7, PILLAR_TOP - 7 - 22))
    return c.im


FLAME_POS = [(int((a + b) / 2.0) - 7, PILLAR_TOP - 7 - 22) for a, b in PILLAR]


def gem_overlay(slot):
    """점등 보석 겹침 스프라이트: 문 그림과 같은 128x128 캔버스에 보석 하나 + 빛 번짐만"""
    c = Canvas()
    draw_gem(c, slot, True)
    return c.im


def draft(out, ctx_png):
    ctx = Image.open(ctx_png).convert('RGBA')
    ox, oy = (347 - 336) * 16, (116 - 97) * 16 - HT                     # 씬 캡처 안 문 자리 (왼쪽 위)
    frames = [door_frame(a) for a in ANGLES]
    S, Z = 3, 2
    page_w = 12 + 4 * (W * S + 16) + 12
    gem_row = [door_frame(0, g, flames=1) for g in ((False,) * 3, (True, False, False), (True, True, False), (True, True, True))]
    scene = []
    for g, a, fl in (((False,) * 3, 0, 0), ((True, True, True), 0, 1), ((True, True, True), ANGLES[-1], 2)):
        p = ctx.copy()
        p.alpha_composite(door_frame(a, g, flames=fl), (ox, oy))
        scene.append(p.crop((96, 64, 448, 400)))
    page_h = 60 + 2 * (HT * S + 40) + HT * S + 60 + scene[0].height * Z + 40
    page = Image.new('RGBA', (max(page_w, 3 * (scene[0].width * Z + 16) + 24), page_h), (35, 36, 42, 255))
    d = ImageDraw.Draw(page)
    d.text((12, 12), '보스룸 문 %s — 128x128 (8x8칸), 문짝 안쪽으로 열림 %d프레임, 아치 보석 3개 (점등 겹침), 화로 불꽃 4프레임' % (VERSION, len(ANGLES)),
           font=load_font(18), fill=(255, 196, 60, 255))
    wallbg = (30, 22, 17, 255)

    def put(im, x, y, tag):
        bg = Image.new('RGBA', im.size, wallbg)
        bg.alpha_composite(im)
        page.alpha_composite(bg.resize((im.width * S, im.height * S), Image.NEAREST), (x, y))
        d.text((x, y + im.height * S + 4), tag, font=load_font(14), fill=(220, 220, 225, 255))

    y = 48
    for i, im in enumerate(frames[:4]):
        put(im, 12 + i * (W * S + 16), y, '열림 f%d (%d°)' % (i, ANGLES[i]))
    y += HT * S + 40
    for i, im in enumerate(frames[4:]):
        put(im, 12 + i * (W * S + 16), y, '열림 f%d (%d°)' % (i + 4, ANGLES[i + 4]))
    y += HT * S + 40
    for i, im in enumerate(gem_row):
        put(im, 12 + i * (W * S + 16), y, '보석 %d개 점등 + 불꽃' % i)
    y += HT * S + 40
    d.text((12, y - 4), '씬에 놓은 모습 (실제 렌더 캡처, 2배): 닫힘 · 보석 3개 점등 · 열림 끝', font=load_font(16), fill=(220, 220, 225, 255))
    for i, p in enumerate(scene):
        page.alpha_composite(p.resize((p.width * Z, p.height * Z), Image.NEAREST), (12 + i * (p.width * Z + 16), y + 22))
    page.save(out)
    gif = out.replace('.png', '.gif')
    seq = []
    for a in list(ANGLES) + [ANGLES[-1]] * 4 + [0] * 4:
        p = ctx.copy()
        p.alpha_composite(door_frame(a, (True, True, True), flames=len(seq) % 4), (ox, oy))
        seq.append(p.crop((96, 64, 448, 400)).resize((704, 672), Image.NEAREST).convert('RGB'))
    seq[0].save(gif, save_all=True, append_images=seq[1:], duration=110, loop=0)
    print(out, page.size, gif)


if __name__ == '__main__':
    draft(*_sys.argv[1:3])
