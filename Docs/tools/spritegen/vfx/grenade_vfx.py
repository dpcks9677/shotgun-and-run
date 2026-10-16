"""수류탄 · 섬광탄 폭발 연출 스프라이트 (탑다운) — 계획서 Docs/grenade-explosion-vfx-plan.md

python Docs/tools/spritegen/vfx/grenade_vfx.py [시안.png] [--gif]   # 시안(+GIF)만 만든다 (기본: Docs/drafts/props/grenade-vfx-draft.png)
python Docs/tools/spritegen/vfx/grenade_vfx.py import              # Assets/09.VFX/Consumable/{Grenade,Flash}/ 에 시트 PNG를 쓴다 (SPRITEGEN_WRITE_ROOT로 리허설)

임포트 설정 · 머티리얼 · 프리팹은 unity/grenade_vfx_setup.cs (unity command eval_file).

[파편 수류탄] 기준 반경 R = 2.5u = 40px. 프레임 96×96 (R + 여유 8px), 중심 = (47.5, 47.5)
- Fireball  14프레임 @30fps  불덩이: 어두운 갈색 덩어리 사이로 주황 · 노랑 용암이 비치는 셀 셰이딩, 중심에서 R까지 부풀고 식는다
- ShockRing 5프레임          R까지 퍼지는 얇은 고리 (가산)
- FlashCore 1프레임          첫 순간 번쩍 (가산)
- Dust      32×32 4종        흙먼지 구름 조각 (파티클 텍스처)
- Scorch    1프레임          그을음 데칼
- Spark     3×3              불씨 점

[섬광탄] 기준 반경 R = 4u = 64px. 프레임 144×144 (R + 여유 8px), 중심 = (71.5, 71.5)
- FlashRing  8프레임 @24fps  속이 빈 도넛이 중심에서 R까지 커지며 얇아진다 (가산)
- FlashCross 7프레임         중앙 십자 섬광(4방향 주 광선 + 대각선 보조 4줄). 순백은 중앙 핵 3px 안에만 (가산)
"""
import math
import os
import random
import sys
import time

from PIL import Image, ImageChops, ImageDraw, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import draftlib  # noqa: E402
import paths  # noqa: E402

VERSION = 'v08'

# ---- 색 (pellet_vfx.py 팔레트와 같은 계열)
FL_W = (255, 240, 160)
FL_Y = (255, 195, 25)
FL_O = (229, 138, 0)
FL_R = (184, 66, 6)
WHITE = (255, 255, 255)
GOLD_IN = (255, 236, 150)     # 섬광 안쪽 (연한 노랑 — 순백 아님)
GOLD_MID = (255, 205, 60)
GOLD_OUT = (230, 155, 20)

CHAR_OUT = (26, 10, 8)        # 불덩이 바깥 외곽선
CHAR = [(14, 8, 12), (26, 14, 18), (42, 22, 26)]   # 연기 덩어리 4단 (검붉은 숯 → 빛 받는 면). 갈색 흙색이 아니라 타는 연기
LAVA = [(112, 30, 8), FL_R, FL_O, FL_Y, FL_W]          # pellet_vfx.py 불꽃 팔레트(FL_R · FL_O · FL_Y · FL_W) + 가장 어두운 붉은색
OUT = (14, 10, 9)                                    # 소품 시트와 같은 외곽선 색(bl.OUT)

DUST_HI = (170, 162, 148)     # 먼지 3톤. 소품 · 펠릿 먼지(132,126,116 계열)와 같은 회갈색 계열을 한 단계씩 밝게 · 어둡게
DUST = (132, 126, 116)
DUST_DK = (92, 88, 82)

# 바닥에 깔리는 요소(충격 링 · 그을음 · 섬광 도넛)의 세로 비율. dev의 ExplosionRangeIndicator._verticalScale(기본 0.65)과 같아야 범위 표시와 맞는다.
# 값을 바꾸면 이 도구로 시트를 다시 만들어 임포트한다 (픽셀을 늘리지 않도록 프리팹 스케일 대신 시트에 구워 둠).
GROUND_V = 0.65
R_FRAG = 40
R_FLASH = 64
SZ_FRAG = 96
FIRE_W, FIRE_H = 96, 112          # 불덩이는 위로 솟으므로 세로가 더 길다
FIRE_GX, FIRE_GY = 47.5, 63.5      # 불덩이 프레임 안의 바닥 중심(피벗)
DUST_W, DUST_H = 48, 32
SZ_FLASH = 144
FRAG_FRAMES = 14
RING_FRAMES = 5
DUST_STAGES = 4                 # 먼지는 배율로 키우지 않고 크기가 다른 프레임 4장을 순서대로 재생한다(도트가 섞이지 않게)
DUST_VARIANTS = 3
FLASH_RING_FRAMES = 8
FLASH_CROSS_FRAMES = 7


