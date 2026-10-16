"""튜토리얼 빨강 복도 붕괴 잔해 — 시안.

CS_TUTORIAL_ESCAPE 연출에서 복도 입구(CollapseDebris, 월드 (6, 15))를 가리고 막는 스프라이트.
복도 폭 6칸(x3~8) 전체를 옆벽에서 옆벽까지 채운다. 96x64 = 6x4칸, 오브젝트 중심 피벗(씬 콜라이더 6x3 중심과 같은 점).
탑다운 3/4: 윗면이 크고 앞면은 띠. 빛은 왼쪽 위. 돌 색과 잔해 문법(heap · prism · column_diag)은
zones/dungeon/collapsed_stairs.py(승인 v08)와 같다 — 같은 Dungeon 테마의 무너진 계단 잔해와 이웃해도 어긋나지 않게.
변형: A = 큰 쓰러진 기둥이 가로지른 더미, B = 천장 판석 + 부러진 들보(갈색)가 얹힌 더미.

    python collapse_debris.py [시안.png]    기본: Docs/drafts/props/tutorial-collapse-debris-draft.png
    python collapse_debris.py import        Assets/04.Sprites/Tile/ZoneProps/TutorialCollapseDebris/ 에 시트 · meta만 생성 (타일 없음, 피벗 = 그림 중심)"""
import os as _os, sys as _sys, random
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

VERSION = 'v01'
SHEET = 'TutorialCollapseDebris'
W, H = 96, 64
FLOOR_Y = 44                       # 이 줄부터 아래는 바닥 위(그림자 · 잔돌)


CX, CY, RX, RY = 48.0, 40.0, 58.0, 13.0       # 더미 바닥 타원 (복도 폭 밖으로 넘쳐 옆벽과 틈이 없게)
HMAX = 25.0                                     # 가운데 높이(px)


def prof(gx, gy):
    """바닥 타원 안이면 (높이, 타원 안쪽 정도 0~1), 밖이면 None"""
    u, v = (gx - CX) / RX, (gy - CY) / RY
    q = u * u + v * v
    if q > 1.0:
        return None
    return HMAX * (1 - q) ** 0.55 * (1 - 0.3 * abs(u)), 1 - q


def base_fill(c, seed):
    """블록 사이 틈이 비지 않게 어두운 속 채우기 (실루엣 = 바닥 앞 가장자리 ~ 꼭대기)"""
    r = random.Random(seed)
    for x in range(W):
        u = (x - CX) / RX
        if abs(u) >= 1:
            continue
        s_ = (1 - u * u) ** 0.5
        yf = int(CY + RY * s_)
        yt = int(CY - RY * s_ * 0.6 - HMAX * (1 - u * u) ** 0.55 * (1 - 0.3 * abs(u)) * 0.9)
        for y in range(max(0, yt + r.randint(0, 2)), min(H - 4, yf) + 1):
            c.px(x, y, S.mix(S.R_FR2, S.R_ED, 0.3 + 0.5 * (y - yt) / max(1, yf - yt) * 0 + 0.2 * r.random()))


def pile(c, seed, planks=False):
    """블록(회전한 상자)을 뒤 → 앞으로 쌓는다. 위치 높이는 prof, 굵은 덩어리 위주(레퍼런스: 잔 자갈이 아니라 큰 돌덩이 더미)"""
    r = random.Random(seed)
    spots = []
    for gy in range(18, 54, 3):
        gx = -4 + r.randint(0, 5)
        while gx < W + 4:
            spots.append((gy + r.randint(-1, 1), gx + r.randint(-2, 2)))
            gx += r.randint(7, 11)
    spots.sort()
    for (gy, gx) in spots:
        pr = prof(min(max(gx, 1), W - 2), gy)
        if pr is None:
            continue
        hgt, inner = pr
        lift = hgt * (0.55 + 0.45 * inner)
        cy = gy - lift
        if cy < 6:
            continue
        a = r.uniform(4.0, 8.0) * (0.75 + 0.4 * inner)
        b = r.uniform(2.4, 3.8)
        ang = r.choice((-1, 1)) * r.uniform(5, 75)
        warm = r.random() < (0.12 if not planks else 0.0)
        S.prism(c, gx, cy, a, b, ang, r.randint(3, 5), warm=warm, joints=1 if a > 6.5 and r.random() < 0.5 else 0)
        if planks and r.random() < 0.28:                          # 부러진 들보 널(갈색, 길고 가늘게)
            S.prism(c, gx + r.randint(-3, 3), cy - 1, r.uniform(8, 12), 1.6, r.choice((-1, 1)) * r.uniform(10, 50), 2, warm=True, joints=r.choice((0, 1, 2)))


