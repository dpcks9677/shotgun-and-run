"""스테이지 1 도형 초안 이미지 렌더러.

Stage1Layout.asset(Unity YAML)을 읽어 도형 초안 PNG를 그린다. 배치를 바꾼 뒤 이미지를 갱신할 때 쓴다.

사용 (저장소 루트에서):
  python Docs/tools/render_stage_draft.py <Stage1Layout.asset> <출력.png> <버전> <날짜> [이동속도]
  예) python Docs/tools/render_stage_draft.py Assets/_Sandbox/Yechann/Layouts/Stage1Layout.asset Docs/drafts/stage1-shape-draft.png v14 2026-10-05 6.5

규칙
  - 출력은 항상 Docs/drafts/stage1-shape-draft.png 한 파일을 덮어쓴다 (-vNN 파일을 새로 만들지 않는다).
  - 버전 · 날짜는 이미지 왼쪽 아래 배지에만 적고, Docs/level-design.md 2장에 같은 버전의 변경 내용을 한 줄 추가한다.
  - 이동속도는 축척 막대(50u) 옆 이동 시간 표기에만 쓰인다 (기본 4.5). 명세 5.1의 값을 넣는다.
  - .asset 은 읽기만 한다.

필요: Python 3 + Pillow, 같은 폴더의 parse_layout.py, Windows 한글 폰트(C:/Windows/Fonts/malgun.ttf, malgunbd.ttf).
  다른 OS면 FONT_REG / FONT_BOLD 경로를 바꾼다.

코드에 고정된 값 (바뀌면 직접 수정)
  - 좌표 변환 px = 196 + 4*x, py = 1100 - 4*y (1u = 4px), 캔버스 1788 x 1332
  - 방 종류 색(FILL), 존 이름(ZONE_NAMES), 범례(LEGEND)
  - 점선 "기믹룸 후보" 상자: x228~290, y222~264
  - 방 스타일 값은 0~9만 지원 (DungeonFloorStyle에 값이 늘면 FILL도 늘려야 함)

검증 방법: git에서 이전 버전 .asset 을 꺼내(git show <커밋>:<경로>) 같은 방식으로 그려 그때 커밋된 PNG와 비교하면
방 · 통로 · 격자 위치가 일치한다 (글자 모양만 폰트 차이로 약간 다름).
"""
import sys
from PIL import Image, ImageDraw, ImageFont
from parse_layout import parse  # 같은 폴더(Docs/tools)의 parse_layout.py

W, H = 1788, 1332
OX, OY, S = 196, 1100, 4

BG = (30, 26, 23)
GRID_MINOR = (58, 52, 48)
GRID_MAJOR = (78, 70, 64)
PANEL = (24, 21, 19)
BORDER = (90, 90, 90)
OUTLINE = (235, 235, 235)
TEXT_STROKE = (20, 16, 14)
CORRIDOR = (94, 94, 102)
BLOCK = (62, 54, 48)

# DungeonFloorStyle 값 -> 채움 색 (Zone, Room, Corridor, HighRiskZone, Spawn, Junction, Exit, Boss, Teleport, Gimmick)
FILL = {
    0: (120, 171, 120), 1: (153, 133, 115), 2: CORRIDOR, 3: (199, 110, 89), 4: (89, 150, 219),
    5: (150, 150, 150), 6: (230, 199, 79), 7: (171, 89, 199), 8: (79, 209, 209), 9: (240, 150, 61),
}
ZONE_GRID = {0: (120, 114, 85), 3: (165, 80, 68)}

ZONE_NAMES = {
    'Z1': '대장간', 'Z2': '드워프 공방', 'Z3': '하수도', 'Z4': '기관실', 'Z5': '마력석 광맥',
    'Z6': '도축업장', 'Z7': '고블린 캠프', 'Z8': '연금술방', 'Z9': '지하 묘지', 'Z10': '의무실',
}

LEGEND = [
    ((120, 171, 120), '메인 존'), ((199, 110, 89), '고위험 존'), ((89, 150, 219), '스폰'),
    ((230, 199, 79), '탈출'), ((150, 150, 150), '갈림길'), ((153, 133, 115), '작은 방 (커넥터 중간)'),
    ((240, 150, 61), '기믹 Room'), ((79, 209, 209), '순간이동'), ((171, 89, 199), '보스룸'),
    ((94, 94, 102), '커넥터 (폭 6u)'),
]

