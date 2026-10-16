"""Tab 지도 doodle 시안 — Stage1Layout에서 손그림 지도와 존별 공개 레이어를 만든다

python map_doodle.py draft [출력 폴더]    시안 (기본 Docs/drafts/props)
  map-doodle-draft.png         전체 공개 상태 (원본 2배 확대, 최근접)
  map-doodle-states-draft.png  안개만 · 일부 공개 · 전체 공개 비교 (원본 크기)
  map-doodle-ingame-draft.png  1920×1080 화면: 게임 화면(MapScreenshot2 잘라 늘림)을 어둡게 덮고 지도를 MAP_SCALE배로 얹은 모습

python map_doodle.py export [출력 폴더]   런타임 재료 (기본 Assets/04.Sprites/Map) — FullMapView가 존 공개 상태에 맞춰 합성
  map-full.png · map-reveal-N.png(존 4개씩 RGBA 채널) · map-fog-exits.png · map-player-dot.png · map-anchors.png · map-meta.json

화면 배율 (기본 해상도 1920×1080 전체화면, 게임 카메라 orthographic size 20 · PPU 16)
  - 게임 픽셀 1개 = 화면 1080 / 640 = 1.6875px
  - 지도는 1u = 2px로 굽고 화면에 MAP_SCALE(1.5)배로 띄움 → 지도 1px = 화면 1.5px ≈ 게임 픽셀, 지도 높이 약 990px로 1080 안에 들어감

바탕 없음: 지도는 투명 레이어에 선 · 칠만 그리고, 런타임에서 화면을 어둡게 덮은 위에 띄움 (양피지 없음)

픽셀 규칙 (스프라이트 PPU 16 · Point 필터와 맞춤)
  - 1u = 2px로 직접 그림. 안티앨리어싱 · 블러 없음, 흐린 경계는 4×4 Bayer 디더
  - 아이콘은 128px에 그린 뒤 줄이고 알파를 자르고 고정 팔레트로 스냅

규칙 요약
  - 칸마다 걸어서 가장 가까운 존 (lights.nearest_zone과 같은 바닥 BFS, 출발은 존 · 존 기믹룸만)
    → 스폰 · 탈출구 · TP · 보스 · 홀 · 커넥터 모두 한 존에 속함. 존 탐사 퀘스트를 보고하면 그 존 레이어가 열림
  - 존: 금빛 외곽선 2px + 어두운 칠 + 아이콘 / 홀: 흐린 금빛 외곽선 2px, 모양을 크게 뭉갬 / 통로: 흐린 금빛 점선
  - 공개 전 구역: 아무것도 안 그림. 탈출구만 점선 원 X (위치를 몇 u 흔듦, 디더로 옅게), 공개되면 선명한 X
  - 플레이어: 지금 있는 도형의 가운데에 붉은 점 (가운데 꽉 참 + 디더 번짐, 정확한 위치는 안 보임)
      존 · 특수 방 = 방 사각형 가운데, 홀 = 홀 덩어리 안에서 벽과 가장 먼 칸, 통로 = 중심선 길이의 절반 지점
"""
import hashlib, json, os, re, sys
from collections import deque

import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(HERE, '..'))
sys.path.insert(0, os.path.join(HERE, '..', 'halls'))
from parse_layout import parse
import v16

ASSET = os.path.join(ROOT, 'Assets', '_Sandbox', 'Yechann', 'Layouts', 'Stage1Layout.asset')
FONT = os.path.join(ROOT, 'Assets', 'TextMesh Pro', 'Fonts', 'Mulmaru.ttf')
DRAFT_DIR = os.path.join(ROOT, 'Docs', 'drafts', 'props')
EXPORT_DIR = os.path.join(ROOT, 'Assets', '04.Sprites', 'Map')

S = 2                   # 1u = 2px (지도 1px ≈ 화면의 게임 픽셀 하나)
MARGIN = 16             # u — 안개 위 탈출구 X(반지름 8u + 흔들림 6u)가 잘리지 않을 만큼
REVEAL_PAD = 3          # 존 레이어를 소속 칸 밖으로 넓히는 폭 (u) — 외곽선이 잘리지 않게
EXIT_JITTER = 6         # 안개 위 탈출구 X를 흔드는 최대 거리 (u)
PLAYER_DOT_CORE = 2     # 플레이어 붉은 점의 꽉 찬 가운데 반지름 (u)
PLAYER_DOT = 5          # 플레이어 붉은 점이 디더로 번지는 바깥 반지름 (u)
HALL_CLOSE = 6          # 홀 요철을 메우는 닫기 반경 (u) — 클수록 홀 모양을 알아보기 어려움
HALL_HOLE = 400         # 이보다 작은 홀 안 구멍(기둥 · 칸막이)은 메움 (u²)
HALL_SMOOTH = 2.5       # 홀 외곽 흐려 다듬는 반경 (u)
HALL_WARP = 1.5         # 홀 외곽 떨림 (u)

# S1 비밀방은 비활성 (level-design 1.4, 2026-10-14: 통로 · S1 전체를 Blocker로 막음) → 지도에서 숨김
HIDDEN = {'S1'}

