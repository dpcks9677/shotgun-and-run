"""구매한 에셋을 풀어 둔 폴더에서 플레이스홀더 이미지를 원본으로 되돌린다.

python Docs/tools/restore_assets.py --tiny-rpg <Tiny RPG 폴더> --fdr <FDR 폴더> [--dry]

- Tiny RPG Character Asset Pack 01 v2.0 (Full 22 Characters): "<캐릭터> with shadows" 폴더의 PNG를
  같은 이름 · 같은 크기인 저장소 파일에 덮어쓴다.
- Dungeon Tileset - Fantasy Dreamland Reborn: Tilesets/FDR_Dungeon.png, FDR_Dungeon_Animations.png를
  Tile/Dungeon/Dungeon.png, DungeonAnimations.png로 덮어쓴다.
.meta는 건드리지 않는다. 되돌리지 못한 파일은 마지막에 목록으로 보여 준다.
"""
import argparse
import shutil
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "Assets/04.Sprites"
REPLACED = [ROOT / line for line in (ROOT / "Docs/public-release/replaced-images.txt").read_text(encoding="utf8").splitlines() if line]
TINY_RPG_DIRS = ("Player", "Enemy", "NPC")
FDR_FILES = {
    "FDR_Dungeon.png": SPRITES / "Tile/Dungeon/Dungeon.png",
    "FDR_Dungeon_Animations.png": SPRITES / "Tile/Dungeon/DungeonAnimations.png",
}


def png_size(path):
    with open(path, "rb") as f:
        return struct.unpack(">II", f.read(24)[16:24])


def restore(source, target, dry):
    if png_size(source) != png_size(target):
        return False
    if not dry:
        shutil.copyfile(source, target)
    return True


def restore_tiny_rpg(pack, dry):
    index = {}
    for p in pack.rglob("*.png"):
        if p.parent.name.endswith("with shadows"):
            index.setdefault(p.name, []).append(p)
    done, skipped = 0, []
    for target in REPLACED:
        if target.relative_to(SPRITES).parts[0] not in TINY_RPG_DIRS:
            continue
        sources = index.get(target.name, [])
        source = next((s for s in sources if s.parent.name == target.parent.name), sources[0] if sources else None)
        if source and restore(source, target, dry):
            done += 1
        else:
            skipped.append(target.relative_to(ROOT))
    return done, skipped


def restore_fdr(pack, dry):
    done, skipped = 0, []
    for name, target in FDR_FILES.items():
        source = next((p for p in pack.rglob(name) if p.parent.name == "Tilesets"), None)
        if source and restore(source, target, dry):
            done += 1
        else:
            skipped.append(target.relative_to(ROOT))
    return done, skipped


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tiny-rpg", type=Path)
    parser.add_argument("--fdr", type=Path)
    parser.add_argument("--dry", action="store_true")
    args = parser.parse_args()
    for label, pack, run in (("Tiny RPG", args.tiny_rpg, restore_tiny_rpg), ("FDR", args.fdr, restore_fdr)):
        if pack:
            done, skipped = run(pack, args.dry)
            print(f"{label}: {done}개 복원, {len(skipped)}개 그대로(플레이스홀더)")
            for s in skipped:
                print("  -", s.as_posix())


main()
