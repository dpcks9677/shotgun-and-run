"""HUD 마지막 탄환(크리티컬 확정) 표시 시안: 붉은 몸통 + 테두리 밖으로 나오는 굵은 흰 스파크 9종(2개 이상 겹쳐 재생 가능).
    python crit_shell.py            시안 PNG · GIF(Docs/drafts/props/crit-shell-draft.png · .gif)만 쓴다. Assets는 건드리지 않는다.
    python crit_shell.py import     확정 색(R2)으로 시트 Assets/04.Sprites/UI/CritShell.png를 쓴다. 이미 있으면 중단. 슬라이스는 unity/crit_shell_import.cs

기준: ui-atlas.png의 hud_shell(18x9). 몸통만 붉게 바꾸고, 스파크는 테두리 밖까지 나가는 별도 오버레이(22x13, 사방 2px 여유)로 그린다.
오버레이는 마지막 탄환 칸 위에 겹치는 자식 Image로 재생한다. 스파크 오버레이를 여러 개 동시에 켜면 겹쳐 보인다.
HUD는 BottomToTop이라 마지막 탄환 = 맨 아래 칸."""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
ATLAS = os.path.join(REPO, 'Assets', '04.Sprites', 'UI', 'ui-atlas.png')
OUT = os.path.join(REPO, 'Docs', 'drafts', 'props', 'crit-shell-draft')
VERSION = 'v06'
FINAL = 1                                               # 확정 색: OPTIONS[1] = R2 장미
SHEET = os.path.join(REPO, 'Assets', '04.Sprites', 'UI', 'CritShell.png')

W, Hh = 18, 9
M = 2                                                   # 오버레이 사방 여유
OW, OH = W + 2 * M, Hh + 2 * M                          # 22 x 13
BODY_ROWS = {2: 0, 3: 1, 4: 2, 5: 2, 6: 0}
BODY_X = range(1, 13)

NORMAL = [(105, 33, 55, 255), (157, 39, 53, 255), (230, 60, 69, 255)]
OPTIONS = [
    ('R1 진홍', [(110, 20, 60, 255), (175, 28, 70, 255), (245, 55, 95, 255)]),
    ('R2 장미', [(120, 25, 85, 255), (190, 35, 105, 255), (250, 60, 125, 255)]),
    ('R3 선홍 (가장 빨강에 가까움)', [(130, 16, 48, 255), (205, 24, 60, 255), (255, 48, 80, 255)]),
]

WHITE = (255, 255, 255, 255)

# 스파크 9종 (탄환 좌표, 몸통 y 2..6 · 테두리 y 1 · 7 · 밖 y 0 · 8).
# 'bolt': 위아래 2px 두께로 그리고, 위 · 아래 테두리에 닿으면 테두리를 지나 밖으로 한 줄 더 내민다. 'raw': 찍힌 그대로.
SPARKS = [
    ('bolt', [(3, 5), (4, 4), (5, 4), (6, 3), (7, 3), (8, 2)]),                                   # 우상향 지그재그
    ('bolt', [(9, 2), (9, 3), (8, 4), (8, 5), (7, 6), (11, 5)]),                                  # 위에서 아래로
    ('bolt', [(2, 3), (3, 3), (3, 4), (4, 5), (5, 5), (5, 6)]),                                   # 좌하향
    ('bolt', [(10, 6), (10, 5), (9, 4), (10, 3), (11, 2), (2, 2)]),                               # 오른쪽 세로 + 왼쪽 점
    ('bolt', [(6, 2), (6, 3), (7, 4), (8, 5), (9, 6), (5, 5), (4, 6)]),                           # 갈라짐
    ('bolt', [(1, 4), (2, 3), (3, 3), (4, 4), (5, 4), (6, 5), (7, 5), (8, 4), (9, 4), (10, 3), (11, 3), (12, 4)]),   # 길게 가로지름
    ('bolt', [(3, 2), (4, 3), (5, 4), (6, 5), (7, 6), (8, 5), (9, 4), (10, 3), (11, 2)]),         # V자
    ('raw', [(x, y) for x in range(5, 14) for y in (4,)] + [(x, y) for x in (9, 10) for y in range(0, 9)]),   # 큰 십자 번쩍
    ('raw', [(3, 2), (3, 3), (3, 4), (2, 3), (4, 3), (11, 4), (11, 5), (11, 6), (10, 5), (12, 5),
             (3, 1), (3, 0), (11, 7), (11, 8)]),                                                  # 작은 십자 두 개 + 테두리 밖 줄
]
NAMES = ['지그재그', '위→아래', '좌하향', '점+세로', '갈라짐', '가로지름', 'V자', '큰 십자', '작은 십자 둘']