ZONE_NAMES = {'Z1': '대장간', 'Z2': '드워프 공방', 'Z3': '하수도', 'Z4': '기관실', 'Z5': '마력석 광맥',
              'Z6': '도축업장', 'Z7': '고블린 캠프', 'Z8': '연금술방', 'Z9': '지하 묘지', 'Z10': '의무실'}

INK = (232, 200, 140)       # 존 선 · 아이콘 선 (금빛)
INK_HALL = (170, 140, 96)   # 홀 선 · 통로 점선 (흐린 금빛)
INK_RED = (214, 66, 52)     # 탈출구 · 보스 · 플레이어
DARK = (28, 21, 16)         # 아이콘 속 빈 곳
ROOM = (52, 39, 28)         # 방 안쪽 칠
ZONE_ALPHA = 0.85
HALL_ALPHA = 0.65
DIM = (10, 8, 6)            # 시안용: 런타임에서 화면을 덮는 색
DIM_ALPHA = 0.85          # 지도가 또렷하고 적 실루엣은 어렴풋이 남는 정도
INGAME_SRC = os.path.join(ROOT, 'Assets', '04.Sprites', 'MapScreenshot2.png')
SCREEN = (1920, 1080)       # 기본 해상도 (ProjectSettings defaultScreenWidth · Height)
GAME_PX = 1080 / 640        # 화면 px / 게임 픽셀: orthographic size 20 → 세로 40u × PPU 16 = 640
MAP_SCALE = 1.5             # 화면 px / 지도 px

ZONE_RE = re.compile(r'^(Z\d+)(-G)?$')
SPEC_RE = re.compile(r'^(E\d|Exit-|TP-|Boss$)')


def kind_of(name):
    if ZONE_RE.match(name): return 'zone'
    if name.startswith('~'): return 'corr'
    if SPEC_RE.match(name): return 'spec'
    return 'hall'

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 + 1 / 32


def blend(a, b, t):
    return tuple(int(round(x + (y - x) * t)) for x, y in zip(a, b))


