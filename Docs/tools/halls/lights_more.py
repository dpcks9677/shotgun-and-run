"""홀 · 통로 · Z3 광원 추가 (2026-10-14, 전역 빛 0.12 뒤 "조명 부족" 피드백)

CHEST_POINTS=<_dump_chest_points.cs 결과> python lights_more.py draft [출력.png] [버전]
CHEST_POINTS=... python lights_more.py apply   → _apply_hall_lights.cs (lights.py와 같은 템플릿, check는 MODE 수정)

기존 배치(lights_prev.json)는 그대로 두고 추가분만 계산한다.
  - 목표: 홀 · 통로는 구역마다 광원 반경 안 칸 40% 안팎 (Z3는 벽 랜턴 몇 개만, 무드 유지)
  - 후보: 북쪽 벽 정면 벽걸이(Decor) · 벽에 붙인 바닥 광원(Block, 가중 FLOOR_COST)
  - 고르기: 목표 미달 구역의 안 밝혀진 칸을 가장 많이 더 밝히는 후보를 하나씩 (탐욕)
  - 피할 것: lights.py · decor.py와 같음 (통로 입구 · 상자 지점 · 물 · 장식 칸 · 4u 길)
"""
import json, os, re, sys
from collections import defaultdict
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lights as LT

TARGET = 0.60              # 홀
TARGET_PASSAGE = 0.25      # 통로 (60칸 이상 긴 통로만 따로 채움 — 짧은 연결 통로는 양쪽 홀 광원이 걸쳐 밝힘)
LONG_PASSAGE = 60
FLOOR_COST = 1.5           # 바닥 광원은 벽걸이보다 이만큼 더 밝혀 줘야 뽑힘 (길을 막으니까)
MIN_GAIN = 10              # 새로 밝히는 칸이 이보다 적으면 그만둠
MIN_GAP = 6.0              # 새 광원과 다른 광원 사이 최소 거리 (벽걸이 중심 기준)
Z3_COUNT = 5               # Z3 벽 랜턴 개수 (무드 유지: 조금만)
Z3_GAP = 16.0
Z3_KIND = 'Hanging_Lantern'
ZONES = ('Z1', 'Z2', 'Z4', 'Z6', 'Z7')    # 손으로 꾸민 존: 벽걸이만, 방 소품과 겹치지 않는 곳에
ZONE_KINDS = {'Z7': ['Torch_Wall']}       # 고블린 캠프는 나무 횃불 (홀의 Z7 쪽은 촛대 그대로)
LIGHTS_DUMP = os.environ.get('LIGHTS_DUMP', os.path.join(LT.ROOT, 'Backups', 'lights_dump.txt'))   # _dump_lights.cs
ZONE_TILES = os.environ.get('ZONE_TILES', os.path.join(LT.ROOT, 'Backups', 'zone_tiles.txt'))      # _dump_zone_tiles.cs
DRAFT = os.path.join(LT.ROOT, 'Docs', 'drafts', 'props', 'hall-lights-more-draft.png')
DECOR_PREV = os.path.join(HERE, 'decor_prev.json')


def kind_of(tile):
    return os.path.splitext(os.path.basename(tile))[0]


def center_of(kind, where, x, y):
    n = LT.KIND[kind][2]
    if where == 'wall':       # y = 바닥 줄 (벽걸이 칸은 y + WALL_ROW)
        return (x + 0.5, y + LT.WALL_ROW + (n - 1) / 2 + 0.5)
    return (x + 0.5, y + 0.5 + (n - 1) * 0.6)


def area_of(name):
    if LT.hall_of(name): return LT.hall_of(name)
    if name.startswith('~'): return name
    if re.match(r'^Z3($|-G)', name): return 'Z3'
    return None


def load_existing():
    """lights_prev.json → 벽걸이 · 바닥 광원 목록 (render 형식)"""
    wall, floor = [], []
    for m, x, y, t in json.load(open(LT.PREV, encoding='utf-8')):
        k = kind_of(t)
        if m == 'Decor': wall.append({'x': x, 'y': y - LT.WALL_ROW, 'kind': k})
        else: floor.append({'x': x, 'y': y, 'kind': k})
    return wall, floor


