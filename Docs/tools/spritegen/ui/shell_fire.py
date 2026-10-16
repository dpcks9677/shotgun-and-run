"""HUD 탄 소모(발사) 연출 시안: 탄환 가로 반전(뇌관 왼쪽, 앞이 오른쪽). 흰 충격파가 뒤에서 와서 금색 뇌관을 때리고,
충격파가 몸통을 지나 탄 앞으로 퍼져 직사각형 흰 판으로 나가며 사라진다. 불꽃은 없다. 크리티컬 탄도 같은 연출을 쓴다.
    python shell_fire.py import     오버레이 시트 Assets/04.Sprites/UI/ShellFire.png를 쓴다(이미 있으면 중단, 덮어쓰려면 import --force: 슬라이스 · guid는 그대로). 슬라이스는 unity/shell_fire_import.cs
    python shell_fire.py            시안 PNG · GIF(Docs/drafts/props/shell-fire-draft.png · .gif · -slow.gif(3배 느림))만 쓴다. Assets는 건드리지 않는다.

날아가는 물체는 없다. 탄 몸통은 그대로 두고 그 위에 겹치는 36x13 오버레이 10장으로 모든 효과를 그린다(탄이 놓이는 자리는 오버레이 x 4, y 2):
  흰 판이 뒤에서 다가옴 → 뇌관을 때림(뇌관이 번쩍 + 흔들림) → 충격파 띠가 몸통을 왼쪽에서 오른쪽으로 훑음 → 앞 끝에서 흰 직사각형 판이 커지며 나감.
빈 탄피(어두움)로 바뀌는 것은 DIM_FROM 프레임부터라 오버레이 밖(코드의 색 틴트)에서 처리한다."""
import os
from PIL import Image, ImageDraw
import crit_shell as cs

OUT = os.path.join(cs.REPO, 'Docs', 'drafts', 'props', 'shell-fire-draft')
SHEET = os.path.join(cs.REPO, 'Assets', '04.Sprites', 'UI', 'ShellFire.png')
VERSION = 'v11'

N = 4                                   # 시안 탄창 칸 수 (게임은 6칸, 같은 방식으로 위에서부터 소모)
PITCH = 11                              # 탄환 18x9 + 간격 2
RM = 4                                  # 오버레이에서 탄 왼쪽 가장자리까지(뒤에서 오는 판이 지나갈 여유)
TM = 4
EW, EH = RM + 18 + 14, 13               # 오버레이 36x13. 탄은 (RM, 2)에 놓이고 중심 행은 6
CY = 6
DIM_FROM = 6                            # 이 프레임부터 탄이 빈 탄피
SH = 2                                  # 흔들림 최대 이동(px)만큼 사방 여유
CW, CH = EW + 2 * SH, TM * 2 + N * PITCH - 2

# 색: 노란 기 없는 흰색 (순백 + 한 단계 어두운 무채색 흰색)
WHITE, SOFT = (255, 255, 255, 255), (222, 225, 232, 255)

# 프레임별 효과. 판 = (왼쪽 x(탄 좌표), 두께, 높이) 또는 None. 모두 속이 찬 일자 직사각형(체크무늬 · 지그재그 없음)
#   (탄 전체 밝기, 뇌관 밝기, 몸통 충격파 띠 x(None=없음), 뒤쪽 판, 앞쪽 판, 지속 ms, 흔들림 (dx, dy))
FRAMES = [
    (0.00, 0.00, None, (-3, 2, 9), None, 40, (0, 0)),       # 바로 뒤에서 다가옴 (2프레임)
    (0.00, 0.10, None, (-2, 2, 9), None, 30, (0, 0)),
    (0.50, 1.00, None, (-1, 1, 11), None, 40, (2, 0)),      # 뇌관을 때림: 눌린 판 + 번쩍 + 밀림
    (0.40, 0.70, 5, (-1, 1, 9), None, 35, (-1, 0)),          # 충격파가 몸통으로: 판은 같은 두께로 낮아지며 사라짐
    (0.35, 0.45, 8, None, None, 35, (1, -1)),
    (0.30, 0.25, 11, None, None, 35, (-1, 1)),
    (0.20, 0.10, 14, None, (18, 2, 7), 40, (1, 0)),         # 앞 끝에 닿아 직사각형 판이 나감
    (0.10, 0.00, None, None, (20, 2, 9), 45, (-1, 0)),
    (0.00, 0.00, None, None, (22, 2, 9), 50, (0, 1)),
    (0.00, 0.00, None, None, (24, 1, 9), 60, (0, 0)),
]
DURATIONS = [f[5] for f in FRAMES]
SHAKE = [f[6] for f in FRAMES]


def flip(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)


