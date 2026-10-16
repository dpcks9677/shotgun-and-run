"""홀 공용 장식 배치 (Docs/hall-decor-plan.md 4장 규칙) — 자리 생성 · 검사 · 시안

CHEST_POINTS=<_dump_chest_points.cs 결과> python decor.py draft [all|sw|se|center|nw] [버전]
CHEST_POINTS=... python decor.py check|apply   → _apply_hall_props.cs (check = 씬 대조만, apply = 칠하기)
  apply 성공 뒤 decor_next.json을 decor_prev.json으로
  → Docs/drafts/props/hall-decor-<지역>-draft.png

규칙 요약 (A + B안: 충돌 없는 벽걸이 · 데칼 + 벽에 붙인 블록만)
  - 테마 = 광원과 같은 지도 (lights.nearest_zone + H6 가운데 방 중립)
  - 농도 = 그 테마 존 문(홀 쪽 입구)에서 걸은 거리: 12u 안 존 8 : 폐허 2, 24u 안 5 : 5, 그 밖 폐허만
  - 벽걸이: 북쪽 벽 정면, 4u 이상 구간에 광원 포함 6u마다 1개 꼴, 광원과 좌우 2u 이상
  - 블록: 한 변 전체가 벽에 붙음, 벽에서 2u까지, 2~4개 묶음, 앞쪽 빈 바닥 4u, 문 4u 길 연결 유지
  - 데칼: 존 문 12u 안 6% · 24u 안 3% · 그 밖 1%, 블록 묶음 · 문 앞에 모음
"""
import glob, json, os, re, sys
from collections import deque, defaultdict
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lights as LI
import layout_edit as L

ROOT = LI.ROOT
TILE = LI.TILE
ZP = os.path.join(TILE, 'ZoneProps')
DRAFT = os.path.join(ROOT, 'Docs', 'drafts', 'props', 'hall-decor-draft.png')

NEAR, MID = 12, 24                 # 농도 기울기 경계 (존 문에서 걸은 거리)
ZONE_P = {NEAR: 0.8, MID: 0.5}     # 존 소품 비율
DECAL_D = {NEAR: 0.06, MID: 0.03}  # 데칼 밀도, 그 밖 0.01
WALL_EVERY = 6                     # 벽걸이 (광원 포함) 간격 (v02: 8 → 6)
WALL_MIN_RUN = 4
LIGHT_GAP = 2                      # 벽걸이 광원과 좌우 거리
CELLS_PER_CLUSTER = 125            # v02: 250 → 125
CLUSTER_GAP = 10                   # 묶음 씨앗 사이 맨해튼 거리
FRONT = 4                          # 블록 앞 빈 바닥
MAX_DEPTH = 2                      # 벽에서 튀어나오는 칸
MOUTH_GAP = 3                      # 문 · 통로 입구에서 (체비쇼프)
CHEST_GAP = 2

PIPE_MIDS = ['_1', '_2', '_flange', '_gauge', '_valve', '_rust']

