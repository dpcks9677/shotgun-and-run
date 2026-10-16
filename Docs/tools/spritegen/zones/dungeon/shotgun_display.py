"""튜토리얼 초록 방(팔각형) 중앙 샷건 전시대 — 시안.

CS_TUTORIAL 3-3: 방 중앙(월드 (36.5, 29))에 샷건이 놓여 있고 플레이어가 E키로 줍는다. 이 전시대는
  1. 원형 단상(Dais): 2단 둥근 돌 포석 + 가운데 원판(룬 고리). 112x80 = 7x5칸. 바닥 위 데칼(걸어서 지나감, 충돌 없음).
  2. 낮은 기둥(Pillar): 각진 상자를 쌓은 금속 · 돌 받침 묶음(레퍼런스 2). 48x48 = 3x3칸, 주 받침 높이 12px(플레이어 키의 절반 남짓).
     윗면 가운데에 받침 홈, 충돌은 발판만. 플레이어가 바로 옆까지 다가가 E키로 상호작용한다.
  3. 샷건은 기둥 위에 떠 있다(씬에서 shotgun 스프라이트를 위아래로 흔들고, 접지 그림자 CollapseShadow를 받침 홈에 깐다).
탑다운 3/4: 원은 세로 0.65배 타원, 윗면이 크고 앞면은 띠. 빛은 왼쪽 위.
그림체: 배경 타일(DungeonDark)처럼 면마다 단색 + 1px 이음매 + 밝은 테두리선만 쓴다. 그라데이션 · 픽셀 노이즈 없음(v01은 노이즈 때문에 배경과 어긋나 거부됨).
검은 외곽선 없음(돌 가장자리 색). 돌 색은 collapsed_stairs.py(승인 v08)의 DungeonDark 벽 원본 팔레트와 같다.

    python shotgun_display.py [시안.png]    기본: Docs/drafts/props/tutorial-shotgun-display-draft.png
    python shotgun_display.py import        Assets/04.Sprites/Tile/ZoneProps/TutorialShotgunDisplay/ 에 시트 · meta만 생성 (타일 없음)"""
import os as _os, sys as _sys, math, random
HERE = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.abspath(_os.path.join(HERE, '..', '..')))  # spritegen 루트 (bl · draftlib · paths)
_sys.path.insert(0, HERE)
import sys
from PIL import Image, ImageDraw
from bl import Cv, load_font
import paths
import draftlib as D
import painter_sim as P
import collapsed_stairs as S

VERSION = 'v02'
SHEET = 'TutorialShotgunDisplay'

mix, hsh = S.mix, S.hsh
GV = 0.65                                   # 바닥 원의 세로 압축 (폭발 범위 표시 · 포탈과 같은 값)

# ---------- 팔레트 ----------
HI, ED = S.R_HI, S.R_ED
DISC, DISC2 = (47, 50, 57, 255), (36, 39, 45, 255)                 # 가운데 원판 · 룬 홈
# 포석: 돌마다 단색 하나, 윗면 왼쪽 · 위 테두리선, 이음매는 어둡게 (배경 벽돌과 같은 문법)
TOPS = [(56, 58, 64, 255), (52, 54, 60, 255), (60, 62, 68, 255)]
RIM, RIM2 = (74, 77, 84, 255), (66, 69, 76, 255)
SEAM = (30, 31, 34, 255)
FACE_L, FACE_M, FACE_R, FACE_B = (47, 49, 54, 255), (41, 43, 47, 255), (35, 36, 40, 255), (28, 29, 32, 255)
# 기둥 금속 · 돌 (레퍼런스 2: 건메탈 + 청록 녹)
M_HI, M_TOP, M_FR, M_FR2, M_ED = (92, 98, 106, 255), (68, 73, 81, 255), (47, 51, 58, 255), (36, 39, 45, 255), (22, 24, 28, 255)
P_HI, P_TOP, P_FR, P_FR2 = (96, 140, 138, 255), (66, 104, 106, 255), (44, 74, 80, 255), (33, 55, 62, 255)
GLOW, GLOW2 = (96, 190, 182, 255), (58, 128, 128, 255)             # 룬 · 받침 홈의 약한 발광 (발광에만 청록)


