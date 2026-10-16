"""스켈레톤 피격 뼛조각 — 8방향 × 변형 3종 × 8프레임 (blood_vfx.py와 같은 시트 규격)

python Docs/tools/spritegen/vfx/bone_vfx.py --preview   # 확대 시안만 (Docs/drafts/props/bone_vfx_preview.png)
python Docs/tools/spritegen/vfx/bone_vfx.py             # Assets에 PNG 쓰기 (Bone.png)

- 시트 · 방향 번호 · 피벗은 blood_vfx.py와 같다 (칸 57×57, 가로 8프레임, 행 = 변형 * 8 + 방향, 방향 0 = 동, 반시계 45° 단위).
  그래서 `Blood` 컴포넌트와 `_sprites` 배열을 그대로 쓴다.
- 피 대신 각진 뼛조각(1~4px 덩어리) + 뼛가루 점. 줄기 · 방울 모양은 쓰지 않는다.
- 색은 Skeleton Archer 몸통에서 뽑은 무채색 뼈색(210/177/135)과 어두운 가루색. 순백은 먼지처럼 보여 쓰지 않는다.
- 조각은 왼쪽 위가 밝고 오른쪽 아래가 어둡다(STYLE 3장). 회전하지 않고 화면 기준 모양 그대로 찍는다.
- 변형 0 보통 흩어짐 · 변형 1 큼직한 조각 몇 개가 멀리 · 변형 2 잘게 부서져 넓게 퍼짐.
"""
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blood_vfx as bv  # noqa: E402
import paths  # noqa: E402

CELL, C0, FRAMES, DIRS, VARIANTS = bv.CELL, bv.C0, bv.FRAMES, bv.DIRS, bv.VARIANTS

B_HI = (210, 206, 204)
B_MD = (177, 169, 166)
B_DK = (135, 135, 135)
D_MD = (112, 109, 106)
D_DK = (78, 74, 72)

# 조각 모양 (dx, dy) 목록. 첫 칸이 왼쪽 위(밝음), 마지막 칸이 오른쪽 아래(어두움)
SHAPES = {
    1: [(0, 0)],
    2: [(0, 0), (1, 0)],
    3: [(0, 0), (0, 1)],
    4: [(0, 0), (1, 0), (0, 1)],
    5: [(0, 0), (1, 0), (0, 1), (1, 1)],
    6: [(0, 0), (1, 0), (2, 0), (1, 1)],
}


def spec(variant):
    r = random.Random(9100 + variant)
    pool, frags, dust = [], [], []
    # 맞은 자리: 뼛가루 얼룩 몇 점 (적 몸에 가려지므로 작게)
    for _ in range(5 + variant):
        pool.append((r.randint(-2, 2), r.randint(-2, 2), r.choice((D_MD, D_DK))))
    if variant == 0:
        count, big, reach, cone = 10, 5, (8, 24), 38
        shapes = [2, 3, 4, 4, 5]
        dust_n = 10
    elif variant == 1:
        count, big, reach, cone = 6, 4, (11, 27), 26
        shapes = [2, 3, 4, 5, 6]
        dust_n = 7
    else:
        count, big, reach, cone = 14, 4, (6, 21), 48
        shapes = [1, 2, 3, 4, 4]
        dust_n = 14
    for i in range(count):
        u = r.uniform(*reach)
        v = r.uniform(-1, 1) * math.tan(math.radians(cone)) * u * 0.85
        shape = r.choice([4, 5, 6]) if i < big else r.choice(shapes)
        frags.append(dict(u=u, v=v, shape=shape, spd=r.uniform(0.85, 1.12)))
    for i in range(dust_n):                                  # 뼛가루: 조각보다 안쪽 · 더 넓게
        u = r.uniform(3, reach[1] * 0.8)
        v = r.uniform(-1, 1) * math.tan(math.radians(cone + 10)) * u
        dust.append(dict(u=u, v=v, col=r.choice((D_MD, D_DK, D_DK)), spd=r.uniform(0.8, 1.0)))
    return dict(pool=pool, frags=frags, dust=dust)