FONT_REG = 'C:/Windows/Fonts/malgun.ttf'
FONT_BOLD = 'C:/Windows/Fonts/malgunbd.ttf'


def font(path, size):
    return ImageFont.truetype(path, size)


def to_px(x, y):
    return OX + S * x, OY - S * y


def rect_px(x, y, w, h):
    x0, y1 = to_px(x, y)          # 왼쪽 아래
    x1, y0 = to_px(x + w, y + h)  # 오른쪽 위
    return x0, y0, x1, y1


def draw_label(d, cx, cy, text, fnt, fill=(255, 255, 255), stroke=2):
    d.text((cx, cy), text, font=fnt, fill=fill, anchor='mm', stroke_width=stroke, stroke_fill=TEXT_STROKE)


def dashed_rect(d, box, color, dash=7, gap=6, width=2):
    x0, y0, x1, y1 = box
    x = x0
    while x < x1:
        e = min(x + dash, x1)
        d.rectangle([x, y0, e, y0 + width - 1], fill=color)
        d.rectangle([x, y1 - width + 1, e, y1], fill=color)
        x += dash + gap
    y = y0
    while y < y1:
        e = min(y + dash, y1)
        d.rectangle([x0, y, x0 + width - 1, e], fill=color)
        d.rectangle([x1 - width + 1, y, x1, e], fill=color)
        y += dash + gap