# 테마: 벽걸이 · 블록 · 데칼 낱개 (ZoneProps 기준 glob, 한 줄 = 변형을 묶은 한 종류) + ZoneDecal 무늬
THEME = {
    'Z1': dict(wall=['Ruins/Banner_Torn_*', 'Ruins/Wall_Crack_S_*'],
               block=['Forge/CoalBox_Wide', 'Forge/QuenchBarrel', 'Forge/CoalBox', 'Forge/WeaponRack_Axes', 'Forge/WeaponRack_Spears',
                      'Forge/WeaponRack_Swords', 'Forge/WeaponRack_Empty'],  # WallWeaponRack은 벽걸이(충돌 없음)라 바닥 Block에 두지 않음
               decal=[], patch='Soot'),
    'Z2': dict(wall=['PIPE:DwarfWorkshop/BrassPipe_H', 'DwarfWorkshop/Warn_Sign_*'],
               block=['DwarfWorkshop/Powder_Keg', 'DwarfWorkshop/Powder_Crate_*', 'DwarfWorkshop/Sack_Saltpeter', 'DwarfWorkshop/Sack_Charcoal',
                      'DwarfWorkshop/Sack_Sulfur', 'DwarfWorkshop/Shot_Crate_*', 'DwarfWorkshop/Shot_Pile_S', 'DwarfWorkshop/Shot_Pile_M',
                      'DwarfWorkshop/Fuse_Reel_*'],
               decal=['DwarfWorkshop/Decal_Powder_Scatter', 'DwarfWorkshop/Decal_Powder_Spill'], patch='Oil'),
    'Z3': dict(wall=['Sewer/Wall_Stain_*', 'Sewer/Wall_Grate_*'],
               block=['Sewer/OilBarrel_Sealed', 'Sewer/OilBarrel_Open', 'Sewer/OilBarrel_Leak', 'Sewer/OilBarrel_Lids', 'Sewer/OilBarrel_Broken',
                      'Sewer/OilBarrel_Tipped', 'Sewer/OilBarrels_Pair', 'Sewer/OilBarrel_Rack', 'Sewer/OilBarrels_Cluster'],
               decal=['Sewer/OilSpill_S', 'Sewer/OilSpill_M'], patch=None),
    'Z4': dict(wall=['PIPE:Machinery/Pipe_H', 'Machinery/Chain_Wall'],
               block=['Machinery/Coal_Barrow', 'Machinery/Scrap_Gear_Pile', 'Forge/CoalBox'],
               decal=['Machinery/Oil_Spill'], patch='Oil'),
    'Z5': dict(wall=['ManaVein/WallTools_*'],
               block=['ManaVein/Minecart_Empty', 'ManaVein/Minecart_Ore', 'Forge/CoalBox', 'Forge/CoalBox_Wide',   # v03: 광차만이면 단조로움
                      'ManaVein/RockSlab_S', 'ManaVein/RockSlab_M', 'ManaVein/RockSlab_M_moss'],
               decal=['ManaVein/Sprout_*', 'ManaVein/Tool_Hammer_*', 'ManaVein/Tool_Pick_*', 'ManaVein/Tool_Shovel_*'], patch='ManaDust'),
    'Z6': dict(wall=['Butchery/Wall_Carcass', 'Butchery/Wall_Sausages', 'Butchery/Wall_Herbs', 'Butchery/Wall_ToolRack', 'Butchery/Wall_Notice',
                     'Butchery/Web_Drape'],
               block=['Butchery/Barrel_Salt', 'Butchery/Barrel_Lid', 'Butchery/Barrel_Blood', 'Butchery/Crate', 'Butchery/Sack', 'Butchery/Bucket',
                      'Butchery/BonePile', 'Butchery/Crate_Large', 'Butchery/Sack_Pile', 'Butchery/Firewood'],
               decal=['Butchery/Blood_Drops', 'Butchery/Blood_Splat', 'Butchery/Blood_Drag', 'Butchery/Blood_Pool', 'Butchery/Blood_Pool_B'], patch=None),
    'Z7': dict(wall=['TailorShop/Wall_Shield_*', 'TailorShop/Wall_Banner_*'],
               block=['Butchery/Crate', 'Butchery/Sack', 'Butchery/Barrel_Lid', 'Butchery/Barrel_Salt', 'TailorShop/Armor_Pile_*',
                      'Butchery/Crate_Large', 'Butchery/Sack_Pile', 'Butchery/Firewood'],
               decal=['GoblinCamp/Decal_Ash_*'], patch=None),   # v02: 먼지 무늬는 어두운 홀에서 흰 구름처럼 보여 재만
    'Z8': dict(wall=['LabProps/WallShelf_books', 'Alchemy/Rune_Text_*'],
               block=['LabProps/Books_stack', 'LabProps/Books_two', 'LabProps/Books_open', 'Alchemy/SoulJar_B', 'Alchemy/SoulJar_G',
                      'Alchemy/SoulJar_P', 'Alchemy/SkullPile_S', 'LabProps/Bookshelf_broken_2'],
               decal=['Alchemy/Decal_Alch_*', 'LabProps/Decal_ink', 'LabProps/Decal_paper', 'LabProps/Decal_wax', 'Alchemy/Decal_Necro_ash'],
               patch=None),   # v03: 약품 얼룩 무늬는 진해서 뺌
    'Z9': dict(wall=['Crypt/Niche_*', 'Alchemy/Necro_BoneOrnament', 'Butchery/Web_Drape'],
               block=['Crypt/Tomb_*', 'Alchemy/SkullPile_S', 'Alchemy/SkullPile_M', 'Butchery/BonePile', 'Alchemy/SoulJar_G'],
               decal=['Alchemy/Decal_Necro_bones'], patch=None),
    'Z10': dict(wall=['Infirmary/Wall_MedHerbs_*', 'Infirmary/Wall_MedTools', 'LabProps/WallShelf_jars', 'LabProps/WallShelf_potions',
                      'Infirmary/Wall_Anatomy_*'],
                block=['Infirmary/Med_crate', 'Infirmary/Med_bandages', 'Infirmary/Med_basin', 'Infirmary/Bucket_*', 'Infirmary/MedCrate_Stack_1x2',
                       'Infirmary/Stretcher_broken', 'Infirmary/Splint_*'],
                decal=['Infirmary/Decal_Med_*'], patch='Blood'),
    'N-D': dict(wall=['Ruins/Wall_Crack_S_*', 'Ruins/Wall_Crack_L_*', 'Ruins/Wall_Crack_Radial_*', 'Ruins/Wall_Crack_Horizontal_*',
                      'Ruins/Beam_Wall_*', 'Ruins/Banner_Torn_*'],
                block=['Ruins/Rubble_S_*', 'Ruins/Barrel_Broken_*', 'Ruins/Crate_Broken_*', 'Ruins/Rubble_M_*', 'Ruins/Rubble_L_*',
                       'Ruins/Pillar_Broken_L_*', 'Ruins/Pillar_Broken_S_*', 'Ruins/Pillar_Fallen_*', 'Ruins/Beam_Fallen_*'],
                decal=['Ruins/Floor_Crack_*', 'Ruins/Debris_*'], patch=None),
}
THEME['N-C'] = dict(wall=THEME['N-D']['wall'] + ['Butchery/Web_Drape'],
                    block=THEME['N-D']['block'] + ['Alchemy/SkullPile_S', 'Alchemy/SkullPile_M', 'Butchery/BonePile'],
                    decal=THEME['N-D']['decal'] + ['Alchemy/Decal_Necro_bones'], patch=None)


