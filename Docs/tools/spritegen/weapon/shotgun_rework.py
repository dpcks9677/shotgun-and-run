"""플레이어 샷건 리워크 — 유물 총: 바랜 금속 · 고목 개머리판 · 금 장식 · 총열 앞쪽 용암 발광 (계획서 shotgun-magic-rework-plan.md 7절)

SHOTGUN_REF=<레퍼런스 이미지> python Docs/tools/spritegen/weapon/shotgun_rework.py   # 시안 Docs/drafts/props/shotgun-rework-draft.png

- 총 그림은 디자인마다 대기 자세 한 장(DESIGNS, 외곽선 제외 안쪽 픽셀)으로 정의하고, 프레임마다 이동 · 펌프 이동 · 기울기만 바꾼다(FRAMES).
- 손은 지금 shotgun.png 같은 프레임의 살색 픽셀을 그대로 옮긴다(손 위치 유지). 외곽선은 마지막에 한 번 두른다.
- 금속 색은 역할 4단(1 밝음 ~ 4 가장 어두움)으로 그리고 METAL에서 톤만 바꾼다(황동 · 은빛 비교). 발광은 펠릿 팔레트만 쓴다.
- 승인 뒤 전체 프레임으로 넓힐 때는 FRAMES에 나머지 슬라이스를 추가하고 sheet()에서 칸마다 draw_frame을 붙인다.
"""
import math
import os
import re
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bl  # noqa: E402
import paths  # noqa: E402

SHEET = os.path.join(paths.SPRITES, 'Weapon', 'shotgun.png')
# 손 위치 · 지금 총 그림의 기준: 덮어쓰기 전 원본 시트(fdf2ab1b · c23a0820 판). Assets 시트는 이 스크립트 결과로 바뀌므로 여기서 읽지 않는다.
BASE_PNG = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'shotgun_base.png')
CELL_W, CELL_H = 64, 32
OUTLINE = (0, 0, 0, 255)                     # 캐릭터 · 지금 총과 같은 순검정
SKIN = {(224, 179, 165, 255), (242, 218, 210, 255)}
MUZZLE = (19.2 + 1.272 * 16, 16 - 0.177 * 16)  # Player.prefab Muzzle(1.272, 0.177) → 칸 픽셀 (피벗 0.3, 0.5)

# 공통: 칸 왼쪽 위 원점, 문자 한 칸 = 픽셀 하나, '.' = 빈칸. 외곽선은 draw_frame이 마지막에 두른다.
WOOD_RED = {'a': bl.H('9f472e'), 'e': bl.H('7a3824'), 'b': bl.H('5c2b1d'), 'c': bl.H('401a12')}      # v01 적갈색
WOOD_RELIC = {'a': bl.H('8a5036'), 'e': bl.H('6e3c2a'), 'b': bl.H('50291d'), 'c': bl.H('341a13')}    # v03 붉은 고목
FOREND_RED = {'h': bl.H('d47a4c'), 'n': bl.H('b2482a'), 'd': bl.H('7c2a1c')}   # 포엔드: 금속보다 밝은 붉은 나무(지금 총 포엔드 계열)
WOOD_OLD = {'a': bl.H('6e4a32'), 'e': bl.H('57392a'), 'b': bl.H('40281d'), 'c': bl.H('2a1912')}      # v02 짙은 고목
FILIGREE = {'G': bl.H('e2c068'), 'g': bl.H('9c7a36')}                                                 # 금 장식 (발광 아님)
LAVA = {'W': bl.H('ffffff'), 'w': bl.FL_W, 'y': bl.FL_Y, 'o': bl.FL_O, 'r': bl.FL_R, 'k': bl.H('2a1008')}  # 펠릿 팔레트 + 균열
METAL = {
    'steel': {'1': bl.H('9baab4'), '2': bl.H('616d83'), '3': bl.H('3b3f52'), '4': bl.H('202131')},   # v01 청회색
    'brass': {'1': bl.H('c4a965'), '2': bl.H('8a7044'), '3': bl.H('574229'), '4': bl.H('2f2318')},   # 바랜 황동
    'silver': {'1': bl.H('aaa598'), '2': bl.H('6f6a60'), '3': bl.H('47403a'), '4': bl.H('29231f')},  # 낡은 은빛(갈색 그림자)
}

# 디자인: 대기 자세 안쪽 픽셀(rows, 첫 줄 y), 펌프 때 움직이는 포엔드(문자열 인덱스 범위, 줄 인덱스), 팔레트
DESIGNS = {
    # v01: 청회색 + 금색 룬(P/C/B/D = 밝은 점 · 주 · 보조 · 그림자), 기관부 위 1px 돌기
    'v01': dict(y=11, forend=(17, 23), tube=(4, 5), rows=[
        '.........11112.................',
        '........122222P122312231223123.',
        '.1aaeaab142CCCC222322232223224.',
        '.2bCebec122DDDD333333333333333.',
        '.2bCbebc2222232C3eaaeaab213....',
        '.3cDcc..3333343B4cbcbbcc324....',
        '.3cc....ebc....................',
        '.......eb......................',
        '.......bc......................',
    ], pal={**WOOD_RED, 'P': bl.FL_W, 'C': bl.FL_Y, 'B': bl.FL_O, 'D': bl.FL_R}),
    # v02: 돌기 없음(높이 −1px), 기관부 · 개머리판 금 장식, 총열 앞쪽 절반 = 갈라진 용암 발광(총구 쪽이 가장 밝음)
    'v02': dict(y=12, forend=(17, 23), tube=(3, 4), rows=[
        '........122G223122312rookyywwW.',
        '.1aaeaab14Gg222222313rrookyywW.',
        '.2bGebec122g233333334krooykyww.',
        '.2bgbebc222223213eaaeaab213....',
        '.3cgcc..333334334cbcbbcc324....',
        '.3cc....ebc....................',
        '.......eb......................',
        '.......bc......................',
    ], pal={**WOOD_OLD, **FILIGREE, **LAVA}),
    # v03: v02 안 B 색 정리(평면 줄 구조 · 금 장식 2곳) + 굴곡 — 기관부 앞뒤 경사 · 방아쇠울 · 개머리판 손목/토 곡선 · 펌프관 끝 둥글게
    'v03w': dict(y=12, forend=(17, 23), tube=(3, 4), rows=[
        '.........111113122312rookyywwW.',
        '.1aaeaae122Gg23222322rrookyywW.',
        '.2bGebcc122g223333333krooykyww.',
        '.2bgebc.122223213eaaaaae22.....',
        '.3cbcc..33333.334bbcbbcc3......',
        '..cc....ebc.3..................',
        '.......eb..33..................',
        '.......bc......................',
    ], pal={**WOOD_RELIC, **FILIGREE, **LAVA}),
    # v03 강: 약 + 개머리판 힐 낙차 · 포엔드 아래 볼록(1px) · 총구 발광 끝이 아래로 1px 넓어짐
    'v03s': dict(y=12, forend=(17, 23), tube=(3, 4, 5), rows=[
        '.........111113122312rookyywwW.',
        '....aaae122Gg23222322rrookyywW.',
        '.1aaeebc122g223333333krooykyww.',
        '.2eGbbc.122223213eaaaaae22..yw.',
        '.3bgcc..33333.334bbcbbcc3......',
        '..cc....ebc.3.....cbbbc........',
        '.......eb..33..................',
        '.......bc......................',
    ], pal={**WOOD_RELIC, **FILIGREE, **LAVA}),
    # v03u: 사용자가 v03 약 대기 프레임을 손으로 고친 것(Docs/drafts/props/shotgun-v03w-shotgun_0.png)을 역변환.
    # 손 · 외곽선을 떼고 색을 문자로 되돌림. 손 아래 가려진 8칸은 v03w 값으로 채움(펌프 · 반동에서 드러남).
    'v03u': dict(y=11, forend=(17, 23), tube=(4, 5), rows=[
        '...............................',   # 가늠쇠 제거 (사용자 요청, 눈 가림)
        '.........111131222312rookyywwW.',
        '.1aaeaae122Gg23111322rrookyywW.',
        '.2bGebcc122g222333333krooykyww.',
        '.2bgebc.1222223234hhhnn422.....',   # 포엔드: 앞뒤 홈(4) + 밝은 붉은 나무 3톤 (펌프 움직임이 보이게)
        '.3cbcc..333233...4nnndd43......',
        '..cc....ebc3...................',
        '.......eb......................',
        '.......bc......................',
    ], pal={**WOOD_RELIC, **FILIGREE, **LAVA, **FOREND_RED}, fill='13.'),
}
X0 = 9   # 모든 디자인의 첫 칸 x