def shadow(c):
    for x in range(W):
        yb = None
        for y in range(H - 5, 0, -1):
            p = c.get(x, y)
            if p is not None and p[3] > 200:
                yb = y
                break
        if yb is None:
            continue
        for k, al in ((1, 105), (2, 105), (3, 55)):
            for xx in ((x, x + 1) if k < 3 else (x,)):
                if c.get(xx, yb + k) is None and yb + k < H:
                    c.px(xx, yb + k, (0, 0, 0, al))


def debris_a():
    """A: 큰 블록 더미 위로 홈 파인 기둥이 비스듬히 쓰러져 얹힘 (레퍼런스 2)"""
    c = Cv(W, H)
    base_fill(c, 1)
    pile(c, 11)
    S.column_diag(c, 8, 17, 62, 38, 8, random.Random(5), clip=lambda x, y: 0 <= x < W)
    for (x, y, a, b, ang, h, w) in ((72, 32, 8, 3.6, 18, 5, False), (84, 39, 7, 3.4, -22, 5, True), (70, 46, 7, 3.2, 8, 5, False),
                                    (30, 48, 8, 3.4, -10, 5, False), (50, 49, 6, 3.2, 40, 5, True), (14, 42, 7, 3.4, 30, 5, False)):
        S.prism(c, x, y, a, b, ang, h, warm=w, joints=1 if a > 7 else 0)
    shadow(c)
    return c


def debris_b():
    """B: 무너진 천장 판석 + 부러진 들보 널 더미 (레퍼런스 1: 널 · 판석이 뒤섞인 낮고 넓은 더미)"""
    c = Cv(W, H)
    base_fill(c, 2)
    pile(c, 23, planks=True)
    for (x, y, a, b, ang, h, w) in ((30, 26, 13, 5, 8, 4, False), (64, 29, 12, 4.6, -12, 4, False), (46, 37, 11, 4.4, 16, 5, False),
                                    (82, 41, 8, 3.4, -30, 5, False), (16, 43, 9, 3.6, 24, 5, False), (58, 49, 10, 3.2, -6, 5, False),
                                    (28, 50, 8, 3.2, 12, 5, False)):
        S.prism(c, x, y, a, b, ang, h, warm=w, joints=2 if a > 11 else 1 if a > 8 else 0)
    S.prism(c, 40, 34, 14, 1.8, -20, 2, warm=True, joints=2)
    S.prism(c, 72, 40, 12, 1.8, 24, 2, warm=True, joints=1)
    shadow(c)
    return c


# ---------- 카메라 흔들림 연출용 소품: 떨어지는 돌 · 그림자 · 먼지 (CutsceneShakeDebris) ----------

def rock(kind):
    """작은 돌덩이 (탑다운 3/4, 빛 왼쪽 위): 기울어진 상자 1~2개 + 깨진 조각. 외곽선 없이 돌 가장자리 색. 크기 계열 4종"""
    spec = [(7, 5, 3.2, 1.8, 20, 2, 0), (8, 6, 3.6, 2.0, -35, 3, 0), (11, 8, 5.0, 2.6, 28, 3, 1), (14, 10, 6.4, 3.2, -18, 4, 1)][kind]
    w, h, a, b, ang, hh, joints = spec
    c = Cv(w + 4, h + 4)
    S.prism(c, (w + 4) / 2.0, (h + 4) / 2.0 - 1, a, b, ang, hh, warm=(kind == 1), joints=joints)
    if kind >= 2:                                       # 돌 위에 얹힌 부스러기
        S.prism(c, (w + 4) / 2.0 + 2, (h + 4) / 2.0 - 3, 2.4, 1.4, -50, 2)
    return c