def load_scene_lights():
    """씬 덤프에서 Halls_Decor 밖의 빛 (존 · R7 등) → [(x, y, r, 세기)]"""
    out = []
    for ln in open(LIGHTS_DUMP, encoding='utf-8'):
        p = ln.strip().split(',')
        if len(p) == 5 and p[0] != 'Halls_Decor': out.append((float(p[1]), float(p[2]), float(p[3]), float(p[4])))
    return out


def load_zone_tiles():
    """존 방 프리팹이 차지한 칸 (바닥 무늬 · 물 · 데칼 제외)"""
    skip = (' Decal', ' Water', ' Pond Frame', ' Plate', ' Floor')
    out = set()
    for ln in open(ZONE_TILES, encoding='utf-8'):
        p = ln.rstrip().split('|')
        if len(p) == 5 and not p[1].endswith(skip): out.add((int(p[2]), int(p[3])))
    return out


def plan_more():
    P = LT.plan()
    owner, water, chest, lab, cat = P['owner'], P['water'], P['chest'], P['lab'], P['cat_rooms']
    wall0, floor0 = load_existing()
    decor = json.load(open(DECOR_PREV, encoding='utf-8'))
    taken = {(x, y) for _, x, y, _ in decor} | load_zone_tiles()   # 장식 · 존 소품이 차지한 칸
    blocks = {(x, y) for m, x, y, _ in decor if m == 'Block'} | {(f['x'], f['y']) for f in floor0}
    wall_cells = {(w['x'], w['y'] + LT.WALL_ROW) for w in wall0}
    decor_wall = {(x, y) for m, x, y, _ in decor if m == 'Decor'} | wall_cells

    def is_cat(c):
        n = owner.get(c, '')
        return any(p in cat for p in n[1:].split('-')) if n.startswith('~') else n in cat

    def theme(c):
        return LT.theme_key(lab.get(c, 'N'), is_cat(c))

    mouths = {c for c, n in owner.items() if n.startswith('~')}
    area = {c: area_of(n) for c, n in owner.items() if area_of(n)}
    groups = defaultdict(list)
    for c, a in area.items():
        if a != 'Z3' and c not in water: groups[a].append(c)
    z3 = [c for c, a in area.items() if a == 'Z3']
    for z in ZONES:
        groups[z] = [c for c, n in owner.items() if n == z and c not in water]
        for c in groups[z]: area[c] = z

    lights = list(load_scene_lights())   # (x, y, r, intensity) — 존 · R7 광원은 이미 씬에 있는 것
    extra = list(lights)
    for w in wall0:
        k = w['kind']; cx, cy = center_of(k, 'wall', w['x'], w['y']); lights.append((cx, cy, LT.KIND[k][3], LT.KIND[k][4]))
    for f in floor0:
        k = f['kind']; cx, cy = center_of(k, 'floor', f['x'], f['y']); lights.append((cx, cy, LT.KIND[k][3], LT.KIND[k][4]))

    def wall_ok(c):
        x, y = c
        if (x, y + 1) in owner: return False
        for dx in (-1, 0, 1):
            for dy in (1, 2, 3):
                if (x + dx, y + dy) in owner: return False
        if any((x + dx, y + dy) in mouths for dx in range(-2, 3) for dy in range(1, 4)): return False
        if any((x + dx, y + LT.WALL_ROW) in decor_wall for dx in (-1, 0, 1)): return False
        return (x, y + LT.WALL_ROW) not in taken and c not in blocks

    def span(c, dx, dy):
        n = 1
        for sgn in (1, -1):
            x, y = c
            while (x + sgn * dx, y + sgn * dy) in owner and (x + sgn * dx, y + sgn * dy) not in blocks:
                x += sgn * dx; y += sgn * dy; n += 1
                if n > 40: break
        return n

    def floor_ok(c):
        if c not in owner or not LT.hall_of(owner[c]) or c in water or c in taken or c in blocks: return False
        if (c[0], c[1] + 1) not in owner: return False
        if all(n in owner for n in ((c[0] - 1, c[1]), (c[0] + 1, c[1]), (c[0], c[1] - 1))): return False
        if any(max(abs(c[0] - a), abs(c[1] - b)) <= LT.CHEST_MARGIN for a, b in chest): return False
        if any(max(abs(c[0] - a), abs(c[1] - b)) <= 1 for a, b in blocks): return False
        if any((c[0] + dx, c[1] + dy) in mouths for dx in range(-2, 3) for dy in range(-2, 3)): return False
        if span(c, 1, 0) < 7 or span(c, 0, 1) < 7: return False
        return True

    def pick_kind(tk, where, c):
        opts = LT.THEME[tk][0] if where == 'wall' else LT.THEME[tk][2]
        return opts[LT.h(c) % len(opts)] if opts else None

    cands = []
    for a, cs in groups.items():
        for c in cs:
            if wall_ok(c):
                tk = theme(c)
                opts = ZONE_KINDS.get(a) or LT.THEME[tk][0]
                k = opts[LT.h(c) % len(opts)]
                if LT.KIND[k][2] == 2 and (c[0], c[1] + LT.WALL_ROW + 1) in taken: continue
                cands.append(('wall', c, k, tk))
            elif a.startswith('H') and floor_ok(c):
                k = pick_kind(theme(c), 'floor', c)
                if k: cands.append(('floor', c, k, theme(c)))

    def lit_by(c, L):
        return (c[0] + 0.5 - L[0]) ** 2 + (c[1] + 0.5 - L[1]) ** 2 <= L[2] ** 2

    lit = {c: any(lit_by(c, L) for L in lights) for cs in groups.values() for c in cs}
    need = {a: (TARGET_PASSAGE if a.startswith('~') else TARGET) * len(cs) - sum(lit[c] for c in cs) for a, cs in groups.items()
            if not a.startswith('~') or len(cs) >= LONG_PASSAGE}
    added_wall, added_floor = [], []
    pool = list(cands)
    while True:
        deficit = {a for a, n in need.items() if n >= 5}
        if not deficit or not pool: break
        best, bg, bs = None, 0, 0
        for cd in pool:
            where, c, k, tk = cd
            cx, cy = center_of(k, where, *c); r = LT.KIND[k][3]
            if any((cx - L[0]) ** 2 + (cy - L[1]) ** 2 < MIN_GAP ** 2 for L in lights): continue
            gain = 0     # 구역마다 남은 부족분까지만 셈 → 작은 홀에 큰 광원이 몰리지 않음
            for a in deficit:
                g = sum(1 for q in groups[a] if not lit[q] and (q[0] + 0.5 - cx) ** 2 + (q[1] + 0.5 - cy) ** 2 <= r * r)
                gain += min(g, need[a])
            score = gain / (FLOOR_COST if where == 'floor' else 1)
            if score > bs: best, bg, bs = cd, gain, score
        if best is None or bg < MIN_GAIN: break
        where, c, k, tk = best
        cx, cy = center_of(k, where, *c); r = LT.KIND[k][3]
        lights.append((cx, cy, r, LT.KIND[k][4])); pool.remove(best)
        item = {'x': c[0], 'y': c[1], 'kind': k, 'theme': tk, 'hall': area[c], 'why': '추가', 'new': True}
        (added_wall if where == 'wall' else added_floor).append(item)
        if where == 'floor': blocks.add(c)
        for a, cs in groups.items():
            for q in cs:
                if not lit[q] and (q[0] + 0.5 - cx) ** 2 + (q[1] + 0.5 - cy) ** 2 <= r * r:
                    lit[q] = True
                    if a in need: need[a] -= 1

    # Z3: 벽 랜턴 몇 개 (가장 많이 밝히는 자리부터, 서로 Z3_GAP 이상)
    z3_added = []
    z3c = [c for c in z3 if wall_ok(c)]
    k = Z3_KIND; r = LT.KIND[k][3]
    z3c = [c for c in z3c if (c[0], c[1] + LT.WALL_ROW + 1) not in taken]
    z3lit = {c: any(lit_by(c, L) for L in lights) for c in z3}
    for _ in range(Z3_COUNT):
        best, bg = None, 0
        for c in z3c:
            cx, cy = center_of(k, 'wall', *c)
            if any((cx - L[0]) ** 2 + (cy - L[1]) ** 2 < MIN_GAP ** 2 for L in lights): continue
            if any((cx - zx) ** 2 + (cy - zy) ** 2 < Z3_GAP ** 2 for zx, zy in [center_of(k, 'wall', *i['pos']) for i in z3_added]): continue
            g = sum(1 for q in z3 if not z3lit[q] and (q[0] + 0.5 - cx) ** 2 + (q[1] + 0.5 - cy) ** 2 <= r * r)
            if g > bg: best, bg = c, g
        if best is None or bg < MIN_GAIN: break
        cx, cy = center_of(k, 'wall', *best); lights.append((cx, cy, r, LT.KIND[k][4]))
        for q in z3:
            if (q[0] + 0.5 - cx) ** 2 + (q[1] + 0.5 - cy) ** 2 <= r * r: z3lit[q] = True
        z3_added.append({'pos': best, 'x': best[0], 'y': best[1], 'kind': k, 'theme': 'Z3' if 'Z3' in LT.COLOR else 'N-D', 'hall': 'Z3', 'why': 'Z3 랜턴', 'new': True})
    for i in z3_added: del i['pos']
    added_wall += z3_added

    # 결과 수치 (구역별 전 → 후)
    base = [(L[0], L[1], L[2]) for L in lights[:len(extra) + len(wall0) + len(floor0)]]
    def frac(cs, ls, scale=1.0):
        return sum(1 for c in cs if any((c[0] + .5 - x) ** 2 + (c[1] + .5 - y) ** 2 <= (r * scale) ** 2 for x, y, r in ls)) / max(1, len(cs))
    allL = [(L[0], L[1], L[2]) for L in lights]
    stats = {a: (len(cs), frac(cs, base), frac(cs, allL), frac(cs, base, .6), frac(cs, allL, .6)) for a, cs in groups.items()}
    stats['Z3'] = (len(z3), frac(z3, base), frac(z3, allL), frac(z3, base, .6), frac(z3, allL, .6))

    for it in wall0: it['theme'] = theme((it['x'], it['y'])); it['hall'] = area.get((it['x'], it['y']))
    for it in floor0: it['theme'] = theme((it['x'], it['y'])); it['hall'] = area.get((it['x'], it['y'])); it['why'] = ''
    P2 = dict(P)
    P2['wall'] = wall0 + added_wall
    P2['floor'] = floor0 + added_floor
    P2['added_wall'], P2['added_floor'], P2['stats_more'] = added_wall, added_floor, stats
    P2['groups'] = groups
    P2['extra'] = extra
    return P2