# 슬라이스 이름 → (이동 dx, dy, 포엔드 이동, 기울임). 값은 지금 시트의 같은 프레임에서 잰 것.
FRAMES = {
    # 슬라이스 이름 → 총 이동(dx, dy) · 포엔드 이동(pump) · 반동 기울임(tilt) · 꺾임(step: x ≥ x0 픽셀을 d만큼 더 올림).
    # 값은 지금 시트의 같은 프레임과 겹침이 가장 큰 변환을 찾아 잰 것. 손은 지금 프레임의 살색 픽셀을 그대로 옮긴다.
    'shotgun_0': dict(label='대기', dx=0, dy=0, pump=0),
    'shotgun_1': dict(label='사격 섬광', dx=0, dy=0, pump=0),
    'shotgun_2': dict(label='사격 반동', dx=-1, dy=0, pump=0, tilt=1),
    'shotgun_3': dict(label='반동 복귀', dx=-1, dy=1, pump=0, tilt=1),
    'shotgun_4': dict(label='사격 가라앉음', dx=0, dy=2, pump=0),
    'shotgun_5': dict(label='가라앉음 복귀', dx=0, dy=1, pump=0),
    'shotgun_6': dict(label='펌프 시작', dx=-1, dy=1, pump=-1),
    'shotgun_7': dict(label='펌프 당김', dx=-1, dy=1, pump=-3),
    'shotgun_8': dict(label='펌프 밀기', dx=1, dy=1, pump=0),
    'shotgun_9': dict(label='펌프 복귀', dx=1, dy=0, pump=0),
    'shotgun_10': dict(label='대기 (펌프 끝)', dx=0, dy=0, pump=0),
    'shotgun_11': dict(label='장전 · 손 내림', dx=0, dy=0, pump=0),
    'shotgun_12': dict(label='장전 · 탄 넣기', dx=0, dy=0, pump=0),
    'shotgun_13': dict(label='장전 · 총 들기', dx=0, dy=-1, pump=0, step=(23, -1)),
    'shotgun_14': dict(label='장전 · 손 뗌', dx=0, dy=0, pump=0, step=(23, -1)),
    'shotgun_15': dict(label='손 없음', dx=-1, dy=0, pump=0),
    # 보간 프레임(지금 시트에 없음): 손은 두 프레임 손의 중간 위치로 옮긴다(hands = (앞 프레임, 뒤 프레임, 비율))
    'shotgun_16': dict(label='반동 절반 (보간)', dx=0, dy=0, pump=0, tilt=0.5, hands=('shotgun_1', 'shotgun_2', 0.5)),
    # 튀는 동작(스프링 오버슈트) 프레임: 손은 대기 손을 총 이동 그대로 따라 옮긴다(앞손은 포엔드도 따라감)
    'shotgun_17': dict(label='반동 지나침 (앞 · 아래)', dx=2, dy=1, pump=0, tilt=-0.125, hands='follow'),
    'shotgun_18': dict(label='펌프 깊이 당김', dx=-2, dy=1, pump=-3, hands='follow'),
    'shotgun_19': dict(label='펌프 앞으로 튐', dx=2, dy=0, pump=0, hands='follow'),
}
# meta에 추가할 슬라이스 (x, y, w, h — 아래가 원점). 지금 시트의 빈 칸 3개
NEW_RECTS = {'shotgun_16': (128, 96, 64, 32), 'shotgun_17': (192, 96, 64, 32),
             'shotgun_18': (256, 96, 64, 32), 'shotgun_19': (256, 0, 64, 32)}
DRAFT_FRAMES = ('shotgun_0', 'shotgun_2', 'shotgun_7')   # 3프레임 시안에 쓰는 대표 프레임


def tilt_dy(x, k=1):
    """반동 기울임: k=1이면 4칸마다 1px씩 올라가는 계단(지금 shotgun_2의 손 · 개머리판 · 총구 높이에 맞춤).
    k는 기울기 비율 — 0.5면 8칸마다 1px (보간 프레임)"""
    return -math.floor(k * (x - 5) / 4)


