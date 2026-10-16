"""Thrower 고블린 스프라이트 — NormalEnemy의 Orc(고블린) 시트에서 도끼를 폭탄으로 바꾸고 투척병 장식을 입힌다.

원본: Assets/04.Sprites/Enemy/Orc with shadows/Orc.png
      800×600, 100×100 칸, 행 = Idle 6 · Walk 8 · Attack1 6 · Attack2 6 · Hurt 4 · Death 4 (34장)

방식
  1. 도끼 찾기: 자루 색(붉은 갈색)에서 시작해 자루 · 날 · 베기 궤적 색으로 이어진 덩어리만 도끼로 본다.
     날 색은 어깨 보호대와 같아서 색만으로 고르지 않는다. 도끼에만 닿은 검은 외곽선도 함께 지운다.
  2. 손: 자루에 닿은 고블린 피부 픽셀. 손 폭탄(5x5)을 여기에 붙인다.
  3. 공격 행은 오버핸드 투척으로 쓴다: 들어 올린 프레임까지 폭탄을 들고, 놓는 프레임부터는 빈손.
  4. 장식: 눈(붉은 픽셀 2개)을 기준점으로 머리띠 · 가슴 탄띠를 붙인다.
  5. Hurt F1 · F2는 F0에 틴트를 씌운 것이라 F0에서 찾은 도끼 위치로 지운다.

사용법
  python goblin_thrower.py            # 시안: Docs/drafts/props/goblin-thrower-draft.png (+ .gif)
  python goblin_thrower.py mask       # 도끼 판별 확인 이미지 (스크래치용)
"""

import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..'))
import paths  # noqa: E402

SRC = paths.repo('Assets', '04.Sprites', 'Enemy', 'Orc with shadows', 'Orc.png')
CELL = 100
ROWS = [('Idle', 6), ('Walk', 8), ('Attack1', 6), ('Attack2', 6), ('Hurt', 4), ('Death', 4)]

HANDLE = {(76, 48, 41), (94, 67, 61)}
BLADE = {(111, 130, 141), (155, 173, 183), (81, 109, 125), (213, 202, 197), (244, 237, 234),
         (183, 165, 155), (215, 213,210)}
AXE = HANDLE | BLADE
BLACK = (0, 0, 0)
N8 = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
N4 = [(1, 0), (-1, 0), (0, 1), (0, -1)]


def cells():
    im = Image.open(SRC).convert('RGBA')
    return im, [[im.crop((c * CELL, r * CELL, c * CELL + CELL, r * CELL + CELL)) for c in range(n)]
                for r, (_, n) in enumerate(ROWS)]


def find_axe(cell):
    """도끼 픽셀 · 도끼에만 닿은 외곽선 · 손 위치"""
    px = cell.load()
    W, H = cell.size
    rgb = lambda x, y: px[x, y][:3] if px[x, y][3] == 255 else None
    seeds = [(x, y) for y in range(H) for x in range(W) if rgb(x, y) in HANDLE]
    axe, stack = set(seeds), list(seeds)
    while stack:
        x, y = stack.pop()
        for dx, dy in N8:
            q = (x + dx, y + dy)
            if 0 <= q[0] < W and 0 <= q[1] < H and q not in axe and rgb(*q) in AXE:
                axe.add(q)
                stack.append(q)
    figure = lambda q: rgb(*q) not in (None, BLACK) and q not in axe
    lines = set()
    for y in range(H):
        for x in range(W):
            if rgb(x, y) != BLACK:
                continue
            nb = [(x + dx, y + dy) for dx, dy in N8 if 0 <= x + dx < W and 0 <= y + dy < H]
            if any(q in axe for q in nb) and not any(figure(q) for q in nb):
                lines.add((x, y))
    handle = [p for p in axe if rgb(*p) in HANDLE]
    grip, best = None, 3
    for y in range(H):
        for x in range(W):
            if not figure((x, y)):
                continue
            d = min(max(abs(x - a), abs(y - b)) for a, b in handle) if handle else 99
            if d < best:
                grip, best = (x, y), d
    return axe, lines, grip