def shadow_disc(w=16, h=8):
    """접지 그림자: 검정 원판, 안쪽 α150 → 가장자리 α60 (런타임에 크기 · 투명도를 곱해 떨어지는 높이를 표현)"""
    c = Cv(w, h)
    for y in range(h):
        for x in range(w):
            d = (((x + 0.5 - w / 2.0) / (w / 2.0)) ** 2 + ((y + 0.5 - h / 2.0) / (h / 2.0)) ** 2)
            if d <= 1.0:
                c.px(x, y, (0, 0, 0, 150 if d < 0.55 else 90 if d < 0.85 else 50))
    return c


def dust_frame(k, n=6, w=32, h=32):
    """먼지 한 덩이가 번지며 옅어지는 n프레임. 3톤 평면(빛 왼쪽 위) + 알파 계단, 가장자리는 들쭉날쭉(디더). 아래쪽이 평평한 둥근 무더기"""
    t = k / float(n - 1)
    c = Cv(w, h)
    cx, cy = w / 2.0, h - 9 - 5.0 * t
    rx, ry = 4.5 + 8.5 * t ** 0.7, 4.0 + 7.0 * t ** 0.7
    alpha = int(round(235 * (1 - t) ** 0.85))
    if alpha < 10:
        return c
    blobs = [(0.0, 0.05, 0.8, 0.8), (-0.55, 0.0, 0.5, 0.55), (0.55, 0.1, 0.5, 0.52), (-0.2, -0.55, 0.5, 0.5), (0.3, -0.45, 0.42, 0.45)]
    for y in range(h):
        for x in range(w):
            best = None
            for (ox, oy, sx, sy) in blobs:
                d = ((x + 0.5 - cx - ox * rx) / (rx * sx)) ** 2 + ((y + 0.5 - cy - oy * ry) / (ry * sy)) ** 2
                if d <= 1.0 and (best is None or d < best):
                    best = d
            if best is None:
                continue
            if best > 0.72 and S.hsh(x, y, 3 + k) < 0.38 + 0.5 * t:   # 가장자리 디더: 번질수록 성기게
                continue
            lit = (cy - (y + 0.5)) / ry * 0.5 + (cx - (x + 0.5)) / rx * 0.35
            col = (132, 124, 114) if lit > 0.28 else (104, 98, 91) if lit > -0.12 else (78, 74, 70)
            a = alpha if best < 0.5 else int(alpha * 0.7)
            c.px(x, y, col + (a,))
    return c


def big_rock(kind):
    """큰 낙하 잔해 3종(출발 구간 뒤쪽에서 떨어지는 것): 큰 돌덩이 / 돌덩이 + 벽돌 / 부러진 기둥 토막. 작은 돌과 같은 문법(상자 + 윗면 밝게)"""
    r = random.Random(60 + kind)
    if kind == 0:
        c = Cv(34, 28)
        S.prism(c, 17, 15, 12, 6, -12, 6, joints=1)
        S.prism(c, 25, 10, 5.5, 3.2, 38, 4)
    elif kind == 1:
        c = Cv(34, 28)
        S.prism(c, 10, 17, 8, 4.6, 24, 5, joints=1)
        S.prism(c, 23, 14, 9.5, 5.2, -22, 6, warm=True, joints=1)
        S.prism(c, 15, 8, 5.5, 3.2, 62, 4)
    else:
        c = Cv(44, 26)
        S.column_lying(c, 4, 5, 34, 11, 0, r)
        S.prism(c, 33, 19, 6, 3.2, -30, 4)
        S.prism(c, 11, 20, 4.5, 2.6, 40, 3)
    return c


