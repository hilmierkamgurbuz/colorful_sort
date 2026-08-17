#!/usr/bin/env python3
"""Build the active two-lobe normal and integrated-ice slot sprites.

All visible RGB comes from built-in ImageGen source PNGs. The processor only
crops, cleans alpha, resizes in premultiplied space, composites ImageGen layers,
and blends ImageGen edge samples to make vertical module boundaries seamless.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageOps


HERE = Path(__file__).resolve().parent
ACTIVE = HERE.parents[2] / "Gameplay"
ACTIVE.mkdir(parents=True, exist_ok=True)


def load(name: str) -> Image.Image:
    return Image.open(HERE / name).convert("RGBA")


def clean_alpha(image: Image.Image, cutoff: int = 10) -> Image.Image:
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[..., 3].astype(np.int32)
    remapped = np.where(
        alpha <= cutoff,
        0,
        np.clip((alpha - cutoff) * 255 // (254 - cutoff), 0, 255),
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


def to_premultiplied(image: Image.Image) -> tuple[np.ndarray, np.ndarray]:
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    return source[..., :3] * alpha[..., None], alpha


def from_premultiplied(premul: np.ndarray, alpha: np.ndarray) -> Image.Image:
    rgb = np.zeros_like(premul)
    visible = alpha > (1.0 / 255.0)
    rgb[visible] = premul[visible] / alpha[visible, None]
    result = np.dstack((np.clip(rgb, 0.0, 1.0), np.clip(alpha, 0.0, 1.0)))
    return Image.fromarray(np.rint(result * 255.0).astype(np.uint8), "RGBA")


def make_repeatable_y(image: Image.Image, band: int = 24) -> Image.Image:
    """Blend only ImageGen edge samples so first and last RGBA rows are equal."""
    premul, alpha = to_premultiplied(image)
    seam_p = (premul[0] + premul[-1]) * 0.5
    seam_a = (alpha[0] + alpha[-1]) * 0.5
    height = image.height
    for y in range(band):
        weight = 1.0 - y / (band - 1)
        premul[y] = premul[y] * (1.0 - weight) + seam_p * weight
        alpha[y] = alpha[y] * (1.0 - weight) + seam_a * weight
    for index, y in enumerate(range(height - band, height)):
        weight = index / (band - 1)
        premul[y] = premul[y] * (1.0 - weight) + seam_p * weight
        alpha[y] = alpha[y] * (1.0 - weight) + seam_a * weight
    return from_premultiplied(premul, alpha)


def periodic_from_strip(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Create a band-free vertical period from a groove-free ImageGen strip."""
    width, height = size
    if height % 2:
        raise ValueError("Periodic target height must be even")
    half = premultiplied_resize(image, (width, height // 2))
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(half, dest=(0, 0))
    result.alpha_composite(ImageOps.flip(half), dest=(0, height // 2))
    return result


def feather_y(image: Image.Image, feather: int = 24) -> Image.Image:
    """Fade an ImageGen band vertically so it can add one centered groove."""
    pixels = np.asarray(image, dtype=np.uint8).copy()
    ramp = np.ones(image.height, dtype=np.float32)
    ramp[:feather] = np.linspace(0.0, 1.0, feather, dtype=np.float32)
    ramp[-feather:] = np.linspace(1.0, 0.0, feather, dtype=np.float32)
    pixels[..., 3] = np.rint(
        pixels[..., 3].astype(np.float32) * ramp[:, None]
    ).astype(np.uint8)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def match_edge(
    image: Image.Image,
    reference_row: np.ndarray,
    edge: str,
    band: int = 8,
) -> Image.Image:
    """Feather a top/bottom join to the repeat-cell seam using ImageGen pixels."""
    premul, alpha = to_premultiplied(image)
    ref = reference_row.astype(np.float32) / 255.0
    ref_alpha = ref[..., 3]
    ref_premul = ref[..., :3] * ref_alpha[..., None]
    if edge == "top":
        rows = range(band)
        weights = [1.0 - y / (band - 1) for y in rows]
    elif edge == "bottom":
        rows = range(image.height - band, image.height)
        weights = [i / (band - 1) for i in range(band)]
    else:
        raise ValueError(f"Unsupported edge: {edge}")
    for y, weight in zip(rows, weights):
        premul[y] = premul[y] * (1.0 - weight) + ref_premul * weight
        alpha[y] = alpha[y] * (1.0 - weight) + ref_alpha * weight
    return from_premultiplied(premul, alpha)


def attenuate(image: Image.Image, factor: float) -> Image.Image:
    pixels = np.asarray(image, dtype=np.uint8).copy()
    pixels[..., 3] = np.rint(pixels[..., 3].astype(np.float32) * factor).astype(np.uint8)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def vertical_alpha_ramp(
    image: Image.Image,
    start_y: int,
    end_y: int,
    start_factor: float,
    end_factor: float,
) -> Image.Image:
    """Fade ImageGen cyan rail pixels toward the physical bottom ice."""
    pixels = np.asarray(image, dtype=np.uint8).copy()
    factors = np.ones(image.height, dtype=np.float32)
    factors[:start_y] = start_factor
    factors[start_y:end_y] = np.linspace(
        start_factor, end_factor, end_y - start_y, dtype=np.float32
    )
    pixels[..., 3] = np.rint(
        pixels[..., 3].astype(np.float32) * factors[:, None]
    ).astype(np.uint8)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def remove_center(image: Image.Image, left: int, right: int) -> Image.Image:
    """Keep only ImageGen rail pixels; remove the source's oversized center groove."""
    pixels = np.asarray(image, dtype=np.uint8).copy()
    pixels[:, left:right] = 0
    return Image.fromarray(pixels, "RGBA")


def composite(base: Image.Image, overlay: Image.Image) -> Image.Image:
    result = base.copy()
    result.alpha_composite(overlay)
    return result


def canvas(piece: Image.Image, size: tuple[int, int], xy: tuple[int, int]) -> Image.Image:
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(piece, dest=xy)
    return result


def save(image: Image.Image, name: str) -> None:
    image.save(ACTIVE / name, format="PNG", optimize=True)


def build() -> None:
    normal_source = load("slot_two_lobe_raw.png")
    frozen_source = load("slot_ice_integrated_raw.png")

    # The visible slot body is 420 px wide, centered in every 640 px canvas.
    normal_top_frame = premultiplied_resize(
        crop(normal_source, (359, 40, 668, 404)), (420, 300)
    )
    full_normal_middle = premultiplied_resize(
        crop(normal_source, (359, 464, 667, 1200)), (420, 512)
    )
    normal_bottom_frame = premultiplied_resize(
        crop(normal_source, (355, 1265, 671, 1486)), (420, 220)
    )
    groove_free_channel = crop(normal_source, (359, 520, 667, 720))
    normal_middle = periodic_from_strip(groove_free_channel, (420, 512))
    normal_middle.alpha_composite(
        feather_y(full_normal_middle.crop((0, 192, 420, 320))), dest=(0, 192)
    )

    normal_top = Image.new("RGBA", (420, 300), (0, 0, 0, 0))
    normal_top.alpha_composite(
        premultiplied_resize(groove_free_channel, (420, 220)), dest=(0, 80)
    )
    normal_top.alpha_composite(normal_top_frame)

    normal_bottom = Image.new("RGBA", (420, 220), (0, 0, 0, 0))
    normal_bottom.alpha_composite(
        premultiplied_resize(groove_free_channel, (420, 185)), dest=(0, 0)
    )
    normal_bottom.alpha_composite(normal_bottom_frame)
    middle_pixels = np.asarray(normal_middle)
    normal_top = match_edge(normal_top, middle_pixels[0], "bottom")
    normal_bottom = match_edge(normal_bottom, middle_pixels[-1], "top")

    save(canvas(normal_top, (640, 320), (110, 20)), "slot_top.png")
    save(canvas(normal_middle, (640, 512), (110, 0)), "slot_cell_repeat.png")
    save(canvas(normal_bottom, (640, 320), (110, 0)), "slot_bottom.png")

    # Frozen top/middle reuse the opaque normal ImageGen cavity and add only a
    # restrained rail tint from the frozen ImageGen render. Physical ice remains
    # exclusively in the frozen bottom asset.
    frozen_top_overlay = attenuate(
        premultiplied_resize(crop(frozen_source, (349, 16, 677, 392)), (420, 300)),
        0.35,
    )
    frozen_top = composite(normal_top, frozen_top_overlay)

    frozen_middle_overlay = periodic_from_strip(
        crop(frozen_source, (346, 500, 679, 650)), (420, 512)
    )
    frozen_middle_overlay = attenuate(remove_center(frozen_middle_overlay, 60, 360), 0.35)
    frozen_middle = composite(normal_middle, frozen_middle_overlay)
    frozen_middle_pixels = np.asarray(frozen_middle)
    frozen_top = match_edge(frozen_top, frozen_middle_pixels[0], "bottom")

    # The frozen source intentionally has a transparent cavity. Fill only its
    # upper rail section with a groove-free crop from the normal ImageGen cavity;
    # the opaque frozen shelf and three icicles cover the lower section.
    bottom_fill = premultiplied_resize(
        crop(normal_source, (359, 520, 667, 720)), (420, 320)
    )
    frozen_bottom = Image.new("RGBA", (420, 600), (0, 0, 0, 0))
    frozen_bottom.alpha_composite(bottom_fill, dest=(0, 0))
    frozen_bottom_overlay = premultiplied_resize(
        crop(frozen_source, (346, 1000, 679, 1505)), (420, 600)
    )
    frozen_bottom_overlay = vertical_alpha_ramp(
        frozen_bottom_overlay, 0, 330, 0.35, 1.0
    )
    frozen_bottom.alpha_composite(frozen_bottom_overlay)
    frozen_bottom = match_edge(frozen_bottom, frozen_middle_pixels[-1], "top")

    save(canvas(frozen_top, (640, 320), (110, 20)), "slot_ice_top.png")
    save(canvas(frozen_middle, (640, 512), (110, 0)), "slot_ice_middle.png")
    save(canvas(frozen_bottom, (640, 640), (110, 0)), "slot_ice_bottom.png")


if __name__ == "__main__":
    build()