def overlay(i):
    """오버레이 i번째(36x13)"""
    shell_a, brass_a, band_x, rear, front, _, _ = FRAMES[i]
    im = Image.new('RGBA', (EW, EH), (0, 0, 0, 0))
    mask = flip(cs.shell(cs.NORMAL))

    def put(x, y, c):
        if 0 <= x < EW and 0 <= y < EH:
            im.putpixel((x, y), c)

    def tint(x, y, a):
        """탄 몸통 · 뇌관 픽셀(외곽선 제외) 위에 흰 빛을 a만큼 얹는다"""
        r, g, b, m = mask.getpixel((x, y))
        if m and (r, g, b) != (0, 0, 0) and a > 0:
            put(RM + x, 2 + y, WHITE[:3] + (int(255 * min(a, 1)),))

    for y in range(9):
        for x in range(18):
            brass = x <= 4
            a = shell_a + (brass_a if brass else 0)
            if band_x is not None and not brass:
                a += 0.75 if x in (band_x, band_x + 1) else 0.35 if x == band_x - 1 else 0
            tint(x, y, a)

    def plate(spec, leading_right):
        """세로 직사각형 판: 진행 방향 쪽 열은 순백, 나머지와 위아래 끝은 한 단계 어두운 흰색"""
        left, thick, height = spec
        for t in range(thick):
            lead = t == (thick - 1 if leading_right else 0)
            for y in range(CY - height // 2, CY + height // 2 + 1):
                edge = y in (CY - height // 2, CY + height // 2)
                put(RM + left + t, y, SOFT if edge else WHITE if lead else SOFT)

    if rear:
        plate(rear, True)                                   # 뒤에서 뇌관 쪽으로 향함(오른쪽이 진행 방향)
    if front:
        plate(front, True)                                  # 탄 앞으로 나감(오른쪽이 진행 방향)
    return im


def render(states, spark_idx):
    """states: 탄환 칸마다 ('full',) · ('fire', k) · ('empty',). 마지막 칸은 크리티컬(기존 스파크 오버레이는 대기 중에만)."""
    img = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    normal, crit = flip(cs.shell(cs.NORMAL)), flip(cs.shell(cs.OPTIONS[cs.FINAL][1]))
    dimmed = cs.dim(normal)
    for i, st in enumerate(states):
        body = crit if i == N - 1 else normal
        x0, y0 = RM + SH, TM + i * PITCH
        if st[0] == 'empty':
            img.alpha_composite(dimmed, (x0, y0))
        elif st[0] == 'full':
            img.alpha_composite(body, (x0, y0))
            if i == N - 1 and spark_idx is not None:
                img.alpha_composite(flip(cs.spark(spark_idx)), (x0 - cs.M, y0 - cs.M))
        else:
            k = st[1]
            dx, dy = SHAKE[k]
            img.alpha_composite(body if k < DIM_FROM else dimmed, (x0 + dx, y0 + dy))
            img.alpha_composite(overlay(k), (SH + dx, y0 - 2 + dy))
    return img


def timeline():
    """(상태 목록, 스파크 번호, ms): 4발을 위에서부터 차례로 쏜다. 4발째가 크리티컬."""
    frames = []
    full = [('full',)] * N
    for ms, sp in ((500, None), (60, 1), (200, None), (60, 7), (300, None)):
        frames.append((full, sp, ms))
    for shot in range(N):
        for k, ms in enumerate(DURATIONS):
            st = [('empty',)] * shot + [('fire', k)] + [('full',)] * (N - shot - 1)
            frames.append((st, 3 if shot == N - 1 else None, ms))
        after = [('empty',)] * (shot + 1) + [('full',)] * (N - shot - 1)
        frames.append((after, 5 if shot == N - 2 else None, 420 if shot < N - 1 else 900))
    return frames


def build_sheet():
    """시트 78x76: 오버레이 36x13을 2열 x 5행(칸 38x15, 간격 2px)으로. 좌표 규칙은 unity/shell_fire_import.cs와 같다(Unity rect.y = 76 - y - 13)."""
    sheet = Image.new('RGBA', (78, 76), (0, 0, 0, 0))
    for k in range(len(FRAMES)):
        sheet.alpha_composite(overlay(k), (1 + 38 * (k % 2), 1 + 15 * (k // 2)))
    return sheet


def main():
    bg, S = (36, 38, 46, 255), 10
    f, fs, fb = cs.font(15), cs.font(12), cs.font(20)

    frames = []
    for states, sp, ms in timeline():
        g = Image.new('RGBA', (CW * S + 40, CH * S + 50), bg)
        d = ImageDraw.Draw(g)
        d.text((20, 10), '4발 탄창 · 맨 아래 = 크리티컬', font=f, fill=(235, 235, 235, 255))
        g.alpha_composite(render(states, sp).resize((CW * S, CH * S), Image.NEAREST), (20, 40))
        frames.append((g, ms))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    cs.save_gif(frames, OUT + '.gif')
    cs.save_gif(frames, OUT + '-slow.gif', speed=3)       # 3배 느리게: 프레임 확인용

    S2, per_row = 4, 6
    sheet = Image.new('RGBA', (1300, 520), bg)
    d = ImageDraw.Draw(sheet)
    d.text((14, 10), f'shell-fire-draft {VERSION} — 흰 충격파가 뇌관을 때리고 탄 앞으로 직사각형으로 퍼짐 {len(FRAMES)}프레임 (GIF에서 움직임 확인)', font=fb, fill=(255, 196, 60, 255))
    for k in range(len(FRAMES)):
        st = [('fire', k)] + [('full',)] * (N - 1)
        tile = render(st, None).crop((0, TM - 3, CW, TM + 12))
        x, y = 14 + (k % per_row) * 212, 60 + (k // per_row) * 140
        sheet.alpha_composite(tile.resize((tile.width * S2, tile.height * S2), Image.NEAREST), (x, y))
        d.text((x, y + tile.height * S2 + 4), f'{k + 1}/{len(FRAMES)}  {DURATIONS[k]}ms  흔들림 {SHAKE[k]}', font=fs, fill=(150, 150, 158, 255))
    sheet.save(OUT + '.png')
    print(OUT + '.png', OUT + '.gif', OUT + '-slow.gif')


if __name__ == '__main__':
    import sys
    if sys.argv[1:2] == ['import']:
        if os.path.exists(SHEET) and '--force' not in sys.argv:
            sys.exit('이미 있음: ' + SHEET)
        build_sheet().save(SHEET)
        print(SHEET)
        print('durations_ms', DURATIONS)
        print('shake_px', SHAKE)
        print('dim_from', DIM_FROM)
    else:
        main()