def chunk_mound(seed, planks=False, w=48, h=36):
    """복도를 막으며 하나씩 떨어지는 잔해 덩이(3x2.25칸): 큰 더미(debris_a/b)와 같은 문법의 작은 무더기. 둘을 나란히 놓으면 복도 폭 6칸을 채움"""
    c = Cv(w, h)
    cx, cy, rx, ry, hmax = w / 2.0, h - 12.0, w / 2.0 + 1, 8.5, 15.0
    r = random.Random(seed)

    def prof2(gx, gy):
        u, v = (gx - cx) / rx, (gy - cy) / ry
        q = u * u + v * v
        if q > 1.0:
            return None
        return hmax * (1 - q) ** 0.55 * (1 - 0.3 * abs(u)), 1 - q
    for x in range(w):                                   # 속 채우기 (블록 사이 틈)
        u = (x - cx) / rx
        if abs(u) >= 1:
            continue
        s_ = (1 - u * u) ** 0.5
        yf = int(cy + ry * s_)
        yt = int(cy - ry * s_ * 0.6 - hmax * (1 - u * u) ** 0.55 * (1 - 0.3 * abs(u)) * 0.9)
        for y in range(max(0, yt + r.randint(0, 2)), min(h - 4, yf) + 1):
            c.px(x, y, S.mix(S.R_FR2, S.R_ED, 0.3 + 0.2 * r.random()))
    spots = []
    for gy in range(int(cy - ry), int(cy + ry) + 1, 3):
        gx = -3 + r.randint(0, 4)
        while gx < w + 3:
            spots.append((gy + r.randint(-1, 1), gx + r.randint(-2, 2)))
            gx += r.randint(6, 9)
    spots.sort()
    for (gy, gx) in spots:
        pr = prof2(min(max(gx, 1), w - 2), gy)
        if pr is None:
            continue
        hgt, inner = pr
        cyy = gy - hgt * (0.55 + 0.45 * inner)
        if cyy < 4:
            continue
        a = r.uniform(3.4, 6.4) * (0.75 + 0.4 * inner)
        b = r.uniform(2.2, 3.4)
        ang = r.choice((-1, 1)) * r.uniform(5, 75)
        S.prism(c, gx, cyy, a, b, ang, r.randint(3, 4), warm=(not planks and r.random() < 0.12), joints=1 if a > 5.5 and r.random() < 0.5 else 0)
        if planks and r.random() < 0.3:
            S.prism(c, gx + r.randint(-3, 3), cyy - 1, r.uniform(7, 10), 1.5, r.choice((-1, 1)) * r.uniform(10, 50), 2, warm=True, joints=r.choice((0, 1)))
    for x in range(w):                                   # 앞 그림자
        yb = None
        for y in range(h - 5, 0, -1):
            p = c.get(x, y)
            if p is not None and p[3] > 200:
                yb = y
                break
        if yb is None:
            continue
        for k, al in ((1, 105), (2, 105), (3, 55)):
            for xx in ((x, x + 1) if k < 3 else (x,)):
                if c.get(xx, yb + k) is None and yb + k < h:
                    c.px(xx, yb + k, (0, 0, 0, al))
    return c


FX_ITEMS = ['CollapseRock_1', 'CollapseRock_2', 'CollapseRock_3', 'CollapseRock_4', 'CollapseShadow'] + ['CollapseDust_%d' % i for i in range(6)]


def fx_items():
    its = []
    for k in range(4):
        its.append(D.make('CollapseRock_%d' % (k + 1), '떨어지는 돌 %d' % (k + 1), rock(k), 'decal', shadow=False, outline=False, note='연출용'))
    its.append(D.make('CollapseShadow', '접지 그림자', shadow_disc(), 'decal', shadow=False, outline=False, note='연출용'))
    for k in range(6):
        its.append(D.make('CollapseDust_%d' % k, '먼지 %d' % (k + 1) if k == 0 else '', dust_frame(k), 'decal', shadow=False, outline=False, note='연출용 · 6프레임'))
    for k in range(3):                                  # 시트 끝에 추가 (기존 guid · internalID 유지)
        its.append(D.make('CollapseBig_%d' % (k + 1), '큰 낙하 잔해 %d' % (k + 1), big_rock(k), 'decal', shadow=False, outline=False, note='연출용 · 출발 구간 뒤'))
    for it in its:
        it['pivot'] = (it['cv'].w / 2.0, it['fin'].h - it['cv'].h / 2.0)     # 그림 한가운데
        it['coll'] = None
    return its