def render(layout_path, out_path, version, date, speed=4.5):
    rooms, paths, blocks = parse(open(layout_path, encoding='utf-8').read())
    im = Image.new('RGB', (W, H), BG)
    d = im_draw = ImageDraw.Draw(im)

    # 격자: 10u 간격, 50u마다 굵은 선 (가로선 먼저, 세로선이 교차점 위에 그려짐)
    for gy in range(-60, 280, 10):
        py = OY - S * gy
        if 0 <= py < H:
            d.line([(0, py), (W, py)], fill=GRID_MAJOR if gy % 50 == 0 else GRID_MINOR, width=1)
    for gx in range(-50, 400, 10):
        px = OX + S * gx
        if 0 <= px < W:
            d.line([(px, 0), (px, H)], fill=GRID_MAJOR if gx % 50 == 0 else GRID_MINOR, width=1)

    # 커넥터 (중심선 + 폭)
    for p in paths:
        w2 = p['Width'] / 2
        pts = p['Points']
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            x0, x1 = sorted((ax, bx))
            y0, y1 = sorted((ay, by))
            if ax == bx:  # 세로
                box = rect_px(ax - w2, y0 - w2, p['Width'], (y1 - y0) + p['Width'])
            else:         # 가로
                box = rect_px(x0 - w2, ay - w2, (x1 - x0) + p['Width'], p['Width'])
            d.rectangle([box[0], box[1], box[2], box[3]], fill=CORRIDOR)

    # 방 (Shape 1 = 팔각형: 네 모서리를 Bevel칸씩 45도로 깎음 → 사각형으로 그린 뒤 팔각형 마스크로 합성)
    for r in rooms:
        x0, y0, x1, y1 = rect_px(r['x'], r['y'], r['width'], r['height'])
        st = r.get('Style', 1)
        octagon = r.get('Shape', 0) == 1 and r.get('Bevel', 0) > 0
        base, d = (im, im_draw) if not octagon else (im.copy(), None)
        d = d or ImageDraw.Draw(base)
        d.rectangle([x0, y0, x1, y1], fill=FILL[st], outline=None if octagon else OUTLINE)
        if st in ZONE_GRID:
            gc = ZONE_GRID[st]
            for gx in range((r['x'] // 10 + 1) * 10, r['x'] + r['width'], 10):
                px = OX + S * gx
                d.line([(px, y0 + 1), (px, y1 - 1)], fill=gc, width=1)
            for gy in range((r['y'] // 10 + 1) * 10, r['y'] + r['height'], 10):
                py = OY - S * gy
                d.line([(x0 + 1, py), (x1 - 1, py)], fill=gc, width=1)
        if octagon:
            k = r['Bevel'] * S
            poly = [(x0 + k, y0), (x1 - k, y0), (x1, y0 + k), (x1, y1 - k), (x1 - k, y1), (x0 + k, y1), (x0, y1 - k), (x0, y0 + k)]
            mask = Image.new('L', im.size, 0)
            ImageDraw.Draw(mask).polygon(poly, fill=255)
            im.paste(base, mask=mask)
            im_draw.polygon(poly, outline=OUTLINE)
        d = im_draw

    # R7 칸막이 (방 위에 어두운 사각형)
    for b in blocks:
        x0, y0, x1, y1 = rect_px(b['x'], b['y'], b['width'], b['height'])
        d.rectangle([x0, y0, x1, y1], fill=BLOCK)

    # 라벨
    f_big = font(FONT_BOLD, 18)
    f_mid = font(FONT_BOLD, 15)
    f_small = font(FONT_REG, 10)
    f_id = font(FONT_BOLD, 12)
    for r in rooms:
        x0, y0, x1, y1 = rect_px(r['x'], r['y'], r['width'], r['height'])
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        n, st = r['Name'], r.get('Style', 1)
        size = f"{r['width']}x{r['height']}" + (' 팔각형' if r.get('Shape', 0) == 1 else '')
        if n == 'R7':
            draw_label(d, cx, y0 - 14, f"R7 회랑 {size}", font(FONT_BOLD, 12), stroke=2)
        elif st in (0, 3):
            draw_label(d, cx, cy - 20, ZONE_NAMES.get(n, n), f_mid)
            draw_label(d, cx, cy, n, f_big)
            draw_label(d, cx, cy + 19, size, f_small)
        elif st in (4, 6, 7):
            draw_label(d, cx, cy - 8, n, f_mid if st == 7 else font(FONT_BOLD, 14))
            draw_label(d, cx, cy + 9, size, f_small)
        else:
            draw_label(d, cx, cy, n, f_id)

    # 기믹룸 후보 (기획 중): 도형 초안 x228-290, y222-264
    gx0, gy0 = to_px(228, 264)
    gx1, gy1 = to_px(290, 222)
    dashed_rect(d, (gx0, gy0, gx1, gy1), (240, 150, 61))
    d.text(((gx0 + gx1) / 2, (gy0 + gy1) / 2), '기믹룸 후보 (기획 중)', font=font(FONT_REG, 11),
           fill=(240, 150, 61), anchor='mm')

    # 제목
    d.text((24, 24), '스테이지 1 도형 초안 · 격자 10u (굵은 선 50u)', font=font(FONT_REG, 14),
           fill=(210, 210, 210), anchor='lm')

    # 범례
    d.rectangle([1645, 8, 1780, 222], fill=PANEL, outline=BORDER)
    for i, (c, label) in enumerate(LEGEND):
        y = 24 + i * 20
        d.rectangle([1653, y - 6, 1665, y + 6], fill=c)
        d.text((1672, y), label, font=font(FONT_REG, 11), fill=(220, 220, 220), anchor='lm')

    # 버전 배지
    d.rectangle([16, 1196, 236, 1270], fill=PANEL, outline=BORDER)
    d.text((31, 1227), version, font=font(FONT_BOLD, 40), fill=(240, 240, 240), anchor='lm')
    d.text((28, 1258), date, font=font(FONT_REG, 13), fill=(200, 200, 200), anchor='lm')

    # 축척 막대: 50u = 200px, 이동 시간 표기
    sec = 50 / speed
    d.rectangle([24, 1302, 224, 1304], fill=(240, 240, 240))
    sec_txt = f"{sec:.0f}s" if sec >= 10 else f"{sec:.1f}s"
    d.text((24, 1297), f"50u ({speed:g}u/s = {sec_txt})", font=font(FONT_REG, 10), fill=(220, 220, 220), anchor='ls')

    im.save(out_path)
    return im


if __name__ == '__main__':
    lp, op, ver, date = sys.argv[1:5]
    sp = float(sys.argv[5]) if len(sys.argv) > 5 else 4.5
    render(lp, op, ver, date, sp)
    print('saved', op)