def cells_more(P2):
    """lights_prev.json 형식 전체 목록 (기존 + 추가)"""
    return sorted([('Decor', p['x'], p['y'] + LT.WALL_ROW, LT.tile_asset(p['kind'])) for p in P2['wall']]
                  + [('Block', f['x'], f['y'], LT.tile_asset(f['kind'])) for f in P2['floor']])


def report(P2):
    st = P2['stats_more']
    tot = lambda f: sum(v[0] * f(v) for v in st.values() if True)
    print('추가: 벽걸이/랜턴 %d · 바닥 %d' % (len(P2['added_wall']), len(P2['added_floor'])))
    print('%-14s %6s %9s %9s' % ('구역', '칸', '반경안 전→후', '밝은중심 전→후'))
    for a in sorted(st, key=lambda s: (not s.startswith('H'), s.startswith('~'), s)):
        n, b, f, b6, f6 = st[a]
        print('%-14s %6d %4.0f%%→%3.0f%% %4.0f%%→%3.0f%%' % (a, n, 100 * b, 100 * f, 100 * b6, 100 * f6))
    for label, sel in (('홀', lambda a: a.startswith('H')), ('통로', lambda a: a.startswith('~'))):
        ks = [a for a in st if sel(a)]
        n = sum(st[a][0] for a in ks)
        print('%s 합계 %d칸: 반경 안 %.0f%%→%.0f%% · 밝은 중심 %.0f%%→%.0f%%' % (
            label, n, 100 * sum(st[a][0] * st[a][1] for a in ks) / n, 100 * sum(st[a][0] * st[a][2] for a in ks) / n,
            100 * sum(st[a][0] * st[a][3] for a in ks) / n, 100 * sum(st[a][0] * st[a][4] for a in ks) / n))


