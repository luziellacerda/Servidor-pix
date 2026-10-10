"""Exact Station cover selection from media/revista, with ROM folder context."""

import hashlib
from pathlib import Path

from PIL import Image


IMAGE_MIME = {"PNG": "image/png", "JPEG": "image/jpeg",
              "WEBP": "image/webp", "GIF": "image/gif"}
EXTENSION_MIME = {".png": "image/png", ".jpg": "image/jpeg",
                  ".jpeg": "image/jpeg", ".webp": "image/webp",
                  ".gif": "image/gif"}


def image_hash(path):
    if not path.is_file() or not 1 <= path.stat().st_size <= 5 * 1024 * 1024:
        return None
    try:
        with Image.open(path) as image:
            mime = IMAGE_MIME.get(image.format)
            width, height = image.size
            image.verify()
        if width < 1 or height < 1 or EXTENSION_MIME.get(path.suffix.lower()) != mime:
            return None
        return hashlib.sha256(path.read_bytes()).hexdigest()
    except (OSError, ValueError, SyntaxError):
        return None


def select_revista_cover(root, rom, magazine, expected=None):
    """Prefer the ROM's exact relative folder, then revista root, then identical copies.

    No XML image or approximate title is a fallback. Different contents within the
    selected folder require an explicit correction of the source before publishing.
    """
    root = root.resolve()
    magazine_root = (root / "media" / "revista").resolve()
    if not magazine_root.is_relative_to(root):
        raise ValueError("revista directory leaves platform root")
    relative = rom.resolve().relative_to(root)
    options = []
    for source in magazine.get(rom.stem, []):
        candidate = Path(source).resolve()
        if not candidate.is_relative_to(magazine_root):
            raise ValueError("revista image leaves magazine directory")
        if candidate.stem != rom.stem:
            raise ValueError("revista image name differs from ROM stem")
        options.append(candidate)
    same_folder = [p for p in options if
                   p.parent == magazine_root / relative.parent]
    at_root = [p for p in options if p.parent == magazine_root]
    chosen, rule = ((same_folder, "rom_folder") if same_folder else
                    (at_root, "revista_root") if at_root else
                    (options, "identical_copies"))
    if not chosen:
        raise ValueError("exact revista cover is missing")
    inspected = [(path, image_hash(path)) for path in chosen]
    if any(value is None for _, value in inspected):
        raise ValueError("selected revista cover is unreadable or invalid")
    if len({value for _, value in inspected}) != 1:
        raise ValueError("selected revista folder has different cover contents")
    candidate, cover_hash = min(inspected, key=lambda pair: str(pair[0]))
    if expected is not None and (
            expected.get("revistaSelectionStatus") != "ok" or
            expected.get("revistaSelectedSha256") != cover_hash or
            expected.get("revistaSelectionRule") != rule):
        raise ValueError("selected revista cover changed since disk inventory")
    return candidate, rule, cover_hash
