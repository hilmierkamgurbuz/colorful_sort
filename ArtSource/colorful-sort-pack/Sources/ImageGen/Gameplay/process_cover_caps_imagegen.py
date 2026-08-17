#!/usr/bin/env python3
"""Build the active blocky cover lid and full-cell bottom cap.

All visible pixels derive from built-in ImageGen PNG sources (including the
already-active ImageGen repeat cell). Processing is crop, alpha cleanup,
premultiplied resize, transparent placement, and a short ImageGen-pixel seam blend.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image


HERE = Path(__file__).resolve().parent
ACTIVE = HERE.parents[2] / "Gameplay"


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def clean_alpha(image: Image.Image, cutoff: int = 10) -> Image.Image:
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[..., 3].astype(np.int32)
    remapped = np.where(
        alpha <= cutoff,
        0,
        np.clip((alpha - cutoff) * 255 // (255 - cutoff), 0, 255),
    ).astype(np.uint8)
    pixels[..., 3] = remapped
    pixels[remapped == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def crop(image: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    return clean_alpha(image.crop(box))


def premultiplied_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premultiplied = source[..., :3] * alpha[..., None]
    planes: list[np.ndarray] = []
    for channel in (*np.moveaxis(premultiplied, 2, 0), alpha):
        plane = Image.fromarray(channel.astype(np.float32), mode="F")
        planes.append(
            np.asarray(plane.resize(size, Image.Resampling.LANCZOS), dtype=np.float32)
        )
    alpha_out = np.clip(planes[3], 0.0, 1.0)
    premul_out = np.stack(planes[:3], axis=2)
    rgb_out = np.zeros_like(premul_out)
    visible = alpha_out > (1.0 / 255.0)
    rgb_out[visible] = premul_out[visible] / alpha_out[visible, None]
    result = np.dstack((np.clip(rgb_out, 0.0, 1.0), alpha_out))
    return Image.fromarray(np.rint(result * 255.0).astype(np.uint8), "RGBA")


def match_top_edge(
    image: Image.Image, reference_row: np.ndarray, band: int = 10
) -> Image.Image:
    """Match the bottom-cell top edge to an ImageGen repeat-cell row."""
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premul = source[..., :3] * alpha[..., None]

    reference = reference_row.astype(np.float32) / 255.0
    ref_alpha = reference[..., 3]
    ref_premul = reference[..., :3] * ref_alpha[..., None]
    for y in range(band):
        weight = 1.0 - y / (band - 1)
        premul[y] = premul[y] * (1.0 - weight) + ref_premul * weight
        alpha[y] = alpha[y] * (1.0 - weight) + ref_alpha * weight

    rgb = np.zeros_like(premul)
    visible = alpha > (1.0 / 255.0)
    rgb[visible] = premul[visible] / alpha[visible, None]
    result = np.dstack((np.clip(rgb, 0.0, 1.0), np.clip(alpha, 0.0, 1.0)))
    return Image.fromarray(np.rint(result * 255.0).astype(np.uint8), "RGBA")


def blend_to_terminal(
    base: Image.Image, terminal: Image.Image, transition: int = 40
) -> Image.Image:
    """Morph the active ImageGen repeat into the new rounded ImageGen closure."""
    base_pixels = np.asarray(base, dtype=np.float32) / 255.0
    term_pixels = np.asarray(terminal, dtype=np.float32) / 255.0
    base_alpha = base_pixels[..., 3]
    term_alpha = term_pixels[..., 3]
    base_premul = base_pixels[..., :3] * base_alpha[..., None]
    term_premul = term_pixels[..., :3] * term_alpha[..., None]

    weights = np.ones(base.height, dtype=np.float32)
    weights[:transition] = np.linspace(0.0, 1.0, transition, dtype=np.float32)
    weight = weights[:, None]
    premul = base_premul * (1.0 - weight[..., None]) + term_premul * weight[..., None]
    alpha = base_alpha * (1.0 - weight) + term_alpha * weight

    rgb = np.zeros_like(premul)
    visible = alpha > (1.0 / 255.0)
    rgb[visible] = premul[visible] / alpha[visible, None]
    result = np.dstack((np.clip(rgb, 0.0, 1.0), np.clip(alpha, 0.0, 1.0)))
    return Image.fromarray(np.rint(result * 255.0).astype(np.uint8), "RGBA")


def save(image: Image.Image, name: str) -> None:
    image.save(ACTIVE / name, format="PNG", optimize=True)


def build() -> None:
    top_source = load(HERE / "cover_top_blocky_raw.png")
    bottom_source = load(HERE / "cover_bottom_cell_raw.png")
    repeat = load(ACTIVE / "cover_cell_repeat.png")

    # 444 px is 5% wider than the active 423 px repeat face. The cap's visible
    # bottom is flush with the 704x320 canvas bottom for direct center alignment.
    top_piece = premultiplied_resize(
        crop(top_source, (115, 164, 1702, 687)), (444, 224)
    )
    top = Image.new("RGBA", (704, 320), (0, 0, 0, 0))
    top.alpha_composite(top_piece, dest=(130, 97))

    # The bottom cap is a complete 640x512 final-cell replacement. Preserve the
    # active repeat verbatim through y=383, then morph its final 128 px into the
    # new ImageGen terminal. The first transition row remains an exact repeat row.
    terminal_piece = premultiplied_resize(
        crop(bottom_source, (261, 1240, 764, 1490)), (423, 128)
    )
    base_lower = repeat.crop((108, 384, 531, 512))
    bottom_piece = blend_to_terminal(base_lower, terminal_piece)
    bottom = Image.new("RGBA", (640, 512), (0, 0, 0, 0))
    bottom.alpha_composite(repeat.crop((0, 0, 640, 384)), dest=(0, 0))
    bottom.alpha_composite(bottom_piece, dest=(108, 384))

    save(top, "cover_top_cap.png")
    save(bottom, "cover_bottom_cap.png")


if __name__ == "__main__":
    build()