def mask_preview(path):
    im, grid = cells()
    Z = 4
    out = Image.new('RGBA', (8 * 48 * Z, 6 * 48 * Z), (40, 40, 48, 255))
    for r, row in enumerate(grid):
        for c, cell in enumerate(row):
            axe, lines, grip = find_axe(cell)
            v = cell.copy()
            for p in axe:
                v.putpixel(p, (255, 0, 255, 255))
            for p in lines:
                v.putpixel(p, (0, 255, 255, 255))
            if grip:
                v.putpixel(grip, (255, 255, 0, 255))
            v = v.crop((26, 26, 74, 74))
            out.alpha_composite(v.resize((48 * Z, 48 * Z), Image.NEAREST), (c * 48 * Z, r * 48 * Z))
    out.save(path)


# ── 장식 · 폭탄 ─────────────────────────────────────────────────────
OUT = (0, 0, 0)
EYE = (123, 8, 8)
SHADOW = (0, 0, 0, 100)
BOMB = [(58, 40, 44), (84, 58, 60), (116, 82, 80), (160, 120, 112)]   # enemy_bomb.py 몸통 계열을 바닥 위에서 보이게 한 단계 밝게
BAND = [(84, 24, 24), (122, 42, 38), (164, 64, 54)]              # 고블린 캠프 군기 #7a2a26 계열
STRAP = [(120, 86, 52), (168, 128, 82)]
PIP = [(58, 40, 44), (160, 120, 112)]
SPARK = [(255, 240, 160), (255, 195, 25), (229, 138, 0)]

# 손 폭탄 5x5 (o = 외곽선, 숫자 = BOMB 단계) + 오른쪽 위 불씨 1px
HAND_BOMB = [
    ".ooo*",
    "o321o",
    "o210o",
    "o100o",
    ".ooo.",
]


def eye_anchor(cell):
    """왼쪽 눈 위치. 눈을 감은 프레임(피격 · 사망)은 머리 윗선 외곽선에서 (+2, +5)"""
    px = cell.load()
    eyes = [(x, y) for y in range(CELL) for x in range(CELL) if px[x, y][:3] == EYE and px[x, y][3] == 255]
    if eyes:
        return min(eyes)
    for y in range(CELL):
        run = [x for x in range(CELL) if px[x, y][3] == 255 and px[x, y][:3] == OUT]
        if len(run) >= 4:
            return run[0] + 2, y + 5
    return None


def is_figure(px, x, y):
    p = px[x, y]
    return p[3] == 255 and p[:3] != OUT


def remove_axe(cell, axe, lines):
    px = cell.load()
    gone = axe | lines
    for x, y in gone:
        px[x, y] = (0, 0, 0, 0)
    # 그림자 위에 있던 도끼 자리는 그림자로 메운다
    for x, y in gone:
        if sum(px[x + dx, y + dy] == SHADOW for dx, dy in N4) >= 2:
            px[x, y] = SHADOW
    # 베기 궤적에서 떨어져 남은 작은 점 정리
    seen = set()
    for y in range(CELL):
        for x in range(CELL):
            if (x, y) in seen or px[x, y][3] != 255:
                continue
            comp, stack = {(x, y)}, [(x, y)]
            while stack:
                a, b = stack.pop()
                for dx, dy in N8:
                    q = (a + dx, b + dy)
                    if 0 <= q[0] < CELL and 0 <= q[1] < CELL and q not in comp and px[q][3] == 255:
                        comp.add(q)
                        stack.append(q)
            seen |= comp
            if len(comp) <= 6:
                for q in comp:
                    px[q] = (0, 0, 0, 0)
    return gone


def paint(px, x, y, col, tint):
    px[x, y] = tint(col) + (255,)