def canvas(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def ease_out(t):
    t = max(0.0, min(1.0, t))
    return 1 - (1 - t) ** 3


def lerp(a, b, t):
    return a + (b - a) * t


def _hash(ix, iy, seed):
    n = (ix * 374761393 + iy * 668265263 + seed * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def vnoise(x, y, seed=0):
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = _hash(ix, iy, seed)
    b = _hash(ix + 1, iy, seed)
    c = _hash(ix, iy + 1, seed)
    d = _hash(ix + 1, iy + 1, seed)
    return lerp(lerp(a, b, fx), lerp(c, d, fx), fy)


# ================================================================ 파편 수류탄

# 불덩이 덩어리 12개 — (X, Y, Z 높이, 반지름, 늘임 x, 등장 u, 상승 속도, 식는 u, 열기). 단위: size.
# 손으로 짠 비대칭 배치다: 큰 밑덩이 3개 + 중간 · 위로 갈수록 작고, 크기 · 높이 · 늘임 · 등장 · 식는 시각이 전부 다르다.
# (둥근 덩어리를 고리로 규칙적으로 늘어놓았더니 '알뭉치'로 보였다)
LUMPS = [
    (-0.34, 0.10, 0.16, 0.42, 1.10, 0.00, 0.05, 0.66, 0.95),
    (0.40, 0.06, 0.14, 0.38, 0.95, 0.02, 0.04, 0.68, 0.95),
    (0.02, 0.30, 0.10, 0.34, 1.15, 0.04, 0.03, 0.64, 0.90),
    (0.00, -0.08, 0.44, 0.44, 1.00, 0.00, 0.10, 0.70, 1.10),
    (-0.58, -0.04, 0.28, 0.27, 0.90, 0.05, 0.12, 0.62, 0.80),
    (0.60, -0.10, 0.34, 0.29, 1.10, 0.07, 0.15, 0.60, 0.80),
    (-0.22, -0.26, 0.82, 0.30, 1.00, 0.10, 0.22, 0.58, 0.75),
    (0.28, -0.22, 0.74, 0.23, 0.95, 0.14, 0.20, 0.54, 0.70),
    (0.04, -0.14, 1.04, 0.21, 1.00, 0.18, 0.26, 0.52, 0.60),
    (-0.78, 0.18, 0.06, 0.17, 1.00, 0.12, 0.03, 0.56, 0.60),
    (0.76, 0.16, 0.05, 0.16, 1.20, 0.16, 0.03, 0.54, 0.60),
    (-0.14, 0.50, 0.05, 0.17, 1.00, 0.10, 0.02, 0.58, 0.70),
]


def fireball_frame(idx, n=FRAG_FRAMES):
    """비스듬히 본 불덩이 — 덩어리 12개를 **하나의 연속 높이장(메타볼)** 으로 합친다.
    덩어리마다 윤곽선을 긋거나 구 셰이딩을 따로 하면 구체 경계가 드러나 어색하므로(v03 피드백),
    기여도를 모두 더한 높이장 H에서 윤곽(H > 문턱) · 열기 · 음영(H의 기울기)을 한 번에 구한다 → 덩어리 사이가 부드럽게 이어지고 골만 살짝 파인다.
    0~6프레임: 노랑 · 주황 불꽃뿐(연기 없음). 7프레임부터 덩어리마다 다른 시각에 어두운 연기로 식고(바깥 · 위가 먼저) 골에 용암이 비친다"""
    u = idx / (n - 1)
    grow = ease_out(0.22 + 0.78 * min(1.0, u / 0.5)) if idx > 0 else 0.30
    cool = max(0.0, (u - 0.5) / 0.5)
    fade = 1.0 - max(0.0, (u - 0.78) / 0.22) * 0.65
    size = 33.0 * grow
    L = (-0.5, -0.6, 0.62)
    ln = math.sqrt(sum(v * v for v in L))
    L = tuple(v / ln for v in L)
    ls = []
    for (X, Y, Z, rho, sx, tb, vz, tc, hot) in LUMPS:
        life = ease_out((u + 0.05 - tb) / 0.28) if u + 0.05 > tb else 0.0
        if life <= 0:
            continue
        zr = Z * 0.85 + u * vz * 1.4
        ls.append((X * size, Y * size - zr * size, rho * size * 1.38 * (0.55 + 0.45 * life) * (1 + 0.06 * math.sin(u * 8 + X * 9)),
                   sx, hot - 0.35 * cool, max(0.0, min(1.0, (u - tc) / 0.20))))
    TH = 0.20
    W, H = FIRE_W + 2, FIRE_H + 2
    hg = [[0.0] * W for _ in range(H)]
    heatg = [[0.0] * W for _ in range(H)]
    darkg = [[0.0] * W for _ in range(H)]
    ovg = [[0.0] * W for _ in range(H)]
    for gy in range(H):
        for gx in range(W):
            dx, dy = gx - 1 - FIRE_GX, gy - 1 - FIRE_GY
            tot = hw = dw = mx = 0.0
            for cx, cy, r, sx, hot, dk in ls:
                t = 1.0 - (math.hypot((dx - cx) / sx, dy - cy) / r) ** 2
                if t > 0:
                    c = t * t
                    tot += c
                    hw += c * hot
                    dw += c * dk
                    if c > mx:
                        mx = c
            hg[gy][gx] = tot
            if tot > 0:
                heatg[gy][gx] = hw / tot
                darkg[gy][gx] = dw / tot
            ovg[gy][gx] = tot - mx                  # 덩어리가 겹치는 정도 = 골(crease)
    im = canvas(FIRE_W, FIRE_H)
    px = im.load()
    for y in range(FIRE_H):
        for x in range(FIRE_W):
            gx, gy = x + 1, y + 1
            tot = hg[gy][gx]
            dxp, dyp = x - FIRE_GX, y - FIRE_GY
            if tot < TH:
                continue
            dark = darkg[gy][gx]
            edge = min(hg[gy][gx - 1], hg[gy][gx + 1], hg[gy - 1][gx], hg[gy + 1][gx]) < TH
            if edge:
                col = tuple(round(lerp(a, b, dark)) for a, b in zip((120, 34, 6), CHAR_OUT))
                px[x, y] = (*col, round(255 * fade))
                continue
            gxd = (hg[gy][gx + 1] - hg[gy][gx - 1]) * 0.5
            gyd = (hg[gy + 1][gx] - hg[gy - 1][gx]) * 0.5
            K = 5.0
            nxv, nyv, nzv = -gxd * K, -gyd * K, 1.0
            nl = math.sqrt(nxv * nxv + nyv * nyv + nzv * nzv)
            dif = max(0.0, (nxv * L[0] + nyv * L[1] + nzv * L[2]) / nl)
            valley = ovg[gy][gx]
            if dark > 0.5:
                sh = 0.65 * dif + 0.18
                col = CHAR[0] if sh < 0.34 else CHAR[1] if sh < 0.62 else CHAR[2]
                if valley > 0.42 and cool < 0.8:      # 연기 덩어리 사이 골에 가는 용암빛
                    col = LAVA[2] if valley > 0.8 else LAVA[1]
            else:
                tt = min(1.0, tot / 1.2)                                   # 높이: 덩어리 마루일수록 밝다
                v = 0.30 * heatg[gy][gx] + 0.62 * dif + 0.38 * tt - 0.27 - dark * 0.9 - min(0.30, valley * 0.35)
                v = 0.58 + (v - 0.58) * 1.6                                # 명암 폭을 넓혀 밝은 면 · 붉은 골이 보이게
                col = LAVA[4] if v > 1.02 else LAVA[3] if v > 0.78 else LAVA[2] if v > 0.52 else LAVA[1] if v > 0.30 else LAVA[0]
            px[x, y] = (*col, round(255 * fade))
    return im


def shock_ring_frame(idx, n=RING_FRAMES):
    """바닥 충격 링 — R까지 퍼지는 얇은 고리 (가산)"""
    rs = [0.35, 0.60, 0.80, 0.95, 1.0]
    ths = [2.4, 2.4, 2.0, 1.7, 1.6]                  # 세로로 0.65배가 되어도 1px 이상 남도록
    als = [235, 215, 170, 110, 55]
    r = (R_FRAG - 1) * rs[idx]
    c = (SZ_FRAG - 1) / 2
    im = canvas(SZ_FRAG, SZ_FRAG)
    px = im.load()
    for y in range(SZ_FRAG):
        for x in range(SZ_FRAG):
            d = math.hypot(x - c, (y - c) / GROUND_V)
            if r - ths[idx] <= d <= r:
                px[x, y] = (255, 228, 170, als[idx])
    return im


def flash_core():
    """파편 폭발 첫 순간 번쩍 — 가산. 흰 핵 + 노랑 테"""
    c = (SZ_FRAG - 1) / 2
    im = canvas(SZ_FRAG, SZ_FRAG)
    px = im.load()
    for y in range(SZ_FRAG):
        for x in range(SZ_FRAG):
            d = math.hypot(x - c, y - c)
            if d <= 16:
                px[x, y] = (255, 246, 214, 255)
            elif d <= 22:
                px[x, y] = (255, 205, 80, 200)
            elif d <= 26:
                px[x, y] = (255, 160, 30, 110)
    return im


def dust_puff(seed, stage):
    """48×32 먼지 구름 한 장 — 겹친 둥근 봉우리(위는 몽글, 아래는 납작). stage 0~3: 같은 구름이 점점 커지는 4프레임.
    기존 작업본(DashDust · 펠릿 먼지)처럼 **불투명한 도트 + 3톤 평면 명암**(위 밝고 아래 어둡다)이고 투명도는 파티클이 준다.
    부드러운 알파 가장자리 · 노이즈는 쓰지 않는다. 바닥 중심(24, 24)을 기준으로 커진다"""
    rnd = random.Random(seed * 31 + 7)
    n = rnd.choice((6, 7, 8))
    circles = []
    for i in range(n):
        t = (i + rnd.uniform(-0.2, 0.2)) / (n - 1)
        cx = 8 + 32 * t
        r = 4.5 + 6.0 * math.sin(math.pi * (0.15 + 0.7 * t)) * rnd.uniform(0.75, 1.1)
        circles.append((cx, 23 - r * 0.78, r))
    circles.append((24, 21, 11))
    sc = (0.30, 0.42, 0.54, 0.66)[stage]               # v07: 최대 크기 0.66배(가장 큰 단계 약 32×20px). 게임에서 Tight 메시 때문에 늘어나 보였던 문제는 임포트(FullRect)로 해결
    circles = [(24 + (cx - 24) * sc, 24 + (cy - 24) * sc, r * sc) for cx, cy, r in circles]
    mask = {}
    for y in range(DUST_H):
        for x in range(DUST_W):
            if y > 24:                                             # 아래는 납작하게 잘라 땅에 붙은 구름
                continue
            e = max(r - math.hypot(x + 0.5 - cx, y + 0.5 - cy) for cx, cy, r in circles)
            if e >= 0:
                mask[(x, y)] = e
    ys = [y for _, y in mask]
    y0, y1 = min(ys), max(ys)
    im = canvas(DUST_W, DUST_H)
    px = im.load()
    for (x, y), e in mask.items():
        v = (y - y0) / max(1.0, y1 - y0)
        col = DUST_HI if v < 0.34 else DUST if v < 0.72 else DUST_DK
        px[x, y] = (*col, 255)
    if seed % 2:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    return im


def scorch():
    """그을음 데칼 96×96 — 불규칙한 얼룩, 가운데가 더 진하다 (3단 알파)"""
    c = (SZ_FRAG - 1) / 2
    im = canvas(SZ_FRAG, SZ_FRAG)
    px = im.load()
    for y in range(SZ_FRAG):
        for x in range(SZ_FRAG):
            dx, dy = x - c, y - c
            dy = dy / GROUND_V                                  # 바닥 타원(세로 0.65배)
            ang = math.atan2(dy, dx)
            rr = 31 + 5 * vnoise(math.cos(ang) * 2 + 5, math.sin(ang) * 2 + 5, 9) + 3 * math.sin(ang * 3 + 1)
            d = math.hypot(dx, dy) + (vnoise(x / 3.0, y / 3.0, 4) - 0.5) * 4
            if d > rr:
                continue
            a = 190 if d < rr * 0.5 else 140 if d < rr * 0.78 else 85
            px[x, y] = (14, 10, 8, a)
    return im


def spark():
    im = canvas(3, 3)
    for x, y, a in ((1, 1, 255), (0, 1, 150), (2, 1, 150), (1, 0, 150), (1, 2, 150)):
        im.putpixel((x, y), (255, 190, 70, a))
    return im


# ================================================================ 섬광탄

def flash_ring_frame(idx, n=FLASH_RING_FRAMES):
    """속이 빈 도넛: 바깥 반경이 중심 → R로 커지고 두께는 얇아진다 (가산)"""
    u = idx / (n - 1)
    ro = 10 + (R_FLASH - 1 - 10) * ease_out(u)
    th = lerp(18, 5, u)
    ri = max(0.0, ro - th)
    al = 0.85 if u < 0.5 else lerp(0.85, 0.22, (u - 0.5) / 0.5)
    c = (SZ_FLASH - 1) / 2
    im = canvas(SZ_FLASH, SZ_FLASH)
    px = im.load()
    for y in range(SZ_FLASH):
        for x in range(SZ_FLASH):
            d = math.hypot(x - c, (y - c) / GROUND_V)               # 바닥 타원(세로 0.65배)
            if d > ro or d < ri:
                continue
            p = (d - ri) / max(ro - ri, 1e-6)
            if ro - d < 1.0 / GROUND_V:
                col = (255, 215, 80)            # 바깥 1px 테
            elif p > 0.66:
                col = GOLD_OUT
            elif p > 0.33:
                col = GOLD_MID
            else:
                col = GOLD_IN
            px[x, y] = (*col, round(255 * al))
    return im


def flash_cross_frame(idx, n=FLASH_CROSS_FRAMES):
    """중앙 십자 섬광: 4방향 주 광선 + 짧은 대각선 보조 4줄 + 핵. 순백은 중앙 반경 3px 안만"""
    Ls = [51, 50, 44, 36, 28, 20, 12]
    cores = [4.0, 4.0, 3.6, 3.0, 2.5, 2.0, 1.5]
    als = [1.0, 1.0, 0.9, 0.8, 0.7, 0.55, 0.40]
    L = Ls[idx]
    al = als[idx]
    c = (SZ_FLASH - 1) / 2
    im = canvas(SZ_FLASH, SZ_FLASH)
    px = im.load()
    for y in range(SZ_FLASH):
        for x in range(SZ_FLASH):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy)
            best = None                                  # (알파, 색)
            # 주 광선
            for ang in (0, 90, 180, 270):
                ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
                a = dx * ca + dy * sa
                b = -dx * sa + dy * ca
                if 0 <= a <= L:
                    t = a / L
                    hw = 3.2 * (1 - t) ** 1.0 + 0.4
                    if abs(b) <= hw:
                        col = GOLD_IN if abs(b) < hw * 0.4 and t < 0.7 else GOLD_OUT if t > 0.75 else GOLD_MID
                        best = (al * (1 - 0.6 * t ** 1.5), col)
            # 대각선 보조 광선 (더 짧고 가늘다)
            if best is None:
                Ld = L * 0.55
                for ang in (45, 135, 225, 315):
                    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
                    a = dx * ca + dy * sa
                    b = -dx * sa + dy * ca
                    if 0 <= a <= Ld:
                        t = a / Ld
                        hw = 1.8 * (1 - t) + 0.3
                        if abs(b) <= hw:
                            best = (al * 0.7 * (1 - 0.5 * t), GOLD_MID if t < 0.6 else GOLD_OUT)
            # 후광과 핵
            if d <= cores[idx] * 0.75 + 0.2:
                best = (1.0, WHITE)
            elif d <= cores[idx] + 1.6:
                best = (1.0, (255, 240, 160))
            elif best is None and d <= 9:
                best = (0.35 * al, GOLD_MID)
            if best is not None:
                px[x, y] = (*best[1], round(255 * min(1.0, best[0])))
    return im


# ================================================================ 시안 합성 (어두운 던전 바닥 위)

def additive(base, sprite, cx, cy, mul=1.0, scale=1.0):
    """base(RGB)에 sprite(RGBA)를 가산 — 알파가 밝기"""
    if scale != 1.0:
        sprite = sprite.resize((max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))), Image.NEAREST)
    r, g, b, a = sprite.split()
    if mul != 1.0:
        a = a.point(lambda v: int(v * mul))
    black = Image.new('RGB', sprite.size, (0, 0, 0))
    lit = Image.composite(Image.merge('RGB', (r, g, b)), black, a)
    layer = Image.new('RGB', base.size, (0, 0, 0))
    layer.paste(lit, (round(cx - sprite.width / 2), round(cy - sprite.height / 2)))
    return ImageChops.add(base, layer)


