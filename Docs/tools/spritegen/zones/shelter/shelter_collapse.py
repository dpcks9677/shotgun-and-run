"""쉘터 출격 입구 아치 붕괴 4프레임 (CS_SHELTER_COLLAPSE 2비트) — 시안.

지금 붕괴 뒤 모습은 던전 벽용 무너진 계단(DungeonCollapsedStairs_N)을 아치 위에 덧씌운 것이라 테두리가 두 겹이고
아치 실루엣이 사라진다. 이 그림은 쉘터 아치(ShelterArch_* 4x7칸, 칸 x -2~1 · y 19~25) 원본 픽셀에서 시작해 무너뜨린다.
- 사용자 결정(2026-10-12): 애니메이션 포함, 무너진 정도 B(보가 가운데서 꺾여 내려앉고 기둥 한쪽이 깨짐), 바닥 잔해 1줄.
- 캔버스 64x112 = 아치와 같은 4x7칸. 아치가 있던 자리는 모두 덮고(사라진 자리는 바깥 어둠 색), 바닥 줄(y19)만 투명 + 잔해.
- f0 균열 → f1 보가 꺾임 · 첫 잔해 → f2 오른쪽 반이 떨어짐 · 먼지 → f3 정지 그림(붕괴 뒤 상태로 계속 보임).
  잔해 더미는 한 번만 그리고 프레임마다 아래부터 드러나게 해서 돌 위치가 프레임 사이에 튀지 않는다.
- 색: 아치 원본 픽셀 + 잔해는 무너진 계단과 같은 DungeonDark 팔레트(collapsed_stairs), 먼지는 TutorialCollapseDebris 먼지 3단계.
- 충돌: 바닥 줄(y19) 4칸 — 시안의 노란 틀.

    python shelter_collapse.py [시안.png]     기본: Docs/drafts/props/shelter-collapse-draft.png
    python shelter_collapse.py import         (사용자 승인 v01, 2026-10-12) 시트 · meta 생성 (타일 없음 — 씬 CollapsedStairs 오브젝트용):
        Assets/04.Sprites/Tile/ZoneProps/ShelterCollapse/ShelterCollapse.png (ShelterArchCollapse_f0~f3, 피벗 = 왼쪽 아래 칸 중심 →
        오브젝트를 칸 (-2, 19) 중심 (-1.5, 19.5)에 둠). 다시 실행하면 guid · internalID 재사용.
배경은 Docs/drafts/props/shelter-entrance-stage1.png · stage2.png (쉘터 씬 칸 x -12~13 · y 13~29를 칸당 16px로 찍은 것,
unity/capture_area.cs 방식 — 1단계: 무너진 계단 · 포탈 끔, 2단계: 지금 무너진 계단 켬)."""
import os as _os, sys as _sys
HERE = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.abspath(_os.path.join(HERE, '..', '..')))  # spritegen 루트
_sys.path.insert(0, _os.path.abspath(_os.path.join(HERE, '..', 'dungeon')))
_sys.path.insert(0, HERE)
import sys, random
from PIL import Image, ImageDraw
from bl import Cv, from_img, load_font
import paths
import draftlib as D
import collapsed_stairs as CS
import shelter_props as SP

VERSION = 'v01'
SHEET = 'ShelterCollapse'
W, H = 64, 112
VOID = (30, 22, 17, 255)
DUST = [(78, 74, 70), (104, 98, 91), (132, 124, 114)]
DUST_A = [60, 108, 152, 194]
mix, hsh = CS.mix, CS.hsh


def arch_image():
    t = SP.arch_tiles()
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for (x, y, n) in SP.ARCH:
        im.alpha_composite(t[n], ((x + 2) * 16, (25 - y) * 16))
    return im


ARCH = arch_image()
A = lambda x, y: ARCH.getpixel((x, y))
B_EDGE, B_BODY, B_HI = A(16, 16), A(20, 20), A(40, 26)        # 아치 돌: 테두리 · 몸통 · 밝은 홈