def draw_bomb(cell, cx, cy, spark, tint):
    """손 폭탄을 (cx, cy) 중심으로. 외곽선은 빈 곳에만 칠해 손 · 몸 위를 덮지 않는다"""
    px = cell.load()
    for j, row in enumerate(HAND_BOMB):
        for i, ch in enumerate(row):
            x, y = cx - 2 + i, cy - 2 + j
            if ch == '.':
                continue
            if ch == 'o':
                if px[x, y][3] != 255:
                    paint(px, x, y, OUT, tint)
            elif ch == '*':
                paint(px, x, y, SPARK[spark], tint)
            else:
                paint(px, x, y, BOMB[int(ch)], tint)


def draw_band(cell, ex, ey, tint):
    """머리띠: 눈 두 줄 위 이마 한 줄 + 뒤통수로 늘어진 매듭 끈"""
    px = cell.load()
    y = ey - 2
    xs = [x for x in range(ex - 6, ex + 8) if is_figure(px, x, y)]
    if not xs:
        return
    for x in xs:
        paint(px, x, y, BAND[2] if x <= xs[0] + 1 else BAND[1], tint)
        if is_figure(px, x, y + 1) and x in (xs[0], xs[-1]):
            paint(px, x, y + 1, BAND[0], tint)
    # 매듭 끈 (뒤통수 왼쪽 바깥으로 두 갈래)
    for x, yy, col in ((xs[0] - 2, y, BAND[1]), (xs[0] - 3, y + 1, BAND[0]), (xs[0] - 2, y + 1, BAND[0])):
        if px[x, yy][3] != 255 or px[x, yy][:3] == OUT:
            paint(px, x, yy, col, tint)
    for x, yy in ((xs[0] - 3, y - 1), (xs[0] - 2, y - 1), (xs[0] - 4, y + 1), (xs[0] - 3, y + 2), (xs[0] - 2, y + 2)):
        if px[x, yy][3] != 255:
            paint(px, x, yy, OUT, tint)


def draw_strap(cell, ex, ey, tint):
    """가슴 탄띠: 오른쪽 어깨 → 왼쪽 허리 사선, 폭탄 두 알"""
    px = cell.load()
    pts = [(ex + 4 - k, ey + 3 + k) for k in range(5)]
    for k, (x, y) in enumerate(pts):
        if not is_figure(px, x, y):
            continue
        if k in (1, 3):
            paint(px, x, y, PIP[0], tint)
            if is_figure(px, x, y - 1):
                paint(px, x, y - 1, PIP[1], tint)
        else:
            paint(px, x, y, STRAP[1], tint)


def centroid(points):
    xs, ys = zip(*points)
    return sum(xs) / len(xs), sum(ys) / len(ys)


def frame_plan(row, col):
    """'hand' 손에 폭탄 · 'fly' 막 놓은 폭탄 · 'drop' 땅에 떨어진 폭탄 · None 빈손"""
    name = ROWS[row][0]
    if name in ('Attack1', 'Attack2'):
        return 'hand' if col <= 2 else 'fly' if col == 3 else None
    if name == 'Death':
        return 'hand' if col <= 1 else 'drop'
    return 'hand'


def fit_tint(src, dst):
    """F0 → F1 같은 자리 색 쌍으로 채널별 a·c + b 근사"""
    import numpy as np
    a, b = src.load(), dst.load()
    pairs = [(a[x, y][:3], b[x, y][:3]) for y in range(CELL) for x in range(CELL)
             if a[x, y][3] == 255 and b[x, y][3] == 255 and a[x, y][:3] != OUT]
    s = np.array([p[0] for p in pairs], float)
    d = np.array([p[1] for p in pairs], float)
    coef = [np.polyfit(s[:, k], d[:, k], 1) for k in range(3)]
    return lambda c: c if c == OUT else tuple(int(max(0, min(255, round(coef[k][0] * c[k] + coef[k][1])))) for k in range(3))


