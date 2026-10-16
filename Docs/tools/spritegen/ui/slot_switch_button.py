"""창고 슬롯 전환 버튼 (13x44, 튜토리얼 키캡과 같은 바탕, 안에 ← 위 · → 아래).
    python slot_switch_button.py            시안 PNG(Docs/drafts/props/slot-switch-draft.png)만 쓴다. Assets는 건드리지 않는다.

크기: 창고 슬롯(hud_slot 22x22)의 2배 높이 = 44px, 두께 13px. 색은 keycaps.py와 같다."""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
ATLAS = os.path.join(REPO, 'Assets', '04.Sprites', 'UI', 'ui-atlas.png')
OUT = os.path.join(REPO, 'Docs', 'drafts', 'props', 'slot-switch-draft.png')
VERSION = 'v02'

W, H = 13, 44


def C(s):
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


OUTLINE = C('1b2033')
FACE_HI = C('f0f2f6')       # 윗변 · 왼변 빛
FACE_LT = C('e4e6ec')       # 윗부분 면
FACE = C('d6d9e0')
FACE_LO = C('c3c7d3')       # 아랫부분 면
FACE_RT = C('b4b9c8')       # 오른변 그늘
SIDE = [C('a3a8ba'), C('8d93a6'), C('6b7186')]    # 앞면(두께) 띠 3줄
INK = C('1b2033')

# ← 7x5 (→는 좌우 반전)
LEFT = ['..X....',
        '.XX....',
        'XXXXXXX',
        '.XX....',
        '..X....']

FRONT = 3                    # 앞면 띠 두께 (키캡이 눌려 있는 높이)


def button():
    """윗면(밝은 위·왼 테두리, 위쪽이 더 밝은 면, 오른쪽 그늘) + 아래 앞면 띠로 눌린 키캡 모양"""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    top_end = H - 1 - FRONT                       # 윗면 마지막 행
    for y in range(H):
        for x in range(W):
            if x in (0, W - 1) or y in (0, H - 1):
                px[x, y] = OUTLINE
            elif y > top_end:
                px[x, y] = SIDE[y - top_end - 1]
            elif y == 1 or x == 1:
                px[x, y] = FACE_HI
            elif x == W - 2:
                px[x, y] = FACE_RT
            elif y == top_end:
                px[x, y] = FACE_RT
            elif y < 8:
                px[x, y] = FACE_LT
            elif y > top_end - 6:
                px[x, y] = FACE_LO
            else:
                px[x, y] = FACE
    for x, y in ((0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)):
        px[x, y] = (0, 0, 0, 0)
    # 앞면 띠 양끝은 윗면보다 한 칸 안쪽에서 둥글게
    for y in (H - 2,):
        px[0, y] = (0, 0, 0, 0)
        px[W - 1, y] = (0, 0, 0, 0)
    for top, rows in ((15, LEFT), (23, [r[::-1] for r in LEFT])):
        for j, row in enumerate(rows):
            for i, v in enumerate(row):
                if v == 'X':
                    px[3 + i, top + j + 1] = FACE_HI          # 새긴 홈: 아래에 빛 한 줄
        for j, row in enumerate(rows):
            for i, v in enumerate(row):
                if v == 'X':
                    px[3 + i, top + j] = INK
    return im


def slot():
    im = Image.open(ATLAS).convert('RGBA')
    return im.crop((60, im.height - 40 - 22, 82, im.height - 40))      # hud_slot: Unity (60,40) 22x22


def font(sz):
    for p in ('C:/Windows/Fonts/malgunbd.ttf', '/System/Library/Fonts/AppleSDGothicNeo.ttc'):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


def scene():
    """창고 UI 모사: 슬롯 5열 x 2행(간격 2px) 오른쪽에 버튼. 게임 UI는 1px = 4 화면 픽셀"""
    gap, cols, rows = 2, 5, 2
    sw, sh = cols * 22 + (cols - 1) * gap, rows * 22 + (rows - 1) * gap
    pad = 6
    im = Image.new('RGBA', (pad + sw + 4 + W + pad, pad * 2 + sh), (42, 42, 46, 255))
    s = slot()
    for r in range(rows):
        for c in range(cols):
            im.alpha_composite(s, (pad + c * (22 + gap), pad + r * (22 + gap)))
    im.alpha_composite(button(), (pad + sw + 4, pad + (sh - H) // 2))
    return im


def main():
    bg = (36, 38, 46, 255)
    img = Image.new('RGBA', (1160, 560), bg)
    d = ImageDraw.Draw(img)
    f, fb, fs = font(15), font(20), font(12)
    d.text((14, 10), f'slot-switch-draft {VERSION} — 창고 슬롯 전환 버튼', font=fb, fill=(255, 196, 60, 255))

    b = button()
    img.alpha_composite(b.resize((W * 8, H * 8), Image.NEAREST), (40, 70))
    d.text((40, 70 + H * 8 + 8), f'{W}x{H}px (8배)', font=f, fill=(235, 235, 235, 255))
    d.text((40, 70 + H * 8 + 30), '슬롯 22px의 2배 = 44px', font=fs, fill=(150, 150, 158, 255))

    img.alpha_composite(b.resize((W, H), Image.NEAREST), (200, 90))
    d.text((190, 70), '1배', font=fs, fill=(150, 150, 158, 255))
    img.alpha_composite(b.resize((W * 4, H * 4), Image.NEAREST), (260, 70))
    d.text((260, 70 + H * 4 + 8), '4배 (게임 UI 배율)', font=fs, fill=(150, 150, 158, 255))

    sc = scene()
    sc = sc.resize((sc.width * 5, sc.height * 5), Image.NEAREST)
    img.alpha_composite(sc, (400, 70))
    d.text((400, 70 + sc.height + 8), '창고 슬롯 오른쪽 배치 예시 (5열 x 2행, 간격 2px, 5배)', font=f, fill=(235, 235, 235, 255))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print(OUT)


if __name__ == '__main__':
    main()
