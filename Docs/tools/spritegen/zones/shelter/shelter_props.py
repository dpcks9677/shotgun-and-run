"""쉘터 리디자인 소품 — 퀘스트 게시판 2종 · 창고 궤짝. 모두 탑다운 3/4 투영 (윗면 + 앞면만, 동 · 서 옆면 없음).
    python shelter_props.py <시안.png>      소품 시안
    python ../../check.py shelter_props.py  자동 검사
    python shelter_props.py import          (승인 후) Assets/04.Sprites/Tile/ZoneProps/Shelter/ 에 시트 · meta · 타일 생성

- Wall_QuestBoard (64x48, 벽걸이): 북쪽 벽(Dungeon 테마 3줄)에 거는 의뢰 게시판. 틀이 벽에서 3px · 차양 판자가 7px 튀어나옴 → 윗면 띠
- QuestBoard_Stand (48x48, 블록 3x1): 기둥 두 개에 세운 게시판. 기둥머리 · 차양 윗면, 게시판 윗변 두께, 발치 돌
- StorageChest (32x32, 블록 2x2): 쇠테 두른 창고 궤짝. 뚜껑 윗면(판자 + 쇠테) 크게, 앞면 띠 + 자물쇠
- ShelterArch_<번호> (16x16): 출격 입구 아치 조각(Catacomb 시트 DungeonSheet_<번호>)을 DungeonDark 벽 색으로 바꾼 것 (입구 A안). 충돌 없음
- GateLintel (16x64) · GateSill (16x48): 측벽 통로 문의 위에서 본 보 · 문턱 (CatacombProps 원본 픽셀)
의뢰 종이는 바랜 양피지(X_*), 핀은 탁한 빨강 · 파랑 · 놋쇠 (발광색 아님)."""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.abspath(_os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', '..')))  # spritegen 루트
_sys.path.insert(0, _os.path.abspath(_os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', '..', 'lights')))  # light_table (타일 그림)
import random, sys
from bl import *
from bl import from_img
import draftlib as D

SHEET = 'Shelter'
VERSION = 'v01'
WD = [W_DD, W_DK, W_MD, W_LT, W_HI]
PAPER = [H('5c5546'), H('8a7c5c'), H('b8a880'), H('d8c9a0')]
PIN = [H('7a2a26'), H('3a4a6a'), H('8c6d32')]
CORK = [H('3a2a1e'), H('4a3524'), H('5a4230')]


def wood_band(c, x0, x1, y, rows, seed=1):
    """판자 앞면 띠: 위 빛 · 아래 그늘 · 나뭇결"""
    r = random.Random(seed)
    for k, yy in enumerate(range(y, y + rows)):
        col = WD[3] if k == 0 else WD[1] if k == rows - 1 else WD[2]
        c.hl(x0, x1, yy, col)
    for _ in range((x1 - x0) // 4):
        xx = r.randint(x0 + 1, x1 - 3)
        yy = r.randint(y + 1, y + rows - 2) if rows > 2 else y
        if c.get(xx, yy) == WD[2]:
            c.hl(xx, xx + 2, yy, WD[1])


def wood_top_band(c, x0, x1, y_front, depth):
    """윗면 (깊이 depth px, 앞 모서리 y_front): 앞 모서리 빛선 → 몸 → 뒤 끝 어둠, 판자 이음은 깊이 방향"""
    for k in range(depth):
        y = y_front - k
        col = WD[4] if k == 0 else WD[3] if k < depth - 1 else WD[1]
        c.hl(x0, x1, y, col)
    for x in range(x0 + 6, x1, 9):
        c.vl(x, y_front - depth + 2, y_front - 1, WD[2])


def note(c, x, y, w, h, pin, kind=0):
    """핀 꽂힌 의뢰 종이 (앞면): 윗변 빛 · 오른쪽 아래 그늘 · 글줄, 들뜬 아래 모서리"""
    c.rect(x, y, x + w - 1, y + h - 1, PAPER[2])
    c.hl(x, x + w - 1, y, PAPER[3]); c.vl(x + w - 1, y + 1, y + h - 1, PAPER[1]); c.hl(x + 1, x + w - 1, y + h - 1, PAPER[1])
    for yy in range(y + 3, y + h - 2, 2):
        c.hl(x + 1, x + w - 3 - (yy % 3), yy, PAPER[0])
    if kind == 1:                                                # 현상수배: 해골 그림
        c.rect(x + w // 2 - 2, y + 2, x + w // 2 + 1, y + 4, PAPER[0]); c.px(x + w // 2 - 1, y + 3, PAPER[2])
    elif kind == 2:                                              # 지도 조각: 길 선 + 표시
        c.line(x + 1, y + h - 3, x + w - 3, y + 2, PAPER[0]); c.px(x + w - 3, y + 3, PIN[0]); c.px(x + w - 4, y + 2, PIN[0])
    c.px(x + w - 1, y + h - 1, None); c.px(x + w - 2, y + h, PAPER[1])          # 들뜬 모서리
    c.px(x + w // 2, y + 1, PIN[pin]); c.px(x + w // 2, y, PIN[pin])             # 핀 머리 (위에서 보임)
    c.px(x + w // 2 + 1, y + 2, PAPER[0])                                         # 핀 그림자


# ================================================================ 벽걸이 의뢰 게시판 (64x48)
def wall_board():
    c = Cv(64, 48)
    # 틀 안 코르크판 (틀보다 3px 안쪽 = 그만큼 위로 · 틀 안쪽 윗변 그늘)
    c.rect(5, 12, 58, 41, CORK[1])
    c.speckle(5, 12, 58, 41, [CORK[0], CORK[2]], 220, 11)
    c.hl(5, 58, 12, CORK[0]); c.hl(5, 58, 13, CORK[0])                           # 위 틀 아래 그늘
    c.vl(58, 12, 41, CORK[0])
    # 의뢰 종이 (판에 붙음)
    note(c, 8, 16, 10, 12, 0, 1); note(c, 21, 15, 9, 10, 1); note(c, 33, 17, 11, 9, 2, 2)
    note(c, 47, 15, 9, 12, 0); note(c, 11, 30, 9, 9, 2); note(c, 24, 28, 10, 11, 0, 1); note(c, 38, 29, 8, 9, 1); note(c, 49, 31, 7, 8, 2)
    # 틀 기둥 (앞면) + 기둥머리 윗면 3px
    for (x0, x1) in ((1, 4), (59, 62)):
        c.rect(x0, 10, x1, 45, WD[2]); c.vl(x0, 10, 45, WD[3]); c.vl(x1, 10, 45, WD[1])
        for y in (18, 28, 38):
            c.hl(x0, x1, y, WD[1])
    # 아래 틀 (앞면 3px) + 윗면 2px (종이 받침 턱)
    c.hl(1, 62, 40, WD[4]); c.hl(1, 62, 41, WD[3]); wood_band(c, 1, 62, 42, 4, 3)
    # 차양 (벽에서 7px 튀어나온 판자 지붕): 윗면 7px + 앞면 3px, 밑 그늘
    wood_top_band(c, 1, 62, 7, 7)
    wood_band(c, 1, 62, 8, 3, 5)
    c.hl(1, 62, 11, W_DD)
    for x in (2, 61):                                            # 차양 받침 못 · 쇠
        c.px(x, 9, I_HI)
    # 위 이름판 (가운데, 차양 앞에 박힌 판)
    c.rect(24, 2, 39, 9, WD[3]); c.hl(24, 39, 2, WD[4]); c.vl(39, 3, 9, WD[1]); c.hl(25, 39, 9, WD[1])
    for x in range(26, 38, 2):
        c.px(x, 5, W_DD); c.px(x + 1, 6, W_DD)
    c.outline()
    for y in range(8, 46):                                       # 벽 그림자 (오른쪽)
        if c.get(63, y) is None and c.get(62, y) is not None:
            c.px(63, y, (0, 0, 0, 100))
    return c


# ================================================================ 세워 둔 의뢰 게시판 (48x48, 블록 3x1)
def stand_board():
    c = Cv(48, 48)
    # 발치 돌 · 흙 (바닥) — 기둥 두 개 밑
    for x0 in (3, 39):
        c.rect(x0, 41, x0 + 5, 43, S_MD); c.hl(x0, x0 + 5, 41, S_LT); c.hl(x0, x0 + 5, 43, S_DK)
    # 판 (앞면): 코르크 + 종이, 판 윗변 두께 2px (위에서 보임)
    c.rect(6, 14, 41, 34, CORK[1]); c.speckle(6, 14, 41, 34, [CORK[0], CORK[2]], 120, 21)
    c.hl(6, 41, 12, WD[4]); c.hl(6, 41, 13, WD[3])                              # 판 윗면 (두께)
    c.hl(6, 41, 14, CORK[0]); c.hl(6, 41, 35, WD[1]); c.hl(6, 41, 36, WD[0])     # 판 아래 틀 + 밑 그늘
    note(c, 8, 16, 9, 11, 0, 1); note(c, 19, 17, 10, 9, 1, 2); note(c, 31, 15, 8, 12, 2); note(c, 21, 27, 7, 6, 0)
    # 기둥 2개 (앞면) + 기둥머리 윗면 3px
    for x0 in (3, 41):
        c.rect(x0, 9, x0 + 3, 42, WD[2]); c.vl(x0, 9, 42, WD[3]); c.vl(x0 + 3, 9, 42, WD[1])
        c.hl(x0, x0 + 3, 6, WD[4]); c.hl(x0, x0 + 3, 7, WD[3]); c.hl(x0, x0 + 3, 8, WD[1])
        c.px(x0 + 1, 24, I_HI); c.px(x0 + 1, 31, I_HI)                          # 판 거는 쇠못
    # 차양 판자 (두 기둥 위, 앞뒤로 6px 깊이 윗면 + 앞면 2px)
    wood_top_band(c, 1, 46, 7, 6)
    c.hl(1, 46, 8, WD[2]); c.hl(1, 46, 9, WD[1]); c.hl(2, 45, 10, W_DD)
    c.outline()
    return c


# ================================================================ 창고 궤짝 (32x32, 블록 2x2)
def storage_chest():
    c = Cv(32, 32)
    # 몸통 앞면 (8px): 판자 + 쇠테 + 자물쇠판
    c.rect(2, 19, 29, 27, WD[2])
    c.hl(2, 29, 19, WD[1]); c.hl(2, 29, 27, WD[0])
    for y in (22, 25):
        c.hl(3, 28, y, WD[1])
    for x in (2, 8, 23, 29):                                     # 세로 쇠테 (앞면)
        c.vl(x, 19, 27, I_MD); c.px(x, 20, I_HI); c.px(x, 26, I_HI)
    c.rect(13, 20, 18, 25, I_MD); c.hl(13, 18, 20, I_HI); c.vl(18, 21, 25, I_DK)  # 자물쇠판
    c.rect(15, 22, 16, 23, OUT); c.px(15, 24, OUT)                               # 열쇠 구멍
    c.hl(4, 27, 28, W_DD)                                        # 받침 그늘
    for x in (3, 27):                                            # 다리
        c.rect(x, 28, x + 1, 29, WD[1])
    # 뚜껑 윗면 (깊이 14px): 판자 결(가로) + 쇠테(깊이 방향 = 세로) + 앞 모서리 빛
    c.rect(1, 3, 30, 16, WD[3])
    for y in (6, 10, 14):
        c.hl(2, 29, y, WD[2])
    r = random.Random(9)
    for _ in range(18):
        x, y = r.randint(3, 28), r.randint(4, 15)
        if c.get(x, y) == WD[3]:
            c.px(x, y, WD[2] if r.random() < 0.7 else WD[4])
    for x in (1, 8, 23, 30):
        c.vl(x, 3, 16, I_MD); c.px(x, 4, I_HI); c.px(x, 15, I_HI)
    c.hl(1, 30, 3, WD[2])                                        # 뒤 끝 (경첩 쪽 어둠)
    c.hl(2, 29, 2, I_DK)                                         # 경첩
    c.hl(1, 30, 16, WD[4])                                       # 앞 모서리 빛선
    c.hl(1, 30, 17, WD[3]); c.hl(1, 30, 18, WD[1])               # 뚜껑 앞면 (두께 2px)
    c.rect(14, 16, 17, 19, I_MD); c.hl(14, 17, 16, I_HI)         # 걸쇠
    c.outline()
    return c


# ================================================================ 그루터기 위 작은 모루 (16x16, 블록 1x1)
def anvil_small():
    """그룬(몸 18x19px)에 맞춘 1칸 모루, 탑다운 3/4 (윗면이 그림의 대부분, 앞면은 2~3px 띠).
    그루터기: 둥근 윗면(나이테)이 모루 둘레로 보이고 앞면은 3줄 띠. 모루: 위에서 본 평면 — 왼쪽 뒤꿈치(직사각형) +
    오른쪽으로 한 칸씩 좁아져 1px로 끝나는 뾰족한 뿔, 하디 구멍(네모) · 프리첼 구멍(점). 뒤꿈치가 왼쪽(왼쪽에서 오는 망치를 받음).
    빛은 왼쪽 위. 대장간 모루와 같은 철(I_*) · 나무(W_*) 색."""
    c = Cv(16, 16)
    # 그루터기 앞면 띠 (둥근 아래): 위 빛선 · 몸 · 아래 그늘 + 세로 나뭇결
    for y, (x0, x1) in {12: (2, 13), 13: (2, 13), 14: (3, 12)}.items():
        c.hl(x0, x1, y, W_MD if y < 14 else W_DK)
    c.hl(2, 13, 12, W_LT); c.px(2, 13, W_LT); c.vl(13, 12, 13, W_DK)
    for x in (4, 7, 10):
        c.vl(x, 12, 13, W_DK)
    c.hl(4, 11, 14, W_DD)
    # 그루터기 윗면 (타원): 둘레 빛 · 나이테 호
    rows = {4: (4, 11), 5: (2, 13), 6: (1, 14), 7: (1, 14), 8: (1, 14), 9: (1, 14), 10: (1, 14), 11: (2, 13)}
    for y, (x0, x1) in rows.items():
        c.hl(x0, x1, y, W_MD)
    c.hl(4, 11, 4, W_HI); c.px(2, 5, W_HI); c.px(3, 5, W_HI); c.px(1, 6, W_HI); c.px(1, 7, W_HI)   # 왼쪽 위 빛
    c.hl(2, 13, 11, W_LT); c.px(14, 9, W_DK); c.px(14, 10, W_DK); c.px(13, 11, W_DK)              # 앞 가장자리 · 오른쪽 그늘
    c.px(11, 10, W_DK); c.hl(12, 13, 9, W_DK); c.px(2, 10, W_DK); c.px(3, 10, W_DK)              # 나이테 호
    # 모루 앞면 띠 (허리 · 발): 윗면 앞 가장자리 밑 2줄
    c.hl(4, 9, 8, I_MD); c.hl(4, 9, 9, I_DK); c.px(4, 8, I_HI)
    # 모루 윗면: 열마다 세로 범위 (왼쪽 꼬리 · 뒤꿈치 · 한 칸씩 좁아지는 뿔)
    spans = {2: (3, 6), 3: (2, 7), 4: (2, 7), 5: (2, 7), 6: (2, 7), 7: (2, 7), 8: (2, 7), 9: (2, 7),
             10: (3, 6), 11: (3, 6), 12: (4, 5), 13: (4, 5), 14: (4, 4)}
    for x, (y0, y1) in spans.items():
        c.vl(x, y0, y1, I_HI)
    c.hl(3, 9, 2, S_HI)                                            # 뒤 모서리 빛
    c.px(2, 3, S_HI)
    c.hl(3, 9, 7, I_MD)                                            # 앞 가장자리 (그늘)
    for x in (10, 11):
        c.px(x, 6, I_MD)
    c.px(12, 5, I_MD); c.px(13, 5, I_DK); c.px(11, 3, S_HI)       # 뿔 아래쪽 면 어둡게 · 위쪽 빛
    # 모루가 그루터기 윗면에 드리우는 그림자 (뿔 아래 · 발 앞): 뿔 끝과 받침 색을 갈라 줌
    for x in range(10, 15):
        c.px(x, spans[x][1] + 1, W_DD)
    c.hl(4, 10, 10, W_DD); c.px(3, 8, W_DD); c.px(3, 9, W_DD)
    # 하디 구멍 (네모) + 프리첼 구멍 (점) + 닳은 타격 자리
    c.rect(7, 4, 8, 5, I_DK); c.px(8, 5, OUT)
    c.px(5, 5, I_DK)
    c.px(4, 3, S_HI); c.px(5, 3, S_HI); c.px(3, 4, I_HI)
    c.outline()
    return c


ITEMS = [
    D.make('Wall_QuestBoard', '벽걸이 의뢰 게시판', wall_board(), 'hang', None, shadow=False, note='64x48 · 북쪽 벽 3줄'),
    D.make('QuestBoard_Stand', '세운 의뢰 게시판', stand_board(), 'block', (3, 1), note='48x48 · 블록 3x1'),
    D.make('StorageChest', '창고 궤짝', storage_chest(), 'block', (2, 2), note='32x32 · 블록 2x2'),
]


# ================================================================ 출격 입구 아치 A안 + 측벽 통로 문 (보 · 문턱)
ARCH = [(-2, 19, 231), (-2, 20, 188), (-2, 21, 141), (-2, 22, 96), (-2, 23, 55), (-2, 24, 30), (-2, 25, 5),
        (-1, 20, 176), (-1, 21, 129), (-1, 22, 86), (-1, 23, 56), (-1, 24, 31), (-1, 25, 6),
        (0, 20, 176), (0, 21, 129), (0, 22, 86), (0, 23, 57), (0, 24, 32), (0, 25, 7),
        (1, 19, 234), (1, 20, 191), (1, 21, 144), (1, 22, 99), (1, 23, 58), (1, 24, 33), (1, 25, 8)]   # 지금 쉘터 입구 조각 (칸 x, y, DungeonSheet 번호)


def arch_tiles():
    """입구 아치 조각(Catacomb 시트)을 한 장에 모아 DungeonDark 벽 색으로 바꾼 뒤 다시 16px로 자름 (색 대응을 조각끼리 같게)"""
    import re
    from PIL import Image
    import paths
    import shelter_gate as GT
    sheet = Image.open(paths.tile('Catacomb', 'Catacomb.png')).convert('RGBA')
    meta = open(paths.tile('Catacomb', 'Catacomb.png.meta'), encoding='utf8').read()
    ids = sorted({n for (_, _, n) in ARCH})
    comp = Image.new('RGBA', (16 * len(ids), 16), (0, 0, 0, 0))
    for k, n in enumerate(ids):
        m = re.search(r'name: DungeonSheet_%d\n      rect:\n        serializedVersion: 2\n        x: (\d+)\n        y: (\d+)' % n, meta)
        x, y = int(m.group(1)), int(m.group(2))
        comp.paste(sheet.crop((x, sheet.height - y - 16, x + 16, sheet.height - y)), (16 * k, 0))
    comp = fill_arch_void(GT.recolor(comp))
    return {n: comp.crop((16 * k, 0, 16 * k + 16, 16)) for k, n in enumerate(ids)}


ARCH_BLACK = (7, 7, 7)          # Catacomb 원본의 문간 · 아치 위 검정 (recolor가 어두운 색은 그대로 둠)
ARCH_VOID = (30, 22, 17)        # 던전 바깥 어둠 (벽 채움 Dungeon_56 · 카메라 배경과 같은 색. 다르면 입구만 따로 떠 보인다)


def fill_arch_void(im):
    """아치 위쪽 바깥과 문간 안쪽의 검정을 던전 바깥 어둠 색으로 바꿈"""
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] > 0 and p[:3] == ARCH_BLACK:
                px[x, y] = ARCH_VOID + (p[3],)
    return out


def gate_parts():
    from PIL import Image
    import shelter_gate as GT
    lintel = GT.PROPS.crop((16, 0, 112, 16)).rotate(90, expand=True).crop((0, 0, 16, 64))   # 아치 보를 위에서 본 것 (윗모서리 빛이 왼쪽)
    sill = GT.PROPS.crop((240, 24, 256, 72))                                                # 세로 문턱 가운데 48px (도로 폭 3칸)
    return from_img(lintel), from_img(sill)


for n, im in arch_tiles().items():
    ITEMS.append(D.make('ShelterArch_%d' % n, '입구 %d' % n, from_img(im), 'tile', None, shadow=False, outline=False, seam=True))
_l, _s = gate_parts()
ITEMS.append(D.make('GateLintel', '측벽 문 보', _l, 'fore', None, shadow=False, outline=False, note='16x64 · 위에서 본 보'))
ITEMS.append(D.make('GateSill', '측벽 문 문턱', _s, 'decal', None, shadow=False, outline=False, note='16x48'))
ITEMS.append(D.make('Anvil_S', '작은 모루', anvil_small(), 'block', (1, 1), note='16x16 · 블록 1x1 · 그룬 강화 연출'))   # 시트 맨 끝에 추가 (기존 스프라이트 순서 유지)


def scene():
    sc = D.dark_room(16 * 12, 16 * 8, wall_rows=3)
    by = {it['id']: it for it in ITEMS}

    def put(n, cx, cy):
        im = by[n]['fin'].img()
        sc.alpha_composite(im, (cx * 16, (cy + 1) * 16 + 4 - im.height))
    put('Wall_QuestBoard', 1, 2); put('QuestBoard_Stand', 7, 5); put('StorageChest', 2, 6)
    return sc


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        import gen_zone
        gen_zone.main(SHEET, 'shelter_props')
    else:
        cats = [('블록 판정 (노란 틀 = 충돌 칸)', ['block']), ('벽걸이 (충돌 없음)', ['hang']), ('입구 아치 A안 (DungeonDark 벽 색, 16x16)', ['tile']),
                ('측벽 통로 문', ['fore', 'decal'])]
        sc = scene()
        im = D.draft(ITEMS, cats, sc, sc.size, '쉘터 소품 시안 %s  ·  의뢰 게시판(벽걸이 · 세움) · 창고 궤짝 — 탑다운 투영' % VERSION, S=3)
        im.save(sys.argv[1] if len(sys.argv) > 1 else 'shelter-props-draft.png')
        print(im.size)
