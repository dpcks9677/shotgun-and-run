"""텔레포트(입장 · 탈출) 연출 픽셀 스프라이트 — 컷씬 CS_TELEPORT_ENTER / CS_TELEPORT_EXIT

원본 파티클 이펙트(TeleportBeamFx: 바닥 고리 섬광 + 빛기둥 + 솟는 빛줄기 + 반짝이)를 그대로 구운 뒤 픽셀로 깬다.
외형은 원본을 따르되 픽셀 격자 · 흰색 단색 · 문턱 알파로 바꾸고, 일자 파티클은 얇게, 바닥은 도넛 동심원으로 바꾼다.

1) 굽기: unity/teleport_vfx_bake.cs (eval_file) → Temp/teleport_raw.png
   원본 프리팹은 커밋 0592596d의 Assets/09.VFX/Cutscene/TeleportBeamFx.prefab · M_TeleportGlow.mat
   (또는 ~/Downloads/TeleportBeamFx_Soft.unitypackage)을 Assets/_TempBake/에 꺼내 두고 굽는다.
   64px/unit(최종의 4배), 프레임 80×128px(5×8칸) × 18, 0.05초 간격, 검은 배경 가산 렌더
2) 픽셀화:
   python Docs/tools/spritegen/vfx/teleport_vfx.py [raw.png] [시안.png]   # 시안만 (기본: Docs/drafts/props/teleport-vfx-draft.png)
   python Docs/tools/spritegen/vfx/teleport_vfx.py import [raw.png]       # Assets/09.VFX/Cutscene/TeleportPixel/TeleportBurst.png

픽셀화 규칙 (STYLE.md 11장: 필터 확대 · 축소 없이 정수 격자, 반투명 없음)
- 4×4 칸 평균으로 1px을 만든다 (64 → 16px/unit)
- 밝기(가산 결과의 최대 채널)를 GAIN배 해서 0~1 세기로 쓴다
- 세기가 THRESHOLD 이상인 픽셀만 꽉 채워 남긴다 (디더 없음)
- 남은 픽셀은 모두 흰색
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import paths  # noqa: E402

VERSION = 'v06'

SCALE = 4
FW, FH = 80, 128            # 최종 프레임 (px)
GAIN = 1.8
THRESHOLD = 0.25            # 세기가 이 값 이상이면 꽉 찬 픽셀 (디더 없음, 2026-10-10 사용자)
FRAMES = 12                 # 앞 12프레임(0.6초)만 쓴다. 뒤는 흩어진 반짝이 점뿐

# 바닥 고리 (2026-10-10 사용자 레퍼런스): 발밑에서 고리 하나가 퍼져 나가며 얇아지고, 끝에서 점선으로 끊기며 사라진다.
# 고리 뒤쪽(위쪽 반)이 앞쪽보다 두껍다
GROUND = (FW / 2, FH - 20)  # 발밑 = 프리팹 피벗 (아래에서 20px)
RING_LIFE = 12              # 퍼지는 프레임 수 (이펙트 전체 길이)
RING_R0 = 6                 # 시작 가로 반지름 (px)
RING_RX = 24                # 다 퍼졌을 때 가로 반지름 (px)
RING_RY = 0.45              # 세로 / 가로 (탑다운 바닥 타원)
RING_BACK = (5.0, 1.0)      # 뒤쪽 두께 (시작, 끝) px
RING_FRONT = (2.0, 1.0)     # 앞쪽 두께 (시작, 끝) px
RING_DASH_FRAMES = 3        # 마지막 이 프레임 수 동안 점선으로 끊긴다
RING_DASH = 3               # 점선 한 칸 길이 (px)

WHITE = (255, 255, 255)
LEVELS = ((0.0, WHITE),)    # 흰색만 쓴다 (2026-10-10 사용자: 파란 기 제거)

DEFAULT_RAW = paths.repo('Temp', 'teleport_raw.png')


def pixelize_frame(raw):
    small = raw.resize((FW, FH), Image.BOX)          # 4×4 칸 평균 (정수 비율)
    src = small.load()
    out = Image.new('RGBA', (FW, FH), (0, 0, 0, 0))
    p = out.load()
    for y in range(FH):
        for x in range(FW):
            r, g, b = src[x, y][:3]
            v = min(1.0, max(r, g, b) / 255 * GAIN)
            if v < THRESHOLD:
                continue
            for lv, c in LEVELS:
                if v >= lv:
                    p[x, y] = c + (255,)
                    break
    return out


def draw_rings(img, idx):
    """발밑에서 퍼져 나가는 고리 하나. 퍼지며 얇아지고 마지막 프레임들은 점선으로 끊긴다"""
    if idx >= RING_LIFE:
        return
    p = img.load()
    cx, cy = GROUND
    t = idx / max(1, RING_LIFE - 1)
    ease = 1 - (1 - t) ** 2
    rx = RING_R0 + (RING_RX - RING_R0) * ease
    ry = max(1.0, rx * RING_RY)
    back = RING_BACK[0] + (RING_BACK[1] - RING_BACK[0]) * t       # 두께는 선형으로 줄여 처음 몇 프레임에 남게 한다
    front = RING_FRONT[0] + (RING_FRONT[1] - RING_FRONT[0]) * t
    dashed = idx >= RING_LIFE - RING_DASH_FRAMES

    def inside(x, y, a, b):
        return a > 0 and b > 0 and ((x + 0.5 - cx) / a) ** 2 + ((y + 0.5 - cy) / b) ** 2 <= 1

    for y in range(FH):
        th = back if y + 0.5 < cy else front
        half = th / 2
        for x in range(FW):
            if not inside(x, y, rx + half, ry + half * RING_RY * 2) or inside(x, y, rx - half, ry - half * RING_RY * 2):
                continue
            if dashed and (x // RING_DASH) % 2:
                continue
            p[x, y] = WHITE + (255,)


def frames(raw_path):
    raw = Image.open(raw_path).convert('RGB')
    n = min(FRAMES, raw.width // (FW * SCALE))
    out = []
    for i in range(n):
        f = pixelize_frame(raw.crop((i * FW * SCALE, 0, (i + 1) * FW * SCALE, FH * SCALE)))
        draw_rings(f, i)
        out.append(f)
    return out


def sheet(fs):
    out = Image.new('RGBA', (FW * len(fs), FH), (0, 0, 0, 0))
    for i, f in enumerate(fs):
        out.paste(f, (i * FW, 0))
    return out


def draft(fs, path):
    s = 3
    bg = (38, 41, 46)
    cols = 9
    rows = (len(fs) + cols - 1) // cols
    pad = 6
    out = Image.new('RGB', (cols * (FW * s + pad) + pad, rows * (FH * s + pad + 12) + pad), bg)
    d = ImageDraw.Draw(out)
    for i, f in enumerate(fs):
        ox = pad + (i % cols) * (FW * s + pad)
        oy = pad + (i // cols) * (FH * s + pad + 12) + 12
        big = f.resize((FW * s, FH * s), Image.NEAREST)
        tile = Image.new('RGB', big.size, bg)
        tile.paste(big, (0, 0), big)
        out.paste(tile, (ox, oy))
        d.text((ox, oy - 11), f'f{i}  {FW}x{FH}px  {VERSION}', fill=(200, 200, 200))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path)
    return path


if __name__ == '__main__':
    args = sys.argv[1:]
    if args and args[0] == 'import':
        fs = frames(args[1] if len(args) > 1 else DEFAULT_RAW)
        dst = paths.out(paths.repo('Assets', '09.VFX', 'Cutscene', 'TeleportPixel', 'TeleportBurst.png'))
        sheet(fs).save(dst)
        print(dst, len(fs), 'frames')
    else:
        fs = frames(args[0] if args else DEFAULT_RAW)
        print(draft(fs, args[1] if len(args) > 1 else paths.repo('Docs', 'drafts', 'props', 'teleport-vfx-draft.png')))