def gun_pixels(design, pal, pump=0):
    """대기 자세 기준 {(x, y): 색}. pump만큼 포엔드 나무를 앞뒤로 옮기고 빈 자리는 관 금속으로 채운다."""
    rows = [list(r) for r in design['rows']]
    if pump:
        f0, f1 = design['forend']
        for j in design['tube']:
            wood = rows[j][f0:f1 + 1]
            fill = design.get('fill', '23.')[design['tube'].index(j)]   # 포엔드가 비운 자리: 관 윗줄 · 아랫줄 · 아래 볼록(빈칸)
            for i in range(f0, f1 + 1):
                rows[j][i] = fill
            for k, ch in enumerate(wood):
                rows[j][f0 + pump + k] = ch
        # 포엔드를 당기면 앞쪽 관 아랫줄을 비워 관이 얇게 드러나게 한다(맨 끝 1칸은 관 마개로 남김, 사용자 요청).
        if pump < 0:
            bottom = rows[design['tube'][1]]
            for i in range(f1 + pump + 1, f1):
                bottom[i] = '.'
    return {(X0 + i, design['y'] + j): pal[ch]
            for j, r in enumerate(rows) for i, ch in enumerate(r) if ch in pal}


def sheet_rects():
    meta = open(SHEET + '.meta', encoding='utf8').read()
    rects = {}
    for name, x, y, w, h in re.findall(
            r'name: (shotgun_\d+)\n\s+rect:\n\s+serializedVersion: 2\n\s+x: (\d+)\n\s+y: (\d+)\n\s+width: (\d+)\n\s+height: (\d+)', meta):
        rects[name] = (int(x), int(y), int(w), int(h))
    return {**rects, **NEW_RECTS}


def old_frame(name):
    im = Image.open(BASE_PNG).convert('RGBA')
    x, y, w, h = sheet_rects()[name]
    top = im.height - y - h
    return im.crop((x, top, x + w, top + h))


def hand_pixels(frame):
    return {(x, y): frame.getpixel((x, y)) for y in range(frame.height) for x in range(frame.width)
            if frame.getpixel((x, y)) in SKIN}


def hand_groups(frame):
    """손 픽셀을 붙어 있는 덩어리로 나눠 x 순서로 (손잡이 손, 앞손 …)"""
    left, groups = hand_pixels(frame), []
    while left:
        stack, g = [next(iter(left))], {}
        while stack:
            p = stack.pop()
            if p in left:
                g[p] = left.pop(p)
                stack += [(p[0] + dx, p[1] + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)]
        groups.append(g)
    return sorted(groups, key=lambda g: min(g))


def between_hands(a, b, k):
    """두 프레임 손의 중간: 손마다 무게중심 이동을 k만큼(반올림) 옮기고, 모양은 가까운 프레임 것"""
    ga, gb = hand_groups(old_frame(a)), hand_groups(old_frame(b))
    out = {}
    for ha, hb in zip(ga, gb):
        ca = [sum(v) / len(ha) for v in zip(*ha)]
        cb = [sum(v) / len(hb) for v in zip(*hb)]
        dx, dy = (math.floor((cb[i] - ca[i]) * k + 0.5) for i in (0, 1))
        src, ox, oy = (ha, dx, dy) if k <= 0.5 else (hb, dx - round(cb[0] - ca[0]), dy - round(cb[1] - ca[1]))
        out.update({(x + ox, y + oy): c for (x, y), c in src.items()})
    return out


def follow_hands(f):
    """대기(shotgun_0) 손을 이 프레임의 총 이동만큼 옮긴다: 덩어리 왼쪽 x에서의 이동 · 기울기, 앞손은 포엔드 이동도 더함"""
    out = {}
    for i, g in enumerate(hand_groups(old_frame('shotgun_0'))):
        x0 = min(x for x, _ in g)
        sx = f['dx'] + (f['pump'] if i == 1 else 0)
        sy = f['dy'] + tilt_dy(x0 + (f['pump'] if i == 1 else 0), f.get('tilt', 0))
        out.update({(x + sx, y + sy): c for (x, y), c in g.items()})
    return out


def draw_frame(name, design, metal):
    """design = DESIGNS 키, metal = METAL 키"""
    f, dz = FRAMES[name], DESIGNS[design]
    cv = bl.Cv(CELL_W, CELL_H)
    for (x, y), c in gun_pixels(dz, {**METAL[metal], **dz['pal']}, f['pump']).items():
        x0, d = f.get('step', (CELL_W, 0))
        cv.px(x + f['dx'], y + f['dy'] + tilt_dy(x, f.get('tilt', 0)) + (d if x >= x0 else 0), c)
    if f.get('hands') == 'follow':
        hands = follow_hands(f)
    elif 'hands' in f:
        hands = between_hands(*f['hands'])
    else:
        hands = hand_pixels(old_frame(name))
    for (x, y), c in hands.items():
        cv.px(x, y, c)
    return cv.outline(OUTLINE).img()


# ---------- 몸체 액션 (분절 · 스쿼시/스트레치 · 흰 번쩍임) ----------
# 총을 분절 3개로 나눠 프레임마다 따로 움직인다(레퍼런스의 통통 튀는 몸체 액션).
#   R 기관부(+손잡이 · 손잡이 손): (dx, dy, k) — k는 tilt_dy 비율, 손잡이(x 18) 기준으로 꺾임(+ = 총구 위)
#   S 개머리판: k — 손목(x 17)에서 꺾임. 기관부의 손목 높이에 붙어 다닌다
#   B 총열 · 포엔드(+앞손): (k, 처짐, 가로, 세로) — 기관부의 이음(x 23) 높이에 붙고, 처짐만큼 더 내려감.
#     가로 +n = 판 · 발광 열을 복제해 늘림(스트레치), −n = 앞쪽 발광 열을 빼서 줄임(스쿼시). 세로 1 = 아랫면 줄 복제(아래로만 두꺼워짐)
#   fx: 'white' = 몸체 전체 흰 번쩍임(외곽선 유지), 'glow' = 발광만 한 단계 밝게
JOINT_S, PIVOT_R, JOINT_B = 17, 18, 23
STRETCH_COLS = (25, 35, 27)      # 복제할 열 (기관부 앞 관 · 발광 중간 · 판 마디) — 순서대로 1 · 2 · 3px
SQUASH_COLS = (34, 36, 32)       # 뺄 열 (발광 · 총구 쪽)
UNDER_ROW = 14                   # 세로 스쿼시 때 복제하는 총열 아랫면 줄
ACTION = {
    'shotgun_1': dict(R=(-1, 0, 0), S=0, B=(0, 0, -2, 1), fx='white'),     # 발사: 흰 번쩍임 · 짧고 뭉툭
    'shotgun_16': dict(R=(-1, -2, 1), S=1, B=(0.25, 1, 0, 0), fx='glow'),  # 손목에서 꺾임 · 총열은 늦게 따라옴
    'shotgun_2': dict(R=(-1, -3, 1), S=1, B=(1, 0, 1, 0)),                 # 꼭대기 · 약한 스트레치
    'shotgun_3': dict(R=(-1, -2, 0.5), S=0.5, B=(-0.5, 0, 1, 0)),         # 복귀 · 총열 앞이 처짐(S자)
    'shotgun_17': dict(R=(2, 1, 0), S=0, B=(-0.5, 0, 2, 0)),              # 앞 · 아래로 지나침 + 스트레치 2
    'shotgun_8': dict(R=(1, 1, 0), S=0, B=(0, 0, 1, 0)),                   # 되돌아옴 · 스트레치 1
    'shotgun_5': dict(R=(0, 1, 0), S=0, B=(0, 0, -1, 0)),                  # 약한 스쿼시
    'shotgun_6': dict(R=(-1, 1, 0), S=0, B=(0, 0, -1, 0), pump=-1),        # 펌프 당기기 · 스쿼시 1
    'shotgun_7': dict(R=(-1, 1, 0), S=0, B=(0, 0, -2, 0), pump=-3),        # 스쿼시 2 · 포엔드 뒤로
    'shotgun_18': dict(R=(-2, 1, 0), S=0, B=(0, 0, -2, 0), pump=-3),       # 버팀
    'shotgun_19': dict(R=(2, 0, 0), S=0, B=(0, 0, 2, 0)),                  # 앞으로 튐 · 스트레치 2
    'shotgun_9': dict(R=(1, 0, 0), S=0, B=(0, 0, 1, 0)),                   # 스트레치 1
}
GLOW_UP = {'r': 'o', 'o': 'y', 'y': 'w', 'w': 'W'}