def build():
    src, grid = cells()
    sheet = Image.new('RGBA', src.size, (0, 0, 0, 0))
    hurt0 = None
    for r, row in enumerate(grid):
        for c, cell in enumerate(row):
            name = ROWS[r][0]
            out = cell.copy()
            tint = lambda col: col
            if name == 'Hurt' and c in (1, 2):
                axe, lines, grip, anchor, axe_c = hurt0
                tint = fit_tint(grid[r][0], cell)
                gone = remove_axe(out, axe, lines)
                px = out.load()
                red = (237, 13, 13)
                for x, y in [(x, y) for y in range(CELL) for x in range(CELL) if px[x, y][:3] == red]:
                    nb = [(x + dx, y + dy) for dx, dy in N8]
                    if any(q in gone for q in nb) and not any(px[q][3] == 255 and px[q][:3] not in (red, OUT) for q in nb):
                        px[x, y] = (0, 0, 0, 0)
            else:
                axe, lines, grip = find_axe(cell)
                anchor = eye_anchor(cell)
                axe_c = centroid(axe) if axe else None
                if name == 'Hurt' and c == 0:
                    hurt0 = (axe, lines, grip, anchor, axe_c)
                remove_axe(out, axe, lines)
            if anchor and not (name == 'Death' and c >= 2):
                draw_band(out, *anchor, tint)
                draw_strap(out, *anchor, tint)
            plan = frame_plan(r, c)
            spark = (r + c) % 2
            if plan == 'hand' and grip and axe_c:
                dx, dy = axe_c[0] - grip[0], axe_c[1] - grip[1]
                n = max(1e-6, (dx * dx + dy * dy) ** 0.5)
                bx, by = round(grip[0] + dx / n * 2), round(grip[1] + dy / n * 2) - 1
                draw_bomb(out, bx, by, spark, tint)
                out.putpixel(grip, cell.getpixel(grip))          # 손이 폭탄을 쥔 것처럼 손 픽셀을 위에
            elif plan == 'fly' and anchor:
                draw_bomb(out, anchor[0] + 9, anchor[1] - 4, spark, tint)
            elif plan == 'drop' and axe_c:
                draw_bomb(out, round(axe_c[0]), round(axe_c[1]) + 2, spark, tint)
            if name == 'Hurt' and c == 1:
                px = out.load()        # 피격 F1의 붉은 테두리를 새 실루엣 바깥에 다시 두른다
                for x, y in [(x, y) for y in range(1, CELL - 1) for x in range(1, CELL - 1)
                             if px[x, y][3] != 255 and any(px[x + dx, y + dy][:3] == OUT and px[x + dx, y + dy][3] == 255 for dx, dy in N4)]:
                    px[x, y] = (237, 13, 13, 255)
            sheet.alpha_composite(out, (c * CELL, r * CELL))
    return sheet


def crop_cell(sheet, r, c, box=(26, 26, 74, 74)):
    return sheet.crop((c * CELL + box[0], r * CELL + box[1], c * CELL + box[2], r * CELL + box[3]))