def __getattr__(name):                         # gen_zone.main이 mod.ITEMS를 읽음 (그릴 때만 계산)
    if name == 'ITEMS':
        return items()
    raise AttributeError(name)


def items():
    its = [
        D.make('CollapseDebris_A', '붕괴 잔해 A (큰 기둥)', debris_a(), 'block', coll=(6, 3), shadow=False, outline=False, note='Wall 6x3'),
        D.make('CollapseDebris_B', '붕괴 잔해 B (천장 판석 · 들보)', debris_b(), 'block', coll=(6, 3), shadow=False, outline=False, note='Wall 6x3'),
    ]
    for it in its:
        it['pivot'] = (48, 36)                 # 그림(96x64) 한가운데 = 씬 오브젝트 중심 (시트 칸은 오른쪽 2px · 아래 4px 패딩)
        it['coll'] = None                      # 충돌은 씬의 BoxCollider2D(6x3)가 맡음 (스프라이트 전용, 타일 없음)
    return its + fx_items()                    # 새 항목은 시트 끝에 추가 (guid · internalID 유지)


def scene_one(sp):
    """빨강 방(14칸 폭) 북쪽 벽에서 폭 6칸 복도가 위로 뻗는 모양 — 게임과 같은 Dungeon 3줄 벽. 잔해 중심 = 월드 (6, 15)"""
    room = {(x, y) for x in range(0, 14) for y in range(0, 13)}
    corr = {(x, y) for x in range(3, 9) for y in range(13, 26)}
    fl = [D.game_tile(paths.DUNGEON_DARK, 176, 496)] + [D.game_tile(paths.DUNGEON_DARK, 192 + 16 * i, 496) for i in range(4)]

    def fp(x, y, r):
        return fl[0] if r.random() < 0.85 else r.choice(fl[1:])

    def wp(i, x, y, r):
        return S.tile(i)
    orig = P.wall_tile
    P.wall_tile = lambda f, x, y, north_rows=4: orig(f, x, y, 3)
    try:
        im, (x0, yt) = P.render(fp, wp, floor=room | corr, reach=4)
    finally:
        P.wall_tile = orig
    if sp is not None:
        # 중심 (6, 15) → 그림 왼쪽 아래 칸 (3, 13), 높이 4칸
        im.alpha_composite(sp, ((3 - x0) * 16, (yt - 13) * 16 + 16 - sp.height))
    # 플레이어(스케일 감각용): 방 안쪽, 잔해 아래
    pl = Image.open(paths.REPO_SLASH + 'Assets/04.Sprites/Player/Swordsman with shadows/Swordsman.png').convert('RGBA').crop((42, 39, 58, 60))
    im.alpha_composite(pl, ((6 - x0) * 16, (yt - 8) * 16))
    return im


def main(out):
    its = items()
    big = [it for it in its if it['kind'] == 'block']
    ims = [scene_one(None)] + [scene_one(it['cv'].img()) for it in big]
    gap = 24
    sc = Image.new('RGBA', (sum(i.width for i in ims) + gap * 2, max(i.height for i in ims)), (35, 36, 42, 255))
    x = 0
    for i in ims:
        sc.alpha_composite(i, (x, 0))
        x += i.width + gap
    im = D.draft(its, [('잔해 스프라이트 — 복도 입구(폭 6칸)를 옆벽에서 옆벽까지 채움, 노랑 = 막는 칸', ('block',)), ('카메라 흔들림 연출용 — 떨어지는 돌 4 · 접지 그림자 · 먼지 6프레임(번지며 옅어짐)', ('decal',))], sc, sc.size,
                 '튜토리얼 붕괴 잔해 %s' % VERSION, S=3, Wd=max(1536, sc.width * 3 + 24))
    im.save(out)
    print('saved', out, im.size)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        _sys.path.insert(0, _os.path.join(HERE, '..', '..'))
        import gen_zone
        gen_zone.main(SHEET, 'collapse_debris', tiles=False)
        sys.exit(0)
    main(sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'tutorial-collapse-debris-draft.png'))