def lean(x, pivot, k):
    return -math.floor(k * (x - pivot) / 4)


def x_ops(sx):
    """가로 스트레치 · 스쿼시에서 열 x가 옮겨 가는 곳: [(새 x, ...)] — 복제 열은 두 번, 뺀 열은 없음"""
    cols = STRETCH_COLS[:sx] if sx > 0 else SQUASH_COLS[:-sx]

    def where(x):
        out = [x]
        for c in sorted(cols, reverse=True):
            nxt = []
            for v in out:
                if sx > 0:
                    nxt += [v, v + 1] if v == c else [v + (v > c)]
                elif v != c:
                    nxt.append(v - (v > c))
            out = nxt
        return out
    return where


def draw_action(name, design, metal):
    """ACTION 프레임: 분절별로 옮기고 꺾은 뒤 손 · 외곽선. 손은 대기 손을 자기 분절과 같이 옮긴다"""
    a, dz = ACTION[name], DESIGNS[design]
    pump = a.get('pump', 0)
    rdx, rdy, rk = a['R']
    bk, sag, sx, sy = a['B']
    chars = {}
    rows = [list(r) for r in dz['rows']]
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            chars[(X0 + i, dz['y'] + j)] = ch
    # 펌프(포엔드 이동)는 gun_pixels 규칙을 그대로 쓰고 문자로 되돌린다
    pal = {**METAL[metal], **dz['pal']}
    inv = {v: k for k, v in pal.items()}
    px = {p: inv[c] for p, c in gun_pixels(dz, pal, pump).items()}

    def r_off(x):
        return rdy + lean(x, PIVOT_R, rk)

    where = x_ops(sx)
    cv = bl.Cv(CELL_W, CELL_H)
    layers = {'S': {}, 'B': {}, 'R': {}}
    for (x, y), ch in px.items():
        if x <= 16 and y <= 17:
            layers['S'][(x + rdx, y + r_off(JOINT_S) + lean(x, JOINT_S, a['S']))] = ch
        elif x <= 22:
            layers['R'][(x + rdx, y + r_off(x))] = ch
        else:
            ys = [y + 1] if sy and y > UNDER_ROW else [y, y + 1] if sy and y == UNDER_ROW else [y]
            for nx in where(x):
                for ny in ys:
                    layers['B'][(nx + rdx, ny + r_off(JOINT_B) + sag + lean(nx, JOINT_B, bk))] = ch
    for seg in ('S', 'B', 'R'):          # 기관부를 맨 위에 — 이음새를 덮는다
        for (x, y), ch in layers[seg].items():
            if a.get('fx') == 'glow':
                ch = GLOW_UP.get(ch, ch)
            c = pal[ch]
            if a.get('fx') == 'white':
                c = bl.H('ffffff') if ch in LAVA or sum(c[:3]) >= 300 else bl.FL_W
            cv.px(x, y, c)
    # 손: 손잡이 손은 기관부, 앞손은 총열 분절(포엔드 이동 · 가로 변형 · 처짐 포함)을 따라간다
    grip, front = hand_groups(old_frame('shotgun_0'))[:2]
    gx0 = min(x for x, _ in grip)
    for (x, y), c in grip.items():
        cv.px(x + rdx, y + r_off(gx0), c)
    fx0 = min(x for x, _ in front) + pump
    nfx = where(fx0)[0]
    fy = r_off(JOINT_B) + sag + lean(nfx, JOINT_B, bk) + (1 if sy else 0)
    for (x, y), c in front.items():
        cv.px(x + pump + (nfx - fx0) + rdx, y + fy, c)
    return cv.outline(OUTLINE).img()


def frame_img(name, design, metal, action=None):
    """action: None = 강체, 'body' = 몸체 액션(지난안), 'muzzle' = 총구 액션"""
    if action == 'body' and name in ACTION:
        return draw_action(name, design, metal)
    if action == 'muzzle' and name in MUZZLE_ACTION:
        return draw_muzzle(name, design, metal)
    return draw_frame(name, design, metal)


