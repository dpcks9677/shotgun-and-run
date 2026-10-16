"""홀 광원 배치 (Docs/hall-lighting-plan.md 4장 규칙) — 테마 계산 · 자리 생성 · 검사 · 시안

python lights.py draft [출력.png] [버전]   시안 (기본 Docs/drafts/props/hall-lights-draft.png)
python lights.py json  [출력.json]         배치 결과만

규칙 요약
  - 홀 칸의 테마 = 걸어서 가장 가까운 존(스폰 · 탈출구 · TP · J15는 중립) — 바닥 전체 다중 출발 BFS
  - 벽걸이: 북쪽 벽 정면(바닥 위가 벽인 칸) 구간마다, 8u 미만 구간은 건너뜀, 횃불 · 랜턴 12u · 촛대 10u,
    구간 끝에서 2u 안쪽, 존 문 옆이면 그 끝부터, 구간 테마 = 구간 가운데 칸, 홀마다 최소 2개
  - 바닥 광원: 랜드마크만 (H6 가운데 방 + 가장 어두운 곳 몇 곳), 벽에 붙임, 좁은 길 · 상자 지점 · 통로 입구 · 물 피함
"""
import json, os, re, sys, hashlib
from collections import deque, defaultdict
from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..'))
sys.path.insert(0, os.path.join(HERE, '..', 'spritegen'))
import layout_edit as L
import region2
from parse_layout import parse

ASSET = os.path.join(ROOT, 'Assets', '_Sandbox', 'Yechann', 'Layouts', 'Stage1Layout.asset')
TILE = os.path.join(ROOT, 'Assets', '04.Sprites', 'Tile')
DRAFT = os.path.join(ROOT, 'Docs', 'drafts', 'props', 'hall-lights-draft.png')

SKIP_RUN = 8            # 이보다 짧은 벽 구간은 건너뜀 (결정 5: 많이 어둡게)
MIN_RUN = 4             # 홀마다 최소 2개를 채울 때 쓸 수 있는 가장 짧은 구간
EDGE = 2                # 구간 끝에서 안쪽으로
MIN_PER_HALL = 2
FLOOR_MAX = 10          # 바닥 광원 (랜드마크) 최대
FLOOR_DARK = 11         # 이보다 멀리 빛이 없는 곳만 바닥 광원 후보
CHEST_MARGIN = 2        # 상자 지점(3×3 가운데)에서 체비쇼프 거리
H6_CENTER = (231, 282, 28, 70)   # H6 가운데 방이 들어 있는 안뜰 덩어리 (level-design 1.6)

# 종류: (시트 png 상대 경로, 스프라이트 이름, 칸 높이, 빛 반경 u, 세기)
KIND = {
    'Torch_Wall':        ('ZoneProps/ManaVein/ManaVein.png', 'Torch_Wall_f0', 1, 6.0, 1.15),
    'Brass_Lantern':     ('ZoneProps/DwarfWorkshop/DwarfWorkshop.png', 'Brass_Lantern_f0', 2, 6.0, 1.05),
    'Hanging_Lantern':   ('ZoneProps/Machinery/Machinery.png', 'Hanging_Lantern_f0', 2, 6.0, 1.05),
    'Sconce_single':     ('ZoneProps/LabProps/LabProps.png', 'Sconce_single_f0', 1, 4.5, 1.05),
    'Sconce_double':     ('ZoneProps/LabProps/LabProps.png', 'Sconce_double_f0', 1, 4.5, 1.05),
    'Necro_SkullSconce': ('ZoneProps/Alchemy/Alchemy.png', 'Necro_SkullSconce_f0', 1, 4.5, 1.05),
    'Brazier_Iron':      ('ZoneProps/ManaVein/ManaVein.png', 'Brazier_Iron_f0', 1, 9.0, 1.25),
    'Brazier_Stone':     ('ZoneProps/ManaVein/ManaVein.png', 'Brazier_Stone_f0', 1, 9.0, 1.25),
    'Brazier_Charcoal':  ('ZoneProps/Alchemy/Alchemy.png', 'Brazier_Charcoal_f0', 1, 9.0, 1.25),
    'Candelabra_brass':  ('ZoneProps/LabProps/LabProps.png', 'Candelabra_brass_f0', 2, 4.5, 1.05),
    'Candelabra_iron3':  ('ZoneProps/LabProps/LabProps.png', 'Candelabra_iron3_f0', 2, 4.5, 1.05),
    'Candles_A':         ('ZoneProps/Butchery/Butchery.png', 'Candles_A_0', 1, 4.5, 1.05),
    'Candles_B':         ('ZoneProps/Butchery/Butchery.png', 'Candles_B_0', 1, 4.5, 1.05),
    'Candles_skull':     ('ZoneProps/LabProps/LabProps.png', 'Candles_skull_f0', 1, 4.5, 1.05),
    # 표에 없음 — 결정 2로 AnimatedTile을 새로 만들 DungeonDark 기둥 화로 (빛은 화로와 같게 가정)
    'Brazier_Pillar':    ('Dungeon/DungeonDark.png', 'FDR_DungeonDark_565', 2, 9.0, 1.25),
}

