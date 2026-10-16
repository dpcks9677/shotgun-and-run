"""스턴 상태이상 표시 — 머리 위를 도는 별 2개 + 반짝이 + 궤도 점선 (6프레임, 12fps)

python Docs/tools/spritegen/vfx/stun_vfx.py --compare   # 후보 크기를 오크 · 슬라임 위에 얹은 비교 시안 (Docs/drafts/props/stun_vfx_compare.png)
python Docs/tools/spritegen/vfx/stun_vfx.py             # Assets에 PNG 쓰기 (Enemy_Stun.png, 가로 한 줄 6프레임)

- 모양은 이전 AI 확대 시안(별 2개가 타원 궤도를 돈다)만 참고해 도트로 새로 찍었다.
- 별: 5×5(작은 것)와 7×7(큰 것). 외곽선 `bl.OUT`, 노랑 3단(왼쪽 위 빛), 윗점 흰 하이라이트 1px.
- 궤도는 타원(ry/rx = 0.45, 탑다운으로 비스듬히 본 고리). 별은 궤도 위를 한 바퀴 돌고 프레임당 60°씩 진행한다.
  뒤쪽(위쪽) 별은 작은 별, 앞쪽(아래쪽) 별은 큰 별로 원근을 준다.
- 점선은 별 바로 뒤쪽 호에만 몇 칸 남기고 나머지는 비운다.
"""
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402
from bl import OUT, H  # noqa: E402

Y_HI, Y_MD, Y_DK = H('fff0a0'), H('ffc319'), H('e58a00')
WHITE = (255, 255, 255, 255)
DASH_LT, DASH_DK = (255, 223, 107, 190), (90, 66, 20, 170)

FRAMES = 6
SIZES = {'S': (14, 10), 'M': (22, 16), 'L': (28, 18)}     # (폭, 높이) 후보. 비교 결과 M(22×16 = 1.375u, 오크 몸 23px)으로 확정.

STAR_5 = ['..o..',
          'ooooo',
          '.ooo.',
          'oo.oo',
          'o...o']
STAR_7 = ['...o...',
          '...o...',
          'ooooooo',
          '.ooooo.',
          '..ooo..',
          '.oo.oo.',
          '.o...o.']
# 별 안쪽 색: 한 글자당 색. 왼쪽 위 빛.
FILL_5 = ['..h..',
          'hhmmm',
          '.hmd.',
          'mm.dd',
          'd...d']
FILL_7 = ['...h...',
          '...h...',
          'hhhmmmd',
          '.hmmmd.',
          '..mmd..',
          '.mm.dd.',
          '.d...d.']


