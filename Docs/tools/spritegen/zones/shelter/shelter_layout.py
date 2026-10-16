"""쉘터 리디자인 배치 — 바닥 · 벽 DungeonDark. 좌상 위(y12~19) = 의뢰소 · 좌상 아래(y3~10) = 창고(ㄴ자) · 우상 = 간이 무기고, 아래쪽은 비움.
방 바닥 x-15~14 × y-7~19 (지금과 같은 크기), 북쪽 벽 3줄 y20~22 (86 · 86_2 · 58), 북쪽 가운데 출격 입구.
길: 출격 도로(x-2~1, y2~19) · 스폰 광장(x-3~2, y-2~1) · 좌우 도로(y-1~1, 벽까지)를 같은 무늬의 어두운 바닥(DungeonDark_Floor_21~25)으로.
좌우 도로 끝은 측벽을 열어 벽 너머로 길이 이어지고(어둠 속으로), 벽 위에 Catacomb 문기둥 둘 + 위에서 본 보 + 문턱.
입구: A안 확정 (2026-10-06) — 입구 아치를 DungeonDark 벽 색으로. 하부(y-7~-3)에 텐트 둘, 창고 관리인 NPC는 추후.
    python shelter_layout.py                     검사만
    python shelter_layout.py cs <shelter_place.cs>   ShelterScene 배치 C# (페인터 ShelterLayout 칠한 뒤)
    python shelter_layout.py draft [시안.png]     DungeonDark 바닥 · 벽 위 시안 (빛은 시안 분위기용)
레이어: Block = 이동 차단, Decor = 충돌 없음 (벽걸이 · 데칼), Fore = 앞에 그림(문기둥). NPC는 씬 오브젝트라 위치만 표시."""
import os as _os, sys as _sys
HERE = _os.path.dirname(_os.path.abspath(__file__))
for p in ('..', _os.path.join('..', '..'), _os.path.join('..', '..', 'lights'), _os.path.join('..', 'catacomb'), _os.path.join('..', 'lab')):
    _sys.path.insert(0, _os.path.abspath(_os.path.join(HERE, p)))
import sys, random, warnings
from collections import deque
warnings.filterwarnings('ignore')
from PIL import Image, ImageDraw

VERSION = 'v04'
FX0, FX1, FY0, FY1 = -15, 14, -7, 19
FLOOR = {(x, y) for x in range(FX0, FX1 + 1) for y in range(FY0, FY1 + 1)}
WALL_FACE = (20, 21, 22)
ARCH_X = (-2, 1)                                                 # 출격 아치 열 (벽걸이 금지)
EXIT = [(-1, 19), (0, 19)]
SPAWN = (0, 0)
ROAD = ({(x, y) for x in range(-2, 2) for y in range(2, 20)} | {(x, y) for x in range(-3, 3) for y in range(-2, 2)}
        | {(x, y) for x in range(FX0, FX1 + 1) for y in range(-1, 2)})        # 보도블럭 (블록 금지)
AISLE = ROAD
NPCS = {'로나 (의뢰)': (-4.5, 18.6), '그룬 (무기)': (9.5, 13.0)}  # 제안 위치 (씬 오브젝트 — 배치 때 옮김)
GATES = [(FX0 - 1, -2), (FX1 + 1, -2)]                           # 측벽 통로 문: 벽 열(x-16 · x15)에 남쪽 기둥 칸 (북쪽 기둥은 y+4)
SIDE_OPEN = range(-1, 2)                                         # 측벽을 여는 줄 (좌우 도로 폭)

NEW = 'Shelter'
SHEET_OF = {}
BLOCK, DECOR, HANG, FORE, SILLS = [], [], [], [], []


def b(sh, n, x, y): BLOCK.append((n, x, y)); SHEET_OF[n] = sh
def d(sh, n, x, y): DECOR.append((n, x, y)); SHEET_OF[n] = sh
def h(sh, n, x, y): HANG.append((n, x, y)); SHEET_OF[n] = sh


FO, DW, BU, IN, LP, TS = 'Forge', 'DwarfWorkshop', 'Butchery', 'Infirmary', 'LabProps', 'TailorShop'

# ======== 좌상 위 절반 (y12~19): 의뢰소 — 입구 바로 옆 벽걸이 게시판 + 그 앞 로나, 원래 장식은 왼쪽 ========
h(NEW, 'Wall_QuestBoard', -6, 20)                                # 입구(아치 x-2) 바로 왼쪽
h(LP, 'Sconce_double', -8, 21)
h(TS, 'Wall_Banner_1', -11, 20)                                  # 길드 깃발
h(LP, 'WallShelf_books', -14, 21)
b(LP, 'Bookshelf_scrolls_2', -15, 19)
b(NEW, 'QuestBoard_Stand', -15, 15)                              # 세운 게시판 (새 의뢰)
b(LP, 'Candelabra_iron3', -12, 15)
b(LP, 'Desk_map', -11, 14)                                       # 의뢰 정리 책상
b(LP, 'Chair_chair', -9, 15)
b(LP, 'Books_stack', -10, 16)
for x in (-13, -12):                                             # 기다리는 의자
    b(LP, 'Chair_stool', x, 12)

