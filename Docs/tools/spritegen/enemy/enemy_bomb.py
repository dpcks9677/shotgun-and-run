"""Thrower 적 폭탄 — 10x10 재래식 폭탄, 심지 불꽃 2프레임 (가로 시트 20x10).

둥근 검붉은 철 몸통 + 코르크 마개 · 감은 끈 + 빨간 밀랍 봉인 + 불붙은 심지.
바닥에 놓인 채 터지기를 기다리므로 회전 없이 심지가 오른쪽 위를 향하게 그린다.

python enemy_bomb.py [시안.png] [버전]   시안 (기본 Docs/drafts/props/enemy-bomb-draft.png)
python enemy_bomb.py import             Assets/04.Sprites/Enemy/EnemyBomb/enemy-bomb.png 쓰기
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..'))
sys.path.insert(0, os.path.join(HERE, '..', 'items'))
import paths  # noqa: E402
import bl  # noqa: E402
from consumable_icons import O, RED  # noqa: E402

N = 10
# 몸통: 검붉은 철 (어두운 → 밝은). 가장 어두운 단계는 외곽선보다 한 단계 밝게
BODY = [(34, 24, 28), (48, 32, 36), (66, 44, 47), (94, 64, 64), (136, 100, 94)]
CORK = [(74, 50, 34), (110, 76, 50), (146, 106, 70)]
ROPE = [(150, 140, 124), (210, 202, 184)]
FUSE = (196, 186, 166)
SPARK = [bl.FL_W, bl.FL_Y, bl.FL_O, bl.FL_R]

ROWS = [
    ".....f....",
    "...KC.....",
    "...WV.....",
    "..3332....",
    ".343221...",
    ".332110...",
    ".2211A0...",
    ".111BD0...",
    "..0000....",
    "..........",
]


def base():
    pal = {str(k): BODY[k] for k in range(5)}
    pal.update({'K': CORK[2], 'C': CORK[1], 'W': ROPE[1], 'V': ROPE[0], 'f': FUSE,
                'A': RED[2], 'B': RED[1], 'D': RED[0]})
    return [[None if ch == '.' else pal[ch] for ch in row] for row in ROWS]


def outline(c):
    out = [r[:] for r in c]
    for y in range(N):
        for x in range(N):
            if c[y][x] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < N and 0 <= ny < N and c[ny][nx] is not None:
                    out[y][x] = O
                    break
    return out


def frame(i):
    c = outline(base())
    # 불꽃은 외곽선 없이 발광색만 (심지 끝 오른쪽 위)
    sparks = [
        {(6, 0): SPARK[1], (6, 1): SPARK[2], (7, 0): SPARK[3]},
        {(6, 0): SPARK[0], (7, 0): SPARK[1], (6, 1): SPARK[1], (7, 1): SPARK[2], (8, 0): SPARK[3], (5, 1): SPARK[3]},
    ][i]
    for (x, y), col in sparks.items():
        c[y][x] = col
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    for y, row in enumerate(c):
        for x, col in enumerate(row):
            if col:
                im.putpixel((x, y), tuple(col[:3]) + (255,))
    return im


FRAMES = 2


def sheet():
    out = Image.new('RGBA', (N * FRAMES, N), (0, 0, 0, 0))
    for i in range(FRAMES):
        out.alpha_composite(frame(i), (N * i, 0))
    return out


def crop_unity(path, x, y, w, h):
    im = Image.open(path).convert('RGBA')
    top = im.height - y - h
    return im.crop((x, top, x + w, top + h))


def scene(i):
    """게임 바닥 위 1배 장면: Thrower · 플레이어 · 플레이어 수류탄(8x8) · 폭탄"""
    import draftlib
    W, H = 112, 56
    sc = draftlib.dark_room(W, H, wall_rows=0)
    thrower = crop_unity(paths.repo('Assets', '04.Sprites', 'Enemy', 'Werebear with shadows', 'Werebear.png'), 0, 600, 100, 100)
    thrower = thrower.crop(thrower.getbbox())
    player = crop_unity(paths.repo('Assets', '04.Sprites', 'Player', 'Swordsman with shadows', 'Swordsman.png'), 41, 639, 18, 23)
    grenade = Image.open(paths.repo('Assets', '04.Sprites', 'Consumable', 'grenade-projectile-frag.png')).convert('RGBA')
    sc.alpha_composite(thrower, (6, H - thrower.height - 10))
    sc.alpha_composite(player, (W - 26, H - player.height - 10))
    sc.alpha_composite(grenade, (W - 40, H - 22))
    bomb = frame(i)
    for x, y in ((40, 30), (58, 38), (72, 26)):
        sc.alpha_composite(bomb, (x, y))
    return sc


def draft(path, version='v01'):
    try:
        font = ImageFont.truetype('malgun.ttf', 16)
    except OSError:
        font = ImageFont.load_default()
    Z, S, pad = 16, 4, 24
    w = pad * 2 + 2 * 112 * S + pad
    img = Image.new('RGBA', (w, 900), (58, 58, 68, 255))
    dr = ImageDraw.Draw(img)
    dr.text((pad, 8), f'Thrower 폭탄 시안 {version}  (10x10 · PPU 16 = 0.625칸, 심지 불꽃 2프레임)', fill=(240, 240, 240), font=font)
    y = 44
    for i in range(FRAMES):
        img.alpha_composite(frame(i).resize((N * Z, N * Z), Image.NEAREST), (pad + i * (N * Z + 24), y))
        dr.text((pad + i * (N * Z + 24), y + N * Z + 4), f'f{i}', fill=(220, 220, 220), font=font)
    ref = Image.open(paths.repo('Assets', '04.Sprites', 'Consumable', 'grenade-projectile-frag.png')).convert('RGBA')
    rx = pad + FRAMES * (N * Z + 24) + 24
    img.alpha_composite(ref.resize((8 * Z, 8 * Z), Image.NEAREST), (rx, y + 2 * Z))
    dr.text((rx, y + N * Z + 4), '비교: 플레이어 수류탄 8x8', fill=(220, 220, 220), font=font)
    y += N * Z + 40
    for i in range(FRAMES):
        sx = pad + i * (112 * S + pad)
        dr.text((sx, y), f'게임 장면 f{i} (4배, 왼쪽 Thrower · 오른쪽 플레이어와 수류탄)', fill=(220, 220, 220), font=font)
        img.alpha_composite(scene(i).resize((112 * S, 56 * S), Image.NEAREST), (sx, y + 24))
    img = img.crop((0, 0, w, y + 24 + 56 * S + pad))
    img.save(path)
    frames = [scene(i).resize((112 * S, 56 * S), Image.NEAREST) for i in range(FRAMES)]
    frames[0].save(os.path.splitext(path)[0] + '.gif', save_all=True, append_images=frames[1:], duration=120, loop=0)


if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'enemy-bomb-draft.png')
    if arg == 'import':
        dst = paths.out(paths.repo('Assets', '04.Sprites', 'Enemy', 'EnemyBomb', 'enemy-bomb.png'))
        sheet().save(dst)
        print('wrote', dst)
        sys.exit()
    draft(arg, *(sys.argv[2:3]))
    print('draft', arg)
