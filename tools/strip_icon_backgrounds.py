"""Remove solid dark backgrounds from ProAqua icons."""
from __future__ import annotations

import os
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(r"E:\Project\Cursor\ProAqua")
MOBILE_IMG = ROOT / "mobile" / "ProAqua.App" / "Resources" / "Images"
CURSOR_ASSETS = Path(r"C:\Users\vasek\.cursor\projects\e-Project-Cursor-ProAqua\assets")


def strip_dark_bg(path: Path, threshold: int = 50) -> None:
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if r < threshold and g < threshold + 10 and b < threshold + 25 and max(r, g, b) < 75:
                px[x, y] = (r, g, b, 0)
            elif r + g + b < 95 and b <= 60:
                px[x, y] = (r, g, b, 0)
    im.save(path)
    print(f"stripped dark: {path.name}")


def strip_app_icon(src: Path, dst: Path) -> None:
    im = Image.open(src).convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            is_cyan = g > 90 and b > 90 and (g + b) > r * 2.2 and (g > r + 20 or b > r + 20)
            px[x, y] = (r, g, b, 255) if is_cyan else (0, 0, 0, 0)
    dst.parent.mkdir(parents=True, exist_ok=True)
    im.save(dst)
    print(f"app icon -> {dst}")


def copy_generated(name: str, dest: Path) -> None:
    src = CURSOR_ASSETS / name
    if not src.exists():
        # also try workspace assets
        alt = ROOT / "assets" / name
        src = alt if alt.exists() else src
    if not src.exists():
        print(f"missing {name}")
        return
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)
    print(f"copied {name} -> {dest}")


def main() -> None:
    strip_app_icon(ROOT / "assets" / "app-icon.png", ROOT / "assets" / "app-icon.png")
    shutil.copy2(ROOT / "assets" / "app-icon.png", MOBILE_IMG / "brand_app.png")

    for name in [
        "icon_level_guest.png",
        "icon_level_silver.png",
        "icon_level_platinum.png",
        "icon_loyalty.png",
        "icon_referral.png",
    ]:
        p = MOBILE_IMG / name
        if p.exists():
            strip_dark_bg(p)

    # loyalty jeep banner (narrow vertical)
    jeep_src = CURSOR_ASSETS / "loyalty_jeep_wash.png"
    if jeep_src.exists():
        im = Image.open(jeep_src).convert("RGB")
        # ensure portrait narrow: crop center to ~9:16 if needed
        w, h = im.size
        target_ratio = 9 / 16
        cur = w / h
        if cur > target_ratio:
            nw = int(h * target_ratio)
            left = (w - nw) // 2
            im = im.crop((left, 0, left + nw, h))
        out = MOBILE_IMG / "loyalty_jeep.png"
        im.save(out, quality=90)
        print(f"loyalty jeep -> {out}")

    seed_dir = ROOT / "backend" / "ProAqua.Api" / "Database" / "seed-images"
    seed_dir.mkdir(parents=True, exist_ok=True)
    mapping = {
        "svc_wash_dark.png": "service_wash.jpg",
        "svc_interior_dark.png": "service_interior.jpg",
        "svc_deep_dark.png": "service_deep.jpg",
        "svc_detail_dark.png": "service_detailing.jpg",
        "svc_ceramic_dark.png": "service_ceramic.jpg",
        "promo_complex_dark.png": "promo_complex.jpg",
        "promo_ceramic_dark.png": "promo_ceramic.jpg",
        "loyalty_jeep_wash.png": "loyalty_jeep.jpg",
    }
    for src_name, dest_name in mapping.items():
        src = CURSOR_ASSETS / src_name
        if not src.exists():
            print(f"skip missing {src_name}")
            continue
        im = Image.open(src).convert("RGB")
        # compress for DB storage
        im.thumbnail((960, 960))
        dest = seed_dir / dest_name
        im.save(dest, format="JPEG", quality=72, optimize=True)
        # also keep mobile local fallbacks for services
        if dest_name.startswith("service_"):
            mobile_name = {
                "service_wash.jpg": "service_wash.png",
                "service_interior.jpg": "service_interior.png",
                "service_deep.jpg": "service_deep_clean.png",
                "service_detailing.jpg": "service_detailing.png",
                "service_ceramic.jpg": "service_ceramic.png",
            }[dest_name]
            im.save(MOBILE_IMG / mobile_name, format="PNG", optimize=True)
        print(f"seed {dest_name} ({dest.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
