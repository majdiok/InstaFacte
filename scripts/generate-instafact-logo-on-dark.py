"""Generate on-dark InstaFact lockup/icon variants from the existing light PNGs.

Reads (never overwrites):
  src/Frontend/factutrust-web/src/assets/branding/instafact-lockup.png
  src/Frontend/factutrust-web/src/assets/branding/instafact-icon.png

Writes:
  src/Frontend/factutrust-web/src/assets/branding/instafact-lockup-on-dark.png
  src/Frontend/factutrust-web/src/assets/branding/instafact-icon-on-dark.png
"""
from __future__ import annotations

import os
import shutil
import sys

from PIL import Image

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
BRANDING = os.path.join(
    REPO_ROOT, "src", "Frontend", "factutrust-web", "src", "assets", "branding"
)
LOCKUP_SRC = os.path.join(BRANDING, "instafact-lockup.png")
ICON_SRC = os.path.join(BRANDING, "instafact-icon.png")
LOCKUP_DST = os.path.join(BRANDING, "instafact-lockup-on-dark.png")
ICON_DST = os.path.join(BRANDING, "instafact-icon-on-dark.png")

# After the icon, before "Insta" (measured: 0.28 still icon, 0.32 already wordmark).
ICON_SPLIT_FRACTION = 0.30
OPAQUE_ALPHA = 16


def _sat(r: int, g: int, b: int) -> float:
    mx = max(r, g, b)
    if mx == 0:
        return 0.0
    return (mx - min(r, g, b)) / mx


def generate_lockup_on_dark(src_path: str) -> Image.Image:
    src = Image.open(src_path).convert("RGBA")
    w, h = src.size
    split_x = int(w * ICON_SPLIT_FRACTION)
    px = src.load()

    src_alpha_sum = 0
    dst_alpha_sum = 0
    left_sat_sum = 0.0
    left_n = 0
    right_luma_sum = 0.0
    right_n = 0

    out = src.copy()
    out_px = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            src_alpha_sum += a
            if x < split_x:
                dst_alpha_sum += a
                if a >= OPAQUE_ALPHA:
                    left_sat_sum += _sat(r, g, b)
                    left_n += 1
                continue
            if a >= OPAQUE_ALPHA:
                out_px[x, y] = (255, 255, 255, a)
                right_luma_sum += 255.0
                right_n += 1
            dst_alpha_sum += a
    if out.size != src.size:
        raise SystemExit(f"lockup size mismatch: {out.size} vs {src.size}")
    if dst_alpha_sum != src_alpha_sum:
        raise SystemExit(
            f"lockup alpha sum changed: {dst_alpha_sum} vs {src_alpha_sum}"
        )
    if left_n == 0 or (left_sat_sum / left_n) < 0.25:
        raise SystemExit(
            f"left region saturation too low: n={left_n} mean={left_sat_sum / max(left_n, 1):.3f}"
        )
    right_mean_luma = right_luma_sum / max(right_n, 1)
    if right_n == 0 or right_mean_luma <= 200:
        raise SystemExit(
            f"right region luma too low: n={right_n} mean={right_mean_luma:.1f}"
        )
    return out


def main() -> int:
    if not os.path.isfile(LOCKUP_SRC):
        raise SystemExit(f"missing source lockup: {LOCKUP_SRC}")
    if not os.path.isfile(ICON_SRC):
        raise SystemExit(f"missing source icon: {ICON_SRC}")

    lockup = generate_lockup_on_dark(LOCKUP_SRC)
    os.makedirs(BRANDING, exist_ok=True)
    lockup.save(LOCKUP_DST, "PNG")
    shutil.copy2(ICON_SRC, ICON_DST)

    icon_src = Image.open(ICON_SRC)
    icon_dst = Image.open(ICON_DST)
    if icon_dst.size != icon_src.size:
        raise SystemExit(f"icon size mismatch: {icon_dst.size} vs {icon_src.size}")

    print("Wrote", LOCKUP_DST, lockup.size)
    print("Wrote", ICON_DST, icon_dst.size)
    return 0


if __name__ == "__main__":
    sys.exit(main())