# ======== 좌상 아래 절반 (y3~10): 창고 — 서벽을 따라 내려와 아래로 꺾이는 ㄴ자 (가운데는 비움) ========
# ㄴ 세로획 (서벽, x-15~-14, y5~10)
b(BU, 'Barrel_Lid', -15, 10); b(BU, 'Crate', -14, 10)
b(BU, 'Barrel_Salt', -15, 9); b(BU, 'Sack', -14, 9)
b(IN, 'MedCrate_Open_2x2', -15, 7)
b(BU, 'Sack_Pile', -15, 6)
b(BU, 'Crate_Large', -15, 5)
# ㄴ 가로획 (아래, y3~4, x-15~-6) — 가운데에 창고 궤짝(상호작용, 앞 y2)
b(BU, 'Crate_Large', -15, 3); b(BU, 'Barrel_Lid', -15, 4); b(BU, 'Crate', -14, 4); b(BU, 'Sack', -13, 4)
b(BU, 'Candles_A', -13, 3)
b(NEW, 'StorageChest', -12, 3)
b(BU, 'Candles_A', -10, 3)
b(BU, 'Firewood', -9, 3); b(BU, 'Crate', -7, 3); b(BU, 'Barrel_Salt', -6, 3)
b(BU, 'Sack', -8, 4)

# ======== 우상: 간이 무기고 — 드워프 기계장치 뺌, 벽걸이 한 칸 위로 ========
h(FO, 'WallWeaponRack', 4, 21)
h(TS, 'Wall_Shield_1', 7, 22)
h(TS, 'Wall_Shield_2', 9, 22)
h(FO, 'WallWeaponRack', 11, 21)
h(LP, 'Sconce_single', 14, 22)
b(FO, 'Workbench_Large', 9, 15)                                  # 그룬 작업대 (그룬은 작업대 앞 남쪽)
b(NEW, 'Anvil_S', 10, 11)                                       # 그룬 오른쪽 작은 모루 (1x1, 강화 연출에서 망치가 닿는 자리)
b(FO, 'QuenchBarrel', 14, 15)
b(DW, 'Shell_Filler_1', 7, 15)                                   # 산탄 충전기
b(FO, 'WeaponRack_Spears', 13, 11)
b(FO, 'WeaponRack_Swords', 13, 7)
b(DW, 'Shot_Crate_1', 5, 11)
b(DW, 'Shot_Crate_3', 7, 11)
b(DW, 'Powder_Keg', 5, 10)
b(DW, 'Powder_Crate_1', 6, 10)
b(TS, 'Mannequin_1', 14, 19)
b(TS, 'Armor_Pile_1', 11, 9)

# ======== 하부: 텐트 둘 (Z7 텐트 그대로, 문은 남쪽) — 창고 관리인 NPC는 추후 ========
b(TS, 'Tent_Frame_1', -11, -5)
b(TS, 'Tent_Frame_2', 7, -5)

# ======== 가운데: 좌우 도로 위 Catacomb 옆면 아치, 출격 도로 양옆 촛대 ========
for (x, y) in GATES:                                             # 측벽 통로 문: 벽 열에 문기둥 둘 + 위에서 본 보(벽 위에 그림) + 문턱
    FORE.append(('CatacombGatePost', x, y + 4)); FORE.append(('GateLintel', x, y + 3)); FORE.append(('CatacombGatePost', x, y))
    SILLS.append((x, -1))
SHEET_OF['CatacombGatePost'] = 'Gate'; SHEET_OF['GateLintel'] = 'Gate'
for (x, y) in ((-4, 6), (2, 6), (-4, 13), (2, 13)):
    b(LP, 'Candelabra_iron3', x, y)


# ---------------------------------------------------------------- 항목 (그림 · 칸 · 충돌)
_items = None


