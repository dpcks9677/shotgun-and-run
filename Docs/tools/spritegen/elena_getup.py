"""엘레나 결박 · 일어서기 시트 생성

Wizard_Death.png(4프레임: 서 있음 · 휘청 · 무릎 · 쓰러짐)에서 두 시트를 만든다.
- Elena_Kidnapped.png (200x100): 쓰러짐 + 숨쉬기(등이 한 칸 올라온 쓰러짐). 결박 중 반복 재생.
- Elena_GetUp.png (400x100): 사망 시트를 거꾸로(쓰러짐 → 무릎 → 휘청 → 서 있음). 결박이 풀릴 때 한 번 재생.

사용: python elena_getup.py [미리보기.png]   (인자 없이 실행하면 시트만 쓴다)
Elena_Kidnapped.png.meta는 기존 guid · 첫 스프라이트를 유지한 채 두 번째 스프라이트를 덧붙이므로 직접 고친다.
Elena_GetUp.png.meta는 없을 때만 새로 만든다.
"""

import os
import sys
import uuid

import numpy as np
from PIL import Image

from convert_npc_sprites import SOURCE_BASE, TARGET_BASE, convert_image, generate_sprite_meta

DEATH = os.path.join(SOURCE_BASE, "Wizard", "Wizard with shadows", "Wizard_Death.png")
OUT_DIR = os.path.join(TARGET_BASE, "NPC_Mage_Elena")

# 쓰러진 등(로브)이 놓인 열 범위. 이 열의 윗부분을 한 칸 올리고 맨 아래 줄은 그대로 둔다(숨 들이쉼).
BACK_COLUMNS = (44, 52)
BACK_TOP, BACK_BOTTOM = 49, 55


def frames(sheet):
    return [sheet.crop((i * 100, 0, i * 100 + 100, 100)) for i in range(sheet.width // 100)]


def breath(lying):
    arr = np.array(lying)
    out = arr.copy()
    x0, x1 = BACK_COLUMNS
    out[BACK_TOP - 1:BACK_BOTTOM, x0:x1] = arr[BACK_TOP:BACK_BOTTOM + 1, x0:x1]
    return Image.fromarray(out, "RGBA")


def strip(images):
    sheet = Image.new("RGBA", (100 * len(images), 100), (0, 0, 0, 0))
    for i, image in enumerate(images):
        sheet.paste(image, (i * 100, 0))
    return sheet


def write_kidnapped_meta(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    if "Elena_Kidnapped_1" in text:
        return

    u = uuid.uuid5(uuid.NAMESPACE_DNS, "Elena_Kidnapped_1")
    internal_id = int.from_bytes(u.bytes[:8], "big", signed=True)

    text = text.replace(
        "    second: Elena_Kidnapped\n",
        f"    second: Elena_Kidnapped\n  - first:\n      213: {internal_id}\n    second: Elena_Kidnapped_1\n",
        1,
    )
    entry = f"""    - serializedVersion: 2
      name: Elena_Kidnapped_1
      rect:
        serializedVersion: 2
        x: 100
        y: 0
        width: 100
        height: 100
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {u.hex}
      internalID: {internal_id}
      vertices: []
      indices: 
      edges: []
      weights: []
"""
    marker = "    outline: []\n    customData: \n    physicsShape: []\n    bones: []\n    spriteID: \n"
    assert marker in text
    text = text.replace(marker, entry + marker, 1)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)


def main():
    death, _ = convert_image(DEATH)
    stand, hit, kneel, lying = frames(death)
    kidnapped = strip([lying, breath(lying)])
    getup = strip([lying, kneel, hit, stand])

    kidnapped_path = os.path.join(OUT_DIR, "Elena_Kidnapped.png")
    getup_path = os.path.join(OUT_DIR, "Elena_GetUp.png")

    if len(sys.argv) > 1:
        preview = Image.new("RGBA", (600, 200), (60, 60, 60, 255))
        preview.alpha_composite(kidnapped, (0, 0))
        preview.alpha_composite(getup, (0, 100))
        preview.resize((1800, 600), Image.NEAREST).save(sys.argv[1])
        return

    kidnapped.save(kidnapped_path, "PNG")
    getup.save(getup_path, "PNG")
    write_kidnapped_meta(kidnapped_path + ".meta")

    getup_meta = getup_path + ".meta"
    if not os.path.exists(getup_meta):
        with open(getup_meta, "w", encoding="utf-8") as f:
            f.write(generate_sprite_meta(uuid.uuid4().hex, "Elena_GetUp.png", getup.width, getup.height))


if __name__ == "__main__":
    main()
