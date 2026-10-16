"""타이틀 로고 축소판: title_redraw.ase의 레이어(Gun · Bullet · Title)를 나눠 각각 줄인다(안쪽 면만 줄이고 외곽선은 다시 두름).

    python title_logo.py <시안.png>      # 원본 1배 / 줄인 판 비교 (6배 확대)
    python title_logo.py import          # Assets/04.Sprites/UI/Title/title-logo.png 교체(여백 잘라냄, 승인 후)

줄이는 방법: 안쪽 면을 면적 평균으로 줄여 원본 팔레트 색으로 되돌리고(알파 50% 기준), 외곽선은 1px로 다시 두른다.
(최근접 축소는 얇은 선을 지운다 — STYLE.md 11장)
"""
import os
import subprocess
import sys
import tempfile
from PIL import Image

import paths

ASE = paths.repo('Assets', '04.Sprites', 'UI', 'Title', 'title_redraw.ase')
ASEPRITE = os.environ.get('ASEPRITE') or r'C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe'
SCALE = float(os.environ.get('LOGO_SCALE', '0.75'))
LAYERS = ('Gun', 'Bullet', 'Title')       # 아래에서 위 순서 (ase 레이어 순서와 같음)


def layers():
    tmp = tempfile.mkdtemp()
    out = {}
    for name in LAYERS:
        p = os.path.join(tmp, name + '.png')
        subprocess.run([ASEPRITE, '-b', '--layer', name, ASE, '--save-as', p], check=True)
        out[name] = Image.open(p).convert('RGBA')
    return out


def is_outline(c):
    return c[3] == 255 and max(c[:3]) < 12


