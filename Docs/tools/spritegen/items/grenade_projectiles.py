"""수류탄 · 섬광탄 투척 투사체 — 8x8 단일 스프라이트 2장 (소모품 아이콘과 같은 팔레트 · 외곽선).

ThrowGrenade가 Visual을 속도 방향으로 돌리므로 신관이 오른쪽(+x)을 향하게 그린다.

python grenade_projectiles.py [시안.png] [버전]   시안 (기본 Docs/drafts/props/grenade-projectiles-draft.png)
python grenade_projectiles.py import             Assets/04.Sprites/Consumable/grenade-projectile-*.png 쓰기
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402
from consumable_icons import O, OLIVE, TAN_OLIVE, CYAN, STEEL, YEL  # noqa: E402

N = 8


def from_rows(rows, pal):
    assert len(rows) == N and all(len(r) == N for r in rows), [len(r) for r in rows]
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                im.putpixel((x, y), (O if ch == 'o' else pal[ch]) + (255,))
    return im


# 파편 수류탄: 올리브 달걀 몸통 + 노란 띠 점 + 오른쪽 강철 신관
def frag():
    pal = {k: OLIVE[i] for i, k in enumerate('012345')}
    pal.update({'c': STEEL[2], 'd': STEEL[3], 'e': STEEL[4], 'y': YEL[0], 'Y': YEL[1]})
    return from_rows([
        "..oooo..",
        ".o5443o.",
        "o454432o",
        "o4Y3Y2eo",
        "o33322co",
        "o2y2y1oo",
        ".o2110o.",
        "..oooo..",
    ], pal)


# 섬광탄: 누운 황록 원통 + 청록 띠 + 오른쪽 강철 신관
def flash():
    pal = {k: TAN_OLIVE[i] for i, k in enumerate('01234')}
    pal.update({'A': CYAN[0], 'B': CYAN[1], 'C': CYAN[2], 'D': CYAN[3],
                'c': STEEL[2], 'e': STEEL[4]})
    return from_rows([
        "........",
        "oooooo..",
        "o34D43oo",
        "o23C32eo",
        "o12B21co",
        "o01A10oo",
        "oooooo..",
        "........",
    ], pal)


SPRITES = {'grenade-projectile-frag': frag, 'grenade-projectile-flash': flash}
NAMES = ['파편 수류탄', '섬광탄']


def player_sprite():
    """비교용 플레이어 대기 프레임 (Player.prefab 기본 스프라이트 rect 41, 639, 18x23)"""
    im = Image.open(paths.repo('Assets', '04.Sprites', 'Player', 'Swordsman with shadows', 'Swordsman.png')).convert('RGBA')
    top = im.height - 639 - 23
    return im.crop((41, top, 41 + 18, top + 23))


def scene(sprite):
    """게임 바닥 위 1배 장면: 플레이어 → 오른쪽 포물선, 속도 방향 회전(Point 필터 흉내)"""
    import draftlib
    W, H = 128, 64
    sc = draftlib.dark_room(W, H, wall_rows=0)
    pl = player_sprite()
    sc.alpha_composite(pl, (8, H - pl.height - 8))
    x0, y0, x1, peak = 22.0, H - 22.0, 116.0, 22.0
    for i in range(7):
        t = i / 6
        x = x0 + (x1 - x0) * t
        y = y0 - 4 * peak * t * (1 - t)
        ang = math.degrees(math.atan2(4 * peak * (1 - 2 * t), x1 - x0))   # 화면 위 = +각도
        r = sprite.rotate(ang, Image.NEAREST, expand=True)
        sc.alpha_composite(r, (int(round(x - r.width / 2)), int(round(y - r.height / 2))))
    return sc


def draft(path, version='v01'):
    icons = Image.open(paths.repo('Assets', '04.Sprites', 'Consumable', 'consumable-icons.png')).convert('RGBA')
    try:
        font = ImageFont.truetype('malgun.ttf', 16)
    except OSError:
        font = ImageFont.load_default()
    Z, S, pad = 16, 4, 24
    sprites = [f() for f in SPRITES.values()]
    w = pad * 2 + 2 * 128 * S + pad
    img = Image.new('RGBA', (w, 900), (58, 58, 68, 255))
    dr = ImageDraw.Draw(img)
    dr.text((pad, 8), f'투척 투사체 시안 {version}  (8x8 · PPU 16 = 0.5칸, 신관이 오른쪽 = 진행 방향)', fill=(240, 240, 240), font=font)
    y = 44
    x = pad
    for i, sp in enumerate(sprites):
        icon = icons.crop((16 * (2 + i), 0, 16 * (3 + i), 16))
        dr.text((x, y), f'{NAMES[i]}  새 투사체 8x8 / 기존 아이콘 16x16 (같은 배율)', fill=(220, 220, 220), font=font)
        img.alpha_composite(sp.resize((N * Z, N * Z), Image.NEAREST), (x, y + 28))
        img.alpha_composite(icon.resize((16 * Z, 16 * Z), Image.NEAREST), (x + N * Z + 24, y + 28))
        x += N * Z + 24 + 16 * Z + 48
    y += 28 + 16 * Z + 24
    for i, sp in enumerate(sprites):
        sx = pad + i * (128 * S + pad)
        dr.text((sx, y), f'{NAMES[i]} 게임 장면 (4배, 궤적 회전 포함)', fill=(220, 220, 220), font=font)
        img.alpha_composite(scene(sp).resize((128 * S, 64 * S), Image.NEAREST), (sx, y + 24))
    img = img.crop((0, 0, w, y + 24 + 64 * S + pad))
    img.save(path)


if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else paths.repo('Docs', 'drafts', 'props', 'grenade-projectiles-draft.png')
    if arg == 'import':
        for name, f in SPRITES.items():
            dst = paths.out(paths.repo('Assets', '04.Sprites', 'Consumable', name + '.png'))
            f().save(dst)
            print('wrote', dst)
        sys.exit()
    draft(arg, *(sys.argv[2:3]))
    print('draft', arg)
