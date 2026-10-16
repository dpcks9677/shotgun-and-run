"""샷건 총구 섬광(룬 고리) · 불티 · 펠릿 선 분해 시퀀스 시안 · GIF 목업 (Docs/shotgun-magic-rework-plan.md 3 · 5절)

python Docs/tools/spritegen/vfx/magic_vfx.py                       # 시안만 (Docs/drafts/props/muzzle-magic-draft.png)
python Docs/tools/spritegen/vfx/magic_vfx.py --ref <프레임 시트 PNG>  # 시안 위쪽에 레퍼런스 축소본 (2열 × 5행 시트)
python Docs/tools/spritegen/vfx/magic_vfx.py --mockup              # GIF 목업 (Docs/drafts/props/muzzle-magic-mockup.gif)
python Docs/tools/spritegen/vfx/magic_vfx.py --write               # 승인 뒤: Assets에 PNG 쓰기

- 색은 pellet_vfx.py의 펠릿 팔레트(WHITE · FL_*)에 크리티컬용 CRIT_ORANGE 1색을 더해 쓴다. 헬퍼(canvas · put · put_sym · strip)도 그쪽 것.
- 탄 머리는 없다. 펠릿은 1px 선(PixelLineRenderer)이 단계마다 단색 하나 · 대시 무늬로 부서지며 사라진다(LINE_STAGES · stage_pixels).
  어두운 연기 대시는 다른 렌더러 하나(SMOKE_C 단색)로 선 옆에 찍는다(SMOKE_STAGES · smoke_pixels).
- 착탄(ImpactWall · ImpactEnemy)은 기존 시트 그대로라 여기서 만들지 않는다.
- 섬광은 지금처럼 총구(왼쪽 가운데)를 피벗으로 +x로 뻗고, 위아래 대칭(행 7 · 8이 축)이다.
- 불티는 파티클 색(FL_W → FL_Y → FL_O)이 곱해지므로 밝은 색(WHITE · FL_W)으로 그린다.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402
from pellet_vfx import FL_O, FL_R, FL_W, FL_Y, FRAME, WHITE, canvas, put, put_sym, strip  # noqa: E402

CRIT_TINT = (1.0, 0.85, 0.6)
VERSION = 'muzzle-magic v05'
FLASH_FPS = 20                 # 섬광 · 펠릿 선 단계 재생 속도 (6프레임 = 0.3초, 레퍼런스 약 17fps에 맞춰 30 → 20)
FLASH_ALPHA = (255, 255, 255, 255, 180, 90)   # 섬광 프레임별 알파(시트에 구워 넣음) — 마지막 두 장이 0.7 → 0.35로 옅어지며 빠진다

# 섬광 역할별 색: 일반 / 크리티컬 (a) — 크리티컬은 핵 밝기는 그대로, 고리 · 조각 · 흩어짐을 한 단계 진하게
FLASH_NORMAL = dict(core=WHITE, core_edge=FL_W, ring=FL_Y, mark=FL_O, shard=FL_Y, shard_edge=FL_O,
                    scatter=FL_O, scatter_dk=FL_R, last=FL_R)
FLASH_CRIT = dict(FLASH_NORMAL, ring=FL_O, mark=FL_R, shard=FL_O, shard_edge=FL_R, scatter=FL_R)
# (a+) 팔레트 밖 1색: FL_Y와 같은 명도(V=1)에 색상만 주황 쪽 — FL_O로 내리면 휘도가 194 → 149로 떨어져서
CRIT_ORANGE = (255, 140, 10)
FLASH_CRIT_PLUS = dict(FLASH_NORMAL, ring=CRIT_ORANGE, mark=FL_O, shard=CRIT_ORANGE, shard_edge=FL_O)


# ---------------------------------------------------------------- 총구 섬광

def diamond(im, cx, half, core_c, edge_c):
    """총구 축 위 마름모 핵: x별 반높이가 1 → half → 1, 가장자리는 edge_c"""
    widths = list(range(1, half + 1)) + list(range(half - 1, 0, -1))
    for i, h in enumerate(widths):
        for dy in range(h):
            put_sym(im, cx + i, 7 - dy, edge_c if dy == h - 1 or i in (0, len(widths) - 1) else core_c)


def ring_pixels(cx, rx, ry):
    """축(행 7 · 8 사이)에 대칭인 세로로 납작한 타원 테두리, 위쪽부터 시계 방향 순서"""
    inside = {(x, y) for x in range(FRAME) for y in range(FRAME)
              if ((x + 0.5 - cx) / (rx + 0.5)) ** 2 + ((y + 0.5 - 8) / (ry + 0.5)) ** 2 <= 1}
    edge = [p for p in inside if any((p[0] + dx, p[1] + dy) not in inside for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    return sorted(edge, key=lambda p: math.atan2(p[0] + 0.5 - cx, 8 - p[1] - 0.5))


def rune_ring(im, cx, rx, ry, c, mark_c, gap=False):
    """룬 고리: 타원 테두리 + 맨 위 · 아래 바깥 눈금 1px. gap이면 한 칸씩 걸러 끊어진 점선"""
    for i, (x, y) in enumerate(ring_pixels(cx, rx, ry)):
        if not (gap and i % 2):
            put(im, x, y, c)
    put_sym(im, int(cx), 8 - ry - 2 - gap, mark_c)


def shard(im, x, y, core_c, edge_c):
    """작은 마름모 조각 3×3 (가운데 + 십자 끝), 위아래로 짝지어 찍힌다"""
    put_sym(im, x, y, core_c)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        put_sym(im, x + dx, y + dy, edge_c)


def muzzle_flash(p=FLASH_NORMAL):
    """6프레임 (갈래 선 없음, v04 결정). 핵은 같은 크기로 한 번 식은 뒤 줄고(WHITE → FL_W → FL_Y → FL_O),
    고리 · 점은 한 박자씩 색이 내려가며(고리 색 → FL_O → FL_R) 마지막 두 장은 FLASH_ALPHA로 옅어진다"""
    f = [canvas(FRAME, FRAME) for _ in range(6)]
    # 0: 밝은 핵 + 작은 고리
    diamond(f[0], 0, 3, p['core'], p['core_edge'])
    rune_ring(f[0], 7.5, 1, 4, p['ring'], p['mark'])         # 약 3×10
    # 1: 핵은 크기 그대로 한 단계 식음, 고리가 커지고 조각 3개
    diamond(f[1], 0, 3, p['core_edge'], p['ring'])
    rune_ring(f[1], 8.5, 2, 5, p['ring'], p['mark'])         # 5×12
    shard(f[1], 12, 3, p['shard'], p['shard_edge'])         # 위 · 아래 조각
    for x, h in ((12, 1), (13, 2), (14, 1)):                 # 앞쪽 조각 (축 위 마름모)
        for dy in range(h):
            put_sym(f[1], x, 7 - dy, p['shard'] if dy == 0 and x == 13 else p['shard_edge'])
    # 2: 핵이 줄며 한 단계 더 식음, 고리는 위아래 끝부터 끊어지기 시작
    diamond(f[2], 0, 2, p['ring'], p['scatter'])
    for i, (x, y) in enumerate(ring_pixels(9.5, 2, 6)):
        if abs(y + 0.5 - 8) <= 3.5:
            put(f[2], x, y, p['ring'])
        elif i % 2 == 0:
            put(f[2], x, y, p['scatter'])
    put_sym(f[2], 9, 0, p['scatter_dk'])
    for x, y, c in ((13, 2, p['shard']), (14, 3, p['shard_edge']), (14, 7, p['shard']), (15, 7, p['shard_edge'])):
        put_sym(f[2], x, y, c)
    # 3: 핵은 점 하나(FL_O), 고리 옆은 이어진 채 FL_O · 위아래는 FL_R 점선
    put_sym(f[3], 0, 7, p['scatter'])
    for i, (x, y) in enumerate(ring_pixels(9.5, 2, 6)):
        if abs(y + 0.5 - 8) <= 3.5:
            put(f[3], x, y, p['scatter'])
        elif i % 2 == 0:
            put(f[3], x, y, p['scatter_dk'])
    for x, y, c in ((14, 1, p['scatter']), (13, 3, p['scatter_dk']), (15, 7, p['scatter'])):
        put_sym(f[3], x, y, c)
    # 4: 고리 전체가 점선(FL_R, 알파 0.7)
    rune_ring(f[4], 10.5, 2, 6, p['last'], p['last'], gap=True)
    for x, y in ((15, 2), (13, 4), (15, 6)):
        put_sym(f[4], x, y, p['last'])
    # 5: 성긴 점만 (FL_R, 알파 0.35)
    for i, (x, y) in enumerate(ring_pixels(11.5, 2, 6)):
        if i % 4 == 0:
            put(f[5], x, y, p['last'])
    for x, y in ((15, 3), (14, 6)):
        put_sym(f[5], x, y, p['last'])
    return [tint(im, (1, 1, 1, a / 255)) if a < 255 else im for im, a in zip(f, FLASH_ALPHA)]


# ---------------------------------------------------------------- 불티

def spark():
    """2칸 시트(3×3씩): 반짝 점(GrenadeSpark 형태) · 1px 점"""
    im = canvas(6, 3)
    put(im, 1, 1, WHITE)
    for x, y in ((0, 1), (2, 1), (1, 0), (1, 2)):
        put(im, x, y, FL_W, 200)
    put(im, 4, 1, WHITE)
    return im


def tint(im, t=CRIT_TINT):
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            px[x, y] = (round(r * t[0]), round(g * t[1]), round(b * t[2]), round(a * (t[3] if len(t) > 3 else 1)))
    return out


def build():
    vfx = paths.repo('Assets', '09.VFX', 'Weapon', 'Pellet')
    return {
        os.path.join(vfx, 'MuzzleFlash.png'): strip(muzzle_flash()),
        os.path.join(vfx, 'MuzzleFlashCrit.png'): strip(muzzle_flash(FLASH_CRIT_PLUS)),
        os.path.join(vfx, 'MagicSpark.png'): spark(),
    }


# ---------------------------------------------------------------- 펠릿 선 분해 (코드로 옮길 규칙)
# 경로 픽셀 i(총구 0 → 끝 n-1), u = i / n. 단계별 (색, 대시 길이 L, 남길 확률 keep, 총구 쪽 잘림 cut).
# 픽셀 i는 u >= cut 이고 hash01(i // L, seed * 8 + 단계) < keep 이면 켠다. t0은 실선. seed는 펠릿마다 정수 하나.
# 섬광 프레임과 1:1 (FLASH_FPS): t0 · t1 · t1.5 · t2 · t2.5 · t3
LINE_STAGES = [(WHITE, 1, 1.0, 0.0), (FL_Y, 4, 0.75, 0.15), (FL_Y, 3, 0.55, 0.28), (FL_O, 2, 0.35, 0.4), (FL_O, 1, 0.18, 0.45),
               (FL_R, 1, 0.07, 0.5)]
LINE_STAGES_CRIT = [(FL_Y, 1, 1.0, 0.0), (CRIT_ORANGE, 4, 0.75, 0.15), (CRIT_ORANGE, 3, 0.55, 0.28), (FL_O, 2, 0.35, 0.4),
                    (FL_O, 1, 0.18, 0.45), (FL_R, 1, 0.07, 0.5)]
CRIT_THICK_STAGES = 2          # 크리티컬은 t0 · t1만 두께 2px (수직 방향으로 1px 더, v04 결정)
# 연기 대시: 단계별 (u 구간, 대시 길이, keep, 위로 뜬 px). 대시마다 hash로 옆쪽(±)과 거리(1 · 2px)를 정한다
SMOKE_C = (14, 11, 18)
SMOKE_STAGES = [None, ((0.05, 0.45), 3, 0.55, 0), ((0.05, 0.5), 3, 0.5, 1), ((0.05, 0.6), 3, 0.4, 1),
                ((0.08, 0.6), 2, 0.3, 2), ((0.1, 0.6), 2, 0.2, 3)]
STAGES = len(LINE_STAGES)      # = 섬광 프레임 수 (6)


def flash_stage(age):
    """발사 뒤 경과 초 → 섬광 프레임 · 펠릿 선 단계 번호, 끝났으면 None"""
    k = int(age * FLASH_FPS)
    return k if 0 <= k < STAGES else None


def hash01(a, b):
    """정수 해시 → [0, 1). C#: ((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 1024 / 1024f"""
    return (((a * 73856093) & 0xffffffff) ^ ((b * 19349663) & 0xffffffff)) % 1024 / 1024