def draw(out=DRAFT, ver='v01'):
    P2 = plan_more()
    report(P2)
    sprites = LT.load_sprites()
    st = P2['stats_more']
    new_items = [(it, it in P2['added_floor']) for it in P2['added_wall'] + P2['added_floor']]

    def rings(im, box, scale=1, width=4):
        d = ImageDraw.Draw(im)
        x0, x1, y0, y1 = box
        for it, sq in new_items:
            n = LT.KIND[it['kind']][2]
            x, y = (it['x'], it['y']) if sq else (it['x'], it['y'] + LT.WALL_ROW)
            if not (x0 <= x <= x1 and y0 <= y <= y1): continue
            cx, cy, r = (x - x0) * 16 + 8, (y1 - y) * 16 + 16 - n * 8, 15
            (d.rectangle if sq else d.ellipse)([cx - r, cy - r, cx + r, cy + r], outline=(255, 0, 255), width=width)
        return im

    panels = []
    for name, box, labels in LT.REGIONS:
        im = LT.render(P2, box, sprites, markers=True, base=0.5, dim=False)
        rings(im, box, width=6)
        d = ImageDraw.Draw(im)
        x0, x1, y0, y1 = box
        for t, (lx, ly) in labels:
            d.text(((lx - x0) * 16, (y1 - ly) * 16), t, font=LT.font(44), fill=(150, 210, 255), anchor='mm', stroke_width=5, stroke_fill=(10, 10, 10))
        panels.append((name, im.resize((im.width // 2, im.height // 2), Image.LANCZOS)))
    # 실제 밝기(전역 0.12) 확대: 새 광원이 있는 홀 세 곳
    zooms = []
    for hall in ('H3', 'Z4', 'Z6', 'Z2'):
        it = next((i for i, _ in new_items if i['hall'] == hall), None)
        if it is None: continue
        cx, cy = it['x'], it['y']
        box = (cx - 20, cx + 20, cy - 12, cy + 12)
        im = LT.render(P2, box, sprites, markers=False, base=0.12, gamma=True, dim=False)
        zooms.append(('확대 (실제 밝기 근사): %s (%d, %d)' % (hall, cx, cy), rings(im, box, width=3)))

    pad, head, side = 24, 90, 520
    lw = max(p[1].width for p in panels[:2]); rw = max(p[1].width for p in panels[2:])
    lh = sum(p[1].height + 50 for p in panels[:2]); rh = sum(p[1].height + 50 for p in panels[2:])
    W = pad * 3 + lw + rw + side
    zh = max([z[1].height for z in zooms] + [0]) + 60
    H = head + max(lh, rh) + pad + zh
    canvas = Image.new('RGBA', (W, H), (16, 14, 12, 255))
    d = ImageDraw.Draw(canvas)
    d.text((pad, 44), '홀 · 통로 · 존 · Z3 광원 추가 시안 — 마젠타 테두리 = 새 광원 (○ 벽걸이 · □ 바닥), 그 밖은 기존', font=LT.font(34), fill=(235, 235, 235), anchor='lm')
    d.rounded_rectangle([W - 190, 18, W - 24, 70], 10, fill=(200, 60, 60))
    d.text((W - 107, 44), ver, font=LT.font(34), fill=(255, 255, 255), anchor='mm')
    y = head
    for name, im in panels[:2]:
        d.text((pad, y + 18), name, font=LT.font(28), fill=(220, 200, 150), anchor='lm'); canvas.alpha_composite(im, (pad, y + 40)); y += im.height + 50
    xr = pad * 2 + lw; y = head
    for name, im in panels[2:]:
        d.text((xr, y + 18), name, font=LT.font(28), fill=(220, 200, 150), anchor='lm'); canvas.alpha_composite(im, (xr, y + 40)); y += im.height + 50
    zy = head + max(lh, rh) + pad; zx = pad
    for name, im in zooms:
        d.text((zx, zy + 18), name, font=LT.font(28), fill=(220, 200, 150), anchor='lm'); canvas.alpha_composite(im, (zx, zy + 40)); zx += im.width + pad * 2
    sx = W - side + pad; sy = head
    d.text((sx, sy), '추가 %d (벽걸이 %d · Z3 랜턴 %d · 바닥 %d)' % (len(new_items), len(P2['added_wall']) - sum(1 for i in P2['added_wall'] if i['hall'] == 'Z3'),
           sum(1 for i in P2['added_wall'] if i['hall'] == 'Z3'), len(P2['added_floor'])), font=LT.font(24), fill=(235, 235, 235), anchor='lm'); sy += 40
    d.text((sx, sy), '구역       반경 안 전 → 후   밝은 중심', font=LT.font(20), fill=(220, 200, 150), anchor='lm'); sy += 28
    for a in sorted((a for a in st if not a.startswith('~')), key=lambda s: (not s.startswith('H'), len(s), s)):
        n, b, f, b6, f6 = st[a]
        d.text((sx, sy), '%-4s %3.0f%% → %3.0f%%     %3.0f%% → %3.0f%%' % (a, 100 * b, 100 * f, 100 * b6, 100 * f6), font=LT.font(20), fill=(200, 200, 200), anchor='lm'); sy += 27
    sy += 14
    for label, sel in (('홀 합계', lambda a: a.startswith('H')), ('통로 합계', lambda a: a.startswith('~'))):
        ks = [a for a in st if sel(a)]; n = sum(st[a][0] for a in ks)
        d.text((sx, sy), '%s %3.0f%% → %3.0f%%  (중심 %3.0f%% → %3.0f%%)' % (label, *[100 * sum(st[a][0] * st[a][i] for a in ks) / n for i in (1, 2, 3, 4)]), font=LT.font(20), fill=(235, 235, 235), anchor='lm'); sy += 27
    d.text((sx, sy + 10), '반경 안 = 광원 빛 반경에 든 바닥 칸 비율', font=LT.font(18), fill=(150, 150, 150), anchor='lm')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    canvas.convert('RGB').save(out)
    print('draft', out, canvas.size)
    return P2


def apply(out_cs=os.path.join(HERE, '_apply_hall_lights.cs')):
    P2 = plan_more()
    report(P2)
    new = cells_more(P2)
    prev = [tuple(c) for c in json.load(open(LT.PREV, encoding='utf-8'))]
    data = lambda cs: '\\n'.join(f'{m} {x} {y} {t}' for m, x, y, t in cs)
    tpl = open(os.path.join(HERE, '_apply_hall_lights.template.cs'), encoding='utf-8').read()
    open(out_cs, 'w', encoding='utf-8', newline='\n').write(tpl.replace('__NEW__', data(new)).replace('__PREV__', data(prev)))
    json.dump(new, open(os.path.join(HERE, 'lights_next.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('apply', out_cs, 'new', len(new), 'prev', len(prev))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'draft'
    if cmd == 'draft': draw(*(sys.argv[2:4]))
    elif cmd == 'apply': apply()
    elif cmd == 'report': report(plan_more())
