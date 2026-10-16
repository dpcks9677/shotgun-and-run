"""튜토리얼 조작 안내 글자 · 키캡 뒤에 까는 9슬라이스 배경 패널 (8x8, PPU 16, 테두리 3px).
    python guide_panel.py            PNG(Assets/04.Sprites/UI/TutorialGuidePanel.png) + 확대 미리보기
    python guide_panel.py --draft    미리보기만 (PNG는 쓰지 않음)

가운데 칸(3~4번)은 채움 한 종류뿐이라 늘려도 줄무늬가 생기지 않는다. 모서리는 1px씩 잘라 둥글게."""
import os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
SHEET = os.path.join(REPO, 'Assets', '04.Sprites', 'UI', 'TutorialGuidePanel.png')
PREVIEW = os.path.join(REPO, 'Docs', 'drafts', 'props', 'tutorial-guide-panel-preview.png')

SIZE = 8
BORDER = 3


def H(s, a=255):
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


FILL = H('1b2033', 200)
EDGE = H('3a4264')
NONE = (0, 0, 0, 0)

# . 투명, E 테두리, F 채움
PATTERN = [
    '.EEEEEE.',
    'EFFFFFFE',
    'EFFFFFFE',
    'EFFFFFFE',
    'EFFFFFFE',
    'EFFFFFFE',
    'EFFFFFFE',
    '.EEEEEE.',
]
COLORS = {'.': NONE, 'E': EDGE, 'F': FILL}


def build():
    img = Image.new('RGBA', (SIZE, SIZE), NONE)
    for y, row in enumerate(PATTERN):
        for x, ch in enumerate(row):
            img.putpixel((x, y), COLORS[ch])
    return img


def stretch(panel, w, h):
    """9슬라이스로 w x h 로 늘린다"""
    out = Image.new('RGBA', (w, h), NONE)
    b, s = BORDER, SIZE
    xs = [(0, b, 0, b), (b, s - b, b, w - b), (s - b, s, w - b, w)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in xs:
            part = panel.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
            out.paste(part, (dx0, dy0))
    return out


def preview(panel, scale=6):
    bg = Image.new('RGBA', (180, 60), H('4a4d5a'))
    for i, (w, h, label) in enumerate(((80, 24, 'WASD'), (160, 24, 'WASD + 마우스'))):
        x, y = 8, 4 + i * 28
        bg.alpha_composite(stretch(panel, w, h), (x, y))
        # 흰 글자 느낌의 박스(임의) + 밝은 회색 키캡 박스
        for gx in range(x + 8, x + 6 + 21, 3):
            for gy in range(y + 9, y + 15):
                bg.putpixel((gx, gy), (255, 255, 255, 255))
        for k in range(2 if w < 100 else 4):
            kx = x + 34 + k * 15
            for gx in range(kx, kx + 12):
                for gy in range(y + 5, y + 19):
                    bg.putpixel((gx, gy), H('d6d9e0'))
    return bg.resize((bg.width * scale, bg.height * scale), Image.NEAREST)


if __name__ == '__main__':
    panel = build()
    if '--draft' not in sys.argv:
        panel.save(SHEET)
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    preview(panel).save(PREVIEW)
    print('\n'.join(PATTERN))