def items():
    global _items
    if _items is None:
        import shelter_props as G
        import r7_layout as R7
        _items = {it['id']: dict(fin=it['fin'], cells=(it['cv'].w // 16, it['cv'].h // 16), coll=it['coll']) for it in G.ITEMS}
        import shelter_gate as GT
        post = GT.PROPS.crop((128, 0, 144, 48))
        pad = Image.new('RGBA', (16, 52), (0, 0, 0, 0)); pad.paste(post, (0, 0))

        class Fin0:
            def __init__(s, im): s.im = im
            def img(s): return s.im
        _items['CatacombGatePost'] = dict(fin=Fin0(pad), cells=(1, 3), coll=None)
        band = GT.PROPS.crop((16, 0, 112, 16)).rotate(90, expand=True).crop((0, 0, 16, 64))   # 아치 보를 위에서 본 것 (윗모서리 빛이 왼쪽)
        lp = Image.new('RGBA', (16, 68), (0, 0, 0, 0)); lp.paste(band, (0, 0))
        _items['GateLintel'] = dict(fin=Fin0(lp), cells=(1, 4), coll=None)

        class Fin:
            def __init__(s, im): s.im = im
            def img(s): return s.im
        for n, sh in SHEET_OF.items():
            if sh in (NEW, 'Gate'):
                continue
            i = R7.info(sh, n)
            tx = Image.open(i['png']).convert('RGBA')
            x, y, w, hh = i['rect']
            _items[n] = dict(fin=Fin(tx.crop((x, tx.height - y - hh, x + w, tx.height - y))), cells=(i['cells_w'], i['cells_h']), coll=i['coll'])
    return _items


def coll_cells(n, x, y):
    c = items()[n]['coll']
    if not c:
        return set()
    if len(c) == 2:
        c = (0, 0) + tuple(c)
    dx, dy, w, hh = c
    return {(x + dx + i, y + dy + j) for i in range(w) for j in range(hh)}


def vis_cells(n, x, y):
    w, hh = items()[n]['cells']
    return {(x + i, y + j) for i in range(w) for j in range(hh)}


def check():
    errs, warns = [], []
    solid, vis = {}, {}
    for (n, x, y) in BLOCK:
        tag = '%s@(%d,%d)' % (n, x, y)
        cc = coll_cells(n, x, y)
        for c in cc:
            if c in solid:
                errs.append('충돌 겹침 %s %s ↔ %s' % (c, tag, solid[c]))
            solid[c] = tag
            if c not in FLOOR:
                errs.append('바닥 밖 충돌 %s %s' % (tag, c))
            if c in AISLE:
                errs.append('통로 침범 %s %s' % (tag, c))
        for c in vis_cells(n, x, y):
            if c in vis and c in cc:
                errs.append('그림 겹침 %s %s ↔ %s' % (c, tag, vis[c]))
            vis.setdefault(c, tag)
    for (x, y) in GATES:                                          # 아치 기둥 (남 · 북) = 이동 차단
        for c in ((x, y), (x, y + 4)):
            if c in solid:
                errs.append('아치 기둥 겹침 %s' % (c,))
            solid[c] = 'Arch'
            if c in ROAD:
                errs.append('아치 기둥이 도로 위 %s' % (c,))
    for (n, x, y) in DECOR:
        for c in vis_cells(n, x, y):
            if c in solid:
                errs.append('데칼이 소품 밑에 깔림 %s@(%d,%d) %s' % (n, x, y, c))
    occ = {}
    for (n, x, y) in HANG:
        for c in vis_cells(n, x, y):
            if c in occ:
                errs.append('벽걸이 겹침 %s %s ↔ %s' % (c, n, occ[c]))
            occ[c] = n
            if c[1] not in WALL_FACE or not (FX0 <= c[0] <= FX1) or ARCH_X[0] <= c[0] <= ARCH_X[1]:
                errs.append('벽면 밖 벽걸이 %s %s' % (n, c))
    for nm, (nx, ny) in NPCS.items():
        cell = (int(nx // 1), int(ny // 1))
        if cell in solid:
            errs.append('NPC 자리 막힘 %s %s' % (nm, cell))
    free = FLOOR - set(solid)
    seen, q = {SPAWN}, deque([SPAWN])
    while q:
        cx, cy = q.popleft()
        for dd in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nb = (cx + dd[0], cy + dd[1])
            if nb in free and nb not in seen:
                seen.add(nb); q.append(nb)
    for c in EXIT:
        if c not in seen:
            errs.append('출격 아치 도달 불가')
    for nm, (nx, ny) in NPCS.items():
        cell = (int(nx // 1), int(ny // 1))
        if not any((cell[0] + a, cell[1] + bb) in seen for a in (-1, 0, 1) for bb in (-1, 0, 1)):
            errs.append('NPC 앞 도달 불가 %s' % nm)
    chest_front = [(-12, 2), (-11, 2)]
    if not any(c in seen for c in chest_front):
        errs.append('창고 궤짝 앞 도달 불가')
    if free - seen:
        warns.append('갇힌 빈칸 %d: %s' % (len(free - seen), sorted(free - seen)[:8]))
    sol = lambda c: c not in free
    slots = [c for c in free if (sol((c[0] - 1, c[1])) and sol((c[0] + 1, c[1]))) or (sol((c[0], c[1] - 1)) and sol((c[0], c[1] + 1)))]
    if slots:
        warns.append('1칸 틈 %d곳: %s' % (len(slots), sorted(slots)))
    return errs, warns, len(solid)


# ---------------------------------------------------------------- 시안 (DungeonDark 바닥 · 벽 + 보도블럭 + 입구)
def background(box, entrance='A'):
    import light_table as LT
    import room_draft as RD
    import shelter_gate as GT
    from bl import Cv
    X0, Y0, X1, Y1 = box
    W, Hh = (X1 - X0 + 1) * 16, (Y1 - Y0 + 1) * 16
    im = Image.new('RGBA', (W, Hh), (30, 22, 17, 255))
    cache = {}

    def blit(sp, pvx, pvy, x, y):
        left = int(round((x - X0 + 0.5) * 16 - pvx))
        top = int(round((Y1 + 1 - y - 0.5) * 16 - (sp.height - pvy)))
        tmp = Image.new('RGBA', im.size, (0, 0, 0, 0))
        tmp.paste(sp, (left, top), sp)
        im.alpha_composite(tmp)

    def put(name, x, y, fx=None):
        if name not in cache:
            try:
                sp, piv, _ = LT.tile_sprite(name)
                cache[name] = (sp, piv)
            except AssertionError:                               # Dark 판에 없는 조각 — 시안에서는 비움
                cache[name] = None
        if cache[name] is None:
            return
        sp, (pvx, pvy) = cache[name]
        blit(fx(sp) if fx else sp, pvx, pvy, x, y)
    r = random.Random(7)
    var = ['DungeonDark_Floor_8'] * 30 + ['DungeonDark_Floor_%d' % k for k in (9, 10, 11, 12, 16, 17, 18, 19, 20)]
    for y in range(FY0, FY1 + 1):
        for x in range(FX0, FX1 + 1):
            put(r.choice(var), x, y)
    # 길: 일반 바닥과 같은 무늬의 어두운 판 (DungeonDark_Floor_21~25, 밝기 약 33 — 일반 38)
    path = ['DungeonDark_Floor_21'] * 6 + ['DungeonDark_Floor_%d' % k for k in (22, 23, 24, 25)]
    for (x, y) in sorted(ROAD):
        put(r.choice(path), x, y)
    W_ = lambda i: 'DungeonDark_Wall_%s' % i
    for y in range(FY0 + 1, FY1 + 1):
        if y in SIDE_OPEN:
            continue
        put(W_(111), FX0 - 1, y); put(W_(114), FX1 + 1, y)
    from PIL import ImageEnhance
    for y in SIDE_OPEN:                                          # 측벽 통로: 벽 너머로 이어지는 길 바닥, 멀수록 어둠
        for k, dk in enumerate((0.72, 0.45, 0.22)):
            for x in (FX0 - 1 - k, FX1 + 1 + k):
                name = 'DungeonDark_Floor_2%d' % (1 + (x * 3 + y) % 5)
                if name not in cache:
                    sp, piv, _ = LT.tile_sprite(name); cache[name] = (sp, piv)
                sp, (pvx, pvy) = cache[name]
                blit(ImageEnhance.Brightness(sp).enhance(dk), pvx, pvy, x, y)
    for x in (FX0 - 2, FX0 - 3, FX1 + 2, FX1 + 3):               # 통로 옆 벽 (위 · 아래)
        put(W_(161), x, SIDE_OPEN[0] - 1)
        put(W_(86), x, SIDE_OPEN[-1] + 1)
    put(W_(112), FX0, FY1); put(W_(113), FX1, FY1)
    put(W_(136), FX0 - 1, FY0); put(W_(137), FX0, FY0); put(W_(138), FX1, FY0); put(W_(139), FX1 + 1, FY0)
    put(W_(160), FX0 - 1, FY0 - 1); put(W_(160), FX0, FY0 - 1)
    for x in range(FX0 + 1, FX1):
        put(W_(161), x, FY0 - 1)
    put(W_(162), FX1, FY0 - 1); put(W_(163), FX1 + 1, FY0 - 1)
    cat = range(-5, 5) if entrance == 'B' else range(0)          # B안: 입구 둘레 Catacomb 벽 키트 (x-5~4)
    for x in range(FX0 - 1, FX1 + 2):
        if ARCH_X[0] <= x <= ARCH_X[1] or x in cat:
            continue
        if x == FX0 - 1:
            put(W_(84), x, 20); put(W_(111), x, 21); put(W_(57), x, 22)
        elif x == FX1 + 1:
            put(W_(88), x, 20); put(W_(114), x, 21); put(W_(60), x, 22)
        else:
            put(W_(86), x, 20); put(W_('86_2'), x, 21); put(W_(58), x, 22)
    if entrance == 'B':
        C_ = lambda i: 'CatacombWall_%s' % i
        for x in cat:
            if ARCH_X[0] <= x <= ARCH_X[1]:
                continue
            if x == cat[0]:
                ids = ('130', '130_2', '130_3', '106')
            elif x == cat[-1]:
                ids = ('132', '132_2', '132_3', '109')
            else:
                ids = ('86', '86_2', '86_3', '58')
            for k, i in enumerate(ids):
                put(C_(i), x, 20 + k)
    m = RD.load_scene(_os.path.abspath(_os.path.join(HERE, '..', '..', '..', '..', '..', 'Backups', 'room_tiles_shelter.txt')))
    rec = GT.recolor if entrance == 'A' else None                # 'now' · 'B' = 아치 원래 색
    for (x, y), (tn, info) in m['Grid/Wall Tilemap'].items():
        if ARCH_X[0] <= x <= ARCH_X[1] and y >= 19 and tn.startswith('DungeonSheet'):
            put(tn, x, y, rec)
    return im


def compose(entrance='A', with_props=True, lights=True):
    import draftlib as DL
    its = items()
    box = (FX0 - 4, FY0 - 2, FX1 + 4, 26)
    X0, Y0, X1, Y1 = box
    im = background(box, entrance)

    def at(img, x, y):
        tmp = Image.new('RGBA', im.size, (0, 0, 0, 0))
        tmp.paste(img, (int(round(x * 16 - X0 * 16)), int(round((Y1 + 1) * 16 - y * 16 - img.height))), img)
        im.alpha_composite(tmp)
    if with_props:
        import shelter_gate as GT
        sill = GT.PROPS.crop((240, 24, 256, 72))                      # 세로 문턱 (도로 폭 3칸에 맞춰 가운데 48px)
        for (x, y) in SILLS:
            at(sill, x, y)
        for (n, x, y) in HANG + DECOR:
            at(its[n]['fin'].img(), x, y - 4 / 16)
        for (n, x, y) in sorted(BLOCK, key=lambda t: -t[2]):
            at(its[n]['fin'].img(), x, y - 4 / 16)
        for (n, x, y) in FORE:
            at(its[n]['fin'].img(), x, y - 4 / 16)
    if lights:
        warm = (255, 180, 90)
        src = []
        for (n, x, y) in BLOCK + HANG:
            if n.startswith(('Candles', 'Candelabra', 'Sconce')):
                src.append((int((x + 0.5 - X0) * 16), int((Y1 + 1 - y - 1.2) * 16), 52, warm, 90))
        im = DL.light(im, src)
    return im, box


def draft(out):
    import draftlib as DL
    im, box = compose('A')
    X0, Y0, X1, Y1 = box
    dr = ImageDraw.Draw(im)
    secs = [('의뢰소', (-15, 12, -4, 19), (230, 200, 120)), ('간이 무기고', (3, 3, 14, 19), (230, 140, 90)), ('창고', (-15, 3, -4, 10), (140, 190, 230))]
    for nm, (a, b_, c_, d_), col in secs:
        x0, y0 = (a - X0) * 16, (Y1 + 1 - d_ - 1) * 16
        x1, y1 = (c_ + 1 - X0) * 16 - 1, (Y1 + 1 - b_) * 16 - 1
        for k in range(x0, x1, 6):
            dr.line((k, y0, k + 2, y0), fill=col + (200,)); dr.line((k, y1, k + 2, y1), fill=col + (200,))
        for k in range(y0, y1, 6):
            dr.line((x0, k, x0, k + 2), fill=col + (200,)); dr.line((x1, k, x1, k + 2), fill=col + (200,))
    for nm, (nx, ny) in NPCS.items():
        px, py = (nx - X0) * 16, (Y1 + 1 - ny) * 16
        dr.ellipse((px - 6, py - 6, px + 6, py + 6), outline=(255, 230, 120, 255), width=2)
    px, py = (SPAWN[0] + 0.5 - X0) * 16, (Y1 + 1 - SPAWN[1] - 0.5) * 16
    dr.ellipse((px - 7, py - 7, px + 7, py + 7), outline=(120, 220, 255, 255), width=2)
    S = 2
    big = im.resize((im.width * S, im.height * S), Image.NEAREST)
    # 입구 비교: 지금 · A안 · B안 (북쪽 벽 x-8~7, y17~26, 3배)
    crops = []
    full, _ = compose('A', with_props=True, lights=True)
    for (cx, cy, label) in ((-8, 26, '출격 입구 (A안: 아치를 DungeonDark 벽 색으로)'), (-19, 7, '서쪽 측벽 통로 문'), (3, 7, '동쪽 측벽 통로 문')):
        cx0, cy0 = (cx - X0) * 16, (Y1 + 1 - cy) * 16
        crop = full.crop((cx0, cy0, cx0 + 16 * 16, cy0 + 10 * 16)).resize((16 * 16 * 3 // 2, 10 * 16 * 3 // 2), Image.NEAREST)
        crops.append((crop, label))
    cw = sum(c.width for c, _ in crops) + 20 * (len(crops) + 1)
    ch = crops[0][0].height + 40
    _, _, ns = check()
    hdr = Image.new('RGBA', (max(big.width, cw), 64), (24, 20, 18, 255))
    hd = ImageDraw.Draw(hdr)
    hd.text((14, 8), '쉘터 리디자인 배치 시안 %s  ·  입구 A안 · 좌상 위 의뢰소 · 좌상 아래 창고(ㄴ자) · 우상 무기고 · 하부 텐트 2 · 측벽 통로 문' % VERSION, font=DL.load_font(19), fill=(255, 196, 60, 255))
    hd.text((14, 36), '노란 원 = NPC (로나: 입구 옆 게시판 앞 · 그룬) · 하늘색 원 = 스폰 · 점선 = 섹션 · 아래 = 입구 · 측벽 통로 문 확대 · 장애물 %d칸' % ns, font=DL.load_font(15), fill=(220, 220, 220, 255))
    page = Image.new('RGBA', (max(big.width, cw), 64 + big.height + ch + 30), (24, 20, 18, 255))
    page.paste(hdr, (0, 0)); page.paste(big, (0, 64))
    pd = ImageDraw.Draw(page)
    x = 20
    yb = 64 + big.height + 10
    for crop, label in crops:
        pd.text((x, yb), label, font=DL.load_font(15), fill=(255, 220, 150, 255))
        page.paste(crop, (x, yb + 26))
        x += crop.width + 20
    page.save(out)
    return page.size


PASSAGE = {(x, y) for y in range(-1, 2) for x in list(range(FX0 - 5, FX0)) + list(range(FX1 + 1, FX1 + 6))}   # 측벽 너머 통로 (페인터 Shelter-W · E, 막힌 끝 칸까지)
PASSAGE_DIM = {1: 0.92, 2: 0.82, 3: 0.72, 4: 0.62, 5: 0.52}       # 벽 열에서 몇 칸 바깥인지 → 바닥 밝기 (벽 조각은 그대로 — 어둡게 하면 사각형으로 튐)


def cs():
    """ShelterScene 배치 C# (unity eval_file). 페인터로 칠한 뒤 한 번 실행. 소품 타일맵이 이미 있으면 멈춤 (손 배치 보호).
    입구 A안 타일 · 길(어두운 바닥) · 통로 어둠 · 옛 가구(Object Tilemap) 지우기 · 소품 타일맵 2개 · 측벽 문 · NPC 이동 · 씬 저장"""
    import shelter_props as SP
    L = ['// shelter_layout.py가 만든 쉘터 배치 (%s). ShelterScene에서, 페인터(ShelterLayout) 칠한 뒤 실행.' % VERSION,
         'var sb = new System.Text.StringBuilder();',
         'var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();',
         'if (!scene.path.EndsWith("ShelterScene.unity")) return "abort: active scene " + scene.path;',
         'var grid = UnityEngine.GameObject.Find("Grid");',
         'System.Func<string, UnityEngine.Tilemaps.Tilemap> TM = n => { var t = grid.transform.Find(n); return t ? t.GetComponent<UnityEngine.Tilemaps.Tilemap>() : null; };',
         'var ground = TM("Ground Tilemap"); var wall = TM("Wall Tilemap"); var obj = TM("Object Tilemap");',
         'if (TM("Shelter Block") != null || TM("Shelter Decor") != null || UnityEngine.GameObject.Find("Shelter Gates") != null) return "abort: already placed";',
         'System.Func<string, UnityEngine.Tilemaps.TileBase> F = name => { var g = UnityEditor.AssetDatabase.FindAssets(name + " t:TileBase"); foreach (var id in g) { var pth = UnityEditor.AssetDatabase.GUIDToAssetPath(id); if (System.IO.Path.GetFileNameWithoutExtension(pth) == name) return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(pth); } return null; };',
         'System.Func<string, string, UnityEngine.Tilemaps.TileBase> Z = (sheet, n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/04.Sprites/Tile/ZoneProps/" + sheet + "/" + n + ".asset");',
         'int miss = 0;',
         'System.Action<UnityEngine.Tilemaps.Tilemap, UnityEngine.Tilemaps.TileBase, int, int, string> SET = (tm, t, x, y, n) => { if (t == null) { miss++; sb.Append("missing " + n + "; "); return; } tm.SetTile(new UnityEngine.Vector3Int(x, y, 0), t); };',
         '// 1) 출격 입구 A안 (Catacomb 아치 조각 → DungeonDark 벽 색 조각, 충돌 없음)']
    for (x, y, n) in SP.ARCH:
        L.append('SET(wall, Z("Shelter", "ShelterArch_%d"), %d, %d, "ShelterArch_%d");' % (n, x, y, n))
    L.append('// 2) 길: 같은 무늬의 어두운 바닥 (DungeonDark_Floor_21~25)')
    L.append('var path = new UnityEngine.Tilemaps.TileBase[] { F("DungeonDark_Floor_21"), F("DungeonDark_Floor_22"), F("DungeonDark_Floor_23"), F("DungeonDark_Floor_24"), F("DungeonDark_Floor_25") };')
    L.append('System.Func<int, int, UnityEngine.Tilemaps.TileBase> PT = (x, y) => { int h = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff; return (h % 10) < 6 ? path[0] : path[1 + h % 4]; };')
    cells = sorted(ROAD | PASSAGE)
    L.append('var road = new int[] { %s };' % ', '.join('%d, %d' % c for c in cells))
    L.append('for (int i = 0; i < road.Length; i += 2) SET(ground, PT(road[i], road[i + 1]), road[i], road[i + 1], "path");')
    L.append('// 3) 측벽 너머 통로 어둠 (바닥만 단계적으로 어둡게)')
    L.append('System.Action<UnityEngine.Tilemaps.Tilemap, int, int, float> DIM = (tm, x, y, k) => { var c = new UnityEngine.Vector3Int(x, y, 0); if (!tm.HasTile(c) || tm.GetTile(c).name.EndsWith("_56")) return;   /* 바깥 어둠 타일은 그대로 */ tm.SetTileFlags(c, UnityEngine.Tilemaps.TileFlags.None); tm.SetColor(c, new UnityEngine.Color(k, k, k, 1f)); };')
    for k, f in PASSAGE_DIM.items():
        for x in (FX0 - k, FX1 + k):
            for y in SIDE_OPEN:
                L.append('DIM(ground, %d, %d, %.2ff);' % (x, y, f))
    L += ['// 4) 옛 가구 지우기 (Object Tilemap)',
          'int old = 0; foreach (var c in obj.cellBounds.allPositionsWithin) if (obj.GetTile(c) != null) { obj.SetTile(c, null); old++; }',
          'sb.Append("old furniture cleared=" + old + "; ");',
          '// 5) 소품 타일맵 2개: Z9 방 프리팹의 Block · Decor 설정(정렬 · 충돌 · 레이어)을 복사',
          'var z9 = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/03.Prefabs/Environment/Rooms/Z9_Catacomb.prefab");',
          'UnityEngine.GameObject srcB = null, srcD = null; foreach (UnityEngine.Transform t in z9.transform) { if (t.name == "Z9 Block") srcB = t.gameObject; if (t.name == "Z9 Decor") srcD = t.gameObject; }',
          'var blockGo = UnityEngine.Object.Instantiate(srcB, grid.transform); blockGo.name = "Shelter Block";',
          'var decorGo = UnityEngine.Object.Instantiate(srcD, grid.transform); decorGo.name = "Shelter Decor";',
          'UnityEditor.PrefabUtility.UnloadPrefabContents(z9);',
          'var block = blockGo.GetComponent<UnityEngine.Tilemaps.Tilemap>(); var decor = decorGo.GetComponent<UnityEngine.Tilemaps.Tilemap>();',
          'block.ClearAllTiles(); decor.ClearAllTiles(); blockGo.transform.localPosition = UnityEngine.Vector3.zero; decorGo.transform.localPosition = UnityEngine.Vector3.zero;',
          'UnityEditor.Undo.RegisterCreatedObjectUndo(blockGo, "Shelter Block"); UnityEditor.Undo.RegisterCreatedObjectUndo(decorGo, "Shelter Decor");']
    for (n, x, y) in BLOCK:
        L.append('SET(block, Z("%s", "%s"), %d, %d, "%s");' % (SHEET_OF[n], n, x, y, n))
    for (n, x, y) in HANG + DECOR:
        L.append('SET(decor, Z("%s", "%s"), %d, %d, "%s");' % (SHEET_OF[n], n, x, y, n))
    L += ['block.CompressBounds(); decor.CompressBounds();',
          '{ var col = block.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>(); if (col != null) col.ProcessTilemapChanges(); var comp = block.GetComponent<UnityEngine.CompositeCollider2D>(); if (comp != null) { comp.GenerateGeometry(); sb.Append("block paths=" + comp.pathCount + "; "); } }',
          '// 6) 측벽 통로 문: 벽 열에 문기둥 둘(커넥터 프리팹) + 위에서 본 보 + 문턱 + 통로 막는 충돌',
          'var gates = new UnityEngine.GameObject("Shelter Gates"); UnityEditor.Undo.RegisterCreatedObjectUndo(gates, "Shelter Gates");',
          'var postPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/03.Prefabs/Environment/CatacombGatePost.prefab");',
          'var sheetSprites = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath("Assets/04.Sprites/Tile/ZoneProps/Shelter/Shelter.png");',
          'System.Func<string, UnityEngine.Sprite> SP = n => { foreach (var o in sheetSprites) if (o is UnityEngine.Sprite s && s.name == n) return s; return null; };',
          'System.Action<string, UnityEngine.Sprite, float, float, string, int> SR = (name, sp, x, y, layer, order) => { var go = new UnityEngine.GameObject(name); go.transform.SetParent(gates.transform); go.transform.position = new UnityEngine.Vector3(x, y, 0); var r = go.AddComponent<UnityEngine.SpriteRenderer>(); r.sprite = sp; r.sortingLayerName = layer; r.sortingOrder = order; if (sp == null) { miss++; sb.Append("missing sprite " + name + "; "); } };']
    for (x, y) in GATES:
        side = 'W' if x < 0 else 'E'
        cx = x + 0.5
        L += ['{ var n = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(postPrefab, gates.transform); n.name = "Gate Post %s N"; n.transform.position = new UnityEngine.Vector3(%.1ff, %d, 0); }' % (side, cx, y + 4),
              'SR("Gate Lintel %s", SP("GateLintel"), %.1ff, %.1ff, "Foreground", 1);' % (side, cx, y + 3 + 0.5),
              '{ var s_ = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(postPrefab, gates.transform); s_.name = "Gate Post %s S"; s_.transform.position = new UnityEngine.Vector3(%.1ff, %d, 0); s_.GetComponentInChildren<UnityEngine.SpriteRenderer>().sortingOrder = 2; }' % (side, cx, y),
              'SR("Gate Sill %s", SP("GateSill"), %.1ff, -0.5f, "Background", 4);' % (side, cx),
              '{ var b_ = new UnityEngine.GameObject("Gate Block %s"); b_.transform.SetParent(gates.transform); b_.layer = wall.gameObject.layer; b_.transform.position = new UnityEngine.Vector3(%.1ff, 0.5f, 0); var bc = b_.AddComponent<UnityEngine.BoxCollider2D>(); bc.size = new UnityEngine.Vector2(1f, 3f); }' % (side, cx)]
    L.append('// 7) NPC 이동 (로나: 입구 옆 게시판 앞 · 그룬: 작업대 앞)')
    for nm, obj in (('로나 (의뢰)', 'NPC_Lona'), ('그룬 (무기)', 'NPC_Grun')):
        x, y = NPCS[nm]
        L.append('{ var g_ = UnityEngine.GameObject.Find("NPC/%s"); if (g_ != null) { UnityEditor.Undo.RecordObject(g_.transform, "move"); g_.transform.position = new UnityEngine.Vector3(%.2ff, %.2ff, 0); } else sb.Append("missing %s; "); }' % (obj, x, y, obj))
    L += ['UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);',
          'UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);',
          'sb.Append("block=" + %d + " decor=" + %d + " arch=" + %d + " road=" + road.Length / 2 + " miss=" + miss + " saved");' % (len(BLOCK), len(HANG + DECOR), len(SP.ARCH)),
          'return sb.ToString();']
    return '\n'.join(L) + '\n'


if __name__ == '__main__':
    errs, warns, ns = check()
    print('block', len(BLOCK), 'decor', len(DECOR), 'hang', len(HANG), '장애물', ns)
    for e in errs: print('오류', e)
    for w in warns: print('경고', w)
    print('오류', len(errs), '경고', len(warns))
    if len(sys.argv) > 2 and sys.argv[1] == 'cs':
        if errs:
            sys.exit('검사 오류가 있어 C#을 만들지 않음')
        open(sys.argv[2], 'w', encoding='utf8').write(cs())
        print(sys.argv[2], '생성')
    if len(sys.argv) > 1 and sys.argv[1] == 'draft':
        print(draft(sys.argv[2] if len(sys.argv) > 2 else 'shelter-layout-draft.png'))