def _ell(x, y, cx, cy, rx, ry):
    u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
    return u * u + v * v


# ---------- 단상 ----------

def slab_bounds(n, seed):
    r = random.Random(seed)
    return sorted((k + r.uniform(-0.25, 0.25)) / n * 2 * math.pi - math.pi for k in range(n))


def slab_of(theta, bounds):
    n = len(bounds)
    for k in range(n):
        if bounds[k] <= theta < (bounds[k + 1] if k + 1 < n else bounds[0] + 2 * math.pi):
            return k
    return n - 1


def tier(c, cx, cy, rx, ry, h, rho_in, n, seed, tops=TOPS):
    """한 단: 바닥 타원(cx, cy, rx, ry) 위로 높이 h 솟은 원판. 윗면은 rho_in(0~1) 바깥 고리만 포석(각도로 나눈 돌판)을 깐다.
    윗면 · 앞면의 이음매는 같은 각도에서 만난다. 색은 돌마다 단색 + 테두리선 + 이음매뿐."""
    cyt = cy - h
    bounds = slab_bounds(n, seed)
    pick = [random.Random(seed * 31 + k).randrange(len(tops)) for k in range(n)]
    ids = {}
    for x in range(int(cx - rx) - 1, int(cx + rx) + 2):                      # 앞면
        u = (x + 0.5 - cx) / rx
        if abs(u) >= 1:
            continue
        s_ = math.sqrt(1 - u * u)
        yt_b, yb_b = cyt + ry * s_, cy + ry * s_
        k = slab_of(math.atan2(s_, u), bounds)
        for y in range(int(math.floor(yt_b)) + 1, int(math.floor(yb_b)) + 1):
            if _ell(x, y, cx, cyt, rx, ry) <= 1.0:
                continue
            col = FACE_L if u < -0.4 else FACE_R if u > 0.4 else FACE_M
            ids[(x, y)] = ('f', k, col, y == int(math.floor(yb_b)))
    for y in range(int(cyt - ry) - 1, int(cyt + ry) + 2):                     # 윗면
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            q = _ell(x, y, cx, cyt, rx, ry)
            if q > 1.0 or q < rho_in * rho_in:
                continue
            k = slab_of(math.atan2((y + 0.5 - cyt) / ry, (x + 0.5 - cx) / rx), bounds)
            ids[(x, y)] = ('t', k, tops[pick[k]], False)
    for (x, y), (kind, k, col, bottom) in ids.items():
        R, B, L, A = ids.get((x + 1, y)), ids.get((x, y + 1)), ids.get((x - 1, y)), ids.get((x, y - 1))
        same = lambda o: o is not None and o[0] == kind and o[1] == k
        out = col
        if kind == 't':
            if B is not None and B[0] == 'f':
                out = RIM2                                    # 앞 가장자리 빛선
            elif (R is not None and R[0] == 't' and not same(R)) or (B is not None and B[0] == 't' and not same(B)):
                out = SEAM
            elif A is None or (A[0] == 't' and not same(A)) or L is None or (L[0] == 't' and not same(L)):
                out = RIM
        else:
            if bottom:
                out = FACE_B
            elif R is not None and R[0] == 'f' and R[1] != k:
                out = SEAM
        c.px(x, y, out)


def cracks(c, spots):
    """손으로 정한 균열 몇 줄 (폐허 느낌). spots = [(x, y, [(dx, dy), ...])], 어두운 이음매색 1px"""
    for (x, y, path) in spots:
        for (dx, dy) in path:
            x += dx
            y += dy
            p = c.get(x, y)
            if p is not None and p[3] > 200:
                c.px(x, y, SEAM)


