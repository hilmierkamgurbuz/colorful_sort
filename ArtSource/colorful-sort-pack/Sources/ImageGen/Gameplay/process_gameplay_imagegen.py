#!/usr/bin/env python3
"""Crop and normalize built-in ImageGen gameplay sprite sources.

Every visible RGB pixel in the exported sprites is sampled from the raw ImageGen
renders. Processing is limited to crop, alpha cleanup, premultiplied resize,
mirroring for a seamless repeat, and transparent-canvas placement.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageOps


HERE = Path(__file__).resolve().parent
OUT = HERE.parents[2] / "Gameplay"
OUT.mkdir(parents=True, exist_ok=True)

RGBA = tuple[int, int, int, int]


def load(name: str) -> Image.Image:
    return Image.open(HERE / name).convert("RGBA")


def clean_alpha(image: Image.Image, cutoff: int = 10) -> Image.Image:
    """Remove near-transparent backdrop residue without painting RGB artwork."""
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
    """Lanczos resize in premultiplied-alpha space to avoid dark/white halos."""
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premultiplied = source[..., :3] * alpha[..., None]

    resized_channels: list[np.ndarray] = []
    for channel in (*np.moveaxis(premultiplied, 2, 0), alpha):
        plane = Image.fromarray(channel.astype(np.float32), mode="F")
        resized_channels.append(
            np.asarray(plane.resize(size, Image.Resampling.LANCZOS), dtype=np.float32)
        )

    alpha_out = np.clip(resized_channels[3], 0.0, 1.0)
    premul_out = np.stack(resized_channels[:3], axis=2)
    rgb_out = np.zeros_like(premul_out)
    visible = alpha_out > (1.0 / 255.0)
    rgb_out[visible] = premul_out[visible] / alpha_out[visible, None]

    result = np.dstack((np.clip(rgb_out, 0.0, 1.0), alpha_out))
    return Image.fromarray(np.rint(result * 255.0).astype(np.uint8), "RGBA")


def transparent(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, (0, 0, 0, 0))


def place(
    source: Image.Image,
    source_box: tuple[int, int, int, int],
    canvas_size: tuple[int, int],
    visible_size: tuple[int, int],
    xy: tuple[int, int],
) -> Image.Image:
    piece = premultiplied_resize(crop(source, source_box), visible_size)
    canvas = transparent(canvas_size)
    canvas.alpha_composite(piece, dest=xy)
    return canvas


def repeat_piece(
    source: Image.Image,
    source_box: tuple[int, int, int, int],
    canvas_size: tuple[int, int],
    visible_width: int,
    x: int,
) -> Image.Image:
    """Create a vertically periodic tile using only mirrored ImageGen samples."""
    half_height = canvas_size[1] // 2
    half = premultiplied_resize(crop(source, source_box), (visible_width, half_height))
    periodic = transparent((visible_width, canvas_size[1]))
    periodic.alpha_composite(half, dest=(0, 0))
    periodic.alpha_composite(ImageOps.flip(half), dest=(0, half_height))
    canvas = transparent(canvas_size)
    canvas.alpha_composite(periodic, dest=(x, 0))
    return canvas


def save(image: Image.Image, name: str) -> None:
    image.save(OUT / name, format="PNG", optimize=True)


def build_slot_family() -> None:
    source = load("slot_family_raw.png")

    # Common visible width and center keep all stack joins aligned at x=320.
    save(
        place(source, (309, 8, 754, 222), (640, 320), (536, 288), (52, 32)),
        "slot_top.png",
    )
    save(
        repeat_piece(source, (319, 320, 743, 700), (640, 512), 536, 52),
        "slot_cell_repeat.png",
    )
    save(
        place(source, (307, 818, 756, 973), (640, 320), (536, 185), (52, 0)),
        "slot_bottom.png",
    )
    save(
        place(source, (837, 855, 1257, 966), (768, 256), (664, 175), (52, 40)),
        "slot_shadow.png",
    )


def build_ice_family() -> None:
    source = load("ice_family_raw.png")
    crystals = load("ice_crystals_raw.png")
    save(
        place(source, (23, 21, 1514, 346), (704, 320), (612, 222), (46, 49)),
        "ice_frost_band.png",
    )
    save(
        place(crystals, (131, 49, 422, 750), (256, 448), (191, 393), (22, 35)),
        "ice_crystal_left.png",
    )
    save(
        place(crystals, (625, 28, 918, 988), (256, 512), (186, 450), (35, 40)),
        "ice_crystal_center.png",
    )
    save(
        place(crystals, (1137, 50, 1414, 734), (256, 448), (191, 393), (43, 35)),
        "ice_crystal_right.png",
    )


def build_cover_family() -> None:
    source = load("cover_family_raw.png")
    save(
        place(source, (106, 49, 916, 273), (704, 320), (598, 197), (53, 54)),
        "cover_top_cap.png",
    )
    save(
        repeat_piece(source, (128, 420, 897, 980), (640, 512), 423, 108),
        "cover_cell_repeat.png",
    )
    save(
        place(source, (123, 1363, 902, 1486), (640, 160), (436, 103), (102, 28)),
        "cover_separator.png",
    )


def build_mystery_family() -> None:
    source = load("mystery_family_raw.png")
    save(
        place(source, (101, 116, 864, 884), (640, 640), (544, 544), (48, 48)),
        "mystery_face_overlay.png",
    )
    save(
        place(source, (1021, 170, 1452, 841), (640, 640), (288, 448), (176, 96)),
        "question_mark_decal.png",
    )


def main() -> None:
    build_slot_family()
    build_ice_family()
    build_cover_family()
    build_mystery_family()


if __name__ == "__main__":
    main()