# 아치 구조 (그림 좌표, y0 = 칸 y25 윗변): 보 y16~31 · x2~61, 기둥머리 y32~42, 기둥 y43~94, 받침 y95~104, 문간 x16~47
LINTEL_Y = range(16, 32)
JAG = [0, 1, 1, 0, -1, 0, 1, 2, 1, 0, 0, -1, 0, 1, 1, 0]       # 보가 갈라지는 선 (y16~31)
CAP_JAG = [0, 1, 1, 2, 1, 0, 0, 1, 2, 2, 1, 0, 1, 1, 0, 0]    # 깨진 오른쪽 기둥머리 윗선 (x48~63)


def brk(y):
    return 33 + JAG[y - 16]


def is_lintel(x, y):
    p = A(x, y)
    return y in LINTEL_Y and p[3] > 0 and p[:3] != VOID[:3]


def sag_left(x, k):
    """왼쪽 반이 처지는 양 (x18 경첩부터 가운데로 갈수록)"""
    return (0, 0, max(0, x - 22) // 6, max(0, x - 18) // 5)[k]


def drop_right(x, k):
    """오른쪽 반이 내려간 양: 오른쪽 끝(x61)을 축으로 왼쪽 끝이 더 떨어짐. f3에서 깨진 기둥머리에 걸침"""
    final = 10 + round((61 - x) * 0.45)
    return (0, round((61 - x) * 0.2), round(final * 0.65), final)[k]


def cap_top(x):
    return 40 + CAP_JAG[x - 48]


def blend(c, x, y, col, a):
    p = c.get(x, y)
    if p is not None and p[3] > 200:
        c.px(x, y, mix(p, col + (255,), a / 255.0))
    elif p is None:
        c.px(x, y, col + (a,))


def crack(c, pts, hi=True):
    """잔금: 어두운 선 + 오른쪽 아래 1px 밝은 턱 (빛 왼쪽 위)"""
    pts = [q for q in pts if c.get(*q) not in (None, VOID)]   # 돌 위에만 (떨어져 나간 자리의 어둠에는 안 그림)
    for (x, y) in pts:
        c.px(x, y, CS.DARK)
    if hi:
        for (x, y) in pts:
            if (x + 1, y) not in pts and (x + 1, y + 1) not in pts and c.get(x + 1, y) not in (None, VOID, CS.DARK):
                c.px(x + 1, y, mix(c.get(x + 1, y), B_HI, 0.35))


def walk(x, y, steps, seed):
    r = random.Random(seed)
    pts = [(x, y)]
    for dx, dy in steps:
        x += dx if dx else r.choice((-1, 0, 0, 1))
        y += dy
        pts.append((x, y))
    return pts


def lintel_layers(k):
    """보 · 기둥머리를 프레임 k에 맞게 그린 아치 캔버스, 문간 열마다 보 아랫면 y(ceil), 움직인 보 조각 픽셀(fg)"""
    c = from_img(ARCH)
    left = {(x, y): A(x, y) for y in LINTEL_Y for x in range(W) if is_lintel(x, y) and x < brk(y)}
    right = {(x, y): A(x, y) for y in LINTEL_Y for x in range(W) if is_lintel(x, y) and x >= brk(y)}
    for (x, y) in list(left) + list(right):
        c.px(x, y, VOID)
    fg = {}
    if k >= 2:                                                   # 오른쪽 기둥머리가 깨져 나감
        for x in range(48, 64):
            t = cap_top(x)
            for y in range(32, t):
                c.px(x, y, VOID)
            fg[(x, t)] = mix(B_HI, B_BODY, 0.45)                 # 깨진 윗면 (드러난 돌)
            fg[(x, t + 1)] = CS.DEEP if hsh(x, t, 2) < 0.3 else B_BODY
        fg[(63, cap_top(63))] = B_EDGE
    for (x, y), p in left.items():
        fg[(x, y + sag_left(x, k))] = p
    for (x, y), p in right.items():
        fg[(x, y + drop_right(x, k))] = mix(p, CS.DARK, 0.12) if k >= 2 else p
    if k >= 1:                                                   # 깨진 끝: 드러난 단면은 한 단계 밝게
        for y in LINTEL_Y:
            x = brk(y)
            fg[(x - 1, y + sag_left(x - 1, k))] = mix(B_HI, B_BODY, 0.5)
            if k >= 2:
                fg[(x, y + drop_right(x, k))] = mix(B_HI, B_BODY, 0.3)
    if k >= 2:                                                   # 떨어진 보 아랫면 그늘 (잔해에 닿는 곳)
        for x in range(33, 62):
            q = (x, 31 + drop_right(x, k))
            if q in fg:
                fg[q] = mix(fg[q], CS.DARK, 0.45)
    for (x, y), p in fg.items():
        c.px(x, y, p)
    ceil = {x: max([y for (xx, y) in fg if xx == x] or [31]) for x in range(16, 48)}
    return c, ceil, fg


def pillar_cracks(c, k):
    if k >= 0:
        crack(c, walk(39, 16, [(0, 1)] * 15, 11))                # 보 가운데 금 (f0부터)
        crack(c, walk(52, 33, [(1, 1), (0, 1), (1, 1), (0, 1), (1, 1), (0, 1), (0, 1)], 3))
    if k >= 1:
        crack(c, walk(6, 44, [(0, 1)] * 14, 17))                 # 왼쪽 기둥
    if k >= 2:
        crack(c, walk(58, 41, [(0, 1)] * 10, 23))                # 깨진 오른쪽 기둥
        crack(c, walk(51, 42, [(0, 1)] * 6, 29))


def rubble_layer(ceil):
    """정지 그림의 잔해 더미 (따로 그린 층): 문간을 보 아랫면까지 메우고 바닥 줄(y96~111)로 쏟아짐"""
    lay = Cv(W, H)
    prof = lambda x: 1 - 0.25 * ((x - 31.5) / 16.0) ** 2

    def hf(gx, gy):
        inside = 16 <= gx <= 47
        if not inside and not (gy >= 99 and 12 <= gx <= 51):
            return None
        front = (112 - gy) * 1.35 * prof(gx) * (1 if inside else 0.45)
        if inside:
            gap = int(hsh(gx // 2, 0, 5) * 4) if hsh(gx // 2, 1, 5) < 0.3 else 0
            pocket = int(max(0, 27 - gx) * 1.6)                  # 처진 왼쪽 보 밑은 어두운 틈으로 남김 (더미가 왼쪽으로 낮아짐)
            h = min(gy - (ceil[gx] + 1 + gap + pocket), front)
        else:
            h = front
        h += (hsh(gx // 3, gy // 3, 7) - 0.5) * 2.0
        return h if h > 0 else None

    floor = lambda x, y: 0 <= x < W and y < H and (y >= 105 or (16 <= x <= 47 and y >= 96))
    extra = [(104, 2.0, 'bricks', (19, 99, 9, 2, 2, False)),
             (109, 0.0, 'chunk', (45, 101, 7, 3, 3)),             # 오른쪽 기둥머리 조각 (받침 앞)
             (107, 1.0, 'bricks', (9, 103, 5, 2, 2, True)),
             (88, 9.0, 'rimslab', (20, 74, 12, 2, 3, 1)),          # 보 테두리 조각
             (96, 5.0, 'chunk', (34, 86, 8, 3, 3)),
             (70, 14.0, 'chunk', (28, 58, 9, 3, 4)),               # 더미 위 큰 돌덩이들 (잔자갈 벽처럼 안 보이게)
             (78, 12.0, 'bricks', (36, 64, 9, 2, 2, False)),
             (84, 10.0, 'chunk', (18, 72, 7, 3, 3)),
             (100, 4.0, 'chunk', (24, 92, 9, 3, 4)),
             (101, 4.0, 'bricks', (37, 94, 9, 2, 2, True))]
    CS.heap(lay, hf, (12, 51, 34, 111), seed=61, floor=floor, chunks_extra=extra)
    # 더미 위 돌이 보 · 기둥 위로 삐져나온 것은 잘라내고, 잘린 윗선 2줄은 보 · 틈이 드리운 그늘
    for x in range(W):
        top = ceil[x] + 1 + int(max(0, 27 - x) * 1.6) if 16 <= x <= 47 else 99
        for y in range(H):
            if lay.get(x, y) is None:
                continue
            if y < top:
                lay.p[y][x] = None
            elif y < top + 2 and lay.get(x, y)[3] == 255:
                lay.px(x, y, mix(lay.get(x, y), CS.DARK, 0.5 if y == top else 0.25))
    return lay


def reveal(lay, k):
    """잔해 층을 프레임 k까지 아래부터 드러낸 것 (드러난 윗줄은 한 단계 밝게 = 쌓이는 중인 윗면)"""
    if k >= 3:
        return lay
    thr0 = (H, 99, 64)[k]
    out = Cv(W, H)
    thr = {x: thr0 + int(hsh(x // 2, k, 13) * 5) for x in range(W)}
    for y in range(H):
        for x in range(W):
            p = lay.get(x, y)
            if p is None or y < thr[x]:
                continue
            out.px(x, y, CS.GRAVEL[3] if (y == thr[x] and p[3] == 255) else p)
    return out


def falling(c, k):
    r = random.Random(70 + k)
    if k == 0:
        for (x, y) in ((29, 48), (36, 61), (31, 75)):
            CS.chunk(c, x, y, 2, 1, 1, r)
    elif k == 1:
        CS.prism(c, 27, 57, 3, 1.5, 35, 3)
        CS.prism(c, 39, 70, 4, 2, -20, 3, warm=True)
        for (x, y) in ((33, 45), (22, 80), (42, 86)):
            CS.chunk(c, x, y, 2, 1, 1, r)
    elif k == 2:
        CS.prism(c, 24, 66, 4, 2, -40, 3)
        for (x, y) in ((40, 58), (30, 54)):
            CS.chunk(c, x, y, 3, 1, 2, r)


def dust(c, cx, cy, rx, ry, seed, power):
    """먼지 구름: 가운데 진하게 → 가장자리 옅게, 둘레는 들쭉날쭉 (TutorialCollapseDebris 먼지 색)"""
    for y in range(int(cy - ry - 2), int(cy + ry + 3)):
        for x in range(int(cx - rx - 2), int(cx + rx + 3)):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 + (hsh(x // 2, y // 2, seed) - 0.5) * 0.5
            if d >= 1:
                continue
            lv = min(len(DUST_A) - 1, int((1 - d) * power * len(DUST_A)))
            col = DUST[min(2, int(hsh(x // 3, y // 2, seed + 1) * 2.2 + (1 - d)))]
            blend(c, x, y, col, DUST_A[lv])


def frame(k, lay):
    c, _, fg = lintel_layers(k)
    rv = reveal(lay, k)
    for y in range(H):
        for x in range(W):
            p = rv.get(x, y)
            if p is not None:
                if p[3] == 255:
                    c.px(x, y, p)
                else:
                    blend(c, x, y, p[:3], p[3])
    for (x, y), p in fg.items():                                 # 보 조각을 잔해 위에 다시 (잔해 돌이 보 위로 삐져나오지 않게)
        c.px(x, y, p)
    if k >= 1:
        crack(c, walk(18, 17, [(0, 1)] * 12, 5))                 # 처진 쪽 경첩 금
    pillar_cracks(c, k)
    falling(c, k)
    if k == 0:
        dust(c, 39, 35, 5, 3, 3, 0.45)
    elif k == 1:
        dust(c, 36, 37, 8, 4, 4, 0.6)
        dust(c, 32, 103, 15, 5, 5, 0.5)
    elif k == 2:
        dust(c, 32, 70, 22, 18, 6, 0.9)
        dust(c, 32, 102, 30, 8, 7, 0.8)
        dust(c, 52, 36, 9, 5, 8, 0.6)
    return c


LABELS = ['f0 균열', 'f1 보가 꺾임', 'f2 무너짐 · 먼지', 'f3 정지 (붕괴 뒤)']


def items():
    _, ceil, _ = lintel_layers(3)
    lay = rubble_layer(ceil)
    return [D.make('ShelterArchCollapse_f%d' % k, LABELS[k], frame(k, lay), 'anim', coll=(4, 1), shadow=False, outline=False,
                   note='충돌 바닥 1줄' if k == 3 else '') for k in range(4)]


def __getattr__(name):
    if name == 'ITEMS':
        return items()
    raise AttributeError(name)


# ---------- 시안 ----------
BG1 = paths.repo('Docs', 'drafts', 'props', 'shelter-entrance-stage1.png')
BG2 = paths.repo('Docs', 'drafts', 'props', 'shelter-entrance-stage2.png')
PX, PY = 160, 64                                              # 배경 캡처에서 아치 왼쪽 위 (칸 x-2 · y25)
CROP = (112, 32, 272, 208)                                     # 패널: 칸 x -5~4 · y 18~28


def panel(bg, sprite=None):
    im = Image.open(bg).convert('RGBA')
    if sprite is not None:
        im.alpha_composite(sprite, (PX, PY))
    return im.crop(CROP)


def scene(its):
    w, h = CROP[2] - CROP[0], CROP[3] - CROP[1]
    gap = 8
    sc = Image.new('RGBA', (4 * w + 3 * gap, 2 * h + gap), (35, 36, 42, 255))
    top = [panel(BG1), panel(BG2), panel(BG1, its[3]['fin'].img())]
    for i, p in enumerate(top):
        sc.alpha_composite(p, (i * (w + gap), 0))
    for i, it in enumerate(its):
        sc.alpha_composite(panel(BG1, it['fin'].img()), (i * (w + gap), h + gap))
    return sc, w, h, gap


def main(out):
    its = items()
    sc, w, h, gap = scene(its)
    S = 3
    im = D.draft(its, [('애니메이션 4프레임 (64x112 = 4x7칸, 아치와 같은 자리) — 노란 틀 = 충돌 (바닥 줄 y19)', ('anim',))], sc, sc.size,
                 '출격 입구 아치 붕괴 %s  ·  B안: 보가 가운데서 꺾여 내려앉고 오른쪽 기둥머리가 깨짐, 잔해 바닥 1줄' % VERSION, S=S, Wd=sc.width * S + 24)
    d = ImageDraw.Draw(im)
    f = load_font(17)
    y0 = im.height - 30 - sc.height * S
    names = ['지금 1단계 (붕괴 전)', '지금 2단계 (던전용 무너진 계단 덧씌움)', '새 그림 f3 (붕괴 뒤 정지)']
    for i, n in enumerate(names):
        d.text((12 + i * (w + gap) * S + 6, y0 + 4), n, font=f, fill=(255, 220, 120, 255))
    for i in range(4):
        d.text((12 + i * (w + gap) * S + 6, y0 + (h + gap) * S + 4), '게임 화면 ' + LABELS[i], font=f, fill=(255, 220, 120, 255))
    im.save(out)
    zoom = Image.new('RGBA', (4 * 70 * 5, 120 * 5), (44, 40, 38, 255))
    for k, it in enumerate(its):
        sp = it['fin'].img()
        zoom.alpha_composite(sp.resize((sp.width * 5, sp.height * 5), Image.NEAREST), (k * 70 * 5, 0))
    zoom.save(out.replace('.png', '-zoom.png'))
    print('saved', out)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        import gen_zone
        gen_zone.main(SHEET, 'shelter_collapse', tiles=False)
        sys.exit(0)
    main(sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'shelter-collapse-draft.png'))