def draft(path, version='v01'):
    import draftlib
    src = Image.open(SRC).convert('RGBA')
    sheet = build()
    try:
        font = ImageFont.truetype('malgun.ttf', 15)
    except OSError:
        font = ImageFont.load_default()
    Z, cw = 4, 48
    pad = 24
    W = pad * 2 + 8 * cw * Z
    img = Image.new('RGBA', (W, 3000), (58, 58, 68, 255))
    dr = ImageDraw.Draw(img)
    dr.text((pad, 8), f'Thrower 고블린 시안 {version}  (Orc 시트 재활용 · 도끼 → 폭탄 · 머리띠 · 가슴 탄띠, 4배)', fill=(240, 240, 240), font=font)
    y = 40
    labels = {'Idle': '대기', 'Walk': '걷기', 'Attack1': '공격1 → 투척 (0~2 들기, 3 놓기, 4~5 빈손)',
              'Attack2': '공격2 → 투척 (같은 구성)', 'Hurt': '피격', 'Death': '사망 (2~3 떨어진 폭탄)'}
    for r, (name, n) in enumerate(ROWS):
        dr.text((pad, y), f'{name} · {labels[name]}', fill=(220, 220, 220), font=font)
        y += 22
        for c in range(n):
            bg = Image.new('RGBA', (cw, cw), (34, 36, 42, 255))
            bg.alpha_composite(crop_cell(sheet, r, c))
            img.alpha_composite(bg.resize((cw * Z, cw * Z), Image.NEAREST), (pad + c * cw * Z, y))
        y += cw * Z + 10
    # 게임 배율 비교: 원본 NormalEnemy vs Thrower
    dr.text((pad, y), '게임 배율 비교 (4배): 왼쪽 NormalEnemy 원본 · 오른쪽 Thrower', fill=(220, 220, 220), font=font)
    y += 22
    sc = draftlib.dark_room(128, 48, wall_rows=0)
    for i, (sheet_im, x0) in enumerate(((src, 8), (sheet, 64))):
        for k, (r, c) in enumerate(((0, 0), (2, 2))):
            sc.alpha_composite(crop_cell(sheet_im, r, c, (30, 26, 70, 66)), (x0 + k * 26 - 6, 2))
    img.alpha_composite(sc.resize((128 * Z, 48 * Z), Image.NEAREST), (pad, y))
    y += 48 * Z + pad
    img = img.crop((0, 0, W, y))
    img.save(path)
    # 움직임 확인 GIF: 대기 → 걷기 → 공격1
    frames = []
    for r, n, dur in ((0, 6, 1), (1, 8, 1), (2, 6, 1)):
        for c in range(n):
            bg = draftlib.dark_room(48, 48, wall_rows=0)
            bg.alpha_composite(crop_cell(sheet, r, c))
            frames.append(bg.resize((48 * 6, 48 * 6), Image.NEAREST))
    frames[0].save(os.path.splitext(path)[0] + '.gif', save_all=True, append_images=frames[1:], duration=110, loop=0)


DST = os.path.join(os.path.dirname(SRC), 'Goblin Thrower.png')
NORMAL_ANIM = paths.repo('Assets', '05.Animations', 'Enemy', 'Normal')
THROWER_ANIM = paths.repo('Assets', '05.Animations', 'Enemy', 'Thrower')
CLIPS = ['Idle', 'Move', 'Attack1', 'Attack2', 'Hit', 'Death']


def guid_of(path):
    with open(path + '.meta', encoding='utf-8') as fh:
        return re.search(r'^guid: ([0-9a-f]{32})', fh.read(), re.M).group(1)


def write_import():
    """시트 PNG + meta(Orc 슬라이스 · 내부 ID 그대로, 새 guid) 쓰기,
    Thrower 클립을 Normal 클립 구성으로 바꿔 쓰기(클립 guid · 이름 유지, Orc 참조 → 새 시트, 근접 타격 이벤트 제거)"""
    from elite_sniper import new_guid
    png = paths.out(DST)
    guid = guid_of(DST) if os.path.exists(DST + '.meta') else new_guid()   # 다시 만들 때 guid 재사용
    with open(SRC + '.meta', encoding='utf-8') as fh:
        meta = fh.read()
    meta = re.sub(r'^guid: [0-9a-f]{32}', 'guid: ' + guid, meta, count=1, flags=re.M)
    meta = meta.replace('Orc_', 'Goblin Thrower_')
    build().save(png)
    with open(png + '.meta', 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(meta)
    print('wrote', png, 'guid', guid)
    orc = guid_of(SRC)
    for name in CLIPS:
        with open(os.path.join(NORMAL_ANIM, name + '.anim'), encoding='utf-8') as fh:
            clip = fh.read()
        clip = clip.replace('guid: ' + orc, 'guid: ' + guid)
        clip = re.sub(r'  m_Events:\n(?:  [ -].*\n)+', '  m_Events: []\n', clip)
        dst = paths.out(os.path.join(THROWER_ANIM, name + '.anim'))
        with open(dst, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(clip)
        print('wrote', dst)


if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'goblin-thrower-draft.png')
    if arg == 'import':
        write_import()
        sys.exit()
    if arg == 'mask':
        mask_preview(sys.argv[2])
        print('mask', sys.argv[2])
        sys.exit()
    draft(arg, *(sys.argv[2:3]))
    print('draft', arg)