def frame(sp, d, f):
    im = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    theta = math.radians(45 * d)

    def put(x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < CELL and 0 <= y < CELL:
            im.putpixel((x, y), (*c, 255))

    e = bv.ease(f / (FRAMES - 1))
    for du, dv, c in sp['pool']:
        put(*bv.to_px(du, dv, theta), c)
    for p in sp['dust']:
        if f == 0:
            continue
        k = min(1.0, e * p['spd'])
        put(*bv.to_px(p['u'] * k, p['v'] * k, theta), p['col'])
    for fr in sp['frags']:
        if f == 0:
            continue
        k = min(1.0, e * fr['spd'])
        x, y = bv.to_px(fr['u'] * k, fr['v'] * k, theta)
        x, y = int(round(x)), int(round(y))
        cells = SHAPES[fr['shape']]
        for i, (dx, dy) in enumerate(cells):
            c = B_HI if i == 0 else B_DK if i == len(cells) - 1 and len(cells) > 2 else B_MD
            put(x + dx, y + dy, c)
    return im


def sheet():
    out = Image.new('RGBA', (CELL * FRAMES, CELL * DIRS * VARIANTS), (0, 0, 0, 0))
    for v in range(VARIANTS):
        sp = spec(v)
        for d in range(DIRS):
            for f in range(FRAMES):
                out.paste(frame(sp, d, f), (f * CELL, (v * DIRS + d) * CELL))
    return out


BONE_SHEET = paths.repo('Assets', '04.Sprites', 'Player', 'Damaged', 'Bone.png')
DRAFT = paths.repo('Docs', 'drafts', 'props', 'bone_vfx_preview.png')


def preview(scale=4):
    """위: 변형별 8방향 마지막 프레임을 스켈레톤 위에. 아래: 변형별 동 · 북동 8프레임 진행."""
    bg = (38, 38, 42, 255)
    skel = Image.open(paths.SPRITES + '/Enemy/Skeleton Archer with shadows/Skeleton Archer.png').convert('RGBA').crop((0, 0, 100, 100))
    skel = skel.crop(skel.getbbox())
    gap = 6
    cell = CELL * scale
    sh = sheet()
    top = Image.new('RGBA', (DIRS * (cell + gap), VARIANTS * (cell + gap)), bg)
    for v in range(VARIANTS):
        for d in range(DIRS):
            fr = sh.crop(((FRAMES - 1) * CELL, (v * DIRS + d) * CELL, FRAMES * CELL, (v * DIRS + d + 1) * CELL))
            base = Image.new('RGBA', (CELL, CELL), bg)
            base.alpha_composite(skel, (C0 - skel.width // 2, C0 - skel.height // 2))
            base.alpha_composite(fr)
            top.alpha_composite(base.resize((cell, cell), Image.NEAREST), (d * (cell + gap), v * (cell + gap)))
    s2 = scale * 2 // 3 or 1
    rows = []
    for v in range(VARIANTS):
        for d in (0, 1):
            strip = sh.crop((0, (v * DIRS + d) * CELL, FRAMES * CELL, (v * DIRS + d + 1) * CELL))
            base = Image.new('RGBA', strip.size, bg)
            base.alpha_composite(strip)
            rows.append(base.resize((strip.width * s2, strip.height * s2), Image.NEAREST))
    W = max(top.width, rows[0].width)
    H = top.height + sum(r.height + gap for r in rows)
    full = Image.new('RGBA', (W, H), bg)
    full.alpha_composite(top, (0, 0))
    y = top.height
    for r in rows:
        full.alpha_composite(r, (0, y))
        y += r.height + gap
    os.makedirs(os.path.dirname(DRAFT), exist_ok=True)
    full.convert('RGB').save(DRAFT)
    print('draft', os.path.relpath(DRAFT, paths.REPO))


if __name__ == '__main__':
    if '--preview' in sys.argv:
        preview()
    else:
        sheet().save(paths.out(BONE_SHEET))
        print('wrote', os.path.relpath(BONE_SHEET, paths.REPO), (CELL * FRAMES, CELL * DIRS * VARIANTS))