def canvas(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def star(im, cx, cy, big):
    """별 한 개를 (cx, cy) 중심으로 찍는다. 외곽선은 별 모양 바깥 1px."""
    shape, fill = (STAR_7, FILL_7) if big else (STAR_5, FILL_5)
    h, w = len(shape), len(shape[0])
    x0, y0 = cx - w // 2, cy - h // 2
    body = {(x, y) for y in range(h) for x in range(w) if shape[y][x] == 'o'}
    for x, y in body:                                        # 외곽선 먼저
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in body:
                put(im, x0 + x + dx, y0 + y + dy, OUT)
    col = {'h': Y_HI, 'm': Y_MD, 'd': Y_DK}
    for x, y in body:
        put(im, x0 + x, y0 + y, col[fill[y][x]])
    put(im, x0 + w // 2, y0 + 1 if big else y0 + 1, WHITE)


def sparkle(im, cx, cy, lit):
    """반짝이: 켜진 프레임은 3×3 십자(+), 꺼진 프레임은 흐린 점 1px. 반투명 번짐은 쓰지 않는다."""
    if lit:
        put(im, cx, cy, Y_HI)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            put(im, cx + dx, cy + dy, Y_MD)
    else:
        put(im, cx, cy, DASH_LT)


def frame(size, k):
    """k번째 프레임. 두 별이 180° 떨어져 궤도를 돈다."""
    w, h = size
    im = canvas(w, h)
    cx, cy = (w - 1) / 2, (h - 1) / 2
    big_star = w >= 18
    pad_x = 5 if big_star else 3                             # 별 반폭 + 외곽선
    rx = cx - pad_x
    ry = rx * 0.55
    ang0 = math.radians(60 * k + 30)
    # 점선: 궤도를 24점으로 나눠 홀수 점만 찍는다. 별 뒤쪽(진행 반대 방향) 절반만 진하게.
    for i in range(24):
        a = 2 * math.pi * i / 24
        x, y = round(cx + rx * math.cos(a)), round(cy + ry * math.sin(a))
        if i % 2 == 0 and im.getpixel((x, y))[3] == 0:
            put(im, x, y, DASH_DK if math.sin(a) < 0 else DASH_LT)
    stars = []
    for j in range(2):
        a = ang0 + math.pi * j
        sx, sy = round(cx + rx * math.cos(a)), round(cy + ry * math.sin(a))
        stars.append((sy, sx))
    for sy, sx in sorted(stars):                             # 위쪽(뒤) 별 먼저, 앞 별이 위에 덮는다
        big = sy >= cy and big_star
        star(im, sx, sy, big)
    # 반짝이 2개: 위 왼쪽 · 아래 오른쪽 구석에 고정, 번갈아 깜박
    sparkle(im, 1, 1, k % 2 == 0)
    sparkle(im, w - 2, h - 2, k % 2 == 1)
    return im


def strip(size):
    w, h = size
    out = canvas(w * FRAMES, h)
    for k in range(FRAMES):
        out.paste(frame(size, k), (k * w, 0))
    return out


STUN_SHEET = paths.repo('Assets', '04.Sprites', 'Enemy', 'Enemy_Stun.png')
DRAFT = paths.repo('Docs', 'drafts', 'props', 'stun_vfx_compare.png')


def compare(scale=6):
    """후보 크기 S · M · L을 오크 · 슬라임 머리 위에 얹은 비교 시안 (픽셀 크기가 같게 정수 배율)"""
    bodies = [('Orc', paths.SPRITES + '/Enemy/Orc with shadows/Orc.png'),
              ('Slime', paths.SPRITES + '/Enemy/Slime with shadows/Slime.png')]
    cells = []
    for name, p in bodies:
        body = Image.open(p).convert('RGBA').crop((0, 0, 100, 100))
        bb = body.getbbox()
        body = body.crop((bb[0] - 10, bb[1] - 2, bb[2] + 10, bb[3] + 2))
        for key, size in SIZES.items():
            cell = canvas(max(body.width, size[0]) + 4, body.height + size[1] + 2)
            cell.paste(body, ((cell.width - body.width) // 2, size[1] + 2), body)
            f = frame(size, 0)
            cell.alpha_composite(f, ((cell.width - size[0]) // 2, 0))
            cells.append((f'{name} {key} {size[0]}x{size[1]}', cell))
    gap = 8
    cw = max(c.width for _, c in cells) * scale + gap
    ch = max(c.height for _, c in cells) * scale + gap
    cols = 3
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new('RGBA', (cw * cols, ch * rows), (38, 38, 42, 255))
    for i, (_, cell) in enumerate(cells):
        big = cell.resize((cell.width * scale, cell.height * scale), Image.NEAREST)
        sheet.alpha_composite(big, ((i % cols) * cw + gap // 2, (i // cols) * ch + gap // 2))
    # 6프레임 풀 스트립도 아래에 붙인다
    strips = [strip(s).resize((s[0] * FRAMES * scale // 2, s[1] * scale // 2), Image.NEAREST) for s in SIZES.values()]
    sw = max(s.width for s in strips)
    full = Image.new('RGBA', (max(sheet.width, sw), sheet.height + sum(s.height + gap for s in strips)), (38, 38, 42, 255))
    full.alpha_composite(sheet, (0, 0))
    y = sheet.height
    for s in strips:
        full.alpha_composite(s, (0, y))
        y += s.height + gap
    os.makedirs(os.path.dirname(DRAFT), exist_ok=True)
    full.convert('RGB').save(DRAFT)
    print('draft', os.path.relpath(DRAFT, paths.REPO))


if __name__ == '__main__':
    if '--compare' in sys.argv:
        compare()
    else:
        key = next((a for a in sys.argv[1:] if a in SIZES), 'M')
        strip(SIZES[key]).save(paths.out(STUN_SHEET))
        print('wrote', os.path.relpath(STUN_SHEET, paths.REPO), SIZES[key])