def normal(base, sprite, cx, cy, mul=1.0, scale=1.0, tint=None, tint_k=0.0):
    if scale != 1.0:
        sprite = sprite.resize((max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))), Image.NEAREST)
    if tint is not None and tint_k > 0:
        r, g, b, a = sprite.split()
        rgb = Image.merge('RGB', (r, g, b))
        rgb = Image.blend(rgb, Image.new('RGB', rgb.size, tint), tint_k)
        sprite = Image.merge('RGBA', (*rgb.split(), a))
    if mul != 1.0:
        r, g, b, a = sprite.split()
        sprite = Image.merge('RGBA', (r, g, b, a.point(lambda v: int(v * mul))))
    rgba = base.convert('RGBA')
    rgba.alpha_composite(sprite, (round(cx - sprite.width / 2), round(cy - sprite.height / 2)))
    return rgba.convert('RGB')


def glow(base, cx, cy, radius, color, strength):
    """바닥에 번지는 빛 웅덩이 (가산, 가장자리로 갈수록 약해진다)"""
    size = int(radius * 2 + 2)
    im = canvas(size, size)
    px = im.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / radius
            if d < 1:
                px[x, y] = (*color, round(255 * strength * (1 - d) ** 2))
    return additive(base, im, cx, cy)


def floor(w, h, seed=3):
    """게임과 같은 다크 바닥 타일 (draftlib.dark_room, 벽 없음)"""
    return draftlib.dark_room(((w + 15) // 16) * 16, ((h + 15) // 16) * 16, wall_rows=0, seed=seed).convert('RGB').crop((0, 0, w, h))


FRAGS = {}


def cached(name, fn, *a):
    key = (name, a)
    if key not in FRAGS:
        FRAGS[key] = fn(*a)
    return FRAGS[key]


def particles(seed, count, spawn_t, jitter_t):
    rnd = random.Random(seed)
    out = []
    for _ in range(count):
        out.append({'ang': rnd.uniform(0, 6.283), 'dist': math.sqrt(rnd.random()), 't0': spawn_t + rnd.uniform(0, jitter_t),
                    'sp': rnd.uniform(0.6, 1.0), 'v': rnd.randrange(4), 'k': rnd.random()})
    return out


FG_W, FG_H = 120, 136
FG_CX, FG_CY = 59.5, 75.5        # 바닥 중심 (불덩이는 위로 솟으므로 위쪽 여유가 크다)


def compose_frag(T, guide=True):
    """파편 수류탄 T초 시점 합성 (1배). 판정 반경 R은 붉은 점선으로 표시.
    순서: 첫 번쩍 + 바닥 빛 → 불덩이(0~0.47초) + 바닥 충격 링 → 0.15초부터 흙먼지(불덩이가 식기 직전부터 피어올라 위로 떠오름, 1초) → 그을음.
    8갈래 충격 줄기(별 모양)는 없다 — 사용자 요청(v06)"""
    cx, cy = FG_CX, FG_CY
    base = floor(FG_W, FG_H, seed=5)
    lt = max(0.0, 1 - T / 0.45)
    if lt > 0:
        base = glow(base, cx, cy, R_FRAG * 1.5, (255, 140, 40), 0.55 * lt)
    if T >= 0.05:
        base = normal(base, cached('scorch', scorch), cx, cy, mul=max(0.0, 1 - T / 8.3), scale=0.85)
    fi = int(T * 30)
    if fi < FRAG_FRAMES:
        base = normal(base, cached('fire', fireball_frame, fi), cx + (FIRE_W / 2 - FIRE_GX - 0.5), cy + (FIRE_H / 2 - FIRE_GY - 0.5))
    # 흙먼지: 0.15초부터(불덩이가 식기 직전). 덩어리 안에서 생겨 위로 떠오르며 퍼진다. 크기는 프레임 4장을 순서대로(배율 X)
    for p in particles(21, 11, 0.15, 0.25):
        age = T - p['t0']
        life = 0.72
        if age < 0 or age > life:
            continue
        k = age / life
        d = p['dist'] * R_FRAG * 0.50 + age * R_FRAG * 0.14 * p['sp']
        x = round(cx + math.cos(p['ang']) * d)
        y = round(cy + math.sin(p['ang']) * d * 0.55 - 5 - age * 11 * p['sp'])
        stage = min(DUST_STAGES - 1, int(k * DUST_STAGES * 0.9))
        a = 0.5 * min(1.0, age / 0.12) * (1 - k) ** 1.1
        red = max(0.0, 1 - age / 0.35) * 0.5
        base = normal(base, cached('dust', dust_puff, p['v'] % DUST_VARIANTS, stage), x, y, mul=a, tint=(200, 100, 60), tint_k=red)
    if 0.05 <= T < 0.35:
        base = additive(base, cached('ring', shock_ring_frame, min(RING_FRAMES - 1, int((T - 0.05) / 0.06))), cx, cy)
    for p in particles(33, 20, 0.0, 0.10):
        age = T - p['t0']
        if age < 0 or age > 0.65:
            continue
        v = R_FRAG * (1.3 + 0.8 * p['k'])
        d = v * (1 - (1 - age / 0.65) ** 2) * 0.7 + 4
        x, y = cx + math.cos(p['ang']) * d, cy + math.sin(p['ang']) * d * 0.7 - age * 14 - 8
        col = (255, 190, 70) if age < 0.3 else (230, 90, 30)
        im = canvas(3, 3)
        for dx, dy in ((1, 1), (0, 1), (2, 1), (1, 0), (1, 2)):
            im.putpixel((dx, dy), (*col, 255 if (dx, dy) == (1, 1) else 130))
        base = additive(base, im, round(x), round(y), mul=1 - age / 0.65)
    if T < 0.09:
        base = additive(base, cached('fl', flash_core), cx, cy - 8, mul=1 - T / 0.09)
    if guide:
        dr = ImageDraw.Draw(base)
        for k in range(72):
            if k % 2 == 0:
                a = 2 * math.pi * k / 72
                dr.point((round(cx + math.cos(a) * R_FRAG), round(cy + math.sin(a) * R_FRAG * GROUND_V)), fill=(220, 60, 60))
    return base


def compose_flash(T, size=SZ_FLASH + 16, guide=True):
    w = size
    c = (w - 1) / 2
    base = floor(w, w, seed=8)
    lt = max(0.0, 1 - T / 0.35)
    if lt > 0:
        base = glow(base, c, c, R_FLASH * 1.2, (255, 225, 120), 0.40 * lt)
    if T < 0.40:
        base = additive(base, cached('fring', flash_ring_frame, min(FLASH_RING_FRAMES - 1, int(T * 24))), c, c)
    if T < 0.40:
        ci = 0 if T < 0.05 else min(FLASH_CROSS_FRAMES - 1, int((T - 0.05) / 0.05))
        base = additive(base, cached('fcross', flash_cross_frame, ci), c, c)
    for p in particles(44, 12, 0.10, 0.15):
        age = T - p['t0']
        if age < 0 or age > 0.5:
            continue
        d = R_FLASH * (0.2 + 0.7 * p['dist']) + age * 10
        x, y = c + math.cos(p['ang']) * d, c + math.sin(p['ang']) * d
        base = additive(base, cached('sp', spark), x, y, mul=(1 - age / 0.5) * 0.9)
    if guide:
        dr = ImageDraw.Draw(base)
        for k in range(96):
            if k % 2 == 0:
                a = 2 * math.pi * k / 96
                dr.point((round(c + math.cos(a) * R_FLASH), round(c + math.sin(a) * R_FLASH * GROUND_V)), fill=(220, 60, 60))
    return base


# ================================================================ 검사

def check():
    """자동 검사: 순백 위치 · 반경 · 알파 상한"""
    problems = []
    c = (SZ_FLASH - 1) / 2
    for i in range(FLASH_CROSS_FRAMES):
        im = flash_cross_frame(i)
        for y in range(im.height):
            for x in range(im.width):
                r, g, b, a = im.getpixel((x, y))
                if a > 0 and (r, g, b) == WHITE and math.hypot(x - c, y - c) > 3.5:
                    problems.append(f'FlashCross {i}: 중앙 밖 순백 ({x},{y})')
    for i in range(FLASH_RING_FRAMES):
        im = flash_ring_frame(i)
        bbox = im.getbbox()
        r = max(c - bbox[0], bbox[2] - 1 - c)
        if i == FLASH_RING_FRAMES - 1 and abs(r - (R_FLASH - 1)) > 1.5:
            problems.append(f'FlashRing 마지막 프레임 반경 {r} ≠ {R_FLASH - 1}')
        if any(px[:3] == WHITE for px in im.get_flattened_data() if px[3]):
            problems.append(f'FlashRing {i}: 순백 사용')
    cf = (SZ_FRAG - 1) / 2
    bbox = fireball_frame(FRAG_FRAMES // 2).getbbox()
    r = max(FIRE_GX - bbox[0], bbox[2] - 1 - FIRE_GX, bbox[3] - 1 - FIRE_GY)     # 좌우 · 아래(바닥) 반경은 R 안
    if r > R_FRAG + 0.5:
        problems.append(f'Fireball 중간 프레임 바닥 반경 {r} > {R_FRAG}')
    if bbox[1] < 1:
        problems.append('Fireball 윗부분이 캔버스 밖으로 잘림')
    return problems


# ================================================================ 시안 이미지

def font(size, bold=True):
    p = paths.font_path(bold)
    return ImageFont.truetype(p, size) if p else ImageFont.load_default()


BG = (24, 22, 26)
FG = (232, 228, 220)
DIM = (150, 146, 140)


def strip(frames, scale, per_row, bg=(38, 40, 46), additive_blend=False, pad=6, mul=1.0):
    """프레임 목록을 확대해 격자로. 가산 대상은 가산으로 보여 준다"""
    fw, fh = frames[0].size
    cols = min(per_row, len(frames))
    rows = (len(frames) + per_row - 1) // per_row
    cell_w, cell_h = fw * scale + pad, fh * scale + pad
    out = Image.new('RGB', (cols * cell_w + pad, rows * cell_h + pad), (16, 14, 18))
    for i, f in enumerate(frames):
        cx, cy = pad + (i % per_row) * cell_w, pad + (i // per_row) * cell_h
        tile = Image.new('RGB', (fw * scale, fh * scale), bg)
        big = f.resize((fw * scale, fh * scale), Image.NEAREST)
        if additive_blend:
            tile = additive(tile, big, fw * scale / 2, fh * scale / 2)
        else:
            tile = normal(tile, big, fw * scale / 2, fh * scale / 2, mul=mul)
        out.paste(tile, (cx, cy))
    return out


def snapshots(fn, times, scale):
    ims = [fn(t).resize((fn(t).width * scale, fn(t).height * scale), Image.NEAREST) for t in times]
    return ims


def row_with_labels(ims, labels, gap=12):
    w = sum(i.width for i in ims) + gap * (len(ims) - 1)
    h = max(i.height for i in ims) + 34
    out = Image.new('RGB', (w, h), BG)
    d = ImageDraw.Draw(out)
    f = font(20)
    x = 0
    for im, lb in zip(ims, labels):
        out.paste(im, (x, 34))
        d.text((x + 4, 4), lb, fill=FG, font=f)
        x += im.width + gap
    return out


def make_draft(dst):
    SC = 3
    t_frag = [0.05, 0.15, 0.30, 0.45, 0.75, 2.0]
    t_flash = [0.02, 0.08, 0.18, 0.30, 0.55]
    frag_row = row_with_labels(snapshots(compose_frag, t_frag, SC), [f'{t:.2f}초' for t in t_frag])
    flash_row = row_with_labels(snapshots(compose_flash, t_flash, SC), [f'{t:.2f}초' for t in t_flash])

    fire = strip([fireball_frame(i) for i in range(FRAG_FRAMES)], 2, 7)
    ring = strip([shock_ring_frame(i) for i in range(RING_FRAMES)], 2, 5, additive_blend=True)
    misc = strip([flash_core()], 2, 1, additive_blend=True)
    dusts = strip([dust_puff(v, st) for v in range(DUST_VARIANTS) for st in range(DUST_STAGES)], 2, DUST_STAGES)
    dusts_half = strip([dust_puff(v, st) for v in range(DUST_VARIANTS) for st in range(DUST_STAGES)], 2, DUST_STAGES, mul=0.5)
    scorch_im = strip([scorch()], 2, 1)
    fring = strip([flash_ring_frame(i) for i in range(FLASH_RING_FRAMES)], 2, 4, additive_blend=True)
    fcross = strip([flash_cross_frame(i) for i in range(FLASH_CROSS_FRAMES)], 2, 4, additive_blend=True)

    W = 2400
    pad = 28
    sections = []

    def head(text, sub=None):
        im = Image.new('RGB', (W, 64 if sub else 44), BG)
        d = ImageDraw.Draw(im)
        d.text((pad, 4), text, fill=(255, 205, 90), font=font(28))
        if sub:
            d.text((pad, 40), sub, fill=DIM, font=font(18, False))
        return im

    def block(label, im):
        t = Image.new('RGB', (im.width, im.height + 30), BG)
        d = ImageDraw.Draw(t)
        d.text((0, 2), label, fill=FG, font=font(18))
        t.paste(im, (0, 30))
        return t

    title = Image.new('RGB', (W, 120), BG)
    d = ImageDraw.Draw(title)
    d.text((pad, 14), '수류탄 · 섬광탄 폭발 VFX 시안 (탑다운)', fill=FG, font=font(40))
    d.text((pad, 70), '3배 확대 · 어두운 던전 바닥 위 합성 · 붉은 점선 = 범위 표시(가로 반경 파편 2.5u = 40px · 섬광 4u = 64px, 세로 0.65배 타원 — dev 인디케이터와 같음)', fill=DIM, font=font(20, False))
    bw = 130
    d.rounded_rectangle((W - bw - pad, 24, W - pad, 72), 10, fill=(255, 205, 90))
    d.text((W - bw - pad + 28, 30), VERSION, fill=(30, 20, 8), font=font(34))
    sections.append(title)

    sections.append(head('1. 파편 수류탄 — 시간별 합성', '비스듬한 시점 · 첫 번쩍 + 바닥 빛 + 충격 링 · 8갈래 별(충격 줄기) 없음. 0.15초부터 흙먼지가 피어올라 약 1초 안에 사라진다. 2.0초에는 그을음만 남는다'))
    sections.append(frag_row)
    sections.append(head('2. 파편 수류탄 — 부품 프레임 (2배)'))
    def blocks_row(items):
        bs = [block(lb, im) for lb, im in items]
        row = Image.new('RGB', (W, max(b.height for b in bs)), BG)
        x = pad
        for b in bs:
            row.paste(b, (x, 0))
            x += b.width + 40
        return row

    sections.append(blocks_row([('Fireball 14프레임 @30fps (96×112, 바닥 중심 = 피벗)', fire)]))
    sections.append(blocks_row([('ShockRing 5프레임 (가산)', ring), ('FlashCore (가산)', misc), ('Scorch 그을음', scorch_im)]))
    sections.append(blocks_row([('Dust 3종 × 크기 4프레임 (48×32, 불투명 도트)', dusts), ('같은 먼지, 파티클 투명도 0.5', dusts_half)]))

    sections.append(head('3. 섬광탄 — 시간별 합성', '도넛이 중심에서 범위 끝까지 커지고, 중앙 십자 섬광은 노랑 혼합(순백은 중앙 핵 3px만) + 노란 잔광'))
    sections.append(flash_row)
    sections.append(head('4. 섬광탄 — 부품 프레임 (2배, 가산)'))
    sections.append(blocks_row([('FlashRing 8프레임 @24fps', fring), ('FlashCross 7프레임', fcross)]))

    H = sum(s.height for s in sections) + 20 * len(sections)
    out = Image.new('RGB', (W, H), BG)
    y = 0
    for s in sections:
        out.paste(s, (pad if s.width < W else 0, y))
        y += s.height + 20
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    for attempt in range(5):                       # 미리보기 프로그램이 파일을 잡고 있으면 저장이 가끔 거부된다
        try:
            out.save(dst)
            break
        except OSError:
            if attempt == 4:
                raise
            time.sleep(1.0)
    return out.size


def scorch_import():
    """그을음 시트용: 시안(합성)에서 0.85배 최근접으로 줄여 쓰던 것과 같은 픽셀 — 비정수 배율을 프리팹에 두지 않도록 미리 줄여서 96×96 가운데에 둔다"""
    small = scorch().resize((82, 82), Image.NEAREST)
    im = canvas(SZ_FRAG, SZ_FRAG)
    im.paste(small, (7, 7))
    return im


def hstrip(frames):
    fw, fh = frames[0].size
    im = canvas(fw * len(frames), fh)
    for i, f in enumerate(frames):
        im.paste(f, (i * fw, 0))
    return im


def build():
    """Assets에 쓸 시트 목록 {경로: 이미지}. 프레임은 가로로 이어 붙인다 (pellet_vfx.py와 같은 방식)"""
    g = paths.repo('Assets', '09.VFX', 'Consumable', 'Grenade')
    f = paths.repo('Assets', '09.VFX', 'Consumable', 'Flash')
    return {
        os.path.join(g, 'GrenadeFireball.png'): hstrip([fireball_frame(i) for i in range(FRAG_FRAMES)]),
        os.path.join(g, 'GrenadeShockRing.png'): hstrip([shock_ring_frame(i) for i in range(RING_FRAMES)]),
        os.path.join(g, 'GrenadeFlashCore.png'): flash_core(),
        os.path.join(g, 'GrenadeDust.png'): hstrip([dust_puff(v, st) for v in range(DUST_VARIANTS) for st in range(DUST_STAGES)]),
        os.path.join(g, 'GrenadeScorch.png'): scorch_import(),
        os.path.join(g, 'GrenadeSpark.png'): spark(),
        os.path.join(f, 'FlashRing.png'): hstrip([flash_ring_frame(i) for i in range(FLASH_RING_FRAMES)]),
        os.path.join(f, 'FlashCross.png'): hstrip([flash_cross_frame(i) for i in range(FLASH_CROSS_FRAMES)]),
    }


def make_gifs(folder, fps=30, scale=4, guide=False):
    """시간 순서대로 합성한 GIF 2개 (파편 2.0초 · 섬광 0.8초 + 끝에 잠깐 멈춤). guide=True면 판정 반경 점선을 넣는다"""
    out = []
    for name, fn, dur, hold in (('frag', compose_frag, 2.0, 0.5), ('flash', compose_flash, 0.8, 0.5)):
        n = int(dur * fps)
        frames = []
        for i in range(n + 1):
            im = fn(i / fps, guide=guide)
            frames.append(im.resize((im.width * scale, im.height * scale), Image.NEAREST))
        pal = [f.quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
        durs = [round(1000 / fps)] * len(pal)
        durs[-1] += round(hold * 1000)
        dst = os.path.join(folder, f'grenade-vfx-{name}{"-guide" if guide else ""}.gif')
        pal[0].save(dst, save_all=True, append_images=pal[1:], duration=durs, loop=0, disposal=2)
        out.append(dst)
    return out


if __name__ == '__main__':
    if 'import' in sys.argv[1:]:
        problems = check()
        if problems:
            sys.exit('검사 실패: ' + '; '.join(problems))
        for path, im in build().items():
            im.save(paths.out(path))
            print('wrote', os.path.relpath(path, paths.REPO), im.size)
        sys.exit(0)
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    dst = args[0] if args else paths.repo('Docs', 'drafts', 'props', 'grenade-vfx-draft.png')
    problems = check()
    for p in problems:
        print('검사:', p)
    print('검사 문제', len(problems), '건')
    print('시안', dst, make_draft(dst))
    if '--gif' in sys.argv:
        for g in make_gifs(os.path.dirname(dst)):
            print('gif', g)