def disc_runes(c, cx, cy, rx, ry):
    """가운데 원판의 룬 고리: 홈(바깥 벽 오른쪽 아래만 밝은 줄) + 8곳의 약한 청록 발광 점"""
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            q = math.sqrt(_ell(x, y, cx, cy, rx, ry))
            p = c.get(x, y)
            if p is None:
                continue
            if 0.66 <= q < 0.80:
                c.px(x, y, DISC2)
            elif 0.80 <= q < 0.88 and (x + 0.5 - cx) + (y + 0.5 - cy) / GV > 0:
                c.px(x, y, mix(DISC, HI, 0.3))
    for k in range(8):
        th = k * math.pi / 4 + math.pi / 8
        x, y = int(cx + rx * 0.73 * math.cos(th)), int(cy + ry * 0.73 * math.sin(th))
        col = GLOW if k % 2 == 0 else GLOW2
        c.px(x, y, col)
        c.px(x + 1, y, col)


DAIS_W, DAIS_H = 112, 80
DAIS_CX, DAIS_CY = 56, 44                    # 바닥 타원 중심 (그림 위에서 잰 y)


def dais():
    c = Cv(DAIS_W, DAIS_H)
    cx = float(DAIS_CX)
    RX0, CY0, H0 = 52.0, float(DAIS_CY), 4
    RY0 = RX0 * GV
    for y in range(DAIS_H):                                               # 접지 그림자 (오른쪽 아래 2px)
        for x in range(DAIS_W):
            if _ell(x - 2, y - 2, cx, CY0, RX0, RY0) <= 1.0:
                c.px(x, y, (0, 0, 0, 110))
    tier(c, cx, CY0, RX0, RY0, H0, 0.70, 14, 5)
    RX1, CY1, H1 = 37.0, CY0 - H0, 3
    RY1 = RX1 * GV
    for y in range(DAIS_H):                                               # 윗단이 아랫단에 드리우는 그림자 (1줄)
        for x in range(DAIS_W):
            if _ell(x - 1, y - 1, cx, CY1, RX1, RY1) <= 1.0 and _ell(x, y, cx, CY1 - H1, RX1, RY1) > 1.0:
                p = c.get(x, y)
                if p is not None and p[3] > 200:
                    c.px(x, y, mix(p, SEAM, 0.35))
    tier(c, cx, CY1, RX1, RY1, H1, 0.58, 10, 9)
    RX2, CY2, H2 = 22.0, CY1 - H1, 2
    RY2 = RX2 * GV
    tier(c, cx, CY2, RX2, RY2, H2, 0.0, 1, 3, tops=[DISC])
    disc_runes(c, cx, CY2 - H2, RX2, RY2)
    cracks(c, [(30, 20, [(1, 0), (1, 1), (1, 0), (0, 1), (1, 1)]), (88, 26, [(-1, 0), (-1, 1), (0, 1), (-1, 0)]),
               (72, 69, [(1, 0), (0, 1), (1, 0)]), (44, 38, [(-1, 0), (-1, 1), (-1, 0)])])
    return c


# ---------- 낮은 기둥(받침) ----------

PIL_W, PIL_H = 48, 48
PIL_CX, PIL_CY = 24, 36                      # 발판 중심 (그림 위에서 잰 y). 단상 가운데 원판 윗면 중심에 놓인다


def box(c, x0, x1, yb, h, d, front, top, hi, edge, patina=0.0, seams=(), seed=1):
    """상자: 앞면 = x0..x1 · 바닥 yb에서 위로 h, 윗면 = 그 위로 d. (탑다운 3/4) 면마다 단색 + 테두리선만."""
    yt = yb - h
    for y in range(yt + 1, yb + 1):                       # 앞면
        for x in range(x0, x1 + 1):
            col = front[1] if y >= yb - 1 else front[0]
            if x == x0:
                col = mix(col, hi, 0.28)
            elif x == x1:
                col = mix(col, edge, 0.5)
            c.px(x, y, col)
    for y in range(yt - d, yt + 1):                       # 윗면
        for x in range(x0, x1 + 1):
            col = top
            if y == yt:
                col = mix(top, hi, 0.55)
            elif y == yt - d or x == x0:
                col = mix(top, hi, 0.4)
            elif x == x1:
                col = mix(top, edge, 0.4)
            c.px(x, y, col)
    for x in range(x0 - 1, x1 + 2):                        # 뒤 · 아래 마감
        c.px(x, yt - d - 1, edge)
        c.px(x, yb + 1, edge)
    for y in range(yt - d, yb + 1):
        c.px(x0 - 1, y, mix(edge, front[1], 0.4))
        c.px(x1 + 1, y, edge)
    for sy in seams:
        for x in range(x0, x1 + 1):
            c.px(x, sy, mix(c.get(x, sy) or front[0], edge, 0.6))
    if patina > 0:                                          # 아래 단색 띠 + 굵게 흘러 오른 청록 녹 2~3곳 (단색 면)
        band = max(2, int(h * patina * 0.3))
        for x in range(x0 + 1, x1):
            for y in range(yb - band + 1, yb + 1):
                c.px(x, y, P_FR2 if y == yb else P_FR)
        r = random.Random(seed)
        for _ in range(3):
            dx = r.randint(x0 + 2, x1 - 4)
            w, ext = r.choice((2, 3)), r.randint(band + 2, int(h * patina) + 3)
            for x in range(dx, dx + w):
                for y in range(yb - ext + 1, yb - band + 1):
                    c.px(x, y, P_TOP if y == yb - ext + 1 else P_FR)
    return yt