# ---------- 총구 액션 (몸체 강체 회전 · 총구 발광 섬광 그라데이션) ----------
# 사용자 피드백: 몸체 변형은 과함, 총구만 늘이면 흘러내려 보임 → 총 전체를 강체로 이동 · 회전하고, 총구 발광 구간만 섬광 단계를 준다.
#   m = (dx, dy, k): k는 tilt_dy 비율. + = 총구 위(반동), − = 총구 아래(숙임). 같은 계단 규칙(x 5 기준)을 부호만 바꿔 써서
#   계단 간격이 총 전체에서 일정하고, 숙일 때는 개머리판이 같은 기울기로 올라간다. dy로 손잡이 높이를 맞춘다.
#   fx: 섬광 단계 — 'flash' 발사(총구 끝이 가장 밝은 그라데이션, 총구 형체는 바꾸지 않음 — 사용자 요청),
#       'fade' 다음(끝 WHITE 2열 · FL_W · 나머지 두 단계 밝게), 'glow' 그다음(한 단계 밝게), 없으면 평소
FLASH_BANDS = {'flash': ((3, 'W'), (3, 'w'), (99, 'y')), 'fade': ((2, 'W'), (3, 'w'))}   # 총구 끝에서부터 (열 수, 색)
MUZZLE_ACTION = {
    'shotgun_1': dict(m=(0, 0, 0), fx='flash'),
    'shotgun_16': dict(m=(0, 0, 0.5), fx='fade'),
    'shotgun_2': dict(m=(-1, 0, 1), fx='glow'),
    'shotgun_3': dict(m=(-1, 1, 0)),                    # 복귀: 수평
    'shotgun_17': dict(m=(2, -2, -2 / 3)),              # 오버슈트: 6칸마다 1px — 총구 끝 +4, 개머리판 끝 −1
    'shotgun_8': dict(m=(1, -1, -0.25)),                # 덜 숙임: 16칸마다 1px — 총구 끝 +2
    'shotgun_5': dict(m=(0, 1, 0)),
    'shotgun_6': dict(m=(-1, 1, 0), pump=-1),
    'shotgun_7': dict(m=(-1, 1, 0), pump=-3),
    'shotgun_18': dict(m=(-2, 1, 0), pump=-3),
    'shotgun_19': dict(m=(2, -1, -0.25)),               # 앞으로 튐 · 살짝 숙임
    'shotgun_9': dict(m=(1, 0, 0)),
}


def draw_muzzle(name, design, metal):
    """MUZZLE_ACTION 프레임: 강체 이동 · 회전(tilt_dy) + 총구 발광 섬광 단계. 손은 대기 손을 회전된 총 위로 옮긴다"""
    a, dz = MUZZLE_ACTION[name], DESIGNS[design]
    pump, fx = a.get('pump', 0), a.get('fx')
    dx, dy, k = a['m']
    pal = {**METAL[metal], **dz['pal']}
    inv = {v: c for c, v in pal.items()}
    px = {p: inv[c] for p, c in gun_pixels(dz, pal, pump).items()}
    lava = {p for p, ch in px.items() if ch in LAVA}
    tip = max(x for x, _ in lava)
    for p in lava:
        ch = px[p]
        if fx in FLASH_BANDS:
            back, col = tip - p[0], None
            for n, c in FLASH_BANDS[fx]:
                if back < n:
                    col = c
                    break
                back -= n
            ch = col or GLOW_UP.get(GLOW_UP.get(ch, ch), GLOW_UP.get(ch, ch))
        elif fx == 'glow':
            ch = GLOW_UP.get(ch, ch)
        px[p] = ch
    cv = bl.Cv(CELL_W, CELL_H)
    for (x, y), ch in px.items():
        # 총구 블록(끝 4칸)은 계단이 갈라지지 않게 한 덩어리로 기울인다(끝만 처져 흘러내려 보이는 것 방지, 사용자 요청).
        cv.px(x + dx, y + dy + tilt_dy(min(x, tip - 3), k), pal[ch])
    for i, g in enumerate(hand_groups(old_frame('shotgun_0'))):
        sx = pump if i == 1 else 0
        x0 = min(x for x, _ in g) + sx
        for (x, y), c in g.items():
            cv.px(x + dx + sx, y + dy + tilt_dy(x0, k), c)
    return cv.outline(OUTLINE).img()


# ---------- 시안 ----------

FLOOR = (40, 36, 47, 255)    # 어두운 던전 바닥 #28242f
PANEL = (30, 27, 36, 255)
INK = (225, 220, 210)
DIM = (150, 144, 160)
REF_IMG = os.environ.get('SHOTGUN_REF')


def zoom(im, k):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def on(bg, im):
    out = Image.new('RGBA', im.size, bg)
    out.alpha_composite(im)
    return out


def cell_panel(im, k=8):
    """8배 확대 + 칸 테두리 + Muzzle 위치 표시(분홍 십자)"""
    big = zoom(on(FLOOR, im), k)
    d = ImageDraw.Draw(big)
    d.rectangle([0, 0, big.width - 1, big.height - 1], outline=(90, 84, 104))
    mx, my = MUZZLE[0] * k, MUZZLE[1] * k
    d.line([mx - 6, my, mx + 6, my], fill=(255, 80, 200))
    d.line([mx, my - 6, mx, my + 6], fill=(255, 80, 200))
    return big


def player_body():
    im = Image.open(paths.PLAYER).convert('RGBA')
    return im.crop((41, im.height - 639 - 23, 59, im.height - 639))   # Swordsman_0 (rect 41, 639, 18×23, 피벗 가운데)


# 무기 칸 좌상단 = 몸 피벗(9, 11.5) + (AimOrigin + WeaponPivot + WeaponSprite)·16 − 칸 피벗(19.2, 16), 몸 좌상단 기준
GUN_AT = (round(9 + (0.434 - 0.646 + 0.014) * 16 - 19.2), round(11.5 - (0.02 - 0.258) * 16 - 16))
EYES = {(56, 6, 4), (255, 255, 255), (139, 14, 9)}   # Swordsman_0 눈 픽셀 색 (눈동자 · 흰자 · 붉은 홍채)


def eye_overlap(gun):
    """오른쪽을 겨눈 대기 배치에서 총의 불투명 픽셀이 덮는 캐릭터 눈 픽셀 수"""
    body = player_body()
    gx, gy = GUN_AT
    return sum(1 for y in range(body.height) for x in range(body.width)
               if body.getpixel((x, y))[:3] in EYES and body.getpixel((x, y))[3]
               and 0 <= x - gx < CELL_W and 0 <= y - gy < CELL_H and gun.getpixel((x - gx, y - gy))[3])


def scene(guns, extra, k=4):
    """캐릭터 + 총(대기)들을 1배로 바닥 위에 놓고, 이어서 extra 프레임들을 총만 놓은 뒤 k배"""
    body = player_body()
    gx, gy = GUN_AT
    canvas = Image.new('RGBA', (16 + len(guns) * 56 + len(extra) * 46, 36), FLOOR)
    x = 16
    for idle in guns:
        canvas.alpha_composite(body, (x, 8))
        canvas.alpha_composite(idle, (x + gx, 8 + gy))
        x += 56
    for fr in extra:
        canvas.alpha_composite(fr, (x - 8, 0))
        x += 46
    return zoom(canvas, k)