# 테마: 벽걸이 후보 · 간격 · 바닥 후보 (3장 표)
THEME = {
    'Z1':  (['Torch_Wall'], 12, ['Brazier_Iron']),
    'Z2':  (['Brass_Lantern'], 12, ['Brazier_Iron']),
    'Z3':  (['Torch_Wall'], 12, []),
    'Z4':  (['Hanging_Lantern'], 12, ['Brazier_Iron']),
    'Z5':  (['Torch_Wall'], 12, ['Brazier_Stone', 'Brazier_Iron']),
    'Z6':  (['Torch_Wall'], 12, ['Candles_A', 'Candles_B']),
    'Z7':  (['Sconce_single', 'Sconce_double'], 10, ['Candelabra_brass']),
    'Z8':  (['Sconce_single', 'Sconce_single', 'Sconce_single', 'Necro_SkullSconce'], 10, ['Candelabra_iron3', 'Brazier_Charcoal']),
    'Z9':  (['Necro_SkullSconce'], 10, ['Candles_A', 'Candles_B']),
    'Z10': (['Sconce_single', 'Sconce_double'], 10, ['Candelabra_iron3', 'Candles_skull']),
    'N-D': (['Torch_Wall'], 12, ['Brazier_Pillar']),
    'N-C': (['Necro_SkullSconce'], 10, ['Candles_A', 'Candles_B']),
}
COLOR = {'Z1': (240, 120, 60), 'Z2': (230, 190, 70), 'Z3': (110, 170, 120), 'Z4': (200, 120, 80), 'Z5': (190, 130, 240),
         'Z6': (220, 70, 70), 'Z7': (240, 150, 200), 'Z8': (90, 200, 230), 'Z9': (120, 220, 120), 'Z10': (230, 230, 230),
         'N-D': (150, 150, 150), 'N-C': (110, 140, 110)}
NEUTRAL = re.compile(r'^(E\d|Exit-|TP-|J15$|Boss$)')


def h(*a):
    return int(hashlib.md5(repr(a).encode()).hexdigest()[:8], 16)


def hall_of(name):
    m = re.match(r'^H(\d+|[A-G])', name)
    return 'H' + m.group(1) if m else None


def load():
    rooms, _, _ = parse(open(ASSET, encoding='utf-8').read())
    cat_rooms = {r['Name'] for r in rooms if r.get('Theme') == 1}
    owner = L.floor_of(ASSET)
    water = set().union(*region2.WATER.values()) | set().union(*region2.GRATE.values())
    chest = []
    p = os.path.join(os.environ.get('CHEST_POINTS', ''))
    if p and os.path.exists(p):
        txt = open(p, encoding='utf-8').read().replace('\\r\\n', '\n')
        for line in txt.splitlines():
            if ':' in line:
                for c in line.split(':', 1)[1].split():
                    x, y = map(int, c.split(','))
                    # 크기를 몰라도 되게 2×1(오른쪽) · 세로 1×2(위)를 모두 덮는 칸으로 셈
                    chest += [(x, y), (x + 1, y), (x, y + 1)]
    return owner, cat_rooms, water, chest


def source_label(name):
    m = re.match(r'^(Z\d+)(-G)?$', name)
    if m: return m.group(1)
    if NEUTRAL.match(name): return 'N'
    return None


def nearest_zone(owner):
    """바닥 전체 다중 출발 BFS — 칸마다 (가장 가까운 출발 라벨, 거리)"""
    lab, dist = {}, {}
    q = deque()
    for c, n in owner.items():
        s = source_label(n)
        if s: lab[c] = s; dist[c] = 0; q.append(c)
    while q:
        c = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (c[0] + dx, c[1] + dy)
            if n in owner and n not in dist:
                dist[n] = dist[c] + 1; lab[n] = lab[c]; q.append(n)
    return lab, dist


def theme_key(label, is_cat):
    return label if label != 'N' else ('N-C' if is_cat else 'N-D')


def wall_runs(owner, hall):
    """홀의 북쪽 벽 정면 구간 [(y, x0, x1)] — 바닥 칸 (x, y) 위 (x, y+1)이 바닥이 아님"""
    byy = defaultdict(list)
    for (x, y), n in owner.items():
        if hall_of(n) == hall and (x, y + 1) not in owner:
            byy[y].append(x)
    runs = []
    for y, xs in byy.items():
        xs.sort(); s = p = xs[0]
        for x in xs[1:]:
            if x != p + 1: runs.append((y, s, p)); s = x
            p = x
        runs.append((y, s, p))
    return sorted(runs, key=lambda r: (-r[0], r[1]))


def door_zone(owner, c):
    """칸 c 바로 위가 통로면 그 통로가 잇는 존 이름들"""
    n = owner.get((c[0], c[1] + 1), '')
    return set(t for t in n[1:].split('-') if re.fullmatch(r'Z\d+', t)) if n.startswith('~') else set()