def tint_prism(c, cx, cy, a, b, ang, h, tint=0.6, **kw):
    """기울어진 상자를 임시 캔버스에 그려 청록 녹 기운으로 물들인 뒤 붙인다"""
    t = Cv(c.w, c.h)
    S.prism(t, cx, cy, a, b, ang, h, **kw)
    for y in range(c.h):
        for x in range(c.w):
            p = t.get(x, y)
            if p is None:
                continue
            lum = (p[0] + p[1] + p[2]) / 3.0
            base = P_HI if lum > 70 else P_TOP if lum > 52 else P_FR if lum > 36 else P_FR2
            c.px(x, y, mix(p, base, tint) if p[3] > 200 else p)


def pillar():
    c = Cv(PIL_W, PIL_H)
    for y in range(34, 48):                                 # 접지 그림자
        for x in range(PIL_W):
            if _ell(x - 3, y - 2, 24.0, 40.0, 22.0, 5.0) <= 1.0:
                c.px(x, y, (0, 0, 0, 105))
    box(c, 30, 41, 36, 15, 5, (P_FR, P_FR2), P_TOP, P_HI, M_ED, seams=(27,))        # 뒤 오른쪽: 청록 상자
    box(c, 6, 15, 37, 9, 4, (M_FR, M_FR2), M_TOP, M_HI, M_ED, patina=0.6, seed=2)  # 뒤 왼쪽: 낮은 판
    box(c, 11, 36, 40, 12, 8, (M_FR, M_FR2), M_TOP, M_HI, M_ED, patina=0.5, seams=(34,), seed=1)   # 주 받침
    for y in range(28, 39):                                 # 앞면 새김선 + 발광 점
        p = c.get(24, y)
        if p is not None:
            c.px(24, y, mix(p, M_ED, 0.5))
    c.px(24, 30, GLOW)
    c.px(25, 30, GLOW2)
    cx_, cy_ = 24.0, 40.0 - 12.0 - 4.0                      # 윗면 받침 홈
    for y in range(int(cy_ - 4), int(cy_ + 5)):
        for x in range(int(cx_ - 10), int(cx_ + 11)):
            q = _ell(x, y, cx_, cy_, 9.0, 3.6)
            if q <= 1.0:
                c.px(x, y, mix(M_FR2, M_ED, 0.4) if q > 0.6 else GLOW2 if q > 0.2 else GLOW)
            elif q <= 1.3 and (y + 0.5 - cy_) > 0:
                p = c.get(x, y)
                if p is not None:
                    c.px(x, y, mix(p, M_HI, 0.35))
    S.prism(c, 8, 42, 6, 2.6, 14, 3, warm=False, joints=0)                          # 앞쪽 비스듬한 파편
    tint_prism(c, 40, 41, 6, 2.6, -22, 4, tint=0.55, warm=False, joints=0)
    S.prism(c, 24, 45, 4, 2.0, 24, 2, warm=False, joints=0)
    return c


# ---------- 시안 ----------