def reference_thumb():
    if not REF_IMG or not os.path.exists(REF_IMG):
        return None
    ref = Image.open(REF_IMG).convert('RGBA')
    return ref.resize((ref.width // 2, ref.height // 2), Image.LANCZOS)


# 시안 열: (제목, 디자인, 금속). 디자인 None = 지금 시트
COLUMNS = [
    ('지금 (shotgun.png)', None, None),
    ('v03 약 (생성)', 'v03w', 'silver'),
    ('v04 · 사용자 수정 반영', 'v03u', 'silver'),
]


def draft(path):
    font, small = bl.load_font(26), bl.load_font(16, bold=False)
    names = list(DRAFT_FRAMES)
    imgs = [{n: old_frame(n) if dz is None else draw_frame(n, dz, mt) for n in names} for _, dz, mt in COLUMNS]

    pw, ph, gap = CELL_W * 8, CELL_H * 8, 16
    W = 40 + len(COLUMNS) * (pw + gap)
    H = 110 + len(names) * (ph + 50) + 3 * (36 * 4 + 60) + 300
    out = Image.new('RGBA', (W, H), PANEL)
    d = ImageDraw.Draw(out)
    d.text((24, 16), 'shotgun-rework v04 · 사용자 수정 반영', font=bl.load_font(34), fill=INK)
    d.text((24, 62), '64×32 칸 · PPU 16 · 8배 확대 · 분홍 십자 = Muzzle 위치(Player.prefab) · 손은 지금 시트 같은 프레임 위치 그대로 · '
           '발광 = 펠릿 팔레트(WHITE · FL_W · FL_Y · FL_O · FL_R)', font=small, fill=DIM)
    y = 100
    for i, (title, _, _) in enumerate(COLUMNS):
        d.text((24 + i * (pw + gap), y), title, font=font, fill=INK)
    y += 40
    for n in names:
        for i, col in enumerate(imgs):
            im = col[n]
            out.alpha_composite(cell_panel(im), (24 + i * (pw + gap), y))
            b = im.getchannel('A').point(lambda a: 255 if a == 255 else 0).getbbox()   # 반투명 섬광 제외
            d.text((32 + i * (pw + gap), y + ph - 26),
                   f'{n} · {FRAMES[n]["label"]} · 폭 {b[2] - b[0]}px · 높이 {b[3] - b[1]}px · 눈 겹침 {eye_overlap(im)}px',
                   font=small, fill=DIM)
        y += ph + 50

    d.text((24, y), '어두운 바닥(#28242f) 위 1배 → 4배 · 캐릭터 + 대기 총: 지금 · v03 약 · v04(사용자 수정)', font=small, fill=DIM)
    sc = scene([col['shotgun_0'] for col in imgs], [])
    out.alpha_composite(sc, (24, y + 24))
    y += sc.height + 50
    for i, (title, dz, _) in enumerate(COLUMNS):
        if dz != 'v03u':
            continue
        d.text((24, y), f'{title} · 대기 · 반동 · 펌프 (1배 → 4배)', font=small, fill=INK)
        sc = scene([], [imgs[i][n] for n in names])
        out.alpha_composite(sc, (24, y + 24))
        y += sc.height + 40

    ref = reference_thumb()
    if ref:
        d.text((24, y), '레퍼런스 (1/2 축소)', font=small, fill=DIM)
        out.alpha_composite(ref, (24, y + 24))
        y += ref.height + 40
    out = out.crop((0, 0, W, y))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.convert('RGB').save(path)
    return out


# ---------- 전체 시트 · GIF ----------

def sheet(design, metal, action='muzzle'):
    """지금 shotgun.png와 같은 크기 · 같은 슬라이스 rect에 모든 프레임을 그린 시트 (그대로 덮어쓰면 Animator 유지).
    기본은 확정안(총구 액션: 강체 반동 · 숙임 + 총구 발광 섬광)"""
    src = Image.open(BASE_PNG)
    out = Image.new('RGBA', src.size, (0, 0, 0, 0))
    for name, (x, y, w, h) in sheet_rects().items():
        out.alpha_composite(frame_img(name, design, metal, action), (x, src.height - y - h))
    return out


def sheet_preview(sheet_im, k=8):
    """시트 배치 그대로 칸마다 k배 + 슬라이스 이름 · 동작 라벨"""
    small = bl.load_font(18, bold=False)
    out = Image.new('RGBA', (sheet_im.width * k + 40, sheet_im.height * k + 80), PANEL)
    d = ImageDraw.Draw(out)
    d.text((20, 16), '샷건 전체 시트 미리보기 · v03u(사용자 수정) · 칸 64×32 · 8배', font=bl.load_font(26), fill=INK)
    for name, (x, y, w, h) in sheet_rects().items():
        top = sheet_im.height - y - h
        px, py = 20 + x * k, 60 + top * k
        out.alpha_composite(cell_panel(sheet_im.crop((x, top, x + w, top + h)), k), (px, py))
        d.text((px + 8, py + 6), f'{name} · {FRAMES[name]["label"]}', font=small, fill=INK)
    return out


ANIM_DIR = paths.repo('Assets', '05.Animations', 'Shotgun')


def clip_keys(clip):
    """Assets .anim의 스프라이트 키 → [(슬라이스 이름, 시작 s)], 클립 길이 s"""
    text = open(os.path.join(ANIM_DIR, clip + '.anim'), encoding='utf8').read()
    meta = open(SHEET + '.meta', encoding='utf8').read()
    ids = dict(re.findall(r'213: (-?\d+)\n\s+second: (shotgun_\d+)', meta))
    guid = re.search(r'guid: (\w+)', meta).group(1)
    keys = [(ids[i], float(t)) for t, i in re.findall(r'time: ([\d.e-]+)\n\s+value: \{fileID: (-?\d+), guid: ' + guid, text)]
    return keys, float(re.search(r'm_StopTime: ([\d.e-]+)', text).group(1))


FIRE_KEYS = ('shotgun_1', 'shotgun_16', 'shotgun_2', 'shotgun_3', 'shotgun_4', 'shotgun_5', 'shotgun_6', 'shotgun_9')
FIRE_FPS = 20


def fire_clip(keys=FIRE_KEYS):
    """20fps 클립 [(슬라이스, 시각)], 길이. 기본은 v04 Fire: 반동 보간(16) + 지금 안 쓰던 3 · 5 — 0.4초"""
    return [(n, i / FIRE_FPS) for i, n in enumerate(keys)], len(keys) / FIRE_FPS


# 튀는 버전(스프링 오버슈트): 레퍼런스처럼 기준을 지나쳤다 돌아온다. Fire + PumpAction ≤ 연사 간격 0.8초
BOUNCE = {
    'Fire': ('shotgun_1', 'shotgun_16', 'shotgun_2', 'shotgun_3', 'shotgun_17', 'shotgun_8', 'shotgun_5', 'shotgun_0'),
    'PumpAction': ('shotgun_6', 'shotgun_7', 'shotgun_18', 'shotgun_18', 'shotgun_19', 'shotgun_9', 'shotgun_10'),
}


def timeline(overrides, clips, tail):
    """[(클립, [(슬라이스, 시각)], 길이)] — 대기 0.5초 → clips → 대기 tail초. overrides에 없는 클립은 Assets .anim"""
    return ([('Idle', [('shotgun_0', 0.0)], 0.5)] + [(c, *overrides.get(c, clip_keys(c))) for c in clips]
            + [('Idle', [('shotgun_0', 0.0)], tail)])


def at(tl, t):
    """타임라인 tl에서 시각 t의 (클립, 슬라이스). 끝난 줄은 대기로"""
    t0 = 0.0
    for clip, keys, stop in tl:
        if t < t0 + stop - 1e-6:
            return clip, [k for k, kt in keys if kt <= t - t0 + 1e-6][-1]
        t0 += stop
    return 'Idle', 'shotgun_0'


def mockup_gif(rows, clips=('Fire', 'PumpAction', 'Reload'), tail=0.4, scale=4, fps=30):
    """캐릭터 + 총을 줄마다 같은 시각으로 재생. rows = [(라벨, {슬라이스: 그림}, {클립: 키 덮어쓰기})].
    Fire 시작(0.5초)에 magic_vfx 섬광 · 펠릿 · 불티 — Muzzle은 게임처럼 고정 위치"""
    import random
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'vfx'))
    import magic_vfx as mv

    rows = [(label, guns, timeline(ov, clips, tail)) for label, guns, ov in rows]

    body = player_body()
    W, H = 150, 52
    bx, by = 12, 20
    gx, gy = bx + GUN_AT[0], by + GUN_AT[1]
    muzzle = (round(gx + MUZZLE[0]), round(gy + MUZZLE[1]))
    ends = mv.pellet_ends(muzzle, (W - muzzle[0]) / 80)
    fire_t = 0.5
    sparks = mv.spark_particles(random.Random(5), False, fire_t, muzzle)
    sp = mv.spark()
    big, dot = sp.crop((0, 0, 3, 3)), sp.crop((3, 0, 6, 3))
    small = bl.load_font(12, bold=False)
    flash = mv.muzzle_flash()

    total = max(sum(stop for _, _, stop in tl) for _, _, tl in rows)
    frames = []
    for n in range(round(total * fps)):
        t = n / fps
        k = math.floor((t - fire_t) * mv.FLASH_FPS + 1e-6) if hasattr(mv, 'FLASH_FPS') else round((t - fire_t) * fps)
        img = Image.new('RGB', (W * scale, H * scale * len(rows)), PANEL[:3])
        d = ImageDraw.Draw(img)
        for r, (label, guns, tl) in enumerate(rows):
            clip, name = at(tl, t)
            out = Image.new('RGB', (W, H), FLOOR[:3])
            mv.blend(out, body, bx, by)
            mv.blend(out, guns[name], gx, gy)
            if 0 <= k < len(mv.LINE_STAGES):
                line, smoke = mv.canvas(W, H), mv.canvas(W, H)
                mv.draw_pellets(line, smoke, muzzle, ends, k, False)
                mv.blend(out, smoke, 0, 0)
                mv.blend(out, line, 0, 0, additive=True)
            if 0 <= k < len(flash):
                mv.blend(out, flash[k], muzzle[0], muzzle[1] - 8)
            for p in sparks:
                age = t - p['t0']
                if not 0 <= age < p['life']:
                    continue
                u = age / p['life']
                alpha = 0.55 + 0.45 * math.cos(2 * math.pi * p['flick'] * u + p['phase'])
                if alpha < 0.25:
                    continue
                col = mv.tint(big if p['big'] else dot, mv.spark_color(u) + [alpha])
                x = p['x'] + mv.SPARK_NOISE * 16 * math.sin(age * 9 + p['phase'])
                mv.blend(out, col, round(x) - 1, round(p['y'] - p['rise'] * age) - 1, additive=True)
            img.paste(out.resize((W * scale, H * scale), Image.NEAREST), (0, r * H * scale))
            d.text((8, r * H * scale + 6), f'{label} · {clip} · {name}', font=small, fill=DIM)
        frames.append(img)
    return frames, [round(1000 * (i + 1) / fps, -1) - round(1000 * i / fps, -1) for i in range(len(frames))]   # GIF는 10ms 단위