def plan():
    owner, cat_rooms, water, chest = load()
    lab, dist = nearest_zone(owner)
    for c in center_room(owner):     # 결정 4: H6 가운데 방은 통째로 중립
        lab[c] = 'N'
    halls = sorted({hall_of(n) for n in owner.values() if hall_of(n)}, key=lambda s: (len(s), s))
    is_cat = lambda c: owner.get(c) in cat_rooms
    wall, floor_l = [], []

    for hall in halls:
        runs = wall_runs(owner, hall)
        placed = []
        for (y, s, e) in runs:
            ln = e - s + 1
            if ln < SKIP_RUN: continue
            placed += place_run(owner, lab, is_cat, hall, y, s, e)
        if len(placed) < MIN_PER_HALL:
            used = {(p['y'], p['run'][0]) for p in placed}
            spare = [r for r in runs if MIN_RUN <= r[2] - r[1] + 1 < SKIP_RUN and (r[0], r[1]) not in used]
            spare.sort(key=lambda r: dist.get((((r[1] + r[2]) // 2), r[0]), 999))
            for (y, s, e) in spare[:MIN_PER_HALL - len(placed)]:
                placed += place_run(owner, lab, is_cat, hall, y, s, e, force_one=True)
        wall += placed

    # 바닥 광원 — H6 가운데 방 (결정 4: 중립 기둥 화로) + 가장 어두운 곳
    lights = [(p['x'] + 0.5, p['y'] + 2.5, KIND[p['kind']][3]) for p in wall]
    corridor = {c for c, n in owner.items() if n.startswith('~') or not hall_of(n)}
    near_cor = {(x + dx, y + dy) for (x, y) in corridor for dx in range(-2, 3) for dy in range(-2, 3)}

    def span(c, dx, dy):
        n = 1
        for sgn in (1, -1):
            x, y = c
            while (x + sgn * dx, y + sgn * dy) in owner:
                x += sgn * dx; y += sgn * dy; n += 1
                if n > 40: break
        return n

    def ok_floor(c):
        if c not in owner or not hall_of(owner[c]) or c in water or c in near_cor: return False
        if (c[0], c[1] + 1) not in owner: return False            # 벽 정면 바로 앞 칸은 벽걸이 자리
        if all(n in owner for n in ((c[0] - 1, c[1]), (c[0] + 1, c[1]), (c[0], c[1] - 1))): return False   # 벽에 붙임
        if any(max(abs(c[0] - a), abs(c[1] - b)) <= CHEST_MARGIN for a, b in chest): return False
        if span(c, 1, 0) < 7 or span(c, 0, 1) < 7: return False   # 4u 길 · 좁은 틈 막지 않게
        if any(abs(c[0] - f['x']) + abs(c[1] - f['y']) < 4 for f in floor_l): return False
        return True

    def lit(c):
        return any((c[0] + 0.5 - lx) ** 2 + (c[1] + 0.5 - ly) ** 2 <= r * r for lx, ly, r in lights)

    def chest_free(c):
        return not any(max(abs(c[0] - a), abs(c[1] - b)) <= CHEST_MARGIN for a, b in chest)

    # H6 가운데 방: 몸통(폭 10칸 이상인 줄) 아래 줄 양쪽 모서리 안쪽에 기둥 화로 한 쌍
    center = center_room(owner)
    rows = defaultdict(list)
    for c in center: rows[c[1]].append(c[0])
    body = sorted(y for y, xs in rows.items() if len(xs) >= 10)
    if body:
        by = body[0] + 1
        bx0, bx1 = min(rows[by]), max(rows[by])
        for cx in (bx0 + 3, bx1 - 3):
            c = (cx, by)
            if c in owner and chest_free(c):
                floor_l.append({'hall': 'H6', 'x': c[0], 'y': c[1], 'theme': 'N-D', 'kind': 'Brazier_Pillar', 'why': 'H6 가운데 방 랜드마크'})
                lights.append((c[0] + 0.5, c[1] + 1.1, 9.0))

    # 존 문 앞이 빛 밖이면 그 존 테마 광원 1개 (북쪽 벽 문 = 벽걸이, 그 밖 = 문 옆 벽에 붙인 바닥 광원)
    for lbl, cs in sorted(door_mouths(owner).items()):
        if any(lit(c) for c in cs): continue
        zone = next(t for t in lbl.split('-') if re.fullmatch(r'Z\d+', t))
        hall = hall_of(owner[cs[0]])
        votes = defaultdict(int)
        for c in cs:
            for d in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                if owner.get((c[0] + d[0], c[1] + d[1]), '').startswith('~'): votes[d] += 1
        side = max(votes, key=votes.get)
        xs = [c[0] for c in cs]; ys = [c[1] for c in cs]
        if side == (0, 1):
            y = max(ys)
            for x in (min(xs) - 2, max(xs) + 2, min(xs) - 3, max(xs) + 3):
                c = (x, y)
                if c in owner and hall_of(owner[c]) and (x, y + 1) not in owner and not any(p['x'] == x and p['y'] == y for p in wall):
                    kinds = THEME[zone][0]; kind = kinds[h(x, y) % len(kinds)]
                    wall.append({'hall': hall, 'x': x, 'y': y, 'run': None, 'theme': zone, 'kind': kind, 'door': True, 'why': '존 문 앞 ' + lbl})
                    lights.append((x + 0.5, y + 2.5, KIND[kind][3]))
                    break
            continue
        fk = THEME[zone][2] or THEME[theme_key('N', is_cat(cs[0]))][2]
        if side[0]:
            x = cs[0][0]
            cands = [(x, max(ys) + 2), (x, min(ys) - 2), (x, max(ys) + 3), (x, min(ys) - 3)]
        else:
            y = cs[0][1]
            cands = [(min(xs) - 2, y), (max(xs) + 2, y), (min(xs) - 3, y), (max(xs) + 3, y)]
        for c in cands:
            behind = (c[0] + side[0], c[1] + side[1])
            if (c in owner and hall_of(owner[c]) and c not in water and behind not in owner and (c[0], c[1] + 1) in owner
                    and chest_free(c) and not any(abs(c[0] - f['x']) + abs(c[1] - f['y']) < 3 for f in floor_l)):
                kind = fk[h(c) % len(fk)]
                floor_l.append({'hall': hall, 'x': c[0], 'y': c[1], 'theme': zone, 'kind': kind, 'why': '존 문 앞 ' + lbl})
                lights.append((c[0] + 0.5, c[1] + 0.5, KIND[kind][3]))
                break

    cand = [c for c in owner if hall_of(owner[c]) and ok_floor(c) and THEME[theme_key(lab.get(c, 'N'), is_cat(c))][2]]
    hall_cells = [c for c in owner if hall_of(owner[c]) and c not in water]
    while sum(1 for f in floor_l if not f['why'].startswith('존 문 앞')) < FLOOR_MAX:   # 문 앞 광원은 따로 셈
        def dark(c):
            return min(((c[0] + 0.5 - lx) ** 2 + (c[1] + 0.5 - ly) ** 2) ** 0.5 - r for lx, ly, r in lights) if lights else 99
        cand = [c for c in cand if ok_floor(c)]
        if not cand: break
        best = max(cand, key=dark)
        if dark(best) < FLOOR_DARK - 6: break
        tk = theme_key(lab.get(best, 'N'), is_cat(best))
        opts = THEME[tk][2]
        k = opts[h(best) % len(opts)]
        floor_l.append({'hall': hall_of(owner[best]), 'x': best[0], 'y': best[1], 'theme': tk, 'kind': k, 'why': '어두운 곳'})
        lights.append((best[0] + 0.5, best[1] + 0.5, KIND[k][3]))

    # 존 문 앞이 빛 반경 안인지
    unlit_doors = []
    for lbl, cs in door_mouths(owner).items():
        if not any((c[0] + 0.5 - lx) ** 2 + (c[1] + 0.5 - ly) ** 2 <= r * r for c in cs for lx, ly, r in lights):
            unlit_doors.append((lbl, min(cs)))
    # 밝기 (반경 안 칸 비율)
    stats = {}
    for hall in halls:
        cs = [c for c in hall_cells if hall_of(owner[c]) == hall]
        lit = sum(1 for c in cs if any((c[0] + 0.5 - lx) ** 2 + (c[1] + 0.5 - ly) ** 2 <= r * r for lx, ly, r in lights))
        stats[hall] = (len(cs), lit, sum(1 for p in wall if p['hall'] == hall), sum(1 for f in floor_l if f['hall'] == hall))
    return dict(owner=owner, cat_rooms=cat_rooms, water=water, chest=chest, lab=lab, wall=wall, floor=floor_l, stats=stats, halls=halls, unlit_doors=unlit_doors)


def center_room(owner):
    """H6 가운데 방: 안뜰 덩어리 가운데에서 바닥을 따라 퍼지되 덩어리 안쪽(가장자리 5칸 제외)만"""
    x0, x1, y0, y1 = H6_CENTER
    inside = lambda c: x0 + 5 <= c[0] <= x1 - 5 and y0 + 5 <= c[1] <= y1 - 5
    mid = ((x0 + x1) // 2, (y0 + y1) // 2)
    start = min((c for c, n in owner.items() if hall_of(n) == 'H6' and inside(c)), key=lambda c: abs(c[0] - mid[0]) + abs(c[1] - mid[1]), default=None)
    if start is None: return []
    seen = {start}; q = deque([start])
    while q:
        c = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (c[0] + dx, c[1] + dy)
            if n in owner and n not in seen and inside(n):
                seen.add(n); q.append(n)
    return sorted(seen)


def door_mouths(owner):
    """존 문(존 이름이 든 통로)이 홀에 닿는 칸 묶음 {통로 이름: [홀 칸]}"""
    out = defaultdict(list)
    for c, n in owner.items():
        if not hall_of(n): continue
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            o = owner.get((c[0] + dx, c[1] + dy), '')
            if o.startswith('~') and any(re.fullmatch(r'Z\d+', t) for t in o[1:].split('-')):
                out[o[1:]].append(c)
    return out


def place_run(owner, lab, is_cat, hall, y, s, e, force_one=False):
    mid = ((s + e) // 2, y)
    tk = theme_key(lab.get(mid, 'N'), is_cat(mid))
    kinds, sp, _ = THEME[tk]
    a, b = s + EDGE, e - EDGE
    if b < a: a = b = (s + e) // 2
    ln = e - s + 1
    k = 1 if force_one else max(1, round(ln / sp))
    zone = tk if tk.startswith('Z') else None
    left_door = zone in door_zone(owner, (s - 1, y)) if zone else False
    right_door = zone in door_zone(owner, (e + 1, y)) if zone else False
    if k == 1:
        xs = [a if left_door else b if right_door else (a + b) // 2]
    elif left_door or right_door:
        xs = [round(a + i * (b - a) / (k - 1)) for i in range(k)]
    else:
        xs = [round(a - 0.5 + (i + 0.5) * (b - a + 1) / k) for i in range(k)]
    out = []
    for x in sorted(set(xs)):
        kind = kinds[h(x, y) % len(kinds)]
        out.append({'hall': hall, 'x': x, 'y': y, 'run': (s, e), 'theme': tk, 'kind': kind,
                    'door': (left_door and x == a) or (right_door and x == b)})
    return out


# ---------------------------------------------------------------- 시안
def sprite_rects(png):
    t = open(png + '.meta', encoding='utf-8').read()
    out = {}
    for m in re.finditer(r"name: (\S+)\n(?:.*\n){0,4}?\s+rect:\n\s+serializedVersion: 2\n\s+x: ([\d.]+)\n\s+y: ([\d.]+)\n\s+width: ([\d.]+)\n\s+height: ([\d.]+)", t):
        out[m.group(1)] = tuple(int(float(v)) for v in m.groups()[1:])
    return out


def load_sprites():
    sheets, out = {}, {}
    for k, (png, name, *_r) in KIND.items():
        path = os.path.join(TILE, png)
        if path not in sheets:
            sheets[path] = (Image.open(path).convert('RGBA'), sprite_rects(path))
        im, rs = sheets[path]
        x, y, w, hh = rs[name]
        out[k] = im.crop((x, im.height - y - hh, x + w, im.height - y))
    return out


def render(P, box, sprites, cat_row=2, markers=False, under=None, base=0.5, gamma=False, dim=True):
    """under(im): 바닥 · 벽을 그린 뒤 빛을 곱하기 전에 부르는 함수 (홀 장식 시안 decor.py가 소품을 그 사이에 그림)"""
    import painter_sim as PS
    import tilepreview
    owner, water = P['owner'], P['water']
    floor = set(owner)
    dt = tilepreview.load_tiles()
    sheet = Image.open(os.path.join(TILE, 'Catacomb', 'Catacomb.png')).convert('RGBA')
    gray = sheet.crop((736, 208, 768, 256))
    cat = P['cat_rooms']

    def is_cat(c):
        n = owner.get(c, '')
        if n.startswith('~'):
            return any(part in cat for part in n[1:].split('-'))
        return n in cat
    x0, x1, y0, y1 = box
    W, H = (x1 - x0 + 1) * 16, (y1 - y0 + 1) * 16
    im = Image.new('RGBA', (W, H), PS.VOID)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px, py = (x - x0) * 16, (y1 - y) * 16
            c = (x, y)
            if c in floor:
                if c in water:
                    im.paste((38, 70, 86, 255), (px, py, px + 16, py + 16))
                elif is_cat(c):
                    im.alpha_composite(gray.crop(((x % 2) * 16, (2 - y % 3) * 16, (x % 2) * 16 + 16, (2 - y % 3) * 16 + 16)), (px, py))
                else:
                    im.alpha_composite(dt['8'], (px, py))
            elif any((x + dx, y + dy) in floor for dx in range(-5, 6) for dy in range(-5, 6)):
                near_cat = any(is_cat((x + dx, y + dy)) for dx in range(-4, 5) for dy in range(-4, 5) if (x + dx, y + dy) in floor)
                if near_cat:
                    k = PS.wall_tile(floor, x, y, north_rows=4)
                    if k != '56': im.alpha_composite(PS.kit_tile(k), (px, py))
                else:
                    k = PS.wall_tile(floor, x, y, north_rows=3)
                    if k in dt: im.alpha_composite(dt[k], (px, py))
    if under: under(im)

    # 빛: 칸 단위 세기 지도 → 부드럽게 키워 곱함 (전역 0.5 + 광원)
    lights = []
    items = []
    for p in P['wall']:
        n = KIND[p['kind']][2]
        row = cat_row if owner.get((p['x'], p['y'])) in cat else 2
        cy = p['y'] + row + (n - 1) / 2
        lights.append((p['x'] + 0.5, cy + 0.5, KIND[p['kind']][3], KIND[p['kind']][4]))
        items.append((p['kind'], p['x'], p['y'] + row, n, p['theme'], 'wall', p.get('door')))
    for f in P['floor']:
        n = KIND[f['kind']][2]
        lights.append((f['x'] + 0.5, f['y'] + 0.5 + (n - 1) * 0.6, KIND[f['kind']][3], KIND[f['kind']][4]))
        items.append((f['kind'], f['x'], f['y'], n, f['theme'], 'floor', f['why'].startswith('존 문 앞')))
    lights += P.get('extra', [])
    lights = [l for l in lights if x0 - l[2] <= l[0] <= x1 + 1 + l[2] and y0 - l[2] <= l[1] <= y1 + 1 + l[2]]
    S = 4
    gw, gh = (x1 - x0 + 1) * S, (y1 - y0 + 1) * S
    g = Image.new('L', (gw, gh))
    gp = g.load()
    for gy in range(gh):
        wy = y1 + 1 - (gy + 0.5) / S
        for gx in range(gw):
            wx = x0 + (gx + 0.5) / S
            v = base
            for lx, ly, r, it in lights:
                d2 = (wx - lx) ** 2 + (wy - ly) ** 2
                if d2 < r * r:
                    v += it * 0.55 * (1 - (d2 ** 0.5) / r) ** 1.4
            gp[gx, gy] = int(255 * (min(1.0, v) ** (1 / 2.2) if gamma else min(1.0, v)))
    g = g.resize((W, H), Image.BILINEAR).filter(ImageFilter.GaussianBlur(4))
    lit = ImageChops.multiply(im.convert('RGB'), Image.merge('RGB', (g, g, g))).convert('RGBA')
    # 존 · 통로 · 다른 칸은 조금 더 어둡게 (홀이 주인공)
    mask = Image.new('L', (W, H), 0)
    md = ImageDraw.Draw(mask)
    for (x, y), n in owner.items():
        if dim and x0 <= x <= x1 and y0 <= y <= y1 and not hall_of(n):
            md.rectangle([(x - x0) * 16, (y1 - y) * 16, (x - x0) * 16 + 15, (y1 - y) * 16 + 15], fill=110)
    lit = Image.composite(Image.new('RGBA', (W, H), (8, 6, 5, 255)), lit, mask)
    d = ImageDraw.Draw(lit)
    for kind, x, y, n, tk, where, door in items:
        if not (x0 <= x <= x1 and y0 <= y <= y1): continue
        sp = sprites[kind]
        px = (x - x0) * 16 + 8 - sp.width // 2
        py = (y1 - y) * 16 + 16 - (n * 16) - (sp.height - n * 16) // 2
        lit.alpha_composite(sp, (px, py))
        if markers:   # 축소 지도용: 벽걸이 = 원, 바닥 = 네모, 존 문 앞 = 흰 테두리
            cx, cy, r = (x - x0) * 16 + 8, (y1 - y) * 16 + 16 - n * 8, 15
            box_ = [cx - r, cy - r, cx + r, cy + r]
            (d.ellipse if where == 'wall' else d.rectangle)(box_, outline=(255, 255, 255) if door else COLOR[tk], width=5)
            if door:
                (d.ellipse if where == 'wall' else d.rectangle)([cx - r + 5, cy - r + 5, cx + r - 5, cy + r - 5], outline=COLOR[tk], width=4)
    # 테마 경계 (홀 안에서 라벨이 바뀌는 곳)
    lab = P['lab']
    for (x, y), n in owner.items():
        if not hall_of(n) or not (x0 <= x <= x1 and y0 <= y <= y1): continue
        t = theme_key(lab.get((x, y), 'N'), (x, y) in owner and owner[(x, y)] in cat)
        for dx, dy in ((1, 0), (0, 1)):
            o = (x + dx, y + dy)
            if o in owner and hall_of(owner[o]) and theme_key(lab.get(o, 'N'), owner[o] in cat) != t:
                px, py = (x - x0) * 16, (y1 - y) * 16
                seg = [(px + 16, py), (px + 16, py + 16)] if dx else [(px, py), (px + 16, py)]
                d.line(seg, fill=COLOR[t] + (200,), width=2)
    return lit


def font(sz):
    return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', sz)


REGIONS = [
    ('남서 (H1~H4)', (14, 114, 14, 124), [('H1', (67, 104)), ('H2', (35, 64)), ('H3', (102, 45)), ('H4', (66, 29)), ('Z2', (30, 108)), ('Z8', (106, 100)), ('Z7', (87, 16)), ('E2', (39, 29))]),
    ('남동 (H5 · H6)', (146, 300, -16, 94), [('H5', (165, 0)), ('H6', (255, 82)), ('Z5', (183, 80)), ('Z6', (209, -10)), ('Z4', (293, 90)), ('가운데 방', (256, 48))]),
    ('중앙 하수도 (H7~H10, Catacomb)', (98, 326, 98, 186), [('H7', (150, 140)), ('H8', (265, 140)), ('H9', (250, 170)), ('H10', (308, 140)), ('Z3', (221, 131)), ('Z9', (148, 178)), ('E3', (301, 178)), ('Exit-B', (174, 103)), ('Z8', (110, 110))]),
    ('북서 (HA~HG)', (-12, 176, 126, 252), [('A', (41, 196)), ('B', (41, 150)), ('C', (4, 186)), ('D', (77, 190)), ('E', (110, 165)), ('F', (103, 222)), ('G', (155, 238)), ('Z1', (53, 240)), ('Z2', (26, 128)), ('Z9', (148, 175)), ('Z10', (175, 245)), ('E1', (83, 153))]),
]


def draft(out=DRAFT, ver='v01'):
    P = plan()
    sprites = load_sprites()
    scale = 0.5
    panels = []
    for name, box, labels in REGIONS:
        im = render(P, box, sprites, markers=True)
        d = ImageDraw.Draw(im)
        x0, x1, y0, y1 = box
        for t, (cx, cy) in labels:
            d.text(((cx - x0) * 16, (y1 - cy) * 16), t, font=font(44), fill=(150, 210, 255), anchor='mm', stroke_width=5, stroke_fill=(10, 10, 10))
        panels.append((name, im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)))
    # Catacomb 줄 비교 (H9 일부, 원래 크기)
    cmp_box = (226, 276, 160, 186)
    cmp = [(f'Catacomb 벽걸이 {r}번째 줄', render(P, cmp_box, sprites, cat_row=r)) for r in (1, 2)]

    # 실제 크기 확대 (테마 경계 · 문 앞 · 가운데 방 · Catacomb)
    zooms = [('확대: H1 (Z2 | Z8 경계)', (54, 82, 92, 120)), ('확대: H6 가운데 방 (중립 기둥 화로)', (241, 272, 31, 62)),
             ('확대: H7 서쪽 (Z9 · Z8 문, Catacomb)', (100, 140, 124, 156))]
    zooms = [(n, render(P, b, sprites)) for n, b in zooms]

    pad, head = 24, 90
    lw = max(p[1].width for p in panels[:2]); rw = max(p[1].width for p in panels[2:])
    left_h = sum(p[1].height + 50 for p in panels[:2])
    right_h = sum(p[1].height + 50 for p in panels[2:])
    side = 760
    W = pad * 3 + lw + rw + side
    zh = max(z[1].height for z in zooms) + 60
    H = head + max(left_h, right_h) + pad + zh
    canvas = Image.new('RGBA', (W, H), (16, 14, 12, 255))
    d = ImageDraw.Draw(canvas)
    d.text((pad, 44), f'홀 광원 배치 시안 {ver} — 기존 광원 재사용 · 가까운 존 테마 · 많이 어둡게 (벽걸이 12u, 8u 미만 구간 건너뜀)', font=font(34), fill=(235, 235, 235), anchor='lm')
    d.rounded_rectangle([W - 190, 18, W - 24, 70], 10, fill=(200, 60, 60))
    d.text((W - 107, 44), ver, font=font(34), fill=(255, 255, 255), anchor='mm')
    y = head
    for name, im in panels[:2]:
        d.text((pad, y + 18), name, font=font(28), fill=(220, 200, 150), anchor='lm')
        canvas.alpha_composite(im, (pad, y + 40)); y += im.height + 50
    xr = pad * 2 + lw
    y = head
    for name, im in panels[2:]:
        d.text((xr, y + 18), name, font=font(28), fill=(220, 200, 150), anchor='lm')
        canvas.alpha_composite(im, (xr, y + 40)); y += im.height + 50

    zy = head + max(left_h, right_h) + pad
    zx = pad
    for name, im in zooms:
        d.text((zx, zy + 18), name, font=font(28), fill=(220, 200, 150), anchor='lm')
        canvas.alpha_composite(im, (zx, zy + 40)); zx += im.width + pad * 2
    # 오른쪽: 범례 · 수치 · 줄 비교
    sx = W - side + pad
    sy = head
    d.text((sx, sy + 10), '테마별 광원 (선 색 = 홀 안 테마 경계)', font=font(26), fill=(220, 200, 150), anchor='lm'); sy += 34
    d.text((sx, sy + 10), '지도 표시: ○ 벽걸이 · □ 바닥 광원 · 흰 테두리 = 존 문 앞', font=font(20), fill=(200, 200, 200), anchor='lm'); sy += 36
    NAME = {'Z1': 'Z1 대장간', 'Z2': 'Z2 드워프', 'Z3': 'Z3 하수도', 'Z4': 'Z4 기관실', 'Z5': 'Z5 광맥', 'Z6': 'Z6 도축',
            'Z7': 'Z7 재봉소', 'Z8': 'Z8 연금술', 'Z9': 'Z9 지하묘지', 'Z10': 'Z10 의무실', 'N-D': '중립 (던전)', 'N-C': '중립 (Catacomb)'}
    for tk, (wk, _, fk) in THEME.items():
        d.rectangle([sx, sy + 6, sx + 18, sy + 24], fill=COLOR[tk])
        d.text((sx + 28, sy + 15), NAME[tk], font=font(20), fill=(230, 230, 230), anchor='lm')
        xx = sx + 220
        for k in list(dict.fromkeys(wk)) + ['|'] + fk:
            if k == '|':
                d.text((xx + 4, sy + 15), '/', font=font(20), fill=(140, 140, 140), anchor='lm'); xx += 22; continue
            sp = sprites[k].resize((sprites[k].width * 2 // (2 if sprites[k].height > 24 else 1), sprites[k].height * 2 // (2 if sprites[k].height > 24 else 1)), Image.NEAREST)
            canvas.alpha_composite(sp, (xx, sy + 18 - sp.height // 2)); xx += sp.width + 6
        sy += 44 if any(sprites[k].height > 24 for k in wk + fk) else 38
    sy += 16
    nw, nf = len(P['wall']), len(P['floor'])
    tot = sum(s[0] for s in P['stats'].values()); lit = sum(s[1] for s in P['stats'].values())
    d.text((sx, sy), f'벽걸이 {nw} · 바닥 {nf} · 밝기(반경 안 칸) {lit * 100 // max(1, tot)}%', font=font(24), fill=(235, 235, 235), anchor='lm'); sy += 36
    for hall in P['halls']:
        c, l, w, f = P['stats'][hall]
        d.text((sx, sy), f'{hall:<4} 벽 {w:>2} · 바닥 {f} · 밝기 {l * 100 // max(1, c):>2}%', font=font(20), fill=(200, 200, 200), anchor='lm'); sy += 27
    sy += 20
    for name, im in cmp:
        im2 = im.resize((int(im.width * 0.85), int(im.height * 0.85)), Image.LANCZOS)
        d.text((sx, sy + 12), name + ' (H9 일부)', font=font(22), fill=(220, 200, 150), anchor='lm')
        canvas.alpha_composite(im2, (sx, sy + 30)); sy += im2.height + 46
    d.text((sx, sy + 6), '존 · 통로는 어둡게 눌러 둠. 존 안 소품 · 얼룩 · 물가 마감은 생략', font=font(18), fill=(150, 150, 150), anchor='lm')
    canvas = canvas.crop((0, 0, W, max(H, sy + 40)))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    canvas.convert('RGB').save(out)
    print('draft', out, canvas.size, f'wall {nw} floor {nf} lit {lit * 100 // max(1, tot)}%')
    return P


WALL_ROW = 2                 # 벽걸이 = 바닥 위 2번째 줄 (Dungeon · Catacomb 모두, 시안 v01 승인)
PREV = os.path.join(HERE, 'lights_prev.json')   # 지금 프리팹에 칠해진 배치 (apply 성공 뒤 lights_next.json을 이 이름으로)


def tile_asset(kind):
    if kind == 'Brazier_Pillar':
        return 'Assets/04.Sprites/Tile/Dungeon/Props/Brazier_Pillar.asset'
    return 'Assets/04.Sprites/Tile/' + os.path.dirname(KIND[kind][0]) + '/' + kind + '.asset'


def cells_of(P):
    """[(타일맵 Decor|Block, x, y, 타일 경로)]"""
    out = [('Decor', p['x'], p['y'] + WALL_ROW, tile_asset(p['kind'])) for p in P['wall']]
    out += [('Block', f['x'], f['y'], tile_asset(f['kind'])) for f in P['floor']]
    return sorted(out)


def apply(out_cs=os.path.join(HERE, '_apply_hall_lights.cs')):
    """프리팹 Halls_Decor에 칠할 C# 생성. 이전 배치(lights_prev.json)와 프리팹이 같을 때만 바꾼다."""
    P = plan()
    new = cells_of(P)
    prev = [tuple(c) for c in json.load(open(PREV, encoding='utf-8'))] if os.path.exists(PREV) else []
    data = lambda cs: '\\n'.join(f'{m} {x} {y} {t}' for m, x, y, t in cs)
    tpl = open(os.path.join(HERE, '_apply_hall_lights.template.cs'), encoding='utf-8').read()
    cs = tpl.replace('__NEW__', data(new)).replace('__PREV__', data(prev))
    open(out_cs, 'w', encoding='utf-8', newline='\n').write(cs)
    json.dump(new, open(os.path.join(HERE, 'lights_next.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('apply', out_cs, 'new', len(new), 'prev', len(prev))


def dump(out):
    P = plan()
    json.dump({'wall': P['wall'], 'floor': P['floor'], 'stats': P['stats'], 'unlit_doors': P['unlit_doors']}, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('json', out, len(P['wall']), len(P['floor']))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'draft'
    if cmd == 'draft':
        draft(*(sys.argv[2:4] if len(sys.argv) > 2 else ()))
    elif cmd == 'apply':
        apply()
    elif cmd == 'json':
        dump(sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, 'lights_plan.json'))