def shell(body):
    im = Image.open(ATLAS).convert('RGBA').crop((60, 28, 60 + W, 28 + Hh))      # Unity y 27 → 이미지 y 28
    px = im.load()
    for y, level in BODY_ROWS.items():
        for x in BODY_X:
            px[x, y] = body[level]
    return im


def put(im, x, y):
    if 0 <= x < OW and 0 <= y < OH:
        im.putpixel((x, y), WHITE)


def spark(i):
    im = Image.new('RGBA', (OW, OH), (0, 0, 0, 0))
    kind, pts = SPARKS[i]
    for x, y in pts:
        put(im, x + M, y + M)
        if kind == 'raw':
            continue
        put(im, x + M, y + M + 1)
        if y == 2:                                             # 윗 테두리에 닿으면 테두리를 지나 밖으로 한 줄 더
            put(im, x + M, y + M - 1)
            put(im, x + M, y + M - 2)
        if y >= 6:                                             # 아래 테두리도 마찬가지
            put(im, x + M, y + M + 2)
    return im


def dim(im):
    """빈 탄피(PlayerAmmoUI 기본값 0.25, 0.25, 0.25, 0.35)"""
    im = im.copy()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            px[x, y] = (int(r * .25), int(g * .25), int(b * .25), int(a * .35))
    return im


def stack(last_body, overlays, n=6, empty=2):
    """HUD 세로 탄창(오버레이 여유 포함): 위에서부터 소모, 맨 아래가 마지막 탄환 + 오버레이(여러 장이면 겹침)"""
    normal = shell(NORMAL)
    im = Image.new('RGBA', (OW, n * (Hh + 2) - 2 + 2 * M), (0, 0, 0, 0))
    for i in range(n):
        s = dim(normal) if i < empty else (shell(last_body) if i == n - 1 else normal)
        im.alpha_composite(s, (M, M + i * (Hh + 2)))
    for o in overlays:
        im.alpha_composite(o, (0, (n - 1) * (Hh + 2)))
    return im


def font(sz):
    for p in ('C:/Windows/Fonts/malgunbd.ttf', '/System/Library/Fonts/AppleSDGothicNeo.ttc'):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


# ((스파크 번호, ...), ms): 빈 튜플은 평소 모습. 평소엔 가만히 있고, 스파크가 불규칙한 간격으로 번쩍이며 가끔 2~3개가 동시에 겹친다.
TIMELINE = [((), 700), ((0,), 60), ((), 1100), ((5, 7), 70), ((), 350), ((3,), 60), ((), 1300), ((7,), 70), ((), 500),
            ((2, 4), 60), ((), 900), ((8, 5), 70), ((), 1200), ((6,), 60), ((), 400), ((1, 3, 8), 70), ((), 1000),
            ((7, 0), 70), ((), 300), ((5,), 60), ((), 1400), ((8,), 70), ((), 600), ((3, 6), 60), ((0, 4), 60), ((), 1000)]


