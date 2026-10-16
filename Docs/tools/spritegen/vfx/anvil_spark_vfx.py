"""모루 타격 불똥 — 망치가 모루에 닿는 자리에서 불똥이 위로 튀어 포물선으로 흩어졌다 식는다 (8프레임, 20fps)

python Docs/tools/spritegen/vfx/anvil_spark_vfx.py --preview   # 시안 (Docs/drafts/props/anvil_spark_vfx-draft.png)
python Docs/tools/spritegen/vfx/anvil_spark_vfx.py             # Assets에 PNG 쓰기 (09.VFX/Shelter/AnvilSpark.png, 가로 한 줄 8프레임)

- 칸은 40×28, 타격 지점은 칸 아래 가운데 (20, 23). Unity에서 피벗을 이 점으로 슬라이스한다.
- 그룬 Reinforce 시트의 F4 · F5에 이미 작은 불똥 픽셀이 그려져 있어서, 이쪽은 더 크게 튀는 불똥만 맡는다.
- 불똥 10개는 고정 속도표(왼쪽 위 · 위 · 오른쪽 위 부채꼴)로 움직인다. 위치는 프레임 번호의 식이라 난수가 없다.
- 색은 나이에 따라 흰노랑 → 노랑 → 주황 → 붉은 불씨(`bl.FL_W · FL_Y · FL_O · FL_R`), 외곽선 없음(빛이라서). 앞 4프레임은 꼬리 1px.
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402
from bl import FL_O, FL_R, FL_W, FL_Y  # noqa: E402

FRAMES = 8
W, H = 40, 28
OX, OY = 20, 23                                   # 타격 지점
GRAVITY = 0.5
DRAG = 0.8                                        # 가로 속도 배율

# (vx, vy, 머리 크기 2px 여부) — 프레임당 픽셀, 위쪽이 -y
SPARKS = [(-2.8, -2.4, False), (-2.0, -3.8, True), (-1.1, -4.4, False), (-0.4, -3.2, False), (0.5, -4.6, True),
          (1.2, -3.6, False), (2.0, -4.2, False), (2.8, -2.6, True), (-1.6, -1.6, False), (1.8, -1.7, False)]
DIM = (FL_R[0] // 2 + 40, FL_R[1] // 2, FL_R[2] // 2, 255)

SHEET = paths.repo('Assets', '09.VFX', 'Shelter', 'AnvilSpark.png')
DRAFT = paths.repo('Docs', 'drafts', 'props', 'anvil_spark_vfx-draft.png')


def spark_pos(vx, vy, k):
    return OX + vx * DRAG * k, OY + vy * k + 0.5 * GRAVITY * k * k


def color(k):
    a = k / (FRAMES - 1)
    return FL_W if a < 0.2 else FL_Y if a < 0.45 else FL_O if a < 0.75 else FL_R


def put(im, x, y, c):
    x, y = round(x), round(y)
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def flash(im, k):
    """맞는 순간 번쩍임: 0프레임 5칸 십자 + 대각선, 1프레임 3칸 십자, 2프레임 중심 한 점"""
    if k == 0:
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            put(im, OX + dx, OY + dy, FL_W)
        for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2), (1, 1), (-1, 1), (1, -1), (-1, -1)):
            put(im, OX + dx, OY + dy, FL_Y)
    elif k == 1:
        put(im, OX, OY, FL_W)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            put(im, OX + dx, OY + dy, FL_Y)
    elif k == 2:
        put(im, OX, OY, FL_O)


def frame(k):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    flash(im, k)
    if k == 0:
        return im
    for i, (vx, vy, big) in enumerate(SPARKS):
        if k == FRAMES - 1 and i % 3 == 0:                          # 마지막 프레임엔 일부가 먼저 꺼진다
            continue
        x, y = spark_pos(vx, vy, k)
        c = color(k)
        put(im, x, y, c)
        if big and k <= 5:
            put(im, x + (1 if vx > 0 else -1), y, c)                # 큰 불똥은 진행 방향 반대쪽으로 1px 더 (머리 2px)
        if k <= 4:
            tx, ty = spark_pos(vx, vy, k - 0.6)                     # 꼬리: 조금 전 위치에 한 단계 어두운 색
            put(im, tx, ty, FL_O if c == FL_W else DIM if c == FL_R else FL_R if c == FL_O else FL_O)
    return im


def strip():
    out = Image.new('RGBA', (W * FRAMES, H), (0, 0, 0, 0))
    for k in range(FRAMES):
        out.paste(frame(k), (k * W, 0))
    return out


def preview(scale=6):
    """어두운 바닥 위 8프레임 + 그룬 · 모루에 겹친 장면(타격 프레임마다)"""
    gap = 6
    bg = (38, 38, 42, 255)
    s = strip()
    top = Image.new('RGBA', (s.width, s.height), bg)
    top.alpha_composite(s)
    top = top.resize((top.width * scale // 2, top.height * scale // 2), Image.NEAREST)

    forge = Image.open(paths.tile('ZoneProps', 'Forge', 'Forge.png')).convert('RGBA')
    anvil = forge.crop((0, forge.height - 56, 34, forge.height - 20))          # 모루 (rect x0 y20 34×36, Unity는 아래 기준 y)
    grun = Image.open(paths.SPRITES + '/NPC/NPC_Blacksmith_Grun/Grun_Reinforce.png').convert('RGBA')
    cw, ch = 6 * 16, 5 * 16                                                    # 6×5칸 창, 왼쪽 아래 = 세계 (7, 11)
    scenes = []
    for k in (0, 1, 2, 3, 4, 6):
        im = Image.new('RGBA', (cw, ch), bg)
        px, py = (10 - 7) * 16, ch - (12 - 11) * 16                             # 모루 칸 (10,12) 왼쪽 아래
        im.alpha_composite(anvil, (px, py - 8 - 24))
        gx, gy = round((9.5 - 7) * 16), round(ch - (13.5 - 11) * 16)            # 그룬 (9.5, 13.5)
        f4 = grun.crop((400, 0, 500, 100))
        im.alpha_composite(f4, (gx - 50, gy - 50))
        hx, hy = gx - 50 + 62, gy - 50 + 57                                     # 망치 닿는 점 (그룬 F4의 불똥 자리)
        im.alpha_composite(frame(k), (hx - OX, hy - OY))
        scenes.append(im.resize((cw * scale // 2, ch * scale // 2), Image.NEAREST))
    row = Image.new('RGBA', (sum(i.width for i in scenes) + gap * (len(scenes) - 1), scenes[0].height), bg)
    x = 0
    for i in scenes:
        row.paste(i, (x, 0))
        x += i.width + gap
    full = Image.new('RGBA', (max(top.width, row.width), top.height + gap + row.height), bg)
    full.alpha_composite(top, (0, 0))
    full.alpha_composite(row, (0, top.height + gap))
    os.makedirs(os.path.dirname(DRAFT), exist_ok=True)
    full.convert('RGB').save(DRAFT)
    print('draft', os.path.relpath(DRAFT, paths.REPO))


if __name__ == '__main__':
    if '--preview' in sys.argv:
        preview()
    else:
        strip().save(paths.out(SHEET))
        print('wrote', os.path.relpath(SHEET, paths.REPO), (W, H, FRAMES))