def gun_check_gif(cols, clips, k=8, fps=30):
    """총만 k배로 나란히: 대기 0.3초 → clips → 대기 0.3초. cols = [(라벨, {슬라이스: 그림}, {클립: 키 덮어쓰기})]"""
    tls = [(label, guns, timeline(ov, clips, 0.3)) for label, guns, ov in cols]
    total = max(sum(stop for _, _, stop in tl) for _, _, tl in tls) - 0.2
    crop = (4, 2, 46, 26)
    cw, ch = (crop[2] - crop[0]) * k, (crop[3] - crop[1]) * k
    small = bl.load_font(14, bold=False)
    frames = []
    for i in range(round(total * fps)):
        t = 0.2 + i / fps
        img = Image.new('RGB', (len(tls) * (cw + 12) + 12, ch + 36), PANEL[:3])
        d = ImageDraw.Draw(img)
        for c, (label, guns, tl) in enumerate(tls):
            clip, name = at(tl, t)
            x = 12 + c * (cw + 12)
            img.paste(on(FLOOR, guns[name]).crop(crop).resize((cw, ch), Image.NEAREST).convert('RGB'), (x, 30))
            d.text((x, 8), f'{label} · {clip} · {name}', font=small, fill=DIM)
        frames.append(img)
    return frames, [round(1000 * (i + 1) / fps, -1) - round(1000 * i / fps, -1) for i in range(len(frames))]


# 레퍼런스 몸체 프레임(ref_body.png: 2열 × 7행, 라벨 순서)과 짝지을 우리 프레임
REF_TILES = ('f0', 'f3', 'f4', 'f5', 'f6', 'f7', 'f8', 'f15', 'f16', 'f18', 'f20', 'f21', 'f22', 'f23')
REF_PAIRS = [('f0', 'shotgun_0'), ('f4', 'shotgun_1'), ('f5', 'shotgun_16'), ('f5', 'shotgun_2'), ('f6', 'shotgun_3'),
             ('f6', 'shotgun_17'), ('f7', 'shotgun_8'), ('f8', 'shotgun_5'), ('f16', 'shotgun_6'), ('f18', 'shotgun_7'),
             ('f20', 'shotgun_18'), ('f21', 'shotgun_19'), ('f22', 'shotgun_9'), ('f23', 'shotgun_10')]