def shrink(im, k, thr=128, fill=False):
    """안쪽 면(외곽선 제외)만 면적 평균으로 줄이고 → 원본 팔레트 색으로 되돌리고 → 알파 50% 기준으로 켠 뒤,
    검은 1px 외곽선(8방향)을 다시 두른다. 외곽선까지 같이 줄이면 1px 선이 끊기거나 지워진다."""
    px = im.load()
    inner = Image.new('RGBA', im.size)
    ip = inner.load()
    pal = set()
    for y in range(im.height):
        for x in range(im.width):
            c = px[x, y]
            if c[3] == 255 and not is_outline(c):
                ip[x, y] = (c[0], c[1], c[2], 255)
                pal.add(c)
    pre = Image.new('RGBA', im.size)
    pp = pre.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = ip[x, y]
            pp[x, y] = (r * a // 255, g * a // 255, b * a // 255, a)
    pad = 2
    w, h = max(1, round(im.width * k)), max(1, round(im.height * k))
    sm = pre.resize((w, h), Image.BOX)
    out = Image.new('RGBA', (w + pad * 2, h + pad * 2))
    o, s_ = out.load(), sm.load()
    on = set()
    for y in range(h):
        for x in range(w):
            r, g, b, a = s_[x, y]
            if a < thr:
                continue
            r, g, b = (min(255, v * 255 // a) for v in (r, g, b))
            o[x + pad, y + pad] = min(pal, key=lambda c: (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2)
            on.add((x + pad, y + pad))
    if fill:      # 글씨: 글자 속 구멍(A · D · O의 속)은 외곽선과 같은 검정으로 채운다
        W2, H2 = out.size
        seen, stack = set(), [(0, 0)]
        while stack:
            x, y = stack.pop()
            if (x, y) in seen or (x, y) in on or not (0 <= x < W2 and 0 <= y < H2):
                continue
            seen.add((x, y))
            stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
        for y in range(H2):
            for x in range(W2):
                if (x, y) not in on and (x, y) not in seen:
                    o[x, y] = (0, 0, 0, 255)
                    on.add((x, y))
    for (x, y) in list(on):
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if (x + dx, y + dy) not in on:
                    o[x + dx, y + dy] = (0, 0, 0, 255)
    return out



# ---- 글씨: 손으로 다시 그린 도트 글자 (3/4 크기). 자동 축소는 획이 가늘어지고 A · D 속 구멍이 뭉개져서 쓰지 않는다 ----
def _g(w, h):
    return [[0] * w for _ in range(h)]


def _rect(g, x0, y0, x1, y1):
    for y in range(max(0, y0), min(len(g) - 1, y1) + 1):
        for x in range(max(0, x0), min(len(g[0]) - 1, x1) + 1):
            g[y][x] = 1


def _cut(g, c, tl=True, tr=True, bl=True, br=True):
    """모서리 깎기: c칸짜리 삼각형을 뺀다"""
    h, w = len(g), len(g[0])
    for i in range(c):
        for j in range(c - i):
            if tl: g[i][j] = 0
            if tr: g[i][w - 1 - j] = 0
            if bl: g[h - 1 - i][j] = 0
            if br: g[h - 1 - i][w - 1 - j] = 0


def glyph(ch, w, h, t, th, c=2):
    g = _g(w, h)
    mid = (h - th) // 2
    if ch == 'H':
        _rect(g, 0, 0, t - 1, h - 1); _rect(g, w - t, 0, w - 1, h - 1); _rect(g, t, mid, w - t - 1, mid + th - 1)
    elif ch == 'T':
        _rect(g, 0, 0, w - 1, th - 1); _rect(g, (w - t) // 2, 0, (w - t) // 2 + t - 1, h - 1)
    elif ch == 'U':
        _rect(g, 0, 0, t - 1, h - 1); _rect(g, w - t, 0, w - 1, h - 1); _rect(g, 0, h - th, w - 1, h - 1); _cut(g, c - 1, tl=False, tr=False)
    elif ch == 'N':
        _rect(g, 0, 0, t - 1, h - 1); _rect(g, w - t, 0, w - 1, h - 1)
        for r in range(h):
            xl = round((w - t) * r / (h - 1))
            _rect(g, xl, r, xl + t - 1, r)
    elif ch == 'O':
        _rect(g, 0, 0, w - 1, h - 1); _rect(g, t, th, w - t - 1, h - th - 1) if False else None
        for y in range(th, h - th):
            for x in range(t, w - t):
                g[y][x] = 0
        _cut(g, c)
    elif ch == 'D':
        _rect(g, 0, 0, w - 1, h - 1)
        for y in range(th, h - th):
            for x in range(t, w - t):
                g[y][x] = 0
        _cut(g, c + 1, tl=False, bl=False)
    elif ch == 'R':
        bowl = h * 11 // 20 + 1
        _rect(g, 0, 0, w - 1, bowl - 1)
        for y in range(th, bowl - th + 1):
            for x in range(t, w - t):
                g[y][x] = 0
        _cut(g, c, bl=False, br=False)
        _rect(g, 0, 0, t - 1, h - 1)
        for r in range(bowl, h):      # 다리: 줄기 쪽에서 시작해 오른쪽 아래로 비스듬히
            xl = t - 1 + round((w - t - t + 1) * (r - bowl + 1) / (h - bowl))
            _rect(g, xl, r, xl + t - 1, r)
    elif ch == 'S':
        _rect(g, 0, 0, w - 1, th - 1); _rect(g, 0, 0, t - 1, mid + th - 1)
        _rect(g, 0, mid, w - 1, mid + th - 1); _rect(g, w - t, mid, w - 1, h - 1)
        _rect(g, 0, h - th, w - 1, h - 1); _cut(g, c)
    elif ch == 'G':
        _rect(g, 0, 0, w - 1, h - 1)
        for y in range(th, h - th):
            for x in range(t, w - t):
                g[y][x] = 0
        _rect(g, w // 2, mid, w - 1, mid + th - 1); _rect(g, w - t, mid, w - 1, h - 1)
        for y in range(th, mid):
            for x in range(w - t, w):
                g[y][x] = 0
        _cut(g, c)
    elif ch == 'A':
        half = w / 2
        flat = max(3, w - 2 * (h // 2 + 1))
        for r in range(h):
            k = r / (h - 1)
            xl = round((half - flat / 2) * (1 - k))
            xr = w - 1 - xl
            if r < th:
                _rect(g, xl, r, xr, r)
            else:
                _rect(g, xl, r, xl + t - 1, r); _rect(g, xr - t + 1, r, xr, r)
        for r in range(h * 5 // 9, h * 5 // 9 + th):
            k = r / (h - 1)
            xl = round((half - flat / 2) * (1 - k))
            _rect(g, xl, r, w - 1 - xl, r)
    return g


def text_line(word, w, h, t, th, gap=1, space=0, c=2):
    cols = []
    for ch in word:
        if ch == ' ':
            cols.append(None)
        else:
            cols.append(glyph(ch, w, h, t, th, c))
    W = sum((len(g[0]) if g else space) for g in cols) + gap * (len(cols) - 1)
    out = _g(W, h)
    x = 0
    for g in cols:
        if g is None:
            x += space + gap
            continue
        for y in range(h):
            for xx in range(len(g[0])):
                if g[y][xx]:
                    out[y][x + xx] = 1
        x += len(g[0]) + gap
    return out


def title_text():
    """SHOTGUN / AND RUN: 위 줄 높이 13 · 글자폭 10, 아래 줄 높이 15 · 글자폭 13. 흰 글자 + 검정 외곽선 1px(속 구멍은 검정으로 채움)"""
    l1 = text_line('SHOTGUN', 10, 13, 3, 3, gap=1, c=2)
    l2 = text_line('AND RUN', 13, 15, 4, 4, gap=1, space=4, c=2)
    W = max(len(l1[0]), len(l2[0]))
    H = len(l1) + 1 + len(l2)
    pad = 2
    img = Image.new('RGBA', (W + pad * 2, H + pad * 2))
    o = img.load()
    on = set()
    for line, y0 in ((l1, 0), (l2, len(l1) + 1)):
        x0 = (W - len(line[0])) // 2
        for y, row in enumerate(line):
            for x, v in enumerate(row):
                if v:
                    o[x0 + x + pad, y0 + y + pad] = (255, 255, 255, 255)
                    on.add((x0 + x + pad, y0 + y + pad))
    W2, H2 = img.size
    seen, stack = set(), [(0, 0)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or (x, y) in on or not (0 <= x < W2 and 0 <= y < H2):
            continue
        seen.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    for y in range(H2):
        for x in range(W2):
            if (x, y) not in on and (x, y) not in seen:
                o[x, y] = (0, 0, 0, 255)
                on.add((x, y))
    for (x, y) in list(on):
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if (x + dx, y + dy) not in on and 0 <= x + dx < W2 and 0 <= y + dy < H2:
                    o[x + dx, y + dy] = (0, 0, 0, 255)
    return img


def logo(k=SCALE):
    """줄인 로고: 세 레이어를 같은 비율로 줄여 원래 위치 관계대로 다시 합친다."""
    ly = layers()
    allbox = Image.new('RGBA', (320, 180))
    for im in ly.values():
        allbox.alpha_composite(im)
    x0, y0, x1, y1 = allbox.getbbox()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    W, H = round((x1 - x0) * k) + 6, round((y1 - y0) * k) + 6
    out = Image.new('RGBA', (W, H))
    for n in LAYERS:
        bb = ly[n].getbbox()
        part = title_text() if n == 'Title' else shrink(ly[n].crop(bb), k)
        bx, by = (bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2
        px = W / 2 + (bx - cx) * k - part.width / 2
        py = H / 2 + (by - cy) * k - part.height / 2
        out.alpha_composite(part, (round(px), round(py)))
    return out.crop(out.getbbox())


def compare(path, S=6):
    orig = Image.new('RGBA', (320, 180))
    for im in layers().values():
        orig.alpha_composite(im)
    orig = orig.crop(orig.getbbox())
    new = logo()
    W = orig.width + new.width + 30
    sheet = Image.new('RGBA', (W, orig.height + 16), (30, 22, 17, 255))
    sheet.alpha_composite(orig, (8, 8))
    sheet.alpha_composite(new, (orig.width + 22, 8))
    sheet.resize((W * S, (orig.height + 16) * S), Image.NEAREST).convert('RGB').save(path)
    print('원본', orig.size, '→', new.size)


if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else 'title-logo-draft.png'
    if arg == 'import':
        logo().save(paths.out(paths.repo('Assets', '04.Sprites', 'UI', 'Title', 'title-logo.png')))
    else:
        compare(arg)