def bayer(shape):
    return np.tile(BAYER, (shape[0] // 4 + 1, shape[1] // 4 + 1))[:shape[0], :shape[1]]


def dither(a):
    """0..1 배열 → Bayer 디더 bool"""
    return a > bayer(a.shape)


def h(*a):
    return int(hashlib.md5(repr(a).encode()).hexdigest()[:8], 16)


def load():
    rooms, paths, owner = v16.cells_of(ASSET)
    # 지도에 안 그리는 방 · 통로 (막아 둔 곳)
    rooms = [r for r in rooms if r['Name'] not in HIDDEN]
    paths = [p for p in paths if not HIDDEN & set(p['Label'].split('-'))]
    owner = {c: n for c, n in owner.items() if n not in HIDDEN and not HIDDEN & set(n.lstrip('~').split('-'))}
    xs = [c[0] for c in owner]; ys = [c[1] for c in owner]
    box = (min(xs) - MARGIN, max(xs) + MARGIN, min(ys) - MARGIN, max(ys) + MARGIN)
    return rooms, paths, owner, box


def zone_of_cells(owner):
    """칸마다 걸어서 가장 가까운 존 — 출발은 존 · 존 기믹룸 칸"""
    lab = {}
    q = deque()
    for c, n in owner.items():
        m = ZONE_RE.match(n)
        if m: lab[c] = m.group(1); q.append(c)
    while q:
        c = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (c[0] + dx, c[1] + dy)
            if n in owner and n not in lab:
                lab[n] = lab[c]; q.append(n)
    return lab


class Canvas:
    def __init__(self, box):
        self.x0, self.x1, self.y0, self.y1 = box
        self.w = (self.x1 - self.x0 + 1) * S
        self.h = (self.y1 - self.y0 + 1) * S

    def px(self, x, y):
        """레이아웃 연속 좌표 → 픽셀 (y 위가 북쪽)"""
        return (x - self.x0) * S, (self.y1 + 1 - y) * S

    def mask(self, cells):
        """칸 집합 → 픽셀 해상도 bool 배열"""
        m = np.zeros((self.y1 - self.y0 + 1, self.x1 - self.x0 + 1), bool)
        for x, y in cells:
            m[self.y1 - y, x - self.x0] = True
        return np.kron(m, np.ones((S, S), bool))


def noise_field(shape, cell, amp, seed):
    """부드러운 2D 노이즈 (-amp..amp) — 성긴 격자 난수를 bicubic으로 늘림"""
    rng = np.random.default_rng(seed)
    gh, gw = shape[0] // cell + 3, shape[1] // cell + 3
    g = Image.fromarray(((rng.random((gh, gw)) * 255)).astype(np.uint8))
    big = g.resize((gw * cell, gh * cell), Image.BICUBIC)
    a = np.asarray(big, float)[:shape[0], :shape[1]] / 255.0
    return (a * 2 - 1) * amp


def warp(mask, seed, amp=2.5 * S / 4, cell=10 * S):
    """마스크(또는 RGBA 배열)를 노이즈로 살짝 비틀어 손그림 떨림을 냄"""
    hh, ww = mask.shape[:2]
    dx = noise_field((hh, ww), cell, amp, seed)
    dy = noise_field((hh, ww), cell, amp, seed + 1)
    yy, xx = np.mgrid[0:hh, 0:ww]
    sx = np.clip((xx + dx).round().astype(int), 0, ww - 1)
    sy = np.clip((yy + dy).round().astype(int), 0, hh - 1)
    return mask[sy, sx]


def smooth(mask, radius):
    """요철 뭉개기: 흐린 뒤 반으로 자름"""
    im = Image.fromarray(mask.astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(radius))
    return np.asarray(im) >= 128


def simplify_hall(mask):
    """홀 간략화: 닫기로 요철 메움 → 작은 구멍(기둥) 메움 → 흐려 다듬기. 큰 안뜰은 남김"""
    r = HALL_CLOSE * S
    disk = np.hypot(*np.mgrid[-r:r + 1, -r:r + 1]) <= r
    # 떨어진 홀끼리 붙어 없는 길이 생기지 않게 덩어리마다 따로 닫음
    parts, n = ndimage.label(mask)
    m = np.zeros_like(mask)
    for i, sl in enumerate(ndimage.find_objects(parts), 1):
        ys = slice(sl[0].start, sl[0].stop); xs = slice(sl[1].start, sl[1].stop)
        part = np.pad(parts[ys, xs] == i, r)
        m[ys, xs] |= ndimage.binary_closing(part, structure=disk)[r:-r, r:-r]
    holes, n = ndimage.label(ndimage.binary_fill_holes(m) & ~m)
    if n:
        size = ndimage.sum(np.ones_like(holes), holes, range(1, n + 1))
        small = np.isin(holes, 1 + np.flatnonzero(size < HALL_HOLE * S * S))
        m |= small
    return smooth(m, HALL_SMOOTH * S)


def outline(mask, width):
    im = Image.fromarray(mask.astype(np.uint8) * 255)
    grown = np.asarray(im.filter(ImageFilter.MaxFilter(width * 2 + 1))) > 0
    return grown & ~mask


def paint(layer, mask, color, alpha=1.0):
    a = (mask.astype(float) * alpha * 255).astype(np.uint8)
    ink = Image.new('RGBA', layer.size, color + (0,))
    ink.putalpha(Image.fromarray(a))
    layer.alpha_composite(ink)


def dashed(draw, pts, color, width, dash=4, gap=4):
    on, left = True, dash
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        seg = np.hypot(x1 - x0, y1 - y0)
        t = 0.0
        while t < seg:
            d = min(left, seg - t)
            a = (x0 + (x1 - x0) * t / seg, y0 + (y1 - y0) * t / seg)
            b = (x0 + (x1 - x0) * (t + d) / seg, y0 + (y1 - y0) * (t + d) / seg)
            if on: draw.line([a, b], fill=color, width=width)
            t += d; left -= d
            if left <= 0:
                on = not on; left = dash if on else gap


def room_center(r):
    return r['x'] + r['width'] / 2, r['y'] + r['height'] / 2


def font(size):
    return ImageFont.truetype(FONT, size)


# 아이콘: 128px 칸에 그린 뒤 살짝 비틀고 줄여서 찍음 (지도에 텍스트 대신)
ICON = 128
LW = 8                  # 아이콘 선 굵기 (128px 기준, 36px로 줄이면 2px)
FILL = blend(DARK, INK, 0.28) + (255,)
TINT_CRYSTAL = (170, 140, 220, 255)
TINT_FLASK = (110, 170, 110, 255)
TINT_WATER = (110, 150, 190, 255)
TINT_RED = INK_RED + (255,)
ICON_PALETTE = [INK, INK_HALL, INK_RED, DARK, FILL[:3], TINT_CRYSTAL[:3], TINT_FLASK[:3], TINT_WATER[:3]]


def rot(pts, deg, c=(64, 64)):
    a = np.radians(deg); ca, sa = np.cos(a), np.sin(a)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in pts]


def shape(d, pts, ink=INK, fill=FILL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=ink + (255,), width=LW, joint='curve')


def ic_anvil(d):        # Z1 대장간: 모루
    shape(d, [(10, 40), (38, 34), (116, 34), (116, 54), (86, 58), (78, 76), (98, 90), (102, 104),
              (26, 104), (30, 90), (50, 76), (44, 58), (34, 56)])


def ic_hammer(d):       # Z2 드워프 공방: 망치
    shape(d, rot([(58, 40), (70, 40), (70, 118), (58, 118)], 35))
    shape(d, rot([(28, 14), (100, 14), (100, 44), (28, 44)], 35))


def ic_waves(d):        # Z3 하수도: 물결 + 물방울
    for k, y in enumerate((70, 90, 110)):
        pts = [(x, y + 7 * np.sin((x + k * 14) / 10)) for x in range(10, 120, 4)]
        d.line(pts, fill=TINT_WATER, width=LW, joint='curve')
    drop = [(64, 8)] + [(64 + 16 * np.cos(t), 40 + 16 * np.sin(t)) for t in np.linspace(-0.25 * np.pi, 1.25 * np.pi, 30)]
    shape(d, drop, ink=INK, fill=TINT_WATER)


def ic_gear(d):         # Z4 기관실: 톱니
    pts = []
    for i in range(10):
        a = i * 2 * np.pi / 10
        for da, r in ((-0.17, 42), (-0.11, 56), (0.11, 56), (0.17, 42)):
            pts.append((64 + r * np.cos(a + da), 64 + r * np.sin(a + da)))
    shape(d, pts)
    d.ellipse([46, 46, 82, 82], fill=DARK + (255,), outline=INK + (255,), width=LW)


def ic_crystal(d):      # Z5 마력석 광맥: 수정
    tint = TINT_CRYSTAL
    shape(d, [(30, 110), (18, 70), (30, 48), (44, 74), (44, 110)], fill=tint)
    shape(d, [(84, 110), (84, 70), (100, 52), (112, 78), (100, 110)], fill=tint)
    shape(d, [(46, 112), (46, 40), (64, 10), (82, 40), (82, 112)], fill=tint)
    d.line([(64, 10), (64, 112)], fill=INK + (255,), width=LW // 2)


def ic_cleaver(d):      # Z6 도축업장: 식칼
    shape(d, rot([(40, 30), (114, 30), (114, 84), (40, 84)], -18))
    hx, hy = rot([(102, 42)], -18)[0]
    d.ellipse([hx - 7, hy - 7, hx + 7, hy + 7], outline=INK + (255,), width=LW // 2)
    shape(d, rot([(8, 48), (42, 48), (42, 66), (8, 66)], -18))


def ic_tent(d):         # Z7 고블린 캠프: 텐트
    shape(d, [(64, 20), (10, 108), (118, 108)])
    shape(d, [(64, 58), (46, 108), (82, 108)], fill=DARK + (255,))
    d.line([(64, 20), (64, 4)], fill=INK + (255,), width=LW)
    shape(d, [(64, 4), (88, 10), (64, 16)], fill=INK_RED + (255,))


def ic_flask(d):        # Z8 연금술방: 플라스크
    d.polygon([(52, 12), (76, 12), (76, 48), (110, 112), (18, 112), (52, 48)], fill=DARK + (255,))
    d.polygon([(40, 80), (88, 80), (110, 112), (18, 112)], fill=TINT_FLASK)
    shape(d, [(52, 12), (76, 12), (76, 48), (110, 112), (18, 112), (52, 48)], fill=(0, 0, 0, 0))
    for x, y, r in ((56, 94, 5), (72, 100, 4), (66, 86, 3)):
        d.ellipse([x - r, y - r, x + r, y + r], outline=INK + (255,), width=3)


def ic_tomb(d):         # Z9 지하 묘지: 묘비
    arc = [(64 + 32 * np.cos(t), 46 - 32 * np.sin(t)) for t in np.linspace(0, np.pi, 24)]
    shape(d, [(96, 106)] + arc + [(32, 106)])
    d.line([(64, 34), (64, 82)], fill=INK + (255,), width=LW)
    d.line([(48, 50), (80, 50)], fill=INK + (255,), width=LW)
    d.line([(14, 112), (114, 112)], fill=INK + (255,), width=LW)


def ic_cross(d):        # Z10 의무실: 의료 십자
    a, b = 46, 82
    shape(d, [(a, 14), (b, 14), (b, a), (114, a), (114, b), (b, b), (b, 114), (a, 114), (a, b), (14, b), (14, a), (a, a)],
          fill=TINT_RED)


def ic_skull(d):        # 보스: 해골
    shape(d, [(64 + 44 * np.cos(t), 54 + 40 * np.sin(t)) for t in np.linspace(0.8 * np.pi, 2.2 * np.pi, 30)] +
          [(92, 96), (88, 116), (40, 116), (36, 96)], ink=INK_RED, fill=DARK + (255,))
    for x in (46, 82):
        d.ellipse([x - 12, 50, x + 12, 74], fill=INK_RED + (255,))
    d.polygon([(64, 80), (57, 94), (71, 94)], fill=INK_RED + (255,))
    for x in (52, 64, 76):
        d.line([(x, 104), (x, 116)], fill=INK_RED + (255,), width=6)


def ic_exit(d):         # 탈출구: X
    d.line([(20, 20), (108, 108)], fill=INK_RED + (255,), width=LW + 5)
    d.line([(20, 108), (108, 20)], fill=INK_RED + (255,), width=LW + 5)


def ic_exit_unsure(d):  # 안개 위 탈출구: X + 점선 원 (대략적인 위치)
    d.line([(38, 38), (90, 90)], fill=INK_RED + (255,), width=LW + 3)
    d.line([(38, 90), (90, 38)], fill=INK_RED + (255,), width=LW + 3)
    for k in range(16):
        a0 = k * 22.5
        d.arc([6, 6, 122, 122], a0, a0 + 11, fill=INK_RED + (255,), width=LW - 2)


ZONE_ICONS = {'Z1': ic_anvil, 'Z2': ic_hammer, 'Z3': ic_waves, 'Z4': ic_gear, 'Z5': ic_crystal,
              'Z6': ic_cleaver, 'Z7': ic_tent, 'Z8': ic_flask, 'Z9': ic_tomb, 'Z10': ic_cross}


def icon(fn, seed):
    im = Image.new('RGBA', (ICON, ICON), (0, 0, 0, 0))
    fn(ImageDraw.Draw(im))
    return Image.fromarray(warp(np.asarray(im), h(seed), amp=2.5, cell=24))


def pixelize(ic, size, palette=ICON_PALETTE):
    """줄이고 알파를 자르고(0/255) 색을 팔레트로 스냅 — 픽셀 아이콘"""
    small = ic.resize((size, size), Image.BOX)
    a = np.asarray(small.getchannel('A'))
    pal = Image.new('P', (1, 1))
    flat = [v for c in palette for v in c]
    pal.putpalette(flat + flat[:3] * (256 - len(palette)))
    rgb = small.convert('RGB').quantize(palette=pal, dither=Image.Dither.NONE).convert('RGBA')
    rgb.putalpha(Image.fromarray(np.where(a >= 110, 255, 0).astype(np.uint8)))
    return rgb


def stamp(img, fn, cx, cy, size, seed):
    size = int(round(size))
    ic = pixelize(icon(fn, seed), size)
    img.alpha_composite(ic, (int(round(cx - size / 2)), int(round(cy - size / 2))))


def render_full(cv, rooms, paths, owner):
    """전체 공개 상태 지도 한 장 (RGBA, 바탕 투명)"""
    img = Image.new('RGBA', (cv.w, cv.h), (0, 0, 0, 0))
    by_kind = {'zone': set(), 'hall': set(), 'corr': set(), 'spec': set()}
    for c, n in owner.items():
        by_kind[kind_of(n)].add(c)

    # 홀 · 특수 방: 모양을 크게 뭉갠 외곽선 (존과 같은 굵기)
    hall = warp(simplify_hall(cv.mask(by_kind['hall'] | by_kind['spec'])), 21, amp=HALL_WARP * S)
    paint(img, hall, ROOM, HALL_ALPHA)
    paint(img, outline(hall, 2), INK_HALL)

    # 존: 어두운 칠 + 2px 외곽선
    zone = warp(smooth(cv.mask(by_kind['zone']), 0.6 * S), 31)
    paint(img, zone, ROOM, ZONE_ALPHA)
    paint(img, outline(zone, 2), INK)

    # 통로: 중심선 흐린 금빛 점선, 통로 칸 안쪽만
    conn = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(conn)
    for i, p in enumerate(paths):
        pts = [cv.px(x, y) for x, y in p['Points']]
        dashed(d, pts, INK_HALL + (255,), 1, dash=3, gap=2)
    # 간략화한 홀 · 존 안으로 들어간 통로 칸은 빼서 방 안에 점선이 남지 않게
    corr = cv.mask(by_kind['corr']) & ~hall & ~zone
    corr = Image.fromarray(corr.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(3))
    conn.putalpha(Image.fromarray(np.minimum(np.asarray(conn.getchannel('A')), np.asarray(corr))))
    img.alpha_composite(conn)

    d = ImageDraw.Draw(img)
    # 특수 지점 기호
    for r in rooms:
        n = r['Name']; cx, cy = cv.px(*room_center(r))
        if n.startswith('Exit-'):
            stamp(img, ic_exit, cx, cy, 8 * S, ('exit', n))
        elif n == 'Boss':
            stamp(img, ic_skull, cx, cy, 14 * S, 'boss')
        elif n.startswith('TP-'):
            for k, rr in enumerate((3.2, 2.0, 0.9)):
                d.arc([cx - rr * S, cy - rr * S, cx + rr * S, cy + rr * S], 40 * k, 40 * k + 300, fill=INK_HALL, width=1)
        elif re.match(r'^E\d$', n):
            d.polygon([(cx, cy - 2.6 * S), (cx - 2.4 * S, cy + 1.8 * S), (cx + 2.4 * S, cy + 1.8 * S)], outline=INK, width=1)

    # 존 아이콘 (이름 대신)
    for r in rooms:
        if r['Name'] in ZONE_ICONS:
            cx, cy = cv.px(*room_center(r))
            size = min(18, 0.65 * min(r['width'], r['height'])) * S
            stamp(img, ZONE_ICONS[r['Name']], cx, cy, size, ('zone', r['Name']))
    return img


def zone_masks(cv, owner):
    """존마다 공개 정도 (0..1). 바닥 근처 모든 칸(메운 기둥 · 외곽선 포함)을 가장 가까운 존 칸으로 나눠
    존끼리 빈틈없이 맞물리게 하고, 가장자리만 흐림. compose에서 더한 뒤 한 번 디더"""
    lab = zone_of_cells(owner)
    zi = {z: i + 1 for i, z in enumerate(ZONE_NAMES)}
    grid = np.zeros((cv.y1 - cv.y0 + 1, cv.x1 - cv.x0 + 1), int)
    for (x, y), z in lab.items():
        grid[cv.y1 - y, x - cv.x0] = zi[z]
    dist, (iy, ix) = ndimage.distance_transform_edt(grid == 0, return_indices=True)
    near = np.where(dist <= HALL_CLOSE + REVEAL_PAD, grid[iy, ix], 0)
    out = {}
    for z, i in zi.items():
        m = Image.fromarray(np.kron(near == i, np.ones((S, S), bool)).astype(np.uint8) * 255)
        out[z] = np.asarray(m.filter(ImageFilter.GaussianBlur(1.5 * S))) / 255
    return out


def render_fog_exits(cv, rooms, owner):
    """안개 위 탈출구 X — 탈출구마다 (속한 존, 레이어). 그 존이 공개되면 숨김"""
    lab = zone_of_cells(owner)
    out = []
    for r in rooms:
        if not r['Name'].startswith('Exit-'): continue
        zone = next(lab[c] for c, n in owner.items() if n == r['Name'])
        xl = Image.new('RGBA', (cv.w, cv.h), (0, 0, 0, 0))
        x, y = room_center(r)
        a = (h('exit', r['Name']) % 360) * np.pi / 180
        dist = EXIT_JITTER * (0.5 + (h('exitd', r['Name']) % 100) / 200)
        cx, cy = cv.px(x + np.cos(a) * dist, y + np.sin(a) * dist)
        stamp(xl, ic_exit_unsure, cx, cy, 16 * S, ('fogexit', r['Name']))
        a = np.asarray(xl.getchannel('A')) > 0
        xl.putalpha(Image.fromarray(np.where(a & dither(np.full(a.shape, 0.7)), 255, 0).astype(np.uint8)))
        out.append((zone, xl))
    return out


def compose(fog_exits, full, masks, revealed):
    """공개한 존만 남긴 지도 (투명 바탕)"""
    out = Image.new('RGBA', full.size, (0, 0, 0, 0))
    for zone, xl in fog_exits:
        if zone not in revealed:
            out.alpha_composite(xl)
    a = np.zeros((full.height, full.width))
    for z in revealed:
        a = np.minimum(1, a + masks[z])
    layer = full.copy()
    # 존 마스크는 서로 나눠 가진 값이라 더하면 공개한 존끼리의 경계가 1로 이어짐 → 합친 값을 한 번에 디더
    layer.putalpha(Image.fromarray(np.where(dither(a), 255, 0).astype(np.uint8)) if revealed else Image.new('L', full.size, 0))
    out.alpha_composite(layer)
    return out


def path_mid(path):
    """통로 중심선 길이의 절반 지점"""
    pts = path['Points']
    segs = list(zip(pts, pts[1:]))
    lens = [abs(b[0] - a[0]) + abs(b[1] - a[1]) for a, b in segs]
    left = sum(lens) / 2
    for (a, b), ln in zip(segs, lens):
        if left <= ln:
            t = left / ln if ln else 0
            return a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        left -= ln
    return pts[-1]


def player_anchors(rooms, paths, owner):
    """칸 → 플레이어 표시 지점 (레이아웃 좌표). 정확한 위치 대신 지금 있는 도형의 가운데"""
    room = {r['Name']: room_center(r) for r in rooms}
    path = {'~' + p['Label']: path_mid(p) for p in paths}
    # 홀 · 특수 방은 그림과 같이 맞닿은 칸끼리 한 덩어리 → 벽에서 가장 먼 칸 (오목한 홀도 안쪽에 찍힘)
    cells = [c for c, n in owner.items() if kind_of(n) in ('hall', 'spec')]
    x0 = min(c[0] for c in cells); y0 = min(c[1] for c in cells)
    m = np.zeros((max(c[0] for c in cells) - x0 + 3, max(c[1] for c in cells) - y0 + 3), bool)
    for x, y in cells:
        m[x - x0 + 1, y - y0 + 1] = True
    parts, _ = ndimage.label(m)
    dist = ndimage.distance_transform_edt(m)
    blob = {}
    for i, sl in enumerate(ndimage.find_objects(parts), 1):
        d = np.where(parts[sl] == i, dist[sl], -1)
        ix, iy = np.unravel_index(np.argmax(d), d.shape)
        blob[i] = (sl[0].start + ix + x0 - 1 + 0.5, sl[1].start + iy + y0 - 1 + 0.5)
    out = {}
    for c, n in owner.items():
        k = kind_of(n)
        if k == 'corr': out[c] = path[n]
        elif k == 'zone': out[c] = room[n]
        else: out[c] = blob[parts[c[0] - x0 + 1, c[1] - y0 + 1]]
    return out


def player_marker(img, cv, x, y):
    marker_at(img, *cv.px(x, y))


def marker_at(img, cx, cy, color=INK_RED):
    """붉은 점: 가운데는 꽉 차고 바깥으로 갈수록 디더로 옅어짐"""
    yy, xx = np.mgrid[0:img.height, 0:img.width]
    r = np.hypot(xx + 0.5 - cx, yy + 0.5 - cy) / S
    fade = PLAYER_DOT - PLAYER_DOT_CORE
    a = (r <= PLAYER_DOT_CORE) | ((r <= PLAYER_DOT) & dither(np.clip((PLAYER_DOT - r) / fade, 0, 1) * 0.8))
    paint(img, a, color)


def frame(img, title):
    pad = 18
    out = Image.new('RGBA', (img.width, img.height + pad), (40, 32, 26, 255))
    out.alpha_composite(Image.new('RGBA', img.size, DIM + (255,)), (0, pad))
    out.alpha_composite(img, (0, pad))
    ImageDraw.Draw(out).text((4, 3), title, font=font(12), fill=(235, 220, 190, 255))
    return out


def draft(out_dir):
    rooms, paths, owner, box = load()
    cv = Canvas(box)
    full = render_full(cv, rooms, paths, owner)
    fog_exits = render_fog_exits(cv, rooms, owner)
    print('탈출구 소속:', [z for z, _ in fog_exits])
    masks = zone_masks(cv, owner)

    lab = zone_of_cells(owner)
    stray = len(owner) - len(lab)
    print(f'칸 {len(owner)} · 존 소속 {len(lab)} · 소속 없음 {stray}')
    for z in ZONE_NAMES:
        print(f'  {z}: {sum(1 for l in lab.values() if l == z)}칸')

    anchors = player_anchors(rooms, paths, owner)

    def cell_in(name):
        cs = sorted(c for c, n in owner.items() if n == name)
        return cs[len(cs) // 2]

    # 예시 플레이어 위치: 홀 · 통로 · 존 하나씩
    states = [
        ('안개만 (퀘스트 보고 전) · 플레이어 H6b 홀', [], cell_in('H6b')),
        ('일부 공개 (Z1 · Z2 · Z5 보고) · 플레이어 Z2-H1 통로', ['Z1', 'Z2', 'Z5'], cell_in('~Z2-H1')),
        ('전체 공개 · 플레이어 Z3', list(ZONE_NAMES), cell_in('Z3')),
    ]
    panels = []
    for title, rev, cell in states:
        im = compose(fog_exits, full, masks, rev)
        player_marker(im, cv, *anchors[cell])
        panels.append(frame(im, title))

    full_out = panels[-1]
    full_out.resize((full_out.width * 2, full_out.height * 2), Image.NEAREST).convert('RGB').save(
        os.path.join(out_dir, 'map-doodle-draft.png'))

    sheet = Image.new('RGB', (sum(p.width for p in panels) + 8 * (len(panels) - 1), panels[0].height), (20, 16, 12))
    x = 0
    for p in panels:
        sheet.paste(p.convert('RGB'), (x, 0)); x += p.width + 8
    sheet.save(os.path.join(out_dir, 'map-doodle-states-draft.png'))

    # 게임 화면 위: 밝은 곳(Z5 수정 광맥) · 보통 곳(Z9 지하 묘지) — 일부 공개 상태
    im = compose(fog_exits, full, masks, ['Z1', 'Z2', 'Z5'])
    player_marker(im, cv, *anchors[cell_in('H6b')])
    bb = full.getbbox()
    print(f'지도 내용 {bb[2] - bb[0]}×{bb[3] - bb[1]}px → 화면 {round((bb[2] - bb[0]) * MAP_SCALE)}×{round((bb[3] - bb[1]) * MAP_SCALE)}px')
    views = [ingame(im, bb, cv, *room_center(next(r for r in rooms if r['Name'] == z))) for z in ('Z5', 'Z9')]
    out = Image.new('RGB', (SCREEN[0] * 2 + 8, SCREEN[1]), (20, 16, 12))
    out.paste(views[0], (0, 0)); out.paste(views[1], (SCREEN[0] + 8, 0))
    out.save(os.path.join(out_dir, 'map-doodle-ingame-draft.png'))
    print('저장:', out_dir)


def ingame(mapimg, bb, cv, x, y):
    """MapScreenshot2에서 (x, y) 둘레 한 화면을 잘라 1920×1080으로 늘리고 어둡게 덮은 뒤 지도를 MAP_SCALE배로 가운데 얹음"""
    shot = Image.open(INGAME_SRC).convert('RGB')
    # 캡처 축척: 바닥 전체 너비(칸) ↔ 캡처의 밝은 영역 너비로 맞춤
    gray = np.asarray(shot.convert('L')) > 40
    cols = np.flatnonzero(gray.any(0)); rows = np.flatnonzero(gray.any(1))
    span_x = cv.x1 - cv.x0 + 1 - 2 * MARGIN; span_y = cv.y1 - cv.y0 + 1 - 2 * MARGIN
    k = (cols[-1] - cols[0]) / span_x
    sx = cols[0] + (x - (cv.x0 + MARGIN)) * k
    sy = rows[0] + ((cv.y1 - MARGIN) - y) * (rows[-1] - rows[0]) / span_y
    w, hh = SCREEN[0] / GAME_PX / 16 * k, SCREEN[1] / GAME_PX / 16 * k
    bg = shot.crop((int(sx - w / 2), int(sy - hh / 2), int(sx + w / 2), int(sy + hh / 2))).resize(SCREEN, Image.BICUBIC)
    bg = bg.convert('RGBA')
    bg.alpha_composite(Image.new('RGBA', SCREEN, DIM + (int(DIM_ALPHA * 255),)))
    # bb: 전체 공개 지도의 범위 — 공개 상태와 상관없이 지도 위치를 고정
    m = mapimg.crop(bb)
    m = m.resize((round(m.width * MAP_SCALE), round(m.height * MAP_SCALE)), Image.NEAREST)
    bg.alpha_composite(m, ((SCREEN[0] - m.width) // 2, (SCREEN[1] - m.height) // 2))
    return bg.convert('RGB')


def export(out_dir):
    """런타임 재료 — 공개 상태마다 굽지 않고 Unity가 합성하도록 같은 크기(지도 내용 범위)로 잘라 저장"""
    rooms, paths, owner, box = load()
    cv = Canvas(box)
    full = render_full(cv, rooms, paths, owner)
    bb = full.getbbox()
    bb = (bb[0] - bb[0] % 4, bb[1] - bb[1] % 4, bb[2], bb[3])    # 왼쪽 위를 4의 배수로 → 디더 무늬 위상이 draft와 같음
    zones = list(ZONE_NAMES)
    crop = lambda a: a[bb[1]:bb[3], bb[0]:bb[2]]
    os.makedirs(out_dir, exist_ok=True)
    save = lambda im, name: im.save(os.path.join(out_dir, name))

    save(full.crop(bb), 'map-full.png')

    # 존 공개 마스크: 존 4개를 R·G·B·A 한 장에 묶음 (map-reveal-0 = Z1~Z4, ...)
    masks = zone_masks(cv, owner)
    empty = np.zeros((bb[3] - bb[1], bb[2] - bb[0]), np.uint8)
    for f in range(0, len(zones), 4):
        chans = [np.round(crop(masks[z]) * 255).astype(np.uint8) for z in zones[f:f + 4]]
        chans += [empty] * (4 - len(chans))
        save(Image.fromarray(np.stack(chans, -1), 'RGBA'), f'map-reveal-{f // 4}.png')

    # 안개 위 탈출구: R = 속한 존 번호(1부터), A = 그려진 곳
    fog = np.zeros((bb[3] - bb[1], bb[2] - bb[0], 4), np.uint8)
    for zone, xl in render_fog_exits(cv, rooms, owner):
        drawn = crop(np.asarray(xl.getchannel('A'))) > 0
        fog[drawn] = (zones.index(zone) + 1, 0, 0, 255)
    save(Image.fromarray(fog, 'RGBA'), 'map-fog-exits.png')

    # 플레이어 점: 흰색으로 구워 두고 Unity에서 Image.color로 물들임 (미니맵 플레이어 아이콘 색과 같은 값)
    dot = Image.new('RGBA', (PLAYER_DOT * 2 * S + 2, PLAYER_DOT * 2 * S + 2), (0, 0, 0, 0))
    marker_at(dot, dot.width / 2, dot.height / 2, (255, 255, 255))
    save(dot, 'map-player-dot.png')

    # 칸 → 표시 지점: 칸마다 한 픽셀, 지도 이미지 왼쪽 아래 기준 픽셀 좌표를 R·G(x) B·A(y) 16비트로 저장.
    # 텍스처 (x, y) = (칸 x - originX, 칸 y - originY), 소속 칸이 없으면 전부 0
    anchors = player_anchors(rooms, paths, owner)
    w, hh = cv.x1 - cv.x0 + 1, cv.y1 - cv.y0 + 1
    grid = np.zeros((hh, w, 4), np.uint8)
    for (x, y), (ax, ay) in anchors.items():
        px, py = cv.px(ax, ay)
        px, py = round(px - bb[0]), round(bb[3] - py)
        grid[cv.y1 - y, x - cv.x0] = (px & 255, px >> 8, py & 255, py >> 8)
    save(Image.fromarray(grid, 'RGBA'), 'map-anchors.png')

    meta = {'originX': cv.x0, 'originY': cv.y0, 'zones': zones, 'fogColor': dict(zip('rgba', INK_RED + (255,)))}
    with open(os.path.join(out_dir, 'map-meta.json'), 'w', encoding='utf-8') as fp:
        json.dump(meta, fp, ensure_ascii=False, indent=2)
    print(f'저장: {out_dir} (지도 {bb[2] - bb[0]}×{bb[3] - bb[1]}px, 칸 {w}×{hh})')


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'draft'
    if cmd == 'draft':
        draft(sys.argv[2] if len(sys.argv) > 2 else DRAFT_DIR)
    elif cmd == 'export':
        export(sys.argv[2] if len(sys.argv) > 2 else EXPORT_DIR)
    else:
        print(__doc__)