# ---------------------------------------------------------------- 타일 → 스프라이트 · 칸
class Sprites:
    """타일 에셋(.asset)의 첫 스프라이트 그림 · 피벗 · 차지하는 칸"""

    def __init__(self):
        self.png_of = {}
        for meta in glob.glob(os.path.join(TILE, '**', '*.png.meta'), recursive=True):
            m = re.search(r'(?m)^guid: (\w+)', open(meta, encoding='utf-8').read())
            if m: self.png_of[m.group(1)] = meta[:-5]
        self.sheets, self.cache = {}, {}

    def sheet(self, png):
        if png not in self.sheets:
            t = open(png + '.meta', encoding='utf-8').read()
            ids = dict((int(v), k) for k, v in re.findall(r'(?m)^      (\S+): (-?\d+)$', t.split('nameFileIdTable:', 1)[1])) if 'nameFileIdTable:' in t else {}
            spr = {}
            for blk in re.split(r'(?m)^    - serializedVersion: 2\n', t.split('spriteSheet:', 1)[1])[1:]:
                n = re.search(r'name: (\S+)', blk).group(1)
                r = [int(float(re.search(rf'(?m)^\s+{k}: ([\d.]+)', blk).group(1))) for k in ('x', 'y', 'width', 'height')]
                p = re.search(r'pivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}', blk)
                phys = blk.split('physicsShape:', 1)[1].split('tessellationDetail', 1)[0] if 'physicsShape:' in blk else ''
                pts = [(float(a), float(b)) for a, b in re.findall(r'\{x: ([\d.\-e]+), y: ([\d.\-e]+)\}', phys)]
                spr[n] = (r, (float(p.group(1)), float(p.group(2))), pts)
            self.sheets[png] = (Image.open(png).convert('RGBA'), ids, spr)
        return self.sheets[png]

    def get(self, asset):
        """→ (그림, 피벗 px (왼쪽, 위에서), 그림 칸 집합, 충돌 칸 집합) — 칸은 앵커 칸 기준 (dx, dy)"""
        if asset in self.cache: return self.cache[asset]
        t = open(asset, encoding='utf-8').read()
        fid, guid = sprite_ref(t)
        im, ids, spr = self.sheet(self.png_of[guid])
        (x, y, w, h), (px, py), pts = spr[ids[int(fid)]]
        crop = im.crop((x, im.height - y - h, x + w, im.height - y))
        ox, oy = px * w, py * h                      # 피벗 (왼쪽 · 아래에서)
        vis = cells_of_box(-ox, w - ox, -oy, h - oy)
        col = set()
        if pts:                                      # physicsShape는 사각형 가운데 기준
            xs = [p[0] + w / 2 - ox for p in pts]; ys = [p[1] + h / 2 - oy for p in pts]
            col = cells_of_box(min(xs), max(xs), min(ys), max(ys))
        self.cache[asset] = (crop, (ox, h - oy), vis, col or vis)
        return self.cache[asset]


def sprite_ref(asset_text):
    """타일 에셋의 첫 스프라이트 (fileID, guid) — m_Script 줄은 빼고 (AnimatedTile도 type: 3)"""
    for line in asset_text.splitlines():
        if 'm_Script:' in line: continue
        m = re.search(r'\{fileID: (-?\d+), guid: (\w+), type: 3\}', line)
        if m: return m.groups()
    raise ValueError('스프라이트 없음')


def cells_of_box(x0, x1, y0, y1, need=6):
    def span(a, b):
        return [i for i in range(-8, 9) if min(b, 16 * i + 8) - max(a, 16 * i - 8) >= need]
    return {(i, j) for i in span(x0, x1) for j in span(y0, y1)}


def assets(pat):
    out = sorted(glob.glob(os.path.join(ZP, pat + '.asset')))
    assert out, '타일 없음: ' + pat
    return out