def build_sheet():
    """시트 76x60: 맨 위 (1,1)에 Shell_Crit 18x9, 그 아래 y=12부터 스파크 22x13을 3열 x 3행(간격 2px, 칸 24x15)으로.
    좌표 규칙은 unity/crit_shell_import.cs와 같다 (Unity rect.y = 60 - y - 높이)."""
    sheet = Image.new('RGBA', (76, 60), (0, 0, 0, 0))
    sheet.alpha_composite(shell(OPTIONS[FINAL][1]), (1, 1))
    for i in range(len(SPARKS)):
        sheet.alpha_composite(spark(i), (1 + 24 * (i % 3), 12 + 15 * (i // 3)))
    return sheet


def save_gif(frames, path, speed=1):
    """frames: [(RGBA 이미지, ms)]. 모든 프레임이 같은 팔레트를 쓰도록 한 번에 양자화해 저장한다(프레임마다 팔레트가 다르면 뷰어에 따라 색이 뒤집혀 보임)."""
    rgb = [im.convert('RGB') for im, _ in frames]
    strip = Image.new('RGB', (rgb[0].width, rgb[0].height * len(rgb)))
    for i, im in enumerate(rgb):
        strip.paste(im, (0, i * im.height))
    pal = strip.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    out = [im.quantize(palette=pal, dither=Image.Dither.NONE) for im in rgb]
    out[0].save(path, save_all=True, append_images=out[1:], duration=[ms * speed for _, ms in frames], loop=0, disposal=2, optimize=False)


def main():
    S, bg = 7, (36, 38, 46, 255)
    ov = [spark(i) for i in range(len(SPARKS))]
    img = Image.new('RGBA', (1000, 640), bg)
    d = ImageDraw.Draw(img)
    f, fs, fb = font(15), font(12), font(20)
    d.text((14, 10), f'crit-shell-draft {VERSION} — 붉은 탄환 + 테두리 밖으로 나오는 흰 스파크 9종, 겹쳐 재생 (GIF에서 움직임 확인)', font=fb, fill=(255, 196, 60, 255))
    for i, (label, body) in enumerate(OPTIONS):
        x0 = 20 + i * 190
        d.text((x0, 50), label.split()[0], font=f, fill=(235, 235, 235, 255))
        st = stack(body, [ov[4], ov[7]])
        img.alpha_composite(st.resize((st.width * S, st.height * S), Image.NEAREST), (x0, 80))
    d.text((20, 80 + 6 * 11 * S + 24), '6발 탄창 · 2발 소모 · 맨 아래 = 마지막 탄환 (갈라짐 + 큰 십자 겹침 예시)', font=fs, fill=(150, 150, 158, 255))
    d.text((620, 50), f'스파크 {len(SPARKS)}종 (22x13 오버레이, R1 몸통 위)', font=f, fill=(235, 235, 235, 255))
    for k, (n, o) in enumerate(zip(NAMES, ov)):
        cx, cy = 620 + (k % 3) * 125, 80 + (k // 3) * 150
        tile = Image.new('RGBA', (OW, OH), (22, 22, 28, 255))
        tile.alpha_composite(shell(OPTIONS[0][1]), (M, M))
        tile.alpha_composite(o)
        img.alpha_composite(tile.resize((OW * 5, OH * 5), Image.NEAREST), (cx, cy))
        d.text((cx, cy + OH * 5 + 4), f'{k + 1} {n}', font=fs, fill=(150, 150, 158, 255))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT + '.png')

    gs, frames = 7, []
    gw, gh = len(OPTIONS) * (OW * gs + 40) + 20, stack(NORMAL, []).height * gs + 50
    for ids, ms in TIMELINE:
        g = Image.new('RGBA', (gw, gh), bg)
        gd = ImageDraw.Draw(g)
        for c, (label, body) in enumerate(OPTIONS):
            x = 20 + c * (OW * gs + 40)
            col = stack(body, [ov[i] for i in ids])
            g.alpha_composite(col.resize((col.width * gs, col.height * gs), Image.NEAREST), (x, 40))
            gd.text((x, 10), label.split()[0], font=f, fill=(235, 235, 235, 255))
        frames.append((g, ms))
    save_gif(frames, OUT + '.gif')
    print(OUT + '.png', OUT + '.gif')


if __name__ == '__main__':
    import sys
    if sys.argv[1:] == ['import']:
        if os.path.exists(SHEET):
            sys.exit('이미 있음: ' + SHEET)
        build_sheet().save(SHEET)
        print(SHEET)
    else:
        main()
