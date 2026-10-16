"""튜토리얼 조작 안내용 도트 키캡 시트 (TMP 인라인 스프라이트 <sprite name="Key_W"> 용).
    python -I keycaps.py            시트 PNG(Assets/04.Sprites/UI/TutorialKeycaps.png) + 확대 미리보기 + 슬라이스 JSON(stdout)
    python -I keycaps.py --draft    미리보기만 (시트는 쓰지 않음)

모든 스프라이트는 14x14, 칸 16x16(사방 1px 투명 여백 → 칸 사이 2px). 높이를 같게 맞춘 이유: TMP는 인라인 스프라이트를 줄 높이에 맞춰 줄이므로.
글자는 5x7 손 글꼴을 캡에 구워 넣는다. 좌표 규칙은 unity/keycaps_import.cs와 같다(x = 1 + 16*i, y = 1)."""
import json, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
SHEET = os.path.join(REPO, 'Assets', '04.Sprites', 'UI', 'TutorialKeycaps.png')
PREVIEW = os.path.join(REPO, 'Docs', 'drafts', 'props', 'tutorial-keycaps-preview.png')

CAP = 14
CELL = 16


def H(s):
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


OUTLINE = H('1b2033')
FACE_HI = H('f0f2f6')
FACE = H('d6d9e0')
FACE_SH = H('8d93a6')
INK = H('1b2033')
BTN_ON = H('ffd45a')
BTN_ON_HI = H('fff0b0')
BODY = H('aeb3c2')
SEAM = H('6b7186')

# 5x7 도트 글꼴
GLYPH = {
    'W': ['X...X', 'X...X', 'X...X', 'X.X.X', 'X.X.X', 'XX.XX', 'X...X'],
    'A': ['.XXX.', 'X...X', 'X...X', 'XXXXX', 'X...X', 'X...X', 'X...X'],
    'S': ['.XXXX', 'X....', 'X....', '.XXX.', '....X', '....X', 'XXXX.'],
    'D': ['XXXX.', 'X...X', 'X...X', 'X...X', 'X...X', 'X...X', 'XXXX.'],
    'E': ['XXXXX', 'X....', 'X....', 'XXXX.', 'X....', 'X....', 'XXXXX'],
    'R': ['XXXX.', 'X...X', 'X...X', 'XXXX.', 'X.X..', 'X..X.', 'X...X'],
    '1': ['..X..', '.XX..', '..X..', '..X..', '..X..', '..X..', '.XXX.'],
    '2': ['.XXX.', 'X...X', '....X', '...X.', '..X..', '.X...', 'XXXXX'],
    '3': ['XXXX.', '....X', '....X', '.XXX.', '....X', '....X', 'XXXX.'],
    '4': ['...X.', '..XX.', '.X.X.', 'X..X.', 'XXXXX', '...X.', '...X.'],
}
KEYS = ['W', 'A', 'S', 'D', 'E', 'R', '1', '2', '3', '4']
NAMES = ['Key_' + k for k in KEYS] + ['Mouse_L', 'Mouse_R']


def corners(c):
    """둥근 모서리: 네 귀퉁이 칸을 비운다"""
    for x, y in ((0, 0), (c.width - 1, 0), (0, c.height - 1), (c.width - 1, c.height - 1)):
        c.putpixel((x, y), (0, 0, 0, 0))


def keycap(ch):
    c = Image.new('RGBA', (CAP, CAP), (0, 0, 0, 0))
    for y in range(CAP):
        for x in range(CAP):
            if x in (0, CAP - 1) or y in (0, CAP - 1):
                c.putpixel((x, y), OUTLINE)
            elif y == 1:
                c.putpixel((x, y), FACE_HI)
            elif y == CAP - 2:
                c.putpixel((x, y), FACE_SH)      # 아래쪽 1px 그림자
            else:
                c.putpixel((x, y), FACE)
    corners(c)
    # 면(y 1..11)의 가운데에 5x7 글자: 가로 (14-5)//2, 세로 3
    ox, oy = 4, 3
    for j, row in enumerate(GLYPH[ch]):
        for i, v in enumerate(row):
            if v == 'X':
                c.putpixel((ox + i, oy + j), INK)
    return c


def mouse(button):
    """세로로 긴 마우스: 위 1/3은 가운데 세로선으로 좌/우 버튼, 눌러야 하는 쪽만 노란색. 몸체는 가로 중앙(x 2..11)"""
    c = Image.new('RGBA', (CAP, CAP), (0, 0, 0, 0))
    for y in range(CAP):
        edge = 4 if y in (0, CAP - 1) else 3 if y in (1, CAP - 2) else 2   # 윗면 · 아랫면은 좁게(둥글게)
        for x in range(edge, CAP - edge):
            if y in (0, CAP - 1) or x in (edge, CAP - 1 - edge) and y not in (0, CAP - 1):
                c.putpixel((x, y), OUTLINE)
            elif y <= 5 and x in (6, 7):
                c.putpixel((x, y), OUTLINE)    # 좌/우 버튼 사이 세로선
            elif y == 6:
                c.putpixel((x, y), OUTLINE)    # 버튼 아래 가로선
            elif y <= 5:
                on = (button == 'L') == (x < 7)
                c.putpixel((x, y), (BTN_ON_HI if y == 1 else BTN_ON) if on else (FACE_HI if y == 1 else FACE))
            elif y == CAP - 3:
                c.putpixel((x, y), FACE_SH)
            else:
                c.putpixel((x, y), FACE)
    return c


def build():
    sprites = [keycap(k) for k in KEYS] + [mouse('L'), mouse('R')]
    sheet = Image.new('RGBA', (CELL * len(sprites), CELL), (0, 0, 0, 0))
    layout = []
    for i, (n, s) in enumerate(zip(NAMES, sprites)):
        x = 1 + CELL * i
        sheet.paste(s, (x, 1))
        layout.append({'name': n, 'x': x, 'y': 1, 'width': CAP, 'height': CAP})   # y는 Unity(아래 기준)와 같음: 시트 높이 16, 위아래 대칭
    return sheet, layout


def preview(sheet, scale=8):
    bg = Image.new('RGBA', sheet.size, H('4a4d5a'))
    bg.alpha_composite(sheet)
    return bg.resize((bg.width * scale, bg.height * scale), Image.NEAREST)


if __name__ == '__main__':
    sheet, layout = build()
    if '--draft' not in sys.argv:
        sheet.save(SHEET)
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    preview(sheet).save(PREVIEW)
    print(json.dumps({'sheet': [sheet.width, sheet.height], 'sprites': layout}, ensure_ascii=False))