def stage_pixels(path, seed, stage, stages=LINE_STAGES):
    c, L, keep, cut = stages[stage]
    n = len(path)
    return c, [p for i, p in enumerate(path) if i / n >= cut and hash01(i // L, seed * 8 + stage) < keep]


def smoke_pixels(path, seed, stage):
    if SMOKE_STAGES[stage] is None:
        return []
    (u0, u1), L, keep, rise = SMOKE_STAGES[stage]
    n = len(path)
    (x0, y0), (x1, y1) = path[0], path[-1]
    horizontal = abs(x1 - x0) >= abs(y1 - y0)
    out = []
    for i, (x, y) in enumerate(path):
        k = i // L
        if not (u0 <= i / n < u1) or hash01(k, seed * 8 + stage + 100) >= keep:
            continue
        d = (1 + (hash01(k, seed + 200) < 0.5)) * (1 if hash01(k, seed + 300) < 0.5 else -1)
        out.append((x, y + d - rise) if horizontal else (x + d, y - rise))
    return out


# ---------------------------------------------------------------- 시안

FLOOR = (0x28, 0x24, 0x2f)
GRID = (70, 64, 82)
TEXT = (220, 220, 230)
DIM = (150, 146, 160)
PELLET = paths.repo('Assets', '09.VFX', 'Weapon', 'Pellet')
PELLETS = ((-15, 74), (-7, 80), (0, 70), (6, 81), (14, 64))   # 펠릿 5발 (각도, 사거리 px)


def blend(dst, src, x0, y0, additive=False):
    """src(RGBA)를 dst(RGB)의 (x0, y0)에 — additive면 rgb·α를 더한다(가산 셰이더 근사)"""
    dp, sp = dst.load(), src.load()
    for y in range(src.height):
        for x in range(src.width):
            r, g, b, a = sp[x, y]
            X, Y = x0 + x, y0 + y
            if not a or not (0 <= X < dst.width and 0 <= Y < dst.height):
                continue
            k = a / 255
            br, bg, bb = dp[X, Y]
            if additive:
                dp[X, Y] = (min(255, round(br + r * k)), min(255, round(bg + g * k)), min(255, round(bb + b * k)))
            else:
                dp[X, Y] = (round(br + (r - br) * k), round(bg + (g - bg) * k), round(bb + (b - bb) * k))


def cell(im, scale, pad=1):
    """확대본: 바닥색 위에, 칸 경계를 1px 선으로 (모양이 칸 안에 드는지 보려고)"""
    w, h = im.width * scale, im.height * scale
    out = Image.new('RGB', (w + 2 * pad, h + 2 * pad), GRID)
    out.paste(Image.new('RGB', (w, h), FLOOR), (pad, pad))
    blend(out, im.resize((w, h), Image.NEAREST), pad, pad)
    return out


def frames_row(frames, scale, gap=6):
    cells = [cell(f, scale) for f in frames]
    out = Image.new('RGB', (sum(c.width for c in cells) + gap * (len(cells) - 1), cells[0].height), FLOOR)
    x = 0
    for c in cells:
        out.paste(c, (x, 0))
        x += c.width + gap
    return out


def bresenham(x0, y0, x1, y1):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x1 > x0 else -1), (1 if y1 > y0 else -1)
    err = dx + dy
    while True:
        yield x0, y0
        if (x0, y0) == (x1, y1):
            return
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def draw_pellets(line, smoke, origin, ends, stage, crit):
    """펠릿 선(stage 단계) · 연기 대시를 line · smoke 캔버스에 찍는다. 크리티컬은 t0 · t1 두께 2px"""
    for seed, end in enumerate(ends):
        path = list(bresenham(*origin, *end))
        c, px = stage_pixels(path, seed, stage, LINE_STAGES_CRIT if crit else LINE_STAGES)
        for x, y in px:
            put(line, x, y, c)
            if crit and stage < CRIT_THICK_STAGES:
                put(line, x, y + 1, c)
        for x, y in smoke_pixels(path, seed, stage):
            put(smoke, x, y, SMOKE_C)


def pellet_ends(origin, spread):
    """PELLETS 각도 · 사거리로 끝점 (spread = 사거리 배율)"""
    return [(origin[0] + round(math.cos(math.radians(a)) * d * spread), origin[1] + round(math.sin(math.radians(a)) * d * spread))
            for a, d in PELLETS]


def scene(stage, crit=False, w=96, h=48):
    """1배 합성 한 단계: 총신 자리 + 섬광 프레임(stage) + 펠릿 5발 선(stage) + 연기 대시 + 위로 떠오르는 불티"""
    out = Image.new('RGB', (w, h), FLOOR)
    mx, my = 14, 24                                    # 총구 (섬광 피벗 = 왼쪽 가운데)
    gun = canvas(12, 3)
    for x in range(12):
        for y in range(3):
            put(gun, x, y, (70, 78, 92) if y else (110, 120, 136))
    blend(out, gun, mx - 12, my - 1)
    line, smoke = canvas(w, h), canvas(w, h)
    draw_pellets(line, smoke, (mx + 1, my), pellet_ends((mx + 1, my), 1), stage, crit)
    blend(out, smoke, 0, 0)
    blend(out, line, 0, 0, additive=True)
    blend(out, muzzle_flash(FLASH_CRIT_PLUS if crit else FLASH_NORMAL)[stage], mx, my - 8)
    # 불티: t1부터 나와 단계마다 2px씩 떠오른다. 파티클 색 FL_W → FL_Y → FL_O, 크리티컬은 틴트 곱
    if stage:
        sp = spark()
        big, dot = sp.crop((0, 0, 3, 3)), sp.crop((3, 0, 6, 3))
        c = [v / 255 for v in (FL_W, FL_Y, FL_Y, FL_O, FL_O)[stage - 1]]
        for (x, y, im) in ((13, 18, big), (18, 15, dot), (21, 19, big), (16, 13, dot)):
            col = tint(im, c)
            blend(out, tint(col) if crit else col, x - 1, y - 1 - 2 * stage, additive=True)
    return out


def draft(ref=None):
    font_p = paths.font_path()
    F = ImageFont.truetype(font_p, 16) if font_p else ImageFont.load_default()
    FS = ImageFont.truetype(font_p, 13) if font_p else ImageFont.load_default()
    S = 8
    old = Image.open(os.path.join(PELLET, 'MuzzleFlash.png')).convert('RGBA')
    old_frames = [old.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME)) for i in range(old.width // FRAME)]

    W = 1880
    out = Image.new('RGB', (W, 3200), FLOOR)
    d = ImageDraw.Draw(out)
    d.text((16, 10), VERSION, font=F, fill=(255, 200, 80))
    y = 40
    if ref:
        # 레퍼런스: 2열 × 5행 프레임 시트에서 사격 구간 앞 4장 (행 우선)
        im = Image.open(ref).convert('RGB')
        cw, ch = im.width // 2, im.height // 5
        d.text((16, y), '레퍼런스 사격 프레임 1~4 (축소)', font=FS, fill=DIM)
        x = 16
        for k in range(4):
            fr = im.crop(((k % 2) * cw, (k // 2) * ch, (k % 2 + 1) * cw, (k // 2 + 1) * ch))
            fr = fr.resize((fr.width * 2 // 5, fr.height * 2 // 5), Image.NEAREST)
            out.paste(fr, (x, y + 20))
            x += fr.width + 8
        y += 20 + fr.height + 24
    rows = [('섬광 일반', frames_row(muzzle_flash(), S)), ('크리티컬 섬광 (a+)', frames_row(muzzle_flash(FLASH_CRIT_PLUS), S))]
    top = y
    for label, im in rows:
        d.text((16, y), label, font=FS, fill=TEXT)
        out.paste(im, (16, y + 20))
        y += im.height + 34
    rx = 16 + rows[0][1].width + 40
    d.text((rx, top), '기존 MuzzleFlash (4배)', font=FS, fill=DIM)
    out.paste(frames_row(old_frames, 4), (rx, top + 20))
    d.text((rx, top + 110), '불티 · 1px (8배)', font=FS, fill=DIM)
    out.paste(cell(spark(), S), (rx, top + 130))

    d.text((16, y), f'펠릿 5발 수명 t0 → t3 (1배 → 3배, 섬광 프레임 = 단계, {FLASH_FPS}fps). 선은 단계마다 단색 1개 + 대시, 어두운 연기 대시는 별도 렌더러',
           font=FS, fill=DIM)
    y += 24
    for label, crit in (('일반 1px (선 WHITE → FL_Y → FL_O → FL_R)', False),
                        ('크리티컬 (a+ 섬광, t0 · t1 2px, 선 FL_Y → CRIT_ORANGE → FL_O → FL_R)', True)):
        d.text((16, y), label, font=FS, fill=TEXT)
        x = 16
        for st in range(STAGES):
            big = scene(st, crit).resize((96 * 3, 48 * 3), Image.NEAREST)
            d.text((x, y + 20), ('t0', 't1', 't1.5', 't2', 't2.5', 't3')[st], font=FS, fill=DIM)
            out.paste(big, (x, y + 38))
            x += big.width + 10
        y += 38 + 48 * 3 + 20
    return out.crop((0, 0, W, y))


# ---------------------------------------------------------------- GIF 목업 (캐릭터 + 지금 샷건 사격 클립 위에 새 연출)

FPS = 30
GUN_FIRE = ('shotgun_1', 'shotgun_2', 'shotgun_4', 'shotgun_6', 'shotgun_9')   # Fire.anim 12fps, 키 5개
GUN_FPS = 12
SHOTS = ((0.0, False), (1.2, True))     # (발사 시각 s, 크리티컬): 일반 → 0.5초 대기 → 크리티컬 → 0.8초 대기
LOOP = 2.7
GLOW_COLOR = (1.0, 0.67, 0.27, 0.9)          # MuzzleFlash.prefab _glowColor
GLOW_COLOR_CRIT = (1.0, 0.92, 0.7, 1.0)      # _criticalGlowColor, 크기 1.5배
GLOW_DURATION = STAGES / FLASH_FPS           # 권장 _glowDuration = 섬광 길이 0.3초 (지금 0.08)
GLOW_EASE = 2                                # 알파 = (1 - t/d)^2 — 처음엔 빨리, 끝은 길게 빠진다 (지금 코드는 1제곱 = 선형)
SPARK_COUNT = (3, 4)
SPARK_LIFE = (0.4, 0.7)
SPARK_RISE = (0.6, 1.0)                 # u/s
SPARK_NOISE = 0.15                      # 좌우 흔들림 진폭 u
SPARK_FLICKERS = (2, 3)                 # 수명 동안 알파가 오르내리는 횟수


def spark_particles(rng, crit, t0, origin):
    """계획서 5.2: 발사당 3~4개, 수명 0.4~0.7초, 초기 속도 거의 0, 월드 위로 0.6~1.0u/s, 약한 좌우 흔들림, 색 FL_W → FL_Y → FL_O, 깜빡임"""
    out = []
    for _ in range(rng.randint(*SPARK_COUNT)):
        out.append(dict(t0=t0, crit=crit, life=rng.uniform(*SPARK_LIFE), rise=rng.uniform(*SPARK_RISE) * 16,
                        x=origin[0] + rng.uniform(-1, 4), y=origin[1] + rng.uniform(-2, 2), phase=rng.uniform(0, 6.28),
                        flick=rng.randint(*SPARK_FLICKERS), big=rng.random() < 0.5))
    return out


def spark_color(u):
    keys = (FL_W, FL_Y, FL_O)
    s = min(u * 2, 1.999)
    a, b = keys[int(s)], keys[int(s) + 1]
    f = s - int(s)
    return [round(a[i] + (b[i] - a[i]) * f) / 255 for i in range(3)]


def mockup(scale=4):
    import random
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'weapon'))
    import shotgun_rework as sr

    body = sr.player_body()
    guns = {n: sr.old_frame(n) for n in GUN_FIRE + ('shotgun_0',)}
    W, H = 200, 100
    bx, by = 10, 38
    gx, gy = bx + sr.GUN_AT[0], by + sr.GUN_AT[1]
    muzzle = (round(gx + sr.MUZZLE[0]), round(gy + sr.MUZZLE[1]))
    ends = pellet_ends(muzzle, (W - muzzle[0]) / 80)
    rng = random.Random(5)
    sparks = [p for t, crit in SHOTS for p in spark_particles(rng, crit, t, muzzle)]
    sp = spark()
    big, dot = sp.crop((0, 0, 3, 3)), sp.crop((3, 0, 6, 3))
    cone = Image.open(os.path.join(PELLET, 'GlowCone.png')).convert('RGBA')
    cone_crit = cone.resize((round(cone.width * 1.5), round(cone.height * 1.5)), Image.NEAREST)
    font_p = paths.font_path()
    FS = ImageFont.truetype(font_p, 12) if font_p else ImageFont.load_default()

    frames = []
    for n in range(round(LOOP * FPS)):
        t = n / FPS
        shot_t, crit = max((s for s in SHOTS if s[0] <= t), key=lambda s: s[0])
        k = n - round(shot_t * FPS)                     # 발사 뒤 프레임 수 (총 클립용)
        st = flash_stage(k / FPS)
        out = Image.new('RGB', (W, H), FLOOR)
        blend(out, body, bx, by)
        gi = int(k / FPS * GUN_FPS)
        blend(out, guns[GUN_FIRE[gi]] if gi < len(GUN_FIRE) else guns['shotgun_0'], gx, gy)
        if k / FPS < GLOW_DURATION:
            c = GLOW_COLOR_CRIT if crit else GLOW_COLOR
            g = cone_crit if crit else cone
            fade = (1 - k / FPS / GLOW_DURATION) ** GLOW_EASE
            blend(out, tint(g, (*c[:3], c[3] * fade)), muzzle[0], muzzle[1] - g.height // 2, additive=True)
        if st is not None:
            line, smoke = canvas(W, H), canvas(W, H)
            draw_pellets(line, smoke, muzzle, ends, st, crit)
            blend(out, smoke, 0, 0)
            blend(out, line, 0, 0, additive=True)
            blend(out, muzzle_flash(FLASH_CRIT_PLUS if crit else FLASH_NORMAL)[st], muzzle[0], muzzle[1] - 8)
        for p in sparks:
            age = t - p['t0']
            if not 0 <= age < p['life']:
                continue
            u = age / p['life']
            alpha = 0.55 + 0.45 * math.cos(2 * math.pi * p['flick'] * u + p['phase'])
            if alpha < 0.25:
                continue
            col = tint(big if p['big'] else dot, spark_color(u) + [alpha])
            if p['crit']:
                col = tint(col)
            x = p['x'] + SPARK_NOISE * 16 * math.sin(age * 9 + p['phase'])
            blend(out, col, round(x) - 1, round(p['y'] - p['rise'] * age) - 1, additive=True)
        big_im = out.resize((W * scale, H * scale), Image.NEAREST)
        d = ImageDraw.Draw(big_im)
        d.text((8, H * scale - 20), '크리티컬' if crit else '일반', font=FS, fill=TEXT)
        if n == 0:
            d.text((W * scale - 90, 6), 'mockup v03', font=FS, fill=DIM)
        frames.append(big_im)
    return frames


if __name__ == '__main__':
    if '--write' in sys.argv:
        for path, im in build().items():
            im.save(paths.out(path))
            print('wrote', os.path.relpath(path, paths.REPO))
    if '--mockup' in sys.argv:
        frames = mockup()
        gif = paths.repo('Docs', 'drafts', 'props', 'muzzle-magic-mockup.gif')
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=[30 if i % 3 else 40 for i in range(len(frames))], loop=0, optimize=True)
        print('mockup', os.path.relpath(gif, paths.REPO), len(frames), 'frames', os.path.getsize(gif), 'bytes')
        sys.exit(0)
    dst = paths.repo('Docs', 'drafts', 'props', 'muzzle-magic-draft.png')
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    ref = sys.argv[sys.argv.index('--ref') + 1] if '--ref' in sys.argv else None
    draft(ref).save(dst)
    print('draft', os.path.relpath(dst, paths.REPO))