def items():
    its = [
        D.make('ShotgunDais', '샷건 단상', dais(), 'decal', shadow=False, outline=False, note='바닥 데칼 · 충돌 없음'),
        D.make('ShotgunPillar', '샷건 기둥 (낮은 받침)', pillar(), 'block', shadow=False, outline=False, note='충돌: 발판만'),
    ]
    # pivot y는 아래 기준(Unity 좌표): fin 높이 - 위에서 잰 y
    its[0]['pivot'] = (DAIS_CX, its[0]['fin'].h - DAIS_CY)   # 단상: 바닥 타원 중심 = 월드 (36.5, 29)
    its[1]['pivot'] = (PIL_CX, its[1]['fin'].h - PIL_CY)     # 기둥: 발판 중심 (단상 가운데 원판 윗면, 바닥에서 9px 위)
    its[0]['coll'] = its[1]['coll'] = None
    return its


def __getattr__(name):                         # gen_zone.main이 mod.ITEMS를 읽음 (그릴 때만 계산)
    if name == 'ITEMS':
        return items()
    raise AttributeError(name)


def gun_sprite():
    sh = Image.open(paths.repo('Assets', '04.Sprites', 'Weapon', 'shotgun.png')).convert('RGBA')
    return sh.crop((64, 0, 128, 32))       # shotgun_15 (손 없는 버전, 시트 맨 윗줄 두 번째 칸 · 유니티 rect x64 y96)


def scene(its, floating=True):
    W, H = 192, 144
    sc = Image.new('RGBA', (W, H), (0, 0, 0, 255))
    fl = [D.game_tile(paths.DUNGEON_DARK, 176, 496)] + [D.game_tile(paths.DUNGEON_DARK, 192 + 16 * i, 496) for i in range(4)]
    r = random.Random(3)
    for ty in range(0, H, 16):
        for tx in range(0, W, 16):
            sc.alpha_composite(fl[0] if r.random() < 0.85 else r.choice(fl[1:]), (tx, ty))
    da, pi = its[0]['fin'].img(), its[1]['fin'].img()
    cx, cy = 96, 76                                         # 단상 바닥 타원 중심 (월드 (36.5, 29))
    sc.alpha_composite(da, (cx - DAIS_CX, cy - DAIS_CY))
    px, py = cx - PIL_CX, cy - 9 - PIL_CY                   # 기둥 발판 중심 = 원판 윗면 중심 (바닥 +9px)
    sc.alpha_composite(pi, (px, py))
    if floating:
        gun = gun_sprite()
        bb = gun.getbbox()
        gx = px + 24 - (bb[0] + bb[2]) // 2
        gy = py + 24 - 4 - bb[3]                            # 받침 홈 위 4px (바닥 높이는 씬에서 ±1px 흔들림)
        sh = Image.new('RGBA', sc.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(sh)
        d.ellipse((px + 24 - 8, py + 24 - 1, px + 24 + 8, py + 24 + 3), fill=(0, 0, 0, 90))
        sc.alpha_composite(sh)
        sc.alpha_composite(gun, (gx, gy))
    pl = Image.open(paths.REPO_SLASH + 'Assets/04.Sprites/Player/Swordsman with shadows/Swordsman.png').convert('RGBA').crop((42, 39, 58, 60))
    sc.alpha_composite(pl, (cx + 30, cy + 14))
    sc.alpha_composite(pl, (cx - 14, cy + 16))
    return sc


def main(out):
    its = items()
    sc = scene(its)
    im = D.draft(its, [('튜토리얼 샷건 전시대 — 단상(바닥 데칼) · 낮은 기둥(위에 샷건이 떠 있음)', ('decal', 'block'))], sc, sc.size,
                 '튜토리얼 샷건 전시대 %s' % VERSION, S=3, Wd=max(1536, sc.width * 3 + 24))
    im.save(out)
    print('saved', out, im.size)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        _sys.path.insert(0, _os.path.join(HERE, '..', '..'))
        import gen_zone
        gen_zone.main(SHEET, 'shotgun_display', tiles=False)
        sys.exit(0)
    main(sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'tutorial-shotgun-display-draft.png'))
