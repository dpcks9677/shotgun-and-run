"""퀘스트 아이템 아이콘 — 16x16 x 9칸 가로 시트 (재료 · 소모품 아이콘과 같은 양식).

순서(슬롯 인덱스 고정):
  0 정밀 숫돌(SQ_MAIN_001)  — 현재 Assets/04.Sprites/Quest/quest-icons.png 픽셀을 그대로 복사
  1 봉인된 트롤 기름통(Z3)   2 승강기 설계도(Z4)   3 공명 마력석 조각(Z5)   4 고블린 보급 장부(Z6)
  5 고블린 작전 지도(Z7)     6 의식 연구 노트(Z8)  7 해골 병사 명부(Z9)     8 고블린 처방집(Z10)
외곽선 26,20,30 · 좌상단 광원 · 윗면 + 앞면 띠(탑다운 3/4).

python quest_icons.py [시안.png] [시트.png] [버전]
    기본값: Docs/drafts/props/quest-icons-draft.png · Docs/drafts/props/quest-icons-sheet.png · v01
임포트 기능은 없다. 숫돌 칸은 손 수정본일 수 있어 다시 생성하지 않고, 승인 후 임포트는 따로 한다.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402
from consumable_icons import N, from_rows, outline  # noqa: E402

# 고운 숫돌(차가운 회청) · 거친 숫돌(녹슨 적갈) 두 겹. 램프는 어두운 → 밝은 순
FINE = [(78, 84, 96), (96, 104, 118), (118, 126, 140), (138, 146, 160), (182, 190, 202), (222, 228, 236)]
COARSE = [(112, 58, 40), (156, 86, 58), (190, 118, 80)]

CLAY = [(84, 40, 26), (124, 62, 36), (164, 90, 52), (200, 124, 76), (228, 162, 110)]
WAX = [(104, 20, 30), (164, 34, 40), (212, 72, 62)]
TWINE = [(116, 96, 60), (176, 152, 100)]
OIL = [(30, 34, 22), (52, 62, 30), (104, 122, 52)]                    # 트롤 기름: 검녹 + 윤기
BLUE = [(20, 36, 72), (32, 60, 116), (48, 90, 160), (80, 130, 196), (136, 178, 226)]
INKW = [(72, 116, 182), (226, 236, 246)]                               # 청사진 흰 선(흐린 · 진한)
TEAL = [(14, 60, 70), (22, 104, 112), (42, 160, 158), (104, 216, 200), (200, 252, 238)]
VIO = [(56, 28, 92), (100, 54, 156), (156, 100, 216), (212, 170, 250)]
LEATH = [(52, 24, 22), (84, 38, 30), (116, 56, 40), (148, 80, 54), (178, 108, 72)]
PAGE = [(148, 134, 108), (198, 184, 148), (230, 218, 184)]
STEEL = [(76, 86, 106), (123, 134, 153), (179, 191, 207)]
PARCH = [(110, 82, 46), (152, 118, 70), (190, 156, 100), (218, 188, 132), (238, 216, 164)]
INK = [(78, 52, 34)]
RED = [(126, 22, 26), (196, 42, 36), (232, 92, 72)]
PURP = [(38, 22, 54), (66, 40, 92), (94, 60, 124), (130, 90, 162)]
AGED = [(146, 132, 98), (186, 172, 130), (216, 204, 160), (238, 228, 190)]
STAIN = [(40, 78, 32), (72, 124, 48), (112, 168, 70)]
BONE = [(92, 85, 70), (138, 129, 112), (184, 173, 148), (207, 196, 170), (232, 226, 206)]
HERB = [(26, 52, 34), (40, 80, 48), (60, 112, 64), (94, 146, 86)]
LEAF = [(52, 110, 40), (96, 164, 64), (150, 206, 96)]


def ramp(keys, cols):
    return {k: c for k, c in zip(keys, cols)}


def body(rows, pal):
    """외곽선 없이 그린 몸통 + 4방향 자동 외곽선"""
    return outline(from_rows(rows, pal))


# ── 0. 정밀 숫돌: 시트에는 현재 PNG 픽셀을 쓴다. 이 함수는 원래 생성식(참고용) ──
def whetstone():
    pal = {'W': FINE[5], 'E': FINE[4], 'D': FINE[3], 'C': FINE[2], 'b': FINE[1], 'A': FINE[0], 'B': FINE[1],
           'q': COARSE[0], 'R': COARSE[1], 'r': COARSE[2]}
    return from_rows([
        "................",
        "................",
        "................",
        "..oooooooooooo..",
        ".oWWEEEEEEEEDDo.",
        ".oWEEbAAAAAbDCo.",
        ".oEEDDbAAAbDDCo.",
        ".oEDDDDDDDDDCCo.",
        ".oEEEEEEEEEEEDo.",
        ".oCCCCCCCCCCCBo.",
        ".oBBBBBBBBBBBAo.",
        ".oRRRRRRRRRRRqo.",
        ".oRrRRRrRRRrRqo.",
        ".oqqqqqqqqqqqqo.",
        "..oooooooooooo..",
        "................",
    ], pal)


# ── 1. 봉인된 트롤 기름통: 토기 항아리 + 붉은 밀랍 마개 + 끈, 어깨에서 흘러내린 검녹 기름 ──
def oil_jar():
    pal = ramp('abcde', CLAY)
    pal.update(ramp('rRs', WAX))
    pal.update(ramp('tT', TWINE))
    pal.update(ramp('kKg', OIL))
    return body([
        "................",
        "......sRRr......",
        ".....sRRRRr.....",
        ".....RRrRrr.....",
        "......TtTt......",
        ".....edccbk.....",
        "....eddcccKb....",
        "...edeedccKbb...",
        "..deeedccckgba..",
        "..cddcccbbkKaa..",
        "..ddeddcccbkba..",
        "..dddcccccbKaa..",
        "...cccccbbbkaa..",
        "....cbbbbaakKk..",
        "................",
        "................",
    ], pal)


# ── 2. 승강기 설계도: 펼친 푸른 종이(흰 선: 도르래 · 케이블 · 승강기 칸, 흐린 레일) + 오른쪽 말린 원통, 끝면에 소용돌이 ──
def blueprint():
    pal = ramp('abcde', BLUE)
    pal.update(ramp('wW', INKW))
    return body([
        "................",
        "................",
        "...........edb..",
        ".ddddddddddedb..",
        ".dcccWWWcccedb..",
        ".dwccWcWccwedb..",
        ".dwccWWWccwedb..",
        ".dwcccWcccwedb..",
        ".dwcWWWWWcwedb..",
        ".dwcWcccWcwedb..",
        ".dwcWWWWWcwedb..",
        ".dcccccccccedb..",
        ".bbbbbbbbbbcWb..",
        "...........bcb..",
        "................",
        "................",
    ], pal)


# ── 3. 공명 마력석 조각: 청록 결정 파편(보라 속심), 아래는 깨진 단면, 옆에 작은 파편 · 반짝임 ──
def shard():
    pal = ramp('abcde', TEAL)
    pal.update(ramp('uvVw', VIO))
    return body([
        "................",
        ".........ed.....",
        "........eedc....",
        "........edcc..e.",
        ".......edwvcb...",
        ".......edvVcb...",
        "......eddVVcb...",
        "......edwVvcb...",
        ".....eddvVucb...",
        ".....edcvucb....",
        "....edccvcbb....",
        "....eddccbb..dc.",
        "...dddcbbaa..ba.",
        "....cc.baa......",
        "................",
        "................",
    ], pal)


# ── 4. 고블린 보급 장부: 두꺼운 붉은 갈색 가죽 장부(왼쪽 등 · 표지 라벨 + 앞면 종이 띠), 세로 끈 한 줄 + 매듭 ──
def ledger():
    pal = ramp('abcde', LEATH)
    pal.update(ramp('pPq', PAGE))
    pal.update(ramp('tT', TWINE))
    pal.update({'L': PAGE[2], 'l': PAGE[0], 'i': INK[0], 'M': STEEL[2], 'm': STEEL[1]})
    return body([
        "................",
        "................",
        "................",
        ".cdeeeeeeeTteeM.",
        ".bddLLLLLdTtddm.",
        ".cddLiiiLdTtdcc.",
        ".bddlLLLlcTtccc.",
        ".cddddcccTTttcb.",
        ".bdcdcccccTtcbb.",
        ".cdccccbccTtbcb.",
        ".bbbbbbbbbTtbbb.",
        ".aqqqqqqqqTtqqq.",
        ".aPpPPPpPPTtPpP.",
        ".aaaaaaaaaTtaaa.",
        "................",
        "................",
    ], pal)


# ── 5. 고블린 작전 지도: 접힌 자국이 있는 양피지, 너덜한 가장자리, 점선 경로 끝 붉은 X ──
def war_map():
    pal = ramp('abcde', PARCH)
    pal.update({'i': INK[0], 'x': RED[0], 'X': RED[1]})
    return body([
        "................",
        "................",
        "..eee.eeedeee...",
        ".eeeeeeedeeeedd.",
        ".eeieeeedeedXdc.",
        ".eedeieecdXdXdc.",
        ".eeeeeeedeeXxdc.",
        ".eedeeeiceXdXdc.",
        ".eeeeeeecexdxdc.",
        ".eedeeeicdeeddc.",
        ".eeieeeedeedddb.",
        ".eeeieiecddddcb.",
        "..ddd.dddcdcb...",
        "................",
        "................",
        "................",
    ], pal)


# ── 6. 의식 연구 노트: 펼친 보라 표지 노트, 왼쪽 마법진 · 오른쪽 글줄, 녹색 약물 얼룩 ──
def ritual_notes():
    pal = ramp('abcd', PURP)
    pal.update(ramp('PQRS', AGED))
    pal.update({'u': PURP[2], 'i': PURP[1]})
    pal.update(ramp('gGh', STAIN))
    return body([
        "................",
        "................",
        "................",
        "..SSSSS..SSSSR..",
        ".dSRuuuRaSSSRRb.",
        ".dSuRRRuaRiiiRb.",
        ".dSuRiRuaRRRRQb.",
        ".dSuRRRuaRiiRQb.",
        ".dSRuuuRaRRgGQb.",
        ".dRRRRRRaRgGhgb.",
        ".dRRRRQQaQQggQb.",
        ".cQQQQQPaPPPPgb.",
        ".bbbbbbbabbbbbb.",
        "..aaaaaa.aaaaa..",
        "................",
        "................",
    ], pal)


# ── 7. 해골 병사 명부: 뼈 막대 두 개에 감긴 세로 두루마리, 이름 줄 + 붉은 밀랍 해골 인장 ──
def roster():
    pal = ramp('abcde', BONE)
    pal.update({'n': BONE[0], 'r': WAX[0], 'R': WAX[1], 'S': BONE[4], 'k': (52, 14, 20)})
    return body([
        "................",
        "..dee......ed...",
        ".deeeeeeeeeeed..",
        "..bccccccccccb..",
        "...eddddddddc...",
        "...ennndnnndc...",
        "...eddddddddc...",
        "...ednnnndnnc...",
        "...eddddddddc...",
        "...eddrRRRrdc...",
        "...edrRSSSRrc...",
        "...edrRkSkRrc...",
        "..dddrRSRSRrdb..",
        ".deeeerrrrrecd..",
        "..bcc......cb...",
        "................",
    ], pal)


# ── 8. 고블린 처방집: 작은 짙은 녹색 책, 표지에 크림 판 + 붉은 십자, 위로 삐져나온 약초 잎 책갈피 ──
def prescriptions():
    pal = ramp('abcd', HERB)
    pal.update(ramp('pPq', PAGE))
    pal.update({'x': RED[0], 'X': RED[1], 'Y': RED[2]})
    pal.update(ramp('lLm', LEAF))
    return body([
        "................",
        "..........m.....",
        ".........mL.....",
        "........mLl.....",
        "....dddddLlb....",
        "....dccccLcb....",
        "....dcqqqqcbp...",
        "....dcqYXqcbp...",
        "....dcYXXXcbp...",
        "....dcqXxqcbp...",
        "....dcqqqqcbp...",
        "....dccccccbp...",
        "....dbbbbbbbp...",
        "....qqqqqqqqp...",
        "....aaaaaaaa....",
        "................",
    ], pal)


ICONS = [oil_jar, blueprint, shard, ledger, war_map, ritual_notes, roster, prescriptions]
NAMES = ['정밀 숫돌(기존)', '봉인된 트롤 기름통', '승강기 설계도', '공명 마력석 조각', '고블린 보급 장부',
         '고블린 작전 지도', '의식 연구 노트', '해골 병사 명부', '고블린 처방집']
IDS = ['PrecisionWhetstone', 'SealedTrollOil', 'LiftBlueprint', 'ResonantShard', 'GoblinSupplyLedger',
       'GoblinWarMap', 'RitualNotes', 'SkeletonRoster', 'GoblinPrescriptions']
SRC = paths.repo('Assets', '04.Sprites', 'Quest', 'quest-icons.png')   # 읽기만 함


def render(cells):
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    px = img.load()
    for y in range(N):
        for x in range(N):
            if cells[y][x] is not None:
                px[x, y] = cells[y][x] + (255,)
    return img


def cells_images():
    """슬롯 0~8 이미지. 0번은 현재 quest-icons.png의 첫 칸을 그대로 복사"""
    src = Image.open(SRC).convert('RGBA').crop((0, 0, N, N))
    return [src] + [render(f()) for f in ICONS]


def sheet():
    cells = cells_images()
    out = Image.new('RGBA', (N * len(cells), N), (0, 0, 0, 0))
    for i, im in enumerate(cells):
        out.alpha_composite(im, (N * i, 0))
    return out


def items():
    """check.py용: 새 아이콘 8종(숫돌 제외)을 draftlib.make 항목으로"""
    import draftlib
    from bl import from_img
    return [draftlib.make(IDS[i + 1], NAMES[i + 1], from_img(render(f())), 'icon', shadow=False)
            for i, f in enumerate(ICONS)]


def _font(sz):
    from bl import load_font
    return load_font(sz)


def _slot():
    """ui-atlas의 hud_slot(22x22, 검정 α156, 모서리 깎임)"""
    atlas = Image.open(paths.repo('Assets', '04.Sprites', 'UI', 'ui-atlas.png')).convert('RGBA')
    return atlas.crop((60, atlas.height - 40 - 22, 82, atlas.height - 40))


def draft(path, version='v01'):
    sh = sheet()
    cells = cells_images()
    mats = Image.open(paths.repo('Assets', '04.Sprites', 'Material', 'material-icons.png')).convert('RGBA')
    BG, FG, SUB = (40, 42, 50, 255), (236, 236, 240), (176, 178, 190)
    Z, pad, colw = 12, 24, 16 * 12 + 24
    w = pad * 2 + colw * 5 - 24
    img = Image.new('RGBA', (w, 1400), BG)
    dr = ImageDraw.Draw(img)
    f_t, f_n, f_s = _font(22), _font(15), _font(13)

    # 제목 + 버전 배지
    dr.text((pad, 16), '퀘스트 인증 아이템 아이콘 시안', fill=FG, font=f_t)
    tw = dr.textlength('퀘스트 인증 아이템 아이콘 시안', font=f_t)
    bx = pad + int(tw) + 14
    dr.rounded_rectangle((bx, 16, bx + 58, 44), 6, fill=(214, 168, 64, 255))
    dr.text((bx + 10, 18), version, fill=(30, 24, 16), font=f_t)
    dr.text((pad, 52), '16x16 · 시트 144x16 (9칸, 슬롯 인덱스 고정) · 0번 숫돌은 현재 PNG 그대로', fill=SUB, font=f_s)

    # 1) 확대 + 이름 (5칸 x 2줄)
    y0 = 84
    for i, im in enumerate(cells):
        cx, cy = pad + (i % 5) * colw, y0 + (i // 5) * (16 * Z + 56)
        dr.rectangle((cx - 2, cy - 2, cx + 16 * Z + 1, cy + 16 * Z + 1), fill=(28, 29, 36, 255))
        img.alpha_composite(im.resize((16 * Z, 16 * Z), Image.NEAREST), (cx, cy))
        dr.text((cx, cy + 16 * Z + 6), f'{i}  {NAMES[i]}', fill=FG if i else SUB, font=f_n)
        if i:
            dr.text((cx, cy + 16 * Z + 26), IDS[i], fill=SUB, font=f_s)
    y = y0 + 2 * (16 * Z + 56) + 8

    # 2) 실제 크기 1배 · 2배 · 3배
    dr.text((pad, y), '실제 크기  1배 · 2배 · 3배', fill=FG, font=f_n)
    y += 26
    x = pad
    for k in (1, 2, 3):
        img.alpha_composite(sh.resize((sh.width * k, 16 * k), Image.NEAREST), (x, y + (48 - 16 * k)))
        x += sh.width * k + 24
    y += 48 + 24

    # 3) 인벤토리 칸 미리보기 (hud_slot 위, 4배) — 퀘스트 아이콘 줄 + 재료 아이콘과 섞은 줄
    dr.text((pad, y), '인벤토리 칸 미리보기 (hud_slot, 4배) — 맨 위: 퀘스트 9종 · 아래 두 줄: 재료 아이콘과 섞음', fill=FG, font=f_n)
    y += 28
    slot = _slot()
    mat = [mats.crop((16 * i, 0, 16 * i + 16, 16)) for i in range(mats.width // 16)]
    rows = [cells,
            [mat[2], cells[1], mat[8], cells[3], mat[4], cells[4], mat[9], cells[8], cells[5]],
            [mat[0], cells[2], mat[5], cells[6], cells[7], mat[3], cells[0], mat[6], cells[4]]]
    for r in rows:
        bar = Image.new('RGBA', (len(r) * 24 + 2, 26), (22, 23, 28, 255))
        for i, ic in enumerate(r):
            bar.alpha_composite(slot, (2 + i * 24, 2))
            bar.alpha_composite(ic, (5 + i * 24, 5))
        img.alpha_composite(bar.resize((bar.width * 4, bar.height * 4), Image.NEAREST), (pad, y))
        y += bar.height * 4 + 10
    dr.text((pad, y), '아래 두 줄 재료: 트롤 기름 통 · 마나 수정 · 가죽 · 약초 · 철괴 · 천 · 용수철 · 금 — 퀘스트 아이콘과 실루엣 · 색이 겹치지 않는지 확인용',
            fill=SUB, font=f_s)
    y += 30
    img.crop((0, 0, w, y + pad - 10)).save(path)


def edge_report(sh):
    """칸별 캔버스 가장자리 불투명 접촉 · 색 수"""
    for i in range(sh.width // N):
        cell = sh.crop((i * N, 0, i * N + N, N))
        px = cell.load()
        touch = [n for n, pts in (('위', [(x, 0) for x in range(N)]), ('아래', [(x, N - 1) for x in range(N)]),
                                  ('왼', [(0, y) for y in range(N)]), ('오른', [(N - 1, y) for y in range(N)]))
                 if any(px[p][3] for p in pts)]
        cols = {px[x, y][:3] for x in range(N) for y in range(N) if px[x, y][3]}
        print(f'{i} {NAMES[i]}: 가장자리 접촉 {touch or "없음"}, 색 {len(cols)}')


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'quest-icons-draft.png')
    raw = sys.argv[2] if len(sys.argv) > 2 else paths.repo('Docs', 'drafts', 'props', 'quest-icons-sheet.png')
    ver = sys.argv[3] if len(sys.argv) > 3 else 'v01'
    draft(out, ver)
    sheet().save(raw)
    edge_report(sheet())
    print('wrote', out)
    print('wrote', raw)