def vs_ref(ref_path, cols, k=8):
    """레퍼런스 프레임 | 우리 프레임(cols 열들)을 한 줄씩. cols = [(라벨, {슬라이스: 그림})]"""
    ref = Image.open(ref_path).convert('RGB')
    tw, th = ref.width // 2, ref.height // 7
    crop = (4, 2, 46, 26)
    cw, ch = (crop[2] - crop[0]) * k, (crop[3] - crop[1]) * k
    rw, rh = tw * ch // th, ch
    small = bl.load_font(16, bold=False)
    out = Image.new('RGB', (12 + rw + len(cols) * (cw + 12) + 12, 50 + len(REF_PAIRS) * (ch + 34)), PANEL[:3])
    d = ImageDraw.Draw(out)
    d.text((12, 12), '레퍼런스 몸체 액션 vs 우리 프레임 (총만 8배)', font=bl.load_font(22), fill=INK)
    for r, (tile, name) in enumerate(REF_PAIRS):
        i = REF_TILES.index(tile)
        y = 50 + r * (ch + 34)
        t = ref.crop(((i % 2) * tw, (i // 2) * th, (i % 2 + 1) * tw, (i // 2 + 1) * th)).resize((rw, rh), Image.NEAREST)
        out.paste(t, (12, y + 24))
        d.text((12, y + 2), f'레퍼런스 {tile}', font=small, fill=DIM)
        for c, (label, guns) in enumerate(cols):
            x = 24 + rw + c * (cw + 12)
            out.paste(on(FLOOR, guns[name]).crop(crop).resize((cw, ch), Image.NEAREST).convert('RGB'), (x, y + 24))
            d.text((x, y + 2), f'{label} · {name} · {FRAMES[name]["label"]}', font=small, fill=DIM)
    return out


def pump_check_gif(k=8, fps=30):
    """PumpAction 구간(대기 0 → .anim 키)만 총 8배: 지금 총 · 새 총 포엔드 수정 전 · 수정 후"""
    prev = dict(DESIGNS['v03u'], fill='23.', rows=[r for r in DESIGNS['v03u']['rows']])
    prev['rows'][4] = '.2bgebc.122222323eaaaaae22.....'   # 포엔드 색 수정 전 (사용자 수정본 그대로)
    prev['rows'][5] = '.3cbcc..333233...bbcbbcc3......'
    DESIGNS['_prev'] = prev
    cols = [('지금 총', old_frame), ('새 총 · 수정 전', lambda n: draw_frame(n, '_prev', 'silver')),
            ('새 총 · 수정 후', lambda n: draw_frame(n, 'v03u', 'silver'))]
    keys, stop = clip_keys('PumpAction')
    timeline = [('shotgun_0', 0.0, 0.3)] + [(n, 0.3 + t, 0) for n, t in keys]
    total = 0.3 + stop
    crop = (4, 6, 44, 26)
    cw, ch = (crop[2] - crop[0]) * k, (crop[3] - crop[1]) * k
    small = bl.load_font(14, bold=False)
    cache = {}
    frames = []
    for i in range(round(total * fps)):
        t = i / fps
        name = [n for n, t0, _ in timeline if t0 <= t + 1e-6][-1]
        img = Image.new('RGB', (len(cols) * (cw + 12) + 12, ch + 36), PANEL[:3])
        d = ImageDraw.Draw(img)
        for c, (label, fn) in enumerate(cols):
            fr = cache.setdefault((c, name), fn(name))
            x = 12 + c * (cw + 12)
            img.paste(on(FLOOR, fr).crop(crop).resize((cw, ch), Image.NEAREST).convert('RGB'), (x, 30))
            d.text((x, 8), f'{label} · {name}', font=small, fill=DIM)
        frames.append(img)
    del DESIGNS['_prev']
    return frames, [round(1000 * (i + 1) / fps, -1) - round(1000 * i / fps, -1) for i in range(len(frames))]


if __name__ == '__main__':
    props = paths.repo('Docs', 'drafts', 'props')
    os.makedirs(props, exist_ok=True)
    if '--sheet' in sys.argv or '--final' in sys.argv:
        sh = sheet('v03u', 'silver')
        sh.save(os.path.join(props, 'shotgun-rework-sheet.png'))
        sheet_preview(sh).convert('RGB').save(os.path.join(props, 'shotgun-rework-sheet-preview.png'))
        print('sheet', sh.size)
    old = {n: old_frame(n) for n in FRAMES}
    new = {n: draw_frame(n, 'v03u', 'silver') for n in FRAMES}
    v04 = {'Fire': fire_clip()}
    bounce = {c: fire_clip(keys) for c, keys in BOUNCE.items()}

    def save_gif(name, frames, durations):
        gif = os.path.join(props, name)
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True)
        print(name, len(frames), 'frames', sum(durations) / 1000, 's', os.path.getsize(gif), 'bytes')

    if '--gif' in sys.argv:
        save_gif('shotgun-rework-mockup.gif', *mockup_gif([('지금', old, {}), ('새 총', new, v04)]))
    if '--bounce' in sys.argv:
        act = {n: frame_img(n, 'v03u', 'silver', action='body') for n in FRAMES}
        mzl = {n: frame_img(n, 'v03u', 'silver', action='muzzle') for n in FRAMES}
        rows = [('지금', old, {}), ('튀는 버전 (강체)', new, bounce), ('총구 액션', mzl, bounce)]
        ref_body = os.environ.get('SHOTGUN_REF_BODY')
        if ref_body:
            vs_ref(ref_body, [('튀는 버전 (강체)', new), ('총구 액션', mzl)]).save(os.path.join(props, 'shotgun-bounce-vs-ref.png'))
        save_gif('shotgun-bounce-mockup.gif', *mockup_gif(rows, clips=('Fire', 'PumpAction'), tail=0.5))
        save_gif('shotgun-bounce-check.gif', *gun_check_gif(rows, ('Fire', 'PumpAction')))
    if '--pump-check' in sys.argv:
        frames, durations = pump_check_gif()
        gif = os.path.join(props, 'shotgun-pump-check.gif')
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True)
        print('pump-check', sum(durations) / 1000, 's', os.path.getsize(gif), 'bytes')
    if len(sys.argv) == 1:
        draft(os.path.join(props, 'shotgun-rework-draft.png'))
