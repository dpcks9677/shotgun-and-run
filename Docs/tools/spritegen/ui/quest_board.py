"""쉘터 퀘스트 게시판 UI — 나무 틀 게시판 + 세로 양피지 카드 (uGUI, 1920x1080 기준 3배 표시).
    python quest_board.py <시안.png>      시안 (Docs/drafts/props/quest-board-draft.png)

색은 쉘터 게시판 타일(Shelter.png Wall_QuestBoard)과 같게: 목재 W_*, 코르크 갈색, 양피지 X_*, 탭 핀 빨강 · 금 · 파랑.
글자는 스프라이트에 굽지 않고 TMP(Mulmaru)로 올린다. 시안의 글자는 Mulmaru TTF로 흉내 낸 것.
예외: 완료 도장의 '완료'는 도장 그림의 일부라 12px로 굽는다."""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.abspath(_os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..')))  # spritegen 루트
import random
import re
import sys
from PIL import Image, ImageDraw, ImageFont
import paths
from bl import (H, OUT, Cv, W_HI, W_LT, W_MD, W_DK, W_DD, I_HI, I_MD, I_DK,
                X_HI, X_MD, X_DK, O_DK, O_DD)

VERSION = 'v01'
S = 3  # 1920x1080 캔버스에서 UI 픽셀 1칸 = 화면 3px

# ---------- 팔레트 ----------
# 양피지 7단계 (X_HI d8c9a0 · X_MD b8a880 · X_DK 8a7c5c 사이를 채움)
P0, P1, P2, P3, P4, P5, P6 = (H('e8dcb6'), X_HI, H('c9b98f'), X_MD, H('a29170'), X_DK, H('66583e'))
# 코르크 (게시판 타일 안쪽)
K_HI, K_LT, K_MD, K_DK, K_DD = H('5a4230'), H('4a3524'), H('3a2a1e'), H('2e2118'), H('211812')
# 탭 핀 (게시판 타일 쪽지와 같은 색)
TAB = {'red': (H('9a3a32'), H('7a2a26'), H('4a1a18')),
       'gold': (H('b08d45'), H('8c6d32'), H('5e4a22')),
       'blue': (H('55688c'), H('3a4a6a'), H('263049'))}
# 황동 버튼 (퀘스트 완료)
BR_HI, BR_LT, BR_MD, BR_DK, BR_DD = H('d8b45e'), H('b08d45'), H('8c6d32'), H('5e4a22'), H('3a2e16')
# 비활성 (무채색 쪽 회갈)
D_HI, D_MD, D_DK = H('6a655a'), H('545048'), H('3c3934')
INK = H('3a2a22')
INK_SOFT = H('5c4a3a')
GRAY_TXT = H('857c6c')
STAMP = H('b0302a')

FONT_TTF = paths.repo('Assets', 'TextMesh Pro', 'Fonts', 'Mulmaru.ttf')


# ---------- 공용 ----------
def sprite_from_sheet(png, name=None, internal_id=None):
    """Unity 시트 meta에서 이름 또는 internalID로 슬라이스를 잘라 Cv로 돌려준다."""
    meta = open(png + '.meta', encoding='utf8').read()
    im = Image.open(png).convert('RGBA')
    for blk in meta.split('- serializedVersion: 2\n')[1:]:
        nm = re.search(r'name: (\S+)', blk)
        iid = re.search(r'internalID: (-?\d+)', blk)
        if (name and nm and nm.group(1) == name) or (internal_id and iid and int(iid.group(1)) == internal_id):
            x, y, w, h = map(int, re.search(r'x: (\d+)\s+y: (\d+)\s+width: (\d+)\s+height: (\d+)', blk).groups())
            crop = im.crop((x, im.height - y - h, x + w, im.height - y))
            c = Cv(w, h)
            for j in range(h):
                for i in range(w):
                    p = crop.getpixel((i, j))
                    if p[3]:
                        c.p[j][i] = p
            return c
    raise KeyError(name or internal_id)


def drop_shadow(c, dx=2, dy=2, a=110):
    """오른쪽 아래 알파 그림자를 붙인 새 캔버스 (코르크 위에 뜬 종이)"""
    o = Cv(c.w + dx, c.h + dy)
    for y in range(c.h):
        for x in range(c.w):
            if c.p[y][x] is not None and c.p[y][x][3] > 200:
                for k in range(1, dx + 1):
                    if o.get(x + k, y + min(k, dy)) is None:
                        o.px(x + k, y + min(k, dy), (0, 0, 0, a if k == 1 else a // 2))
                for k in range(1, dy + 1):
                    if o.get(x + min(k, dx), y + k) is None:
                        o.px(x + min(k, dx), y + k, (0, 0, 0, a if k == 1 else a // 2))
    o.paste(c, 0, 0)
    return o


# ---------- 게시판 ----------
def cork_tile(n=32, seed=11):
    """이어 붙이는 코르크 칸: 좌표를 n으로 감아 찍어 경계가 이어진다."""
    c = Cv(n, n)
    c.rect(0, 0, n - 1, n - 1, K_MD)
    r = random.Random(seed)
    def p(x, y, col):
        c.px(x % n, y % n, col)
    for _ in range(n * n // 9):          # 얼룩 덩어리 (2~3px)
        x, y = r.randrange(n), r.randrange(n)
        col = r.choice([K_LT, K_LT, K_DK])
        p(x, y, col)
        if r.random() < .5:
            p(x + 1, y, col)
        if r.random() < .3:
            p(x, y + 1, col)
    for _ in range(n * n // 40):         # 밝은 알갱이
        p(r.randrange(n), r.randrange(n), K_HI)
    for _ in range(n * n // 30):         # 어두운 알갱이
        p(r.randrange(n), r.randrange(n), K_DD)
    for _ in range(3):                   # 압정 자국: 구멍 + 오른쪽 아래 밝은 턱
        x, y = r.randrange(n), r.randrange(n)
        p(x, y, K_DD); p(x + 1, y + 1, K_HI)
    return c


def beam_h(c, x0, y0, x1, y1, seed):
    """가로 들보: 가로 나뭇결 + 위 밝게 · 아래 어둡게"""
    r = random.Random(seed)
    c.rect(x0, y0, x1, y1, W_MD)
    c.hl(x0, x1, y0, W_HI)
    c.hl(x0, x1, y0 + 1, W_LT)
    c.hl(x0, x1, y1, W_DD)
    c.hl(x0, x1, y1 - 1, W_DK)
    for y in range(y0 + 3, y1 - 2, 3):   # 나뭇결
        x = x0 + r.randint(0, 6)
        while x < x1:
            ln = r.randint(5, 14)
            c.hl(x, min(x1, x + ln), y, r.choice([W_DK, W_DK, W_LT]))
            x += ln + r.randint(4, 12)
    x = x0 + r.randint(30, 60)
    while x < x1 - 8:                    # 판자 이음
        c.vl(x, y0 + 2, y1 - 2, W_DD)
        c.vl(x + 1, y0 + 2, y1 - 2, W_LT)
        x += r.randint(70, 130)
    c.speckle(x0, y0 + 2, x1, y1 - 2, [W_DK, W_LT, W_HI], (x1 - x0) * (y1 - y0) // 18, seed + 3, only=[W_MD])


def beam_v(c, x0, y0, x1, y1, seed):
    r = random.Random(seed)
    c.rect(x0, y0, x1, y1, W_MD)
    c.vl(x0, y0, y1, W_HI)
    c.vl(x0 + 1, y0, y1, W_LT)
    c.vl(x1, y0, y1, W_DD)
    c.vl(x1 - 1, y0, y1, W_DK)
    for x in range(x0 + 3, x1 - 2, 3):
        y = y0 + r.randint(0, 6)
        while y < y1:
            ln = r.randint(5, 14)
            c.vl(x, y, min(y1, y + ln), r.choice([W_DK, W_DK, W_LT]))
            y += ln + r.randint(4, 12)
    c.speckle(x0 + 2, y0, x1 - 2, y1, [W_DK, W_LT], (x1 - x0) * (y1 - y0) // 18, seed + 3, only=[W_MD])


def bracket(c, x, y, fx, fy):
    """모서리 쇠 꺾쇠 (fx, fy = 1/-1 방향)"""
    pts = [(i, j) for i in range(10) for j in range(10) if i < 4 or j < 4]
    for i, j in pts:
        c.px(x + i * fx, y + j * fy, I_MD)
    for i, j in pts:
        if (i == 0 or j == 0):
            c.px(x + i * fx, y + j * fy, I_HI if (fx > 0 and i == 0) or (fy > 0 and j == 0) else I_DK)
    for i, j in ((1, 1), (1, 7), (7, 1)):
        c.px(x + i * fx, y + j * fy, OUT)
        c.px(x + (i + 1) * fx, y + (j + 1) * fy, I_HI)


def plaque(w, h, seed=5):
    """새긴 나무 팻말: 테두리 홈 + 양끝 못"""
    c = Cv(w, h)
    beam_h(c, 1, 1, w - 2, h - 2, seed)
    c.rect(3, 3, w - 4, h - 4, W_DK)
    c.rect(4, 4, w - 5, h - 5, W_MD)
    c.hl(4, w - 5, 4, W_LT)
    c.speckle(5, 5, w - 6, h - 6, [W_DK, W_LT], (w * h) // 20, seed + 1, only=[W_MD])
    for nx in (2, w - 3):
        c.px(nx, h // 2, I_HI); c.px(nx, h // 2 + 1, I_DK)
    c.cut(1, 1, w - 2, h - 2)
    c.outline()
    return c


def board(w, h, top=24, side=12):
    """게시판 전체 틀 (시안용 한 장). 임포트 때는 9-slice(왼 · 오 · 아래 12, 위 24)로 자른다."""
    c = Cv(w, h)
    beam_v(c, 1, 1, side, h - 2, 21)
    beam_v(c, w - 1 - side, 1, w - 2, h - 2, 22)
    beam_h(c, 1, 1, w - 2, top - 1, 23)
    beam_h(c, 1, h - 1 - side, w - 2, h - 2, 24)
    # 안쪽 홈 (코르크와 닿는 곳): 위 · 왼은 그늘이 진하게
    c.hl(side + 1, w - side - 2, top, W_DD)
    c.vl(side + 1, top, h - side - 2, W_DD)
    c.hl(side + 1, w - side - 2, h - side - 2, W_LT)
    c.vl(w - side - 2, top, h - side - 2, W_LT)
    bracket(c, 2, 2, 1, 1)
    bracket(c, w - 3, 2, -1, 1)
    bracket(c, 2, h - 3, 1, -1)
    bracket(c, w - 3, h - 3, -1, -1)
    c.cut(1, 1, w - 2, h - 2)
    c.outline()
    return c


def close_button(pressed=False):
    c = Cv(16, 16)
    beam_h(c, 1, 1, 14, 14, 41)
    c.rect(3, 3, 12, 12, W_DK if not pressed else W_DD)
    for i in range(6):
        c.px(5 + i, 5 + i, X_HI); c.px(10 - i, 5 + i, X_HI)
        c.px(6 + i, 5 + i, X_DK); c.px(11 - i, 5 + i, X_DK)
    c.cut(1, 1, 14, 14)
    c.outline()
    return c


def divider(h):
    """수락한 카드와 수락 가능한 카드 사이 세로 각목 + 못"""
    c = Cv(8, h)
    beam_v(c, 1, 1, 6, h - 2, 51)
    for y in (6, h - 8):
        c.px(3, y, I_HI); c.px(4, y + 1, I_DK)
    c.outline()
    return drop_shadow(c, 2, 2, 90)


def scroll_track(w):
    c = Cv(w, 6)
    c.rect(0, 0, w - 1, 5, K_DD)
    c.hl(1, w - 2, 0, (0, 0, 0, 255))
    c.hl(1, w - 2, 5, K_LT)
    c.cut(0, 0, w - 1, 5)
    return c


def scroll_thumb(w):
    c = Cv(w, 8)
    beam_h(c, 1, 1, w - 2, 6, 61)
    for x in range(w // 2 - 3, w // 2 + 4, 3):  # 손잡이 홈
        c.vl(x, 3, 4, W_DD)
    c.cut(1, 1, w - 2, 6)
    c.outline()
    return c


# ---------- 카드 ----------
CW, CH = 96, 176


CARD_SEED = 7       # 임포트 카드 3색은 같은 실루엣 (완료 가능 테두리 하나를 같이 씀)
RULE_Y = 47         # 제목 아래 잉크 구분선 (카드에 구움)


def card(tab='gold', seed=CARD_SEED, rule=True):
    """세로 양피지: 찢긴 가장자리 · 가장자리 바램 · 섬유 결 · 오른쪽 아래 접힌 귀 · 위 가운데 탭 핀"""
    r = random.Random(seed)
    c = Cv(CW, CH)
    x0, y0, x1, y1 = 1, 4, CW - 2, CH - 2
    # 찢긴 가장자리: 변마다 1~2px 홈
    inside = set((x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1))
    for x in range(x0, x1 + 1):
        if r.random() < .16:
            inside.discard((x, y0))
            if r.random() < .3: inside.discard((x + 1, y0))
        if r.random() < .16:
            inside.discard((x, y1))
    for y in range(y0, y1 + 1):
        if r.random() < .14:
            inside.discard((x0, y))
            if r.random() < .3: inside.discard((x0, y + 1))
        if r.random() < .14:
            inside.discard((x1, y))
    fold = 9  # 오른쪽 아래 접힌 귀
    for x in range(x1 - fold + 1, x1 + 1):
        for y in range(y1 - fold + 1, y1 + 1):
            if (x - (x1 - fold + 1)) + (y - (y1 - fold + 1)) >= fold - 1:
                inside.discard((x, y))
    # 가장자리까지 거리로 바램
    def edge_d(x, y):
        d = 0
        while d < 6 and all((x + dx, y + dy) in inside for dx, dy in ((-d - 1, 0), (d + 1, 0), (0, -d - 1), (0, d + 1))):
            d += 1
        return d
    noise = {}
    for (x, y) in inside:
        noise[(x, y)] = r.random()
    for (x, y) in inside:
        d = edge_d(x, y)
        n = noise[(x, y)]
        if d == 0:
            col = P5 if n < .7 else P4
        elif d == 1:
            col = P4 if n < .55 else P3
        elif d <= 3:
            col = P3 if n < .45 else P2
        else:
            col = P2 if n < .25 else P1
        c.px(x, y, col)
    # 왼쪽 위 빛: 위 · 왼 안쪽 줄 한 단계 밝게
    for (x, y) in inside:
        if (x - 1, y) not in inside or (x, y - 1) not in inside:
            if c.get(x + 1, y + 1) is not None and noise[(x, y)] > .35:
                c.px(x + 1, y + 1, P1 if c.get(x + 1, y + 1) in (P2, P3) else c.get(x + 1, y + 1))
    # 바랜 얼룩 (낮은 대비, 가장자리 쪽)
    for _ in range(5):
        cx = r.choice([r.randint(6, 20), r.randint(CW - 22, CW - 8)])
        cy = r.randint(20, CH - 30)
        rx, ry = r.randint(4, 8), r.randint(3, 6)
        for y in range(cy - ry, cy + ry + 1):
            for x in range(cx - rx, cx + rx + 1):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 and c.get(x, y) in (P1, P2) and r.random() < .8:
                    c.px(x, y, P3 if c.get(x, y) == P2 else P2)
    # 섬유 결: 짧은 가로 줄
    for _ in range(70):
        x, y = r.randint(x0 + 4, x1 - 6), r.randint(y0 + 4, y1 - 4)
        ln = r.randint(2, 4)
        col = r.choice([P0, P3, P3])
        for k in range(ln):
            if c.get(x + k, y) in (P1, P2):
                c.px(x + k, y, col)
    # 접힌 귀: 잘린 대각선 바로 안쪽 삼각형을 뒷면색으로, 접힌 선은 밝게
    fx0, fy0 = x1 - fold + 1, y1 - fold + 1
    for i in range(fold):
        for j in range(fold):
            x, y = fx0 + i, fy0 + j
            s = i + j
            if fold - 5 <= s <= fold - 2 and i >= 2 and j >= 2:
                c.px(x, y, P4 if s < fold - 2 else P5)
    for i in range(fold):
        x, y = fx0 + i, fy0 + (fold - 1 - i)
        if c.get(x - 1, y - 1) is not None:
            c.px(x - 1, y - 1, P0 if i % 2 else P1)
    if rule:
        ink_rule(c, 10, CW - 12, RULE_Y, seed)
    # 외곽선: 아래 · 오른쪽 진하게, 위 · 왼쪽은 양피지 가장 어두운 색
    out = []
    for y in range(CH):
        for x in range(CW):
            if c.p[y][x] is None:
                ns = [(dx, dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if c.get(x + dx, y + dy) is not None]
                if ns:
                    dark = any(dx == -1 or dy == -1 for dx, dy in ns)
                    out.append((x, y, OUT if dark else P6))
    for x, y, col in out:
        c.px(x, y, col)
    # 탭 핀 (게시판 타일 쪽지와 같은 모양: 위로 솟은 색 띠)
    hi, md, dk = TAB[tab]
    tx = CW // 2 - 2
    c.rect(tx, 0, tx + 3, 7, md)
    c.vl(tx, 0, 7, hi)
    c.vl(tx + 3, 1, 7, dk)
    c.hl(tx, tx + 3, 8, dk)
    c.px(tx + 1, 1, hi)
    for y in range(1, 4):                # 종이 위로 솟은 부분만 외곽선
        c.px(tx - 1, y, OUT)
        c.px(tx + 4, y, OUT)
    c.hl(tx + 1, tx + 4, 9, P4)          # 탭 그림자 (종이에)
    return drop_shadow(c, 3, 3, 120)


def card_glow(seed=CARD_SEED):
    """완료 가능 카드 뒤에 까는 황동빛 테: 카드 실루엣을 3px 넓힌 띠 (안쪽일수록 진하게)"""
    base = card('gold', seed, rule=False)
    w, h = base.w + 6, base.h + 6
    solid = set((x + 3, y + 3) for y in range(base.h) for x in range(base.w)
                if base.p[y][x] is not None and base.p[y][x][3] > 200)
    c = Cv(w, h)
    ring = solid
    for a in (210, 130, 60):
        nxt = set()
        for (x, y) in ring:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if q not in solid and c.get(*q) is None and 0 <= q[0] < w and 0 <= q[1] < h:
                    nxt.add(q)
        for q in nxt:
            c.px(q[0], q[1], (232, 196, 106, a))
        solid = solid | nxt
        ring = nxt
    return c


def ink_rule(c, x0, x1, y, seed=3):
    """손으로 그은 잉크 구분선"""
    r = random.Random(seed)
    yy = y
    for x in range(x0, x1 + 1):
        if r.random() < .08:
            yy = y + r.choice([-1, 0, 0, 1]) if yy == y else y
        if r.random() < .92:
            c.px(x, yy, P5 if r.random() < .8 else P4)


def ribbon():
    """'필수' 리본 (스토리 퀘스트): 왼쪽 위 모서리, 오른쪽 끝은 제비꼬리"""
    hi, md, dk = TAB['red']
    c = Cv(30, 12)
    for y in range(1, 11):
        for x in range(1, 28):
            notch = x >= 24 and abs(y - 5.5) < (x - 23) * 1.2
            if not notch:
                c.px(x, y, md)
    c.hl(1, 26, 1, hi)
    c.hl(1, 24, 10, dk)
    c.vl(1, 1, 10, hi)
    for x in range(3, 23, 2):
        c.px(x, 2, hi if x % 4 else md)
    c.outline()
    return c


def stamp():
    """완료 도장: 붉은 이중 원 + 가운데 '완료' (12px 구움), 잉크 번짐 · 빠짐"""
    n = 48
    c = Cv(n, n)
    r = random.Random(7)
    cx = cy = (n - 1) / 2
    for y in range(n):
        for x in range(n):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** .5
            if 20 <= d <= 22.6 or 16.5 <= d <= 17.6:
                if r.random() > .1:
                    c.px(x, y, STAMP if r.random() > .25 else H('8a2420'))
    # 글자
    f = ImageFont.truetype(FONT_TTF, 12)
    im = Image.new('L', (40, 16), 0)
    d = ImageDraw.Draw(im)
    d.fontmode = '1'
    d.text((0, 0), '완료', font=f, fill=255)
    bb = im.getbbox()
    gw, gh = bb[2] - bb[0], bb[3] - bb[1]
    ox, oy = int(cx - gw / 2) + 1, int(cy - gh / 2) + 1
    for j in range(bb[1], bb[3]):
        for i in range(bb[0], bb[2]):
            if im.getpixel((i, j)) > 128 and r.random() > .08:
                c.px(ox + i - bb[0], oy + j - bb[1], STAMP)
                if r.random() < .5:
                    c.px(ox + i - bb[0] + 1, oy + j - bb[1], STAMP)   # 굵게 (도장 획)
    # 위 · 아래 별점
    for x, y in ((cx - 9, cy - 11), (cx + 9, cy - 11), (cx - 9, cy + 11), (cx + 9, cy + 11)):
        c.px(int(x), int(y), STAMP)
    # 반투명 잉크
    for y in range(n):
        for x in range(n):
            p = c.p[y][x]
            if p is not None:
                c.p[y][x] = (p[0], p[1], p[2], 215)
    return c


# ---------- 버튼 ----------
def button(state, w=72, h=18):
    """state: normal · hover · pressed · disabled · turnin. 9-slice 가로 테두리 6px."""
    c = Cv(w, h)
    if state == 'turnin':
        hi, lt, md, dk, dd = BR_HI, BR_LT, BR_MD, BR_DK, BR_DD
    elif state == 'disabled':
        hi, lt, md, dk, dd = D_HI, D_HI, D_MD, D_DK, D_DK
    elif state == 'hover':
        hi, lt, md, dk, dd = H('84604a'), W_HI, H('604838'), W_LT, W_DD
    else:
        hi, lt, md, dk, dd = W_HI, W_LT, W_MD, W_DK, W_DD
    top = 2 if state == 'pressed' else 1
    bot = h - 2 if state == 'pressed' else h - 4
    c.rect(1, top, w - 2, bot, md)
    if state != 'disabled':
        c.hl(2, w - 3, top, hi)
        c.hl(1, w - 2, top + 1, lt)
        c.vl(1, top, bot, lt)
        c.vl(w - 2, top, bot, dk)
        c.speckle(3, top + 2, w - 4, bot - 1, [lt, dk], w // 3, 71, only=[md])
    if state != 'pressed':
        c.rect(1, bot + 1, w - 2, h - 2, dd)   # 두께 (아래 앞면)
        c.hl(2, w - 3, bot + 1, dk)
    c.cut(1, top, w - 2, h - 2)
    c.outline()
    return c


# ---------- 시안 그리기 ----------
def font(sz):
    return ImageFont.truetype(FONT_TTF, sz)


def blit(img, cv, x, y, s=S):
    im = cv.img()
    im = im.resize((im.width * s, im.height * s), Image.NEAREST)
    img.alpha_composite(im, (int(x), int(y)))


def text(img, x, y, s, sz, col, anchor='la', shadow=None, width=None, spacing=6):
    d = ImageDraw.Draw(img)
    d.fontmode = '1'
    f = font(sz)
    lines = wrap(s, f, width) if width else s.split('\n')
    for i, ln in enumerate(lines):
        yy = y + i * (sz + spacing)
        if shadow:
            d.text((x + 2, yy + 2), ln, font=f, fill=shadow, anchor=anchor)
        d.text((x, yy), ln, font=f, fill=col, anchor=anchor)
    return y + len(lines) * (sz + spacing)


def wrap(s, f, width):
    out, cur = [], ''
    for word in s.split(' '):
        t = (cur + ' ' + word).strip()
        if f.getlength(t) <= width:
            cur = t
        else:
            if cur:
                out.append(cur)
            cur = word
    if cur:
        out.append(cur)
    return out


ICONS = {}


def icon(key):
    if not ICONS:
        mat = paths.repo('Assets', '04.Sprites', 'Material', 'material-icons.png')
        ICONS['iron'] = sprite_from_sheet(mat, internal_id=1719320820)
        ICONS['lead'] = sprite_from_sheet(mat, internal_id=1787881068)
        ICONS['herb'] = sprite_from_sheet(mat, internal_id=-732418233)
        ICONS['whetstone'] = sprite_from_sheet(paths.repo('Assets', '04.Sprites', 'Quest', 'quest-icons.png'),
                                               internal_id=-1700376075)
    return ICONS[key]


def draw_card(img, x, y, q):
    """q: dict(title, giver, desc, rewards[(icon, n)], xp, tab, state, progress)"""
    st = q['state']
    if st == 'ready':
        blit(img, card_glow(), x - 3 * S, y - 3 * S)
    blit(img, card(q.get('tab', 'gold')), x, y)
    cx = x + CW * S // 2
    if q.get('story'):
        blit(img, ribbon(), x + 2 * S, y + 9 * S)
        text(img, x + 15 * S, y + 15 * S, '필수', 18, X_HI, anchor='mm')
    ty = y + 24 * S
    text(img, cx, ty, q['title'], 30, INK, anchor='ma')
    text(img, cx, ty + 40, '의뢰인 · ' + q['giver'], 18, INK_SOFT, anchor='ma')
    yy = text(img, x + 24, ty + 90, q['desc'], 22, INK, width=CW * S - 48)
    if q.get('progress'):
        text(img, x + 24, yy + 6, q['progress'], 22, H('7a4a1c'))
    # 보상
    ry = y + 116 * S
    text(img, x + 24, ry, '보상', 18, INK_SOFT)
    text(img, x + 80, ry - 2, '%d XP' % q['xp'], 22, INK)
    ix = x + 22
    iy = ry + 26
    for key, n in q['rewards']:
        blit(img, icon(key), ix, iy)
        ix += 50
        if n > 1:
            text(img, ix - 2, iy + 22, 'x%d' % n, 18, INK_SOFT)
            ix += 30
    # 아래 영역
    by = y + 150 * S
    bx = x + (CW - 72) // 2 * S
    if st == 'available':
        blit(img, button('normal'), bx, by)
        text(img, cx, by + 7 * S, '수락', 22, X_HI, anchor='mm', shadow=W_DD)
    elif st == 'full':
        blit(img, button('disabled'), bx, by)
        text(img, cx, by + 7 * S, '수락 불가', 22, H('9a9384'), anchor='mm')
    elif st == 'ready':
        blit(img, button('turnin'), bx, by)
        text(img, cx, by + 7 * S, '퀘스트 완료', 22, H('2a2010'), anchor='mm')
    elif st == 'active':
        text(img, cx, by + 9 * S, '퀘스트 취소', 18, GRAY_TXT, anchor='mm')
    elif st == 'story_active':
        text(img, cx, by + 9 * S, '진행 중', 18, GRAY_TXT, anchor='mm')
    elif st == 'story_ready':
        text(img, cx, by + 5 * S, q['giver'] + '에게 보고', 22, H('7a4a1c'), anchor='mm')
        text(img, cx, by + 13 * S, '쉘터에서 말을 걸어 주세요', 16, GRAY_TXT, anchor='mm')
    if st == 'done':                     # 버튼 자리에 도장 (설명 글자를 가리지 않게)
        blit(img, stamp(), x + 24 * S, y + 124 * S)


Q_STORY = dict(title='마법사 구출', giver='로나', story=True, tab='red', seed=4, state='story_active',
               desc='H2 홀에 붙잡힌 마법사 엘레나를 구출하고 살아서 탈출하세요.', xp=500, rewards=[('herb', 3)])
Q_READY = dict(title='대장간 인증서', giver='그룬', tab='gold', seed=7, state='ready',
               desc='Z1 대장간의 존 상자에서 인증서를 찾아 살아서 탈출하세요.', xp=200, rewards=[('iron', 2)])
Q_ACTIVE = dict(title='약초 채집', giver='로나', tab='blue', seed=9, state='active',
                desc='치유 약초를 모아 살아서 탈출하세요.', progress='치유 약초 0/3', xp=150,
                rewards=[('herb', 2), ('lead', 1)])
Q_FULL1 = dict(title='잃어버린 연장', giver='그룬', tab='gold', seed=12, state='full',
               desc='Z2 드워프 공방의 회수 상자에서 연장 꾸러미를 되찾아 오세요.', xp=250, rewards=[('iron', 1), ('lead', 2)])
Q_FULL2 = dict(title='두 존의 보물', giver='로나', tab='blue', seed=15, state='full',
               desc='Z3 · Z4 사이 어딘가의 보물 상자를 찾아 살아서 탈출하세요.', xp=300, rewards=[('lead', 3)])


def screen(cards_left, cards_right, counter, full, popup=False):
    """1920x1080 화면 하나: 어둡게 덮은 쉘터 위 게시판"""
    W, Hh = 1920, 1080
    img = Image.new('RGBA', (W, Hh), (30, 22, 17, 255))
    # 뒤 배경(쉘터 바닥 느낌) 살짝
    r = random.Random(1)
    d = ImageDraw.Draw(img)
    for _ in range(900):
        x, y = r.randrange(0, W, S * 4), r.randrange(0, Hh, S * 4)
        d.rectangle((x, y, x + S * 4 - 1, y + S * 4 - 1), fill=(40 + r.randint(0, 8), 32, 26, 255))
    img.alpha_composite(Image.new('RGBA', (W, Hh), (0, 0, 0, 140)))
    BW, BH = 540, 250
    bx, by = (W - BW * S) // 2, (Hh - BH * S) // 2
    # 코르크 (틀 안쪽에만, Image Tiled)
    ci = cork_tile().img().resize((32 * S, 32 * S), Image.NEAREST)
    inner = (bx + 13 * S, by + 25 * S, bx + (BW - 13) * S, by + (BH - 13) * S)
    layer = Image.new('RGBA', (inner[2] - inner[0], inner[3] - inner[1]))
    for yy in range(0, layer.height, 32 * S):
        for xx in range(0, layer.width, 32 * S):
            layer.alpha_composite(ci, (xx, yy))
    img.alpha_composite(layer, inner[:2])
    # 카드 (보이는 영역 = 코르크 안쪽, 넘치는 카드는 잘림)
    view = Image.new('RGBA', (W, Hh), (0, 0, 0, 0))
    x = bx + 22 * S
    cy = by + 38 * S
    for q in cards_left:
        draw_card(view, x, cy, q)
        x += (CW + 12) * S
    blit(view, divider(BH - 46), x - 2 * S, by + 27 * S)
    x += 14 * S
    for q in cards_right:
        draw_card(view, x, cy, q)
        x += (CW + 12) * S
    mask = Image.new('L', (W, Hh), 0)
    ImageDraw.Draw(mask).rectangle((bx + 13 * S, by + 25 * S, bx + (BW - 13) * S - 1, by + (BH - 13) * S - 1), fill=255)
    img.paste(view, (0, 0), Image.composite(view, Image.new('RGBA', (W, Hh)), mask).split()[3])
    # 안쪽 그늘 (틀 아래로 코르크가 들어간 느낌)
    sh = Image.new('RGBA', (W, Hh), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    for k, a in ((0, 120), (1, 70), (2, 30)):
        sd.rectangle((bx + (13 + k) * S, by + (25 + k) * S, bx + (BW - 13) * S - 1, by + (25 + k) * S + S - 1), fill=(0, 0, 0, a))
        sd.rectangle((bx + (13 + k) * S, by + (25 + k) * S, bx + (13 + k) * S + S - 1, by + (BH - 13) * S - 1), fill=(0, 0, 0, a))
    img.alpha_composite(sh)
    # 틀
    fr = board(BW, BH)
    for yy in range(25, BH - 13):        # 틀 안쪽(코르크 자리)은 비움
        for xx in range(13, BW - 13):
            fr.p[yy][xx] = None
    blit(img, fr, bx, by)
    # 스크롤
    blit(img, scroll_track(BW - 60), bx + 30 * S, by + (BH - 22) * S)
    blit(img, scroll_thumb(150), bx + 34 * S, by + (BH - 23) * S)
    # 머리 팻말 · 수주 수 · 닫기
    pl = plaque(150, 26)
    blit(img, pl, bx + (BW - 150) // 2 * S, by - 4 * S)
    text(img, bx + BW * S // 2, by + 9 * S, '의뢰 게시판', 30, X_HI, anchor='mm', shadow=W_DD)
    cp = plaque(70, 18, 8)
    blit(img, cp, bx + (BW - 120) * S, by + 3 * S)
    text(img, bx + (BW - 85) * S, by + 12 * S, '수주', 20, X_MD, anchor='mm')
    text(img, bx + (BW - 66) * S, by + 12 * S, counter, 22, H('e07060') if full else X_HI, anchor='mm', shadow=W_DD)
    blit(img, close_button(), bx + (BW - 26) * S, by + 4 * S)
    if popup:
        img.alpha_composite(Image.new('RGBA', (W, Hh), (0, 0, 0, 150)))
        pw, ph = 170, 84
        px, py = (W - pw * S) // 2, (Hh - ph * S) // 2
        p = popup_panel(pw, ph)
        blit(img, p, px, py)
        text(img, px + pw * S // 2, py + 20 * S, '의뢰를 취소할까요?', 30, INK, anchor='mm')
        text(img, px + pw * S // 2, py + 38 * S, '[약초 채집]은 게시판으로 돌아가고,', 20, INK_SOFT, anchor='mm')
        text(img, px + pw * S // 2, py + 47 * S, '언제든 다시 받을 수 있어요.', 20, INK_SOFT, anchor='mm')
        for i, (lab, stt) in enumerate((('취소하기', 'normal'), ('돌아가기', 'normal'))):
            bxx = px + (14 + i * 76) * S
            blit(img, button(stt), bxx, py + 60 * S)
            text(img, bxx + 36 * S, py + 67 * S, lab, 22, X_HI, anchor='mm', shadow=W_DD)
    return img


def popup_panel(w, h):
    """확인 팝업: 카드와 같은 양피지 + 나무 테 (9-slice 8)"""
    c = Cv(w, h)
    beam_h(c, 1, 1, w - 2, h - 2, 81)
    r = random.Random(5)
    for y in range(6, h - 6):
        for x in range(6, w - 6):
            d = min(x - 6, y - 6, w - 7 - x, h - 7 - y)
            n = r.random()
            col = (P4 if n < .6 else P3) if d == 0 else (P3 if n < .4 else P2) if d < 3 else (P2 if n < .2 else P1)
            c.px(x, y, col)
    c.hl(6, w - 7, 5, W_DD); c.vl(5, 5, h - 7, W_DD)
    c.hl(6, w - 7, h - 6, W_HI); c.vl(w - 6, 6, h - 6, W_HI)
    for _ in range(80):
        x, y = r.randint(10, w - 14), r.randint(9, h - 9)
        for k in range(r.randint(2, 4)):
            if c.get(x + k, y) in (P1, P2):
                c.px(x + k, y, r.choice([P0, P3]))
    for (x, y) in ((3, 3), (w - 4, 3), (3, h - 4), (w - 4, h - 4)):
        c.px(x, y, I_HI); c.px(x + 1, y + 1, I_DK)
    c.cut(1, 1, w - 2, h - 2)
    c.outline()
    return c


def states_panel():
    """상태별 카드 · 버튼 · 마커 모음 (1920 x 760)"""
    W, Hh = 1920, 760
    img = Image.new('RGBA', (W, Hh), (34, 28, 24, 255))
    cork = cork_tile().img().resize((32 * S, 32 * S), Image.NEAREST)
    for yy in range(0, 640, 32 * S):
        for xx in range(0, 1320, 32 * S):
            img.alpha_composite(cork, (xx, yy + 60))
    text(img, 24, 14, '카드 상태 (코르크 위, 3배)', 26, H('e8dcb6'))
    qa = dict(Q_FULL1, state='available', seed=21)
    qd = dict(Q_READY, state='done', seed=22)
    qs = dict(Q_STORY, title='대장간의 숫돌', giver='그룬', state='story_ready', seed=23,
              desc='Z1 대장간의 존 상자에서 정밀 숫돌을 찾아 살아서 탈출하세요.', xp=300,
              rewards=[('iron', 1), ('lead', 1), ('herb', 3)])
    labels = ['수락 가능 (칸 남음)', '완료 처리 직후 (도장)', '스토리 · 완료 가능']
    for i, q in enumerate((qa, qd, qs)):
        x = 40 + i * 420
        draw_card(img, x, 120, q)
        text(img, x + CW * S // 2, 84, labels[i], 22, H('e8dcb6'), anchor='mm')
    # 오른쪽: 버튼 · 마커
    ox = 1360
    text(img, ox, 14, '버튼 (72x18, 9-slice 가로 6)', 26, H('e8dcb6'))
    rows = [('normal', '수락', X_HI), ('hover', '수락', X_HI), ('pressed', '수락', X_HI),
            ('disabled', '수락 불가', H('9a9384')), ('turnin', '퀘스트 완료', H('2a2010'))]
    names = ['기본', '올림', '누름', '비활성', '완료 (황동)']
    for i, (st, lab, col) in enumerate(rows):
        y = 70 + i * 74
        blit(img, button(st), ox + 120, y)
        text(img, ox + 120 + 36 * S, y + 7 * S + (S if st == 'pressed' else 0), lab, 22, col, anchor='mm',
             shadow=W_DD if st not in ('disabled', 'turnin') else None)
        text(img, ox, y + 18, names[i], 20, H('c9b98f'))
    text(img, ox + 120 + 36 * S, 70 + 5 * 74 + 18, '퀘스트 취소', 18, GRAY_TXT, anchor='mm')
    text(img, ox, 70 + 5 * 74 + 8, '취소 글씨', 20, H('c9b98f'))
    # 쉘터 게시판 + 마커
    y0 = 520
    text(img, ox, y0 - 30, '쉘터 게시판 위 마커 (게임 화면 4배)', 22, H('e8dcb6'))
    wb = sprite_from_sheet(paths.repo('Assets', '04.Sprites', 'Tile', 'ZoneProps', 'Shelter', 'Shelter.png'), 'QuestBoard_Stand')
    eb = paths.repo('Assets', '04.Sprites', 'UI', 'EmotionBubble', 'EmotionBubbles.png')
    for i, (nm, lab) in enumerate((('EmotionBubble_Alert', '새 의뢰'), ('EmotionBubble_Question', '완료 가능'))):
        x = ox + i * 270
        blit(img, wb, x, y0 + 40, 4)
        blit(img, sprite_from_sheet(eb, nm), x + 25 * 4 - 32, y0 - 10 + 8, 4)
        text(img, x + 100, y0 + 260, lab, 20, H('c9b98f'), anchor='mm')
    return img


def parts_panel():
    """부품 원본 (4배) — 임포트할 스프라이트 목록"""
    W, Hh = 1920, 470
    img = Image.new('RGBA', (W, Hh), (34, 28, 24, 255))
    text(img, 24, 14, '스프라이트 부품 (원본 px, 카드 2배 · 나머지 4배)', 26, H('e8dcb6'))
    items = [(card('gold', 1), 2, 'card 96x176 +그림자 3 (탭 3색)'), (board(64, 64), 4, 'board 9-slice (위 24 · 나머지 12)'),
             (stamp(), 4, 'stamp 48x48'), (cork_tile(), 4, 'cork 32x32 (Tiled)'),
             (divider(60), 4, 'divider (세로 9-slice)')]
    small = [(ribbon(), 'ribbon_required 30x12'), (plaque(64, 18, 8), 'plaque 9-slice 6'),
             (close_button(), 'close 16x16'), (scroll_thumb(40), 'scroll_thumb 9-slice 6'),
             (scroll_track(40), 'scroll_track 9-slice 2')]
    x = 24
    for cv, s, lab in items:
        blit(img, cv, x, 60, s)
        text(img, x, 60 + cv.h * s + 10, lab, 18, H('c9b98f'))
        x += max(cv.w * s, 150) + 40
    y = 60
    for cv, lab in small:
        blit(img, cv, x, y, 4)
        text(img, x + cv.w * 4 + 16, y + cv.h * 2 - 10, lab, 18, H('c9b98f'))
        y += cv.h * 4 + 20
    return img


def main(outp):
    a = screen([Q_STORY, Q_READY, Q_ACTIVE], [Q_FULL1, Q_FULL2], '3/3', True)
    b = states_panel()
    c = parts_panel()
    p = screen([Q_STORY, Q_READY, Q_ACTIVE], [Q_FULL1, Q_FULL2], '3/3', True, popup=True)
    pad = 60
    W = 1920
    total = Image.new('RGBA', (W, pad + 1080 + pad + 1080 + pad + b.height + pad + c.height), (20, 16, 14, 255))
    y = 0
    text(total, 24, 12, '퀘스트 게시판 UI 시안 %s — ① 게시판 (수주 3/3: 스토리 + 의뢰 2, 수락 불가)' % VERSION, 30, H('ffffff'))
    y += pad
    total.alpha_composite(a, (0, y)); y += 1080
    text(total, 24, y + 12, '② 퀘스트 취소 확인 팝업', 30, H('ffffff'))
    y += pad
    total.alpha_composite(p, (0, y)); y += 1080
    text(total, 24, y + 12, '③ 상태 · 버튼 · 마커', 30, H('ffffff'))
    y += pad
    total.alpha_composite(b, (0, y)); y += b.height
    y += pad
    total.alpha_composite(c, (0, y))
    total.convert('RGB').save(outp)
    print('saved', outp, total.size)


# ---------- 임포트 (Assets/04.Sprites/UI/QuestBoard/quest-board.png · meta) ----------
SHEET_DIR = ('Assets', '04.Sprites', 'UI', 'QuestBoard')
SHEET = 'quest-board'
BOARD_W, BOARD_H = 540, 250      # 게시판 원본 px (화면 = 3배)
FRAME_SRC = (128, 96)            # 틀 원본: Image Tiled, 가운데 비움


def frame_src():
    w, h = FRAME_SRC
    c = board(w, h)
    for y in range(25, h - 14):  # 안쪽 홈(왼 13 · 위 24 · 오 w-14 · 아래 h-14)은 테두리에 남김
        for x in range(14, w - 14):
            c.p[y][x] = None
    return c


def import_items():
    """(이름, Cv, border(왼, 아래, 오, 위))"""
    return [
        ('qb_frame', frame_src(), (14, 14, 14, 25)),
        ('qb_cork', cork_tile(), None),
        ('qb_card_red', card('red'), None),
        ('qb_card_gold', card('gold'), None),
        ('qb_card_blue', card('blue'), None),
        ('qb_card_glow', card_glow(), None),
        ('qb_stamp', stamp(), None),
        ('qb_ribbon', ribbon(), None),
        ('qb_header', plaque(150, 26), None),
        ('qb_counter', plaque(70, 18, 8), None),
        ('qb_close', close_button(), None),
        ('qb_close_pressed', close_button(True), None),
        ('qb_btn_normal', button('normal'), None),
        ('qb_btn_hover', button('hover'), None),
        ('qb_btn_pressed', button('pressed'), None),
        ('qb_btn_disabled', button('disabled'), None),
        ('qb_btn_turnin', button('turnin'), None),
        ('qb_divider', divider(BOARD_H - 46), None),
        ('qb_scroll_track', scroll_track(BOARD_W - 60), (2, 0, 2, 0)),
        ('qb_scroll_thumb', scroll_thumb(40), (6, 0, 6, 0)),
        ('qb_popup', popup_panel(170, 84), None),
    ]


def do_import():
    import os, glob
    from draftlib import sheet
    d = paths.repo(*SHEET_DIR)
    png, meta_p = os.path.join(d, SHEET + '.png'), os.path.join(d, SHEET + '.png.meta')
    items = import_items()
    sh, rects = sheet([{'id': n, 'fin': cv} for n, cv, _ in items])
    out_png = paths.out(png)
    sh.save(out_png)

    # 다시 만들 때는 텍스처 guid · internalID · spriteID 재사용 (프리팹 참조 유지)
    old_guid, old_iid, old_sid, old_sheet_sid = None, {}, {}, None
    if os.path.exists(meta_p):
        t = open(meta_p, encoding='utf8').read()
        old_guid = re.search(r'^guid: ([0-9a-f]{32})', t, re.M).group(1)
        for m in re.finditer(r'^      name: (\S+)\n(?:.*\n)*?      spriteID: (\w+)\n      internalID: (-?\d+)', t, re.M):
            old_sid[m.group(1)], old_iid[m.group(1)] = m.group(2), int(m.group(3))
        m = re.search(r'^    spriteID: (\w+)', t, re.M)
        old_sheet_sid = m.group(1) if m else None
    rnd = random.SystemRandom()
    taken = set()
    for p in glob.glob(paths.repo('Assets', '**', '*.meta'), recursive=True):
        if os.path.normpath(p) == os.path.normpath(meta_p):
            continue
        m = re.search(r'^guid: ([0-9a-f]{32})', open(p, encoding='utf8', errors='ignore').read(200), re.M)
        if m:
            taken.add(m.group(1))

    def hexid():
        while True:
            g = '%032x' % rnd.getrandbits(128)
            if g not in taken:
                taken.add(g)
                return g

    tpl = open(paths.FORGE_META, encoding='utf8').read()
    head = tpl[:tpl.index('    sprites:\n') + len('    sprites:\n')]
    head = re.sub(r'^guid: [0-9a-f]{32}', 'guid: ' + (old_guid or hexid()), head, flags=re.M)
    head = re.sub(r'textureCompression: \d', 'textureCompression: 0', head)
    tail = tpl[tpl.rindex('\n    outline: []') + 1:]
    tail = re.sub(r'^    spriteID: \w+', '    spriteID: ' + (old_sheet_sid or hexid()), tail, flags=re.M)
    tail = re.sub(r'(    nameFileIdTable:\n)(?:      .*\n)*', r'\1@@TABLE@@\n', tail)

    border = {n: b for n, _, b in items}
    ids, body = {}, []
    for (name, rx, ry, w, h) in rects:
        iid = old_iid.get(name, 0)
        while iid == 0 or iid in ids.values():
            iid = rnd.randint(-2**31, 2**31 - 1)
        ids[name] = iid
        b = border[name] or (0, 0, 0, 0)
        body.append(
            '    - serializedVersion: 2\n      name: %s\n      rect:\n        serializedVersion: 2\n'
            '        x: %d\n        y: %d\n        width: %d\n        height: %d\n'
            '      alignment: 0\n      pivot: {x: 0.5, y: 0.5}\n      border: {x: %d, y: %d, z: %d, w: %d}\n      customData: \n'
            '      outline: []\n      physicsShape: []\n      tessellationDetail: 0\n      bones: []\n      spriteID: %s\n'
            '      internalID: %d\n      vertices: []\n      indices: \n      edges: []\n      weights: []\n'
            % (name, rx, sh.height - ry - h, w, h, b[0], b[1], b[2], b[3], old_sid.get(name) or hexid(), iid))
    table = '\n'.join('      %s: %d' % (k, v) for k, v in sorted(ids.items()))
    open(paths.out(meta_p), 'w', encoding='utf8', newline='\n').write(head + ''.join(body) + tail.replace('@@TABLE@@', table))
    print('saved', out_png, sh.size, len(ids), 'sprites')


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'import':
        do_import()
    else:
        main(sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'quest-board-draft.png'))