def patch_pieces(kind, sp):
    """ZoneDecal_<kind>_<변형> 무늬 → {변형: [(dx, dy, 에셋)]} (시트 위치 그대로 칸으로)"""
    folder = os.path.join(TILE, 'Dungeon', 'ZoneDecal')
    by = defaultdict(list)
    for a in glob.glob(os.path.join(folder, f'ZoneDecal_{kind}_*.asset')):
        v = os.path.basename(a)[:-6].split('_')[2]
        crop, (ox, oy), _, _ = sp.get(a)
        t = open(a, encoding='utf-8').read()
        fid, guid = sprite_ref(t)
        _, ids, spr = sp.sheet(sp.png_of[guid])
        (x, y, w, h), _, _ = spr[ids[int(fid)]]
        by[v].append((x // 16, y // 16, a))
    out = {}
    for v, ps in by.items():
        mx, my = min(p[0] for p in ps), min(p[1] for p in ps)
        out[v] = [(px - mx, py - my, a) for px, py, a in ps]
    return out


# ---------------------------------------------------------------- 배치
def plan():
    LP = LI.plan()                                 # 광원 (지금 프리팹과 같은 배치 — lights_prev.json과 대조됨)
    owner, water, chest, lab = LP['owner'], LP['water'], LP['chest'], LP['lab']
    cat = LP['cat_rooms']
    sp = Sprites()
    hall_cells = {c for c, n in owner.items() if LI.hall_of(n)}
    floor = hall_cells - water
    is_cat = lambda c: owner.get(c) in cat
    tkey = lambda c: LI.theme_key(lab.get(c, 'N'), is_cat(c))
    H = LI.h

    # 존 문(홀 쪽 입구)에서 걸은 거리 — 존마다 홀 칸 안에서 BFS
    mouths = LI.door_mouths(owner)
    zdist = {}
    for z in {t for k in mouths for t in k.split('-') if re.fullmatch(r'Z\d+', t)}:
        src = {c for k, cs in mouths.items() if z in k.split('-') for c in cs}
        d = {c: 0 for c in src}; q = deque(src)
        while q:
            c = q.popleft()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (c[0] + dx, c[1] + dy)
                if n in hall_cells and n not in d:
                    d[n] = d[c] + 1; q.append(n)
        zdist[z] = d

    def ddist(c):
        t = lab.get(c, 'N')
        return zdist.get(t, {}).get(c, 999) if t != 'N' else 999

    def zone_p(c):
        d = ddist(c)
        return ZONE_P[NEAR] if d <= NEAR else ZONE_P[MID] if d <= MID else 0.0

    def pick_theme(c, salt, force_zone=False):
        """존 소품 비율대로 존 테마 / 폐허(중립) 테마"""
        tk = tkey(c)
        if tk.startswith('N'): return tk
        if force_zone or (H(c, salt, 'zone') % 1000) / 1000 < zone_p(c): return tk
        return 'N-C' if is_cat(c) else 'N-D'

    corridor = {c for c, n in owner.items() if n.startswith('~')}
    mouth_cells = {c for c in hall_cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (c[0] + dx, c[1] + dy) in corridor}
    near_mouth = {(x + dx, y + dy) for (x, y) in mouth_cells for dx in range(-MOUTH_GAP, MOUTH_GAP + 1) for dy in range(-MOUTH_GAP, MOUTH_GAP + 1)}
    chest_set = set(chest)
    near_chest = {(x + dx, y + dy) for (x, y) in chest for dx in range(-CHEST_GAP, CHEST_GAP + 1) for dy in range(-CHEST_GAP, CHEST_GAP + 1)}
    lfloor = set()
    for f in LP['floor']:
        lfloor |= {(f['x'], f['y']), (f['x'], f['y'] + 1)}
    near_lfloor = {(x + dx, y + dy) for (x, y) in lfloor for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    near_water = {(x + dx, y + dy) for (x, y) in water for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    wall_lights = defaultdict(list)                # (홀, 벽 줄 y) → 광원 x
    for p in LP['wall']:
        wall_lights[p['y']].append(p['x'])

    halls = LP['halls']
    walls, blocks, decals = [], [], []
    blocked = set()

    # ---- 벽걸이
    for hall in halls:
        cat_hall = any(is_cat(c) for c in hall_cells if LI.hall_of(owner[c]) == hall)
        rows = 4 if cat_hall else 3
        for (y, s, e) in LI.wall_runs(owner, hall):
            ln = e - s + 1
            if ln < WALL_MIN_RUN: continue
            lx = [x for x in wall_lights[y] if s <= x <= e]
            n = max(1, round(ln / WALL_EVERY)) - len(lx)
            if n <= 0: continue
            taken = set()
            for i in range(n):
                target = s + (i + 0.5) * ln / n
                for x in sorted(range(s + 1, e), key=lambda x: (abs(x - target), x)):
                    c = (x, y)
                    tk = pick_theme(c, 'wall')
                    kinds = THEME[tk]['wall']
                    kind = kinds[H(x, y, 'wk') % len(kinds)]
                    item = wall_item(kind, sp, H(x, y))
                    if item is None: continue
                    w = max(dx for dx, _, _ in item) + 1
                    hgt = max(dy for _, dy, _ in item) + 1
                    if hgt > rows: continue
                    cols = set(range(x, x + w))
                    if max(cols) >= e or any(abs(cx - l) < LIGHT_GAP for cx in cols for l in lx): continue
                    if any(abs(cx - t) < 2 for cx in cols for t in taken): continue
                    if any((cx, y) not in owner or (cx, y + 1) in owner for cx in cols): continue
                    row = 2 if hgt <= 2 else 1
                    if kind.startswith('PIPE:'): row = rows      # 배관은 벽 정면 맨 윗줄
                    for dx, dy, a in item:
                        walls.append({'hall': hall, 'x': x + dx, 'y': y + row + dy, 'asset': a, 'theme': tk})
                    taken |= cols
                    break

    # ---- 블록 묶음
    def footprint(a, anchor):
        _, _, vis, col = sp.get(a)
        return {(anchor[0] + dx, anchor[1] + dy) for dx, dy in col}, {(anchor[0] + dx, anchor[1] + dy) for dx, dy in vis}

    def can_place(F, V, d):
        if not F or any(c not in floor or c in blocked or c in chest_set or c in near_mouth or c in near_chest or c in near_lfloor
                        or c in near_water for c in F):
            return False
        # 한 변 전체가 벽 (d 방향)
        side = [c for c in F if (c[0] + d[0], c[1] + d[1]) not in F]
        if any((c[0] + d[0], c[1] + d[1]) in owner for c in side): return False
        depth = len({c[1] for c in F}) if d[1] else len({c[0] for c in F})
        if depth > MAX_DEPTH: return False
        # 앞쪽 빈 바닥
        for c in F:
            if (c[0] - d[0], c[1] - d[1]) in F: continue
            for k in range(1, FRONT + 1):
                n = (c[0] - d[0] * k, c[1] - d[1] * k)
                if n not in floor or n in blocked or n in chest_set or n in lfloor: return False
        # 벽걸이 광원 바로 아래에는 1칸 높이만
        if len({c[1] for c in V}) > 1:
            top = max(c[1] for c in F)
            if (min(c[0] for c in F), top + 1) not in owner and any(abs(c[0] - lx) <= 1 for c in F for lx in wall_lights[top]):
                return False
        return True

    hall_of_cell = {c: LI.hall_of(owner[c]) for c in hall_cells}
    cor_cells = defaultdict(set)
    for c in corridor:
        cor_cells[owner[c]].add(c)
    door_groups = defaultdict(dict)                       # 홀 → 통로 이름 → 그 통로 칸 전체
    for c in corridor:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (c[0] + dx, c[1] + dy)
            if n in hall_of_cell:
                door_groups[hall_of_cell[n]][owner[c]] = cor_cells[owner[c]]
    region_of = defaultdict(set)
    for c, hl in hall_of_cell.items():
        region_of[hl].add(c)

    def hall_ok(hall):
        groups = list(door_groups[hall].values())
        if len(groups) < 2: return True
        fl = (region_of[hall] | set().union(*groups)) - blocked
        return L.connected(fl, groups)

    base_ok = {hall: hall_ok(hall) for hall in halls}

    def wall_dirs(c):
        return [d for d in ((0, 1), (0, -1), (1, 0), (-1, 0)) if (c[0] + d[0], c[1] + d[1]) not in owner]

    def score(c):
        n8 = sum((c[0] + dx, c[1] + dy) not in owner for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy)
        ds = wall_dirs(c)
        corner = any(a[0] != b[0] and a[1] != b[1] for a in ds for b in ds)
        return (2 if corner else 0) + n8

    def grow(seed, hall, force_zone=False):
        """seed 둘레 벽을 따라 2~4개 묶음. 성공하면 [(에셋, 앵커, 테마)]"""
        ds = wall_dirs(seed)
        if not ds: return None
        d = ds[H(seed, 'd') % len(ds)] if len(ds) > 1 else ds[0]
        want = 2 + H(seed, 'n') % 3
        placed, cells = [], set()
        for i in range(want * 3):
            if len(placed) >= want: break
            tk = pick_theme(seed, ('b', i), force_zone)
            kinds = THEME[tk]['block']
            kind = kinds[H(seed, i, 'bk') % len(kinds)]
            opts = assets(kind)
            a = opts[H(seed, i, 'bv') % len(opts)]
            best = None
            for ax in range(seed[0] - 5, seed[0] + 6):
                for ay in range(seed[1] - 5, seed[1] + 6):
                    F, V = footprint(a, (ax, ay))
                    if not can_place(F, V, d): continue
                    if cells and not any((c[0] + dx, c[1] + dy) in cells for c in F for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): continue
                    if not cells and seed not in F: continue
                    k = (sum(abs(c[0] - seed[0]) + abs(c[1] - seed[1]) for c in F) / len(F), H(ax, ay))
                    if best is None or k < best[0]: best = (k, (ax, ay), F, tk)
            if best:
                _, anc, F, tk2 = best
                placed.append((a, anc, tk2)); cells |= F; blocked.update(F)
        if len(placed) < 2:
            blocked.difference_update(cells); return None
        if base_ok[hall] and not hall_ok(hall):
            blocked.difference_update(cells); return None
        return placed

    for hall in halls:
        region = region_of[hall]
        target = max(1, round(len(region - water) / CELLS_PER_CLUSTER))
        seeds = []
        cand = [c for c in region if c in floor and wall_dirs(c) and c not in near_mouth and c not in near_chest]
        # 1) 존 문 앞 표식: 문에서 4~8u 벽 앞, 그 존 소품만
        for lbl, cs in sorted(mouths.items()):
            if LI.hall_of(owner[cs[0]]) != hall: continue
            z = next(t for t in lbl.split('-') if re.fullmatch(r'Z\d+', t))
            near = [c for c in cand if 4 <= zdist[z].get(c, 99) <= 8 and lab.get(c) == z]
            for c in sorted(near, key=lambda c: (-score(c), H(c))):
                got = grow(c, hall, force_zone=True)
                if got:
                    seeds.append(c); blocks += [dict(hall=hall, asset=a, x=p[0], y=p[1], theme=t, door=lbl) for a, p, t in got]
                    break
        n_door = len(seeds)
        # 2) 나머지: 모서리 · 홈 우선, 묶음 사이 10u
        for c in sorted(cand, key=lambda c: (-score(c), H(c, 'seed'))):
            if len(seeds) - n_door >= target: break       # 문 앞 표식 묶음은 따로 셈 (v02)
            if any(abs(c[0] - s[0]) + abs(c[1] - s[1]) < CLUSTER_GAP for s in seeds): continue
            got = grow(c, hall)
            if got:
                seeds.append(c); blocks += [dict(hall=hall, asset=a, x=p[0], y=p[1], theme=t, door=None) for a, p, t in got]

    # ---- 데칼
    used = set()
    patches = {k: patch_pieces(k, sp) for k in {t['patch'] for t in THEME.values() if t['patch']}}
    near_block = {(x + dx, y + dy) for (x, y) in blocked for dx in range(-3, 4) for dy in range(-3, 4)}

    def density(c):
        d = ddist(c)
        return DECAL_D[NEAR] if d <= NEAR else DECAL_D[MID] if d <= MID else 0.01

    for hall in halls:
        region = sorted(c for c in region_of[hall] if c in floor)
        goal = sum(density(c) for c in region)
        got = 0

        def key(c):   # 가중 무작위 순서: 블록 둘레 · 문 앞일수록 앞쪽
            w = density(c) * (3 if c in near_block or ddist(c) <= 6 else 0.6)
            return ((H(c, 'dk') % 10007 + 1) / 10008) ** (1 / w)
        for c in sorted(region, key=key, reverse=True):
            if got >= goal: break
            if c in blocked or c in used or c in near_chest or c in lfloor: continue
            tk = pick_theme(c, 'decal')
            T = THEME[tk]
            if T['patch'] and H(c, 'pp') % 2 == 0:
                vs = patches[T['patch']]
                v = sorted(vs)[H(c, 'pv') % len(vs)]
                cs = [((c[0] + dx, c[1] + dy), a) for dx, dy, a in vs[v]]
                if all(p in floor and p not in blocked and p not in used and p not in chest_set for p, _ in cs):
                    for p, a in cs:
                        decals.append(dict(hall=hall, asset=a, x=p[0], y=p[1], theme=tk, layer='Decal')); used.add(p)
                    got += len(cs)
                continue
            pool = T['decal'] or THEME['N-C' if is_cat(c) else 'N-D']['decal']
            opts = assets(pool[H(c, 'dkind') % len(pool)])
            a = opts[H(c, 'dv') % len(opts)]
            F, _ = footprint(a, c)
            if all(p in floor and p not in blocked and p not in used and p not in chest_set for p in F):
                decals.append(dict(hall=hall, asset=a, x=c[0], y=c[1], theme=tk, layer='Decor')); used |= F
                got += len(F)

    stats = {}
    for hall in halls:
        n = len(region_of[hall] - water)
        bc = sum(len(footprint(b['asset'], (b['x'], b['y']))[0]) for b in blocks if b['hall'] == hall)
        stats[hall] = dict(cells=n, blocks=sum(1 for b in blocks if b['hall'] == hall), block_cells=bc,
                           walls=len({(w['x'], w['y']) for w in walls if w['hall'] == hall}),
                           decal_cells=sum(1 for d in decals if d['hall'] == hall), conn=hall_ok(hall), base_conn=base_ok[hall])
    return dict(LP=LP, sp=sp, walls=walls, blocks=blocks, decals=decals, stats=stats, blocked=blocked)


def wall_item(kind, sp, salt):
    """벽걸이 한 개 → [(dx, dy, 에셋)] (배관은 1×1 조각을 이어 붙인 3~4u 한 줄)"""
    if kind.startswith('PIPE:'):
        pre = os.path.join(ZP, kind[5:])
        n = 3 + salt % 2
        mids = [pre + PIPE_MIDS[(salt >> (3 * i)) % len(PIPE_MIDS)] + '.asset' for i in range(n - 2)]
        parts = [pre + '_cap_L.asset'] + mids + [pre + '_cap_R.asset']
        assert all(os.path.exists(p) for p in parts), parts
        return [(i, 0, p) for i, p in enumerate(parts)]
    opts = assets(kind)
    a = opts[salt % len(opts)]
    _, _, vis, _ = sp.get(a)
    x0, y0 = min(c[0] for c in vis), min(c[1] for c in vis)
    if (x0, y0) != (0, 0): return None              # 앵커가 왼쪽 아래 칸이 아닌 그림은 건너뜀
    return [(0, 0, a)] + [(dx, dy, None) for dx, dy in vis if (dx, dy) != (0, 0)]


# ---------------------------------------------------------------- 시안
COLOR = LI.COLOR


def draw_items(im, P, box):
    x0, x1, y0, y1 = box
    sp = P['sp']
    items = [(d, 0) for d in P['decals']] + [(w, 1) for w in P['walls'] if w['asset']] + [(b, 1) for b in P['blocks']]
    items.sort(key=lambda t: (t[1], -t[0]['y'], t[0]['x']))
    for it, _ in items:
        if not (x0 - 4 <= it['x'] <= x1 + 4 and y0 - 4 <= it['y'] <= y1 + 4): continue
        crop, (ox, oy), _, _ = sp.get(it['asset'])
        X, Y = (it['x'] - x0) * 16 + 8, (y1 - it['y']) * 16 + 8
        im.alpha_composite(crop, (int(round(X - ox)), int(round(Y - oy))))


def render(P, box):
    lsp = LI.load_sprites()
    return LI.render(P['LP'], box, lsp, under=lambda im: draw_items(im, P, box))


# 지역별 시안: (파일 이름 키, 제목, 홀). 홀마다 ×2 확대 패널
REGIONS = [('sw', '남서 시험 구역', ('H1', 'H2', 'H3', 'H4')),
           ('se', '남동', ('H5', 'H6')),
           ('center', '중앙 하수도 (Catacomb)', ('H7', 'H8', 'H9', 'H10')),
           ('nw', '북서', ('HA', 'HB', 'HC', 'HD', 'HE', 'HF', 'HG'))]
SCALE = 2
ROW_MAX = 4000                     # 패널 줄 폭 (넘으면 다음 줄)
NAME = {'Z1': 'Z1 대장간', 'Z2': 'Z2 드워프 공방', 'Z3': 'Z3 하수도', 'Z4': 'Z4 기관실', 'Z5': 'Z5 광맥', 'Z6': 'Z6 도축업장',
        'Z7': 'Z7 고블린 캠프', 'Z8': 'Z8 연금술방', 'Z9': 'Z9 지하묘지', 'Z10': 'Z10 의무실', 'N-D': '중립 · 폐허 (던전)', 'N-C': '중립 · 폐허 (Catacomb)'}


def hall_box(P, hall, m=3):
    cs = [c for c, n in P['LP']['owner'].items() if LI.hall_of(n) == hall]
    xs, ys = [c[0] for c in cs], [c[1] for c in cs]
    return (min(xs) - m, max(xs) + m, min(ys) - m, max(ys) + m + 2)     # 위쪽은 북쪽 벽 정면까지


def hall_title(P, hall):
    lab, owner, cat = P['LP']['lab'], P['LP']['owner'], P['LP']['cat_rooms']
    cnt = defaultdict(int)
    for c, n in owner.items():
        if LI.hall_of(n) == hall: cnt[LI.theme_key(lab.get(c, 'N'), n in cat)] += 1
    return f'{hall} — ' + ' · '.join(NAME[t].split(' ')[0] if t.startswith('Z') else '중립' for t in sorted(cnt, key=lambda t: -cnt[t]))


def draft(key='all', ver='v03'):
    P = plan()
    for k, title, halls in REGIONS:
        if key in ('all', k):
            draft_region(P, k, title, halls, ver)
    return P


def draft_region(P, key, title, halls, ver):
    out = os.path.join(os.path.dirname(DRAFT), f'hall-decor-{key}-draft.png')
    sp = P['sp']
    panels = []
    for hall in halls:
        im = render(P, hall_box(P, hall))
        panels.append((hall_title(P, hall), im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)))
    rows, cur, rw = [], [], 0
    for p in panels:                                   # 줄 채우기: 폭을 넘으면 다음 줄
        if cur and rw + p[1].width + 24 > ROW_MAX:
            rows.append(cur); cur, rw = [], 0
        cur.append(p); rw += p[1].width + 24
    rows.append(cur)
    pad, head, side = 24, 90, 820
    roww = [sum(p[1].width + pad for p in r) for r in rows]
    rowh = [max(p[1].height for p in r) + 50 for r in rows]
    W = pad * 2 + max(roww) + side
    H = head + sum(rowh) + pad
    canvas = Image.new('RGBA', (W, H), (16, 14, 12, 255))
    d = ImageDraw.Draw(canvas)
    d.text((pad, 44), f'홀 공용 장식 시안 {ver} — {title} (×{SCALE}) · A+B안 (벽걸이 · 데칼 + 벽 붙임 블록) · 가까운 존 무드 → 멀수록 폐허',
           font=LI.font(32), fill=(235, 235, 235), anchor='lm')
    d.rounded_rectangle([W - 190, 18, W - 24, 70], 10, fill=(200, 60, 60))
    d.text((W - 107, 44), ver, font=LI.font(34), fill=(255, 255, 255), anchor='mm')
    zy = head
    for r, rh in zip(rows, rowh):
        zx = pad
        for n, z in r:
            d.text((zx, zy + 18), n, font=LI.font(26), fill=(220, 200, 150), anchor='lm')
            canvas.alpha_composite(z, (zx, zy + 40)); zx += z.width + pad
        zy += rh

    # 오른쪽: 테마별 쓴 소품 · 수치
    sx, sy = W - side + pad, head
    d.text((sx, sy + 10), '테마별로 이 시안에 놓인 소품 (선 색 = 홀 안 테마 경계)', font=LI.font(24), fill=(220, 200, 150), anchor='lm'); sy += 40
    sw_items = [it for it in P['walls'] + P['blocks'] + P['decals'] if it['hall'] in halls and it['asset']]
    for tk in [t for t in THEME if any(it['theme'] == t for it in sw_items)]:
        d.rectangle([sx, sy + 4, sx + 18, sy + 22], fill=COLOR[tk])
        d.text((sx + 28, sy + 13), NAME[tk], font=LI.font(20), fill=(230, 230, 230), anchor='lm'); sy += 30
        for grp, lab in (('walls', '벽걸이'), ('blocks', '블록'), ('decals', '데칼')):
            seen = []
            for it in P[grp]:
                if it['hall'] in halls and it['theme'] == tk and it['asset'] and 'ZoneDecal' not in it['asset']:
                    base = re.sub(r'_\d$', '', os.path.basename(it['asset'])[:-6])
                    if base not in [s[0] for s in seen]: seen.append((base, it['asset']))
            if grp == 'decals' and THEME[tk]['patch'] and any(it['theme'] == tk and 'ZoneDecal' in it['asset'] for it in P['decals'] if it['hall'] in halls):
                seen.append(('ZoneDecal_' + THEME[tk]['patch'], None))
            if not seen: continue
            d.text((sx + 10, sy + 14), lab, font=LI.font(17), fill=(170, 170, 170), anchor='lm')
            xx, rowh = sx + 80, 0
            for base, a in seen:
                if a is None:
                    d.text((xx, sy + 14), base, font=LI.font(15), fill=(200, 200, 200), anchor='lm'); xx += 170; continue
                crop = sp.get(a)[0]
                s = 2 if crop.height <= 24 else 1
                c2 = crop.resize((crop.width * s, crop.height * s), Image.NEAREST)
                if xx + c2.width > W - pad:
                    sy += max(rowh, 28) + 4; xx, rowh = sx + 80, 0
                canvas.alpha_composite(c2, (xx, sy + 14 - min(c2.height, 56) // 2))
                xx += c2.width + 6; rowh = max(rowh, min(c2.height, 56))
            sy += max(rowh, 28) + 6
        sy += 8
    sy += 10
    d.text((sx, sy), '홀별 수치', font=LI.font(24), fill=(220, 200, 150), anchor='lm'); sy += 34
    for hall in halls:
        s = P['stats'][hall]
        conn = '길 연결 OK' if s['conn'] else ('길 끊김!' if s['base_conn'] else '원래 연결 검사 불가')
        d.text((sx, sy), f"{hall}  바닥 {s['cells']} · 블록 {s['blocks']}개 ({s['block_cells']}칸, {s['block_cells'] * 100 / max(1, s['cells']):.1f}%) · "
                         f"벽걸이 {s['walls']}칸 · 데칼 {s['decal_cells']}칸 · {conn}", font=LI.font(18), fill=(210, 210, 210), anchor='lm'); sy += 28
    tot = P['stats']
    allc = sum(s['cells'] for s in tot.values()); allb = sum(s['block_cells'] for s in tot.values())
    d.text((sx, sy + 8), f"(맵 전체 계산: 블록 {sum(s['blocks'] for s in tot.values())}개 · {allb}칸 {allb * 100 / allc:.1f}% · "
                         f"데칼 {sum(s['decal_cells'] for s in tot.values())}칸)", font=LI.font(17), fill=(160, 160, 160), anchor='lm'); sy += 40
    for line in ['규칙: 블록은 한 변 전체가 벽에 붙고 벽에서 2u까지, 2~4개 묶음, 앞쪽 빈 바닥 4u,',
                 '      문 · 통로 입구 3u · 상자 지점 2u · 바닥 광원 1u 밖. 존 문 앞 4~8u에 그 존 묶음 1개 먼저.',
                 '벽걸이: 북쪽 벽 정면, 광원 포함 6u마다 1개 꼴, 광원과 좌우 2u 이상.',
                 '농도: 존 문 12u 안 존 8:폐허 2 · 24u 안 5:5 · 그 밖 폐허만. 데칼 6% · 3% · 1%.',
                 '존 · 통로는 어둡게 눌러 둠 (존 안 소품은 그리지 않음).']:
        d.text((sx, sy), line, font=LI.font(16), fill=(150, 150, 150), anchor='lm'); sy += 24
    canvas = canvas.crop((0, 0, W, max(H, sy + 30)))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    canvas.convert('RGB').save(out)
    print('draft', out, canvas.size)
    for h_ in halls: print(h_, P['stats'][h_])


PREV = os.path.join(HERE, 'decor_prev.json')   # 지금 프리팹에 칠해진 배치 (apply 성공 뒤 decor_next.json을 이 이름으로)


def rel(path):
    return os.path.relpath(path, ROOT).replace('\\', '/')


def cells_of(P):
    """[(층 Decal|Block|Decor, x, y, 타일 경로, w|f)] — w = 벽 정면 칸"""
    out = [('Decor', w['x'], w['y'], rel(w['asset']), 'w') for w in P['walls'] if w['asset']]
    out += [('Block', b['x'], b['y'], rel(b['asset']), 'f') for b in P['blocks']]
    out += [(d['layer'], d['x'], d['y'], rel(d['asset']), 'f') for d in P['decals']]
    keys = [c[:3] for c in out]
    assert len(keys) == len(set(keys)), '같은 층 같은 칸에 두 개'
    return sorted(out)


def apply(mode='paint', out_cs=os.path.join(HERE, '_apply_hall_props.cs')):
    """프리팹 Halls_Props에 칠할 C# 생성. 이전 배치(decor_prev.json)와 프리팹이 같을 때만 바꾼다."""
    P = plan()
    new = cells_of(P)
    prev = [tuple(c) for c in json.load(open(PREV, encoding='utf-8'))] if os.path.exists(PREV) else []
    data = lambda cs: '\\n'.join(' '.join(map(str, c)) for c in cs)     # C# 문자열 안 줄바꿈
    tpl = open(os.path.join(HERE, '_apply_hall_props.template.cs'), encoding='utf-8').read()
    cs = tpl.replace('__MODE__', mode).replace('__NEW__', data(new)).replace('__PREV__', data([c[:4] for c in prev]))
    open(out_cs, 'w', encoding='utf-8', newline='\n').write(cs)
    json.dump([c[:4] for c in new], open(os.path.join(HERE, 'decor_next.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('apply', mode, out_cs, 'new', len(new), 'prev', len(prev))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'draft'
    if cmd == 'draft':
        draft(*sys.argv[2:4])
    elif cmd in ('apply', 'check'):
        apply('paint' if cmd == 'apply' else 'check')
