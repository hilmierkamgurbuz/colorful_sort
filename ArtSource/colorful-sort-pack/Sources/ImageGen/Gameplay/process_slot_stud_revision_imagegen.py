#!/usr/bin/env python3
"""Build the shallow-crown / four-stud slot revision from ImageGen rasters.

No vector or procedural artwork is drawn here.  Every visible gameplay pixel is
sampled from a built-in ImageGen PNG.  Processing is limited to alpha cleanup,
crop, premultiplied resampling, spatial normalization and edge blending.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt, gaussian_filter1d, map_coordinates


HERE = Path(__file__).resolve().parent
ACTIVE = HERE.parents[2] / "Gameplay"
ACTIVE.mkdir(parents=True, exist_ok=True)

CANVAS_W = 640
PIECE_X = 110
PIECE_W = 420


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def premultiplied_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premul = source[..., :3] * alpha[..., None]
    output: list[np.ndarray] = []
    for plane in (*np.moveaxis(premul, 2, 0), alpha):
        output.append(
            np.asarray(
                Image.fromarray(plane, mode="F").resize(
                    size, Image.Resampling.LANCZOS
                ),
                dtype=np.float32,
            )
        )
    out_alpha = np.clip(output[3], 0.0, 1.0)
    out_premul = np.stack(output[:3], axis=2)
    out_rgb = np.zeros_like(out_premul)
    visible = out_alpha > (1.0 / 255.0)
    out_rgb[visible] = out_premul[visible] / out_alpha[visible, None]
    return from_float_rgba(np.dstack((out_rgb, out_alpha)))


def from_float_rgba(array: np.ndarray) -> Image.Image:
    array = np.clip(array, 0.0, 1.0)
    pixels = np.rint(array * 255.0).astype(np.uint8)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def sample_rgba(source: Image.Image, sx: np.ndarray, sy: np.ndarray) -> Image.Image:
    """Bilinearly sample an ImageGen RGBA source in premultiplied space."""
    rgba = np.asarray(source, dtype=np.float32) / 255.0
    alpha = rgba[..., 3]
    premul = rgba[..., :3] * alpha[..., None]
    coordinates = np.stack((sy, sx), axis=0)
    sampled_premul = np.stack(
        [
            map_coordinates(
                premul[..., channel], coordinates, order=1, mode="constant", cval=0.0
            )
            for channel in range(3)
        ],
        axis=2,
    )
    sampled_alpha = map_coordinates(
        alpha, coordinates, order=1, mode="constant", cval=0.0
    )
    sampled_rgb = np.zeros_like(sampled_premul)
    visible = sampled_alpha > (1.0 / 255.0)
    sampled_rgb[visible] = sampled_premul[visible] / sampled_alpha[visible, None]
    return from_float_rgba(np.dstack((sampled_rgb, sampled_alpha)))


def normalize_alpha(image: Image.Image, cutoff: int = 5) -> Image.Image:
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[..., 3].astype(np.int32)
    alpha = np.where(
        alpha <= cutoff,
        0,
        np.clip((alpha - cutoff) * 255 // (254 - cutoff), 0, 255),
    ).astype(np.uint8)
    pixels[..., 3] = alpha
    pixels[alpha == 0, :3] = 0
    return Image.fromarray(pixels, "RGBA")


def place(piece: Image.Image, size: tuple[int, int], xy: tuple[int, int]) -> Image.Image:
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(piece, dest=xy)
    return result


def graft_reference_strip(
    image: Image.Image,
    reference: Image.Image,
    edge: str,
    band: int,
    blend: int = 32,
) -> Image.Image:
    """Graft a real ImageGen repeat strip so a join is tonal as well as exact."""
    rgba = np.asarray(image, dtype=np.float32) / 255.0
    alpha = rgba[..., 3]
    premul = rgba[..., :3] * alpha[..., None]
    reference_rgba = np.asarray(reference, dtype=np.float32) / 255.0
    if edge == "top":
        rows = list(range(band))
        ref = reference_rgba[:band]
        weights = np.ones(band, dtype=np.float32)
        weights[-blend:] = np.linspace(1.0, 0.0, blend, dtype=np.float32)
    elif edge == "bottom":
        rows = list(range(image.height - band, image.height))
        ref = reference_rgba[-band:]
        weights = np.ones(band, dtype=np.float32)
        weights[:blend] = np.linspace(0.0, 1.0, blend, dtype=np.float32)
    else:
        raise ValueError(edge)
    ref_a = ref[..., 3]
    ref_p = ref[..., :3] * ref_a[..., None]
    for index, (y, weight) in enumerate(zip(rows, weights)):
        premul[y] = premul[y] * (1.0 - weight) + ref_p[index] * weight
        alpha[y] = alpha[y] * (1.0 - weight) + ref_a[index] * weight
    rgb = np.zeros_like(premul)
    visible = alpha > (1.0 / 255.0)
    rgb[visible] = premul[visible] / alpha[visible, None]
    return from_float_rgba(np.dstack((rgb, alpha)))


def premultiplied_mix(a: Image.Image, b: Image.Image, weight: np.ndarray) -> Image.Image:
    """Mix two ImageGen raster layers without introducing synthetic RGB."""
    aa = np.asarray(a, dtype=np.float32) / 255.0
    bb = np.asarray(b, dtype=np.float32) / 255.0
    a_alpha = aa[..., 3]
    b_alpha = bb[..., 3]
    a_premul = aa[..., :3] * a_alpha[..., None]
    b_premul = bb[..., :3] * b_alpha[..., None]
    w = np.asarray(weight, dtype=np.float32)
    out_alpha = a_alpha * (1.0 - w) + b_alpha * w
    out_premul = a_premul * (1.0 - w[..., None]) + b_premul * w[..., None]
    out_rgb = np.zeros_like(out_premul)
    visible = out_alpha > (1.0 / 255.0)
    out_rgb[visible] = out_premul[visible] / out_alpha[visible, None]
    return from_float_rgba(np.dstack((out_rgb, out_alpha)))


def compose_top_with_canonical_middle(
    unique_crown: Image.Image, canonical_middle: Image.Image
) -> Image.Image:
    """Keep only the crown unique; use 160 exact canonical rows below it."""
    # Rows 192..511 are groove-free at the lower end and terminate on the
    # repeat's exact periodic row.  Thus output rows 160..319 are byte-for-byte
    # the canonical middle's rows 352..511.
    canonical_body = canonical_middle.crop((0, 192, CANVAS_W, 512))
    unique_canvas = place(unique_crown, (CANVAS_W, 320), (PIECE_X, 0))
    yy = np.arange(320, dtype=np.float32)
    weight = np.zeros(320, dtype=np.float32)
    weight[128:160] = np.linspace(0.0, 1.0, 32, dtype=np.float32)
    weight[160:] = 1.0
    return premultiplied_mix(
        unique_canvas, canonical_body, np.broadcast_to(weight[:, None], (320, CANVAS_W))
    )


def compose_bottom_with_canonical_middle(
    platform: Image.Image, canonical_middle: Image.Image
) -> Image.Image:
    """Use 128 exact canonical rows before transitioning into the ImageGen seat."""
    canonical_body = canonical_middle.crop((0, 0, CANVAS_W, 320))
    # Retain the stud plate and complete front lip, while fitting the unique
    # seat below a broad canonical connection band.
    compact_seat = premultiplied_resize(platform.crop((0, 40, PIECE_W, 300)), (PIECE_W, 192))
    unique_canvas = place(compact_seat, (CANVAS_W, 320), (PIECE_X, 128))
    yy = np.arange(320, dtype=np.float32)
    weight = np.zeros(320, dtype=np.float32)
    weight[128:176] = np.linspace(0.0, 1.0, 48, dtype=np.float32)
    weight[176:] = 1.0
    return premultiplied_mix(
        canonical_body, unique_canvas, np.broadcast_to(weight[:, None], (320, CANVAS_W))
    )


def shallow_crown(source: Image.Image) -> tuple[Image.Image, dict[str, float]]:
    """Raster-normalize the ImageGen crown without drawing replacement shapes."""
    pixels = np.asarray(source)
    alpha = pixels[..., 3]
    left, right = 73, 952
    source_x = np.arange(left, right, dtype=np.float32)
    profile = np.empty(right - left, dtype=np.float32)
    for index, x in enumerate(range(left, right)):
        ys = np.flatnonzero(alpha[:, x] >= 128)
        profile[index] = float(ys[0]) if len(ys) else np.nan
    valid = np.isfinite(profile)
    profile = np.interp(source_x, source_x[valid], profile[valid])
    profile = gaussian_filter1d(profile, sigma=2.0)

    # Slight horizontal remap puts the two ImageGen peaks at 27% / 73%.
    out_norm = np.linspace(0.0, 1.0, PIECE_W, dtype=np.float32)
    source_norm = np.interp(
        out_norm,
        [0.0, 0.27, 0.50, 0.73, 1.0],
        [0.0, 0.258, 0.50, 0.742, 1.0],
    )
    sx_line = left + source_norm * (right - left - 1)
    p_line = np.interp(sx_line, source_x, profile)

    shoulder_source = float(max(profile[:6].mean(), profile[-6:].mean()))
    rise_source = np.clip(shoulder_source - p_line, 0.0, None)
    max_rise_source = float(rise_source.max())
    center_rise_source = float(rise_source[PIECE_W // 2])

    desired_rise = 60.0  # 60 / 420 = 14.29%
    desired_valley_depth_ratio = 0.54
    desired_center_rise = desired_rise * (1.0 - desired_valley_depth_ratio)
    gamma = np.log(desired_center_rise / desired_rise) / np.log(
        center_rise_source / max_rise_source
    )
    shoulder_y = 80.0
    q_line = shoulder_y - desired_rise * np.power(
        rise_source / max_rise_source, gamma
    )

    yy = np.arange(320, dtype=np.float32)[:, None]
    sx = np.broadcast_to(sx_line[None, :], (320, PIECE_W))
    scale = PIECE_W / float(right - left)
    sy = p_line[None, :] + (yy - q_line[None, :]) / scale
    result = sample_rgba(source, sx, sy)

    metrics = {
        "rise_px": desired_rise,
        "rise_percent": desired_rise / PIECE_W * 100.0,
        "valley_depth_percent_of_rise": desired_valley_depth_ratio * 100.0,
        "peak_left_percent": 27.0,
        "peak_right_percent": 73.0,
        "shoulder_y": shoulder_y,
    }
    return result, metrics


def icy_rim(normal: Image.Image, ice_middle: Image.Image) -> Image.Image:
    """Blend a restrained rim using pixels sampled from ImageGen ice rails."""
    base = np.asarray(normal, dtype=np.float32) / 255.0
    ice = np.asarray(ice_middle, dtype=np.float32) / 255.0
    alpha = base[..., 3]
    inside = alpha > 0.15
    distance = distance_transform_edt(inside)
    yy, xx = np.mgrid[0 : normal.height, 0 : normal.width]

    local_x = np.clip(xx, 0, PIECE_W - 1)
    rail_depth = np.clip(distance * 1.7 + 10.0, 10.0, 38.0).astype(np.int32)
    ice_x = np.where(local_x < PIECE_W // 2, rail_depth, PIECE_W - 1 - rail_depth)
    ice_y = np.clip(
        np.rint(yy * (ice_middle.height - 1) / (normal.height - 1)).astype(np.int32),
        0,
        ice_middle.height - 1,
    )
    sampled = ice[ice_y, ice_x]

    boundary = np.clip((17.0 - distance) / 17.0, 0.0, 1.0)
    crown_or_sides = (yy < 105) | (xx < 52) | (xx >= PIECE_W - 52)
    weight = 0.18 * boundary * crown_or_sides * inside
    output = base.copy()
    output[..., :3] = (
        base[..., :3] * (1.0 - weight[..., None])
        + sampled[..., :3] * weight[..., None]
    )
    output[..., 3] = alpha
    return from_float_rgba(output)


def stud_platform(source: Image.Image, normal_lip_source: Image.Image) -> Image.Image:
    """Crop the smaller, reference-like four-stud seat before frost begins."""
    # The frost glow climbs the outer corners before the center shelf appears.
    # Row 980 is the last fully violet cross-section across the complete lip.
    seat = premultiplied_resize(source.crop((226, 720, 799, 980)), (PIECE_W, 300))

    # Finish the seat with the complete violet lower bevel from the separate
    # normal ImageGen render.  This avoids borrowing any cyan frost pixels.
    lip = premultiplied_resize(
        normal_lip_source.crop((175, 1340, 850, 1452)), (PIECE_W, 72)
    )
    lip_pixels = np.asarray(lip, dtype=np.uint8).copy()
    lip_pixels[:20, :, 3] = np.rint(
        lip_pixels[:20, :, 3].astype(np.float32)
        * np.linspace(0.0, 1.0, 20, dtype=np.float32)[:, None]
    ).astype(np.uint8)
    lip_pixels[lip_pixels[..., 3] == 0, :3] = 0
    seat.alpha_composite(Image.fromarray(lip_pixels, "RGBA"), dest=(0, 228))
    return seat


def ice_fringe(source: Image.Image) -> Image.Image:
    """Normalize the three ImageGen icicles to the measured drop proportions."""
    height = 270
    yy, xx = np.mgrid[0:height, 0:PIECE_W]
    sx = 226.0 + xx * (799.0 - 226.0 - 1.0) / (PIECE_W - 1.0)
    normalized_x = np.abs(xx - (PIECE_W - 1.0) / 2.0) / ((PIECE_W - 1.0) / 2.0)
    # Center drop ~60% of width; side drops ~38%; center/side ~1.58.
    vertical_scale = 0.54 + 0.12 * np.power(normalized_x, 0.8)
    sy = 1004.0 + yy / vertical_scale
    return sample_rgba(source, sx.astype(np.float32), sy.astype(np.float32))


def tint_bottom_rails(piece: Image.Image, ice_middle: Image.Image) -> Image.Image:
    overlay = premultiplied_resize(ice_middle, piece.size)
    base = np.asarray(piece, dtype=np.float32) / 255.0
    icy = np.asarray(overlay, dtype=np.float32) / 255.0
    yy, xx = np.mgrid[0 : piece.height, 0 : piece.width]
    side = (xx < 48) | (xx >= PIECE_W - 48)
    lower = np.clip((yy - 80.0) / 220.0, 0.0, 1.0)
    weight = 0.12 * lower * side * (base[..., 3] > 0.1)
    base[..., :3] = base[..., :3] * (1.0 - weight[..., None]) + icy[..., :3] * weight[..., None]
    return from_float_rgba(base)


def save(image: Image.Image, name: str) -> None:
    pixels = np.asarray(image, dtype=np.uint8).copy()
    pixels[pixels[..., 3] == 0, :3] = 0
    Image.fromarray(pixels, "RGBA").save(ACTIVE / name, format="PNG", optimize=True)


def assemble(top: Image.Image, middle: Image.Image, bottom: Image.Image, count: int) -> Image.Image:
    result = Image.new(
        "RGBA", (CANVAS_W, top.height + middle.height * count + bottom.height), (0, 0, 0, 0)
    )
    result.alpha_composite(top, dest=(0, 0))
    for index in range(count):
        result.alpha_composite(middle, dest=(0, top.height + index * middle.height))
    result.alpha_composite(bottom, dest=(0, top.height + count * middle.height))
    return result


def preview() -> None:
    variants = [
        ("normal-1", 1, False),
        ("normal-6", 6, False),
        ("ice-1", 1, True),
        ("ice-6", 6, True),
    ]
    cards: list[Image.Image] = []
    for _, count, frozen in variants:
        prefix = "slot_ice_" if frozen else "slot_"
        top = load(ACTIVE / f"{prefix}top.png")
        middle_name = "slot_ice_middle.png" if frozen else "slot_cell_repeat.png"
        middle = load(ACTIVE / middle_name)
        bottom = load(ACTIVE / f"{prefix}bottom.png")
        stack = assemble(top, middle, bottom, count).crop((80, 0, 560, top.height + middle.height * count + bottom.height))
        target_h = 1350
        target_w = max(1, round(stack.width * target_h / stack.height))
        stack = premultiplied_resize(stack, (target_w, target_h))
        card = Image.new("RGBA", (330, 1430), (33, 27, 86, 255))
        card.alpha_composite(stack, dest=((330 - target_w) // 2, 40))
        cards.append(card)
    sheet = Image.new("RGBA", (4 * 330 + 5 * 24, 1478), (18, 14, 50, 255))
    for index, card in enumerate(cards):
        sheet.alpha_composite(card, dest=(24 + index * 354, 24))
    sheet.save(HERE / "SlotStudRevisionPreview.png", format="PNG", optimize=True)


def join_zoom_preview() -> None:
    panels: list[Image.Image] = []
    for frozen in (False, True):
        prefix = "slot_ice_" if frozen else "slot_"
        top = load(ACTIVE / f"{prefix}top.png")
        middle_name = "slot_ice_middle.png" if frozen else "slot_cell_repeat.png"
        middle = load(ACTIVE / middle_name)
        bottom = load(ACTIVE / f"{prefix}bottom.png")
        stack = assemble(top, middle, bottom, 1)
        top_join = stack.crop((80, 240, 560, 400))
        bottom_y = top.height + middle.height
        bottom_join = stack.crop((80, bottom_y - 80, 560, bottom_y + 80))
        panels.extend([top_join, bottom_join])
    scale = 2
    panels = [premultiplied_resize(panel, (panel.width * scale, panel.height * scale)) for panel in panels]
    sheet = Image.new("RGBA", (2 * 960 + 3 * 24, 2 * 320 + 3 * 24), (18, 14, 50, 255))
    for index, panel in enumerate(panels):
        x = 24 + (index % 2) * (960 + 24)
        y = 24 + (index // 2) * (320 + 24)
        sheet.alpha_composite(panel, dest=(x, y))
    sheet.save(HERE / "SlotStudRevisionJoinZoom.png", format="PNG", optimize=True)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def qa(metrics: dict[str, float]) -> None:
    normal_top = load(ACTIVE / "slot_top.png")
    normal_mid = load(ACTIVE / "slot_cell_repeat.png")
    normal_bottom = load(ACTIVE / "slot_bottom.png")
    ice_top = load(ACTIVE / "slot_ice_top.png")
    ice_mid = load(ACTIVE / "slot_ice_middle.png")
    ice_bottom = load(ACTIVE / "slot_ice_bottom.png")

    def seam(a: Image.Image, b: Image.Image) -> int:
        aa = np.asarray(a, dtype=np.int16)
        bb = np.asarray(b, dtype=np.int16)
        return int(np.abs(aa[-1] - bb[0]).max())

    print("crown", metrics)
    print("normal seams top/mid mid/self mid/bottom", seam(normal_top, normal_mid), seam(normal_mid, normal_mid), seam(normal_mid, normal_bottom))
    print("ice seams top/mid mid/self mid/bottom", seam(ice_top, ice_mid), seam(ice_mid, ice_mid), seam(ice_mid, ice_bottom))

    def broad_band(label: str, actual: Image.Image, expected: Image.Image) -> None:
        aa = np.asarray(actual, dtype=np.int16)
        bb = np.asarray(expected, dtype=np.int16)
        delta = np.abs(aa - bb)
        grad_a = np.diff(aa.astype(np.float32), axis=0)
        grad_b = np.diff(bb.astype(np.float32), axis=0)
        grad_delta = np.abs(grad_a - grad_b)
        print(
            label,
            "rgba max/mean",
            int(delta.max()),
            f"{float(delta.mean()):.6f}",
            "vertical-gradient max/mean",
            f"{float(grad_delta.max()):.6f}",
            f"{float(grad_delta.mean()):.6f}",
        )

    broad_band("normal top canonical160", normal_top.crop((0, 160, 640, 320)), normal_mid.crop((0, 352, 640, 512)))
    broad_band("ice top canonical160", ice_top.crop((0, 160, 640, 320)), ice_mid.crop((0, 352, 640, 512)))
    broad_band("normal bottom canonical128", normal_bottom.crop((0, 0, 640, 128)), normal_mid.crop((0, 0, 640, 128)))
    broad_band("ice bottom canonical128", ice_bottom.crop((0, 0, 640, 128)), ice_mid.crop((0, 0, 640, 128)))
    for name, image in [
        ("slot_top.png", normal_top),
        ("slot_bottom.png", normal_bottom),
        ("slot_ice_top.png", ice_top),
        ("slot_ice_bottom.png", ice_bottom),
    ]:
        array = np.asarray(image)
        bbox = image.getchannel("A").getbbox()
        hidden_rgb = int(array[array[..., 3] == 0, :3].max(initial=0))
        print(name, image.mode, image.size, "bbox", bbox, "hiddenRGB", hidden_rgb, "sha256", sha256(ACTIVE / name))


def build() -> dict[str, float]:
    crown_source = normalize_alpha(load(HERE / "slot_shallow_crown_raw.png"))
    platform_source = normalize_alpha(load(HERE / "slot_stud_ice_bottom_raw.png"))
    normal_platform_source = normalize_alpha(load(HERE / "slot_stud_bottom_raw.png"))
    normal_middle = load(ACTIVE / "slot_cell_repeat.png")
    ice_middle = load(ACTIVE / "slot_ice_middle.png")

    crown, metrics = shallow_crown(crown_source)
    normal_top = compose_top_with_canonical_middle(crown, normal_middle)

    ice_crown = icy_rim(crown, ice_middle.crop((PIECE_X, 0, PIECE_X + PIECE_W, 512)))
    ice_top = compose_top_with_canonical_middle(ice_crown, ice_middle)

    platform = stud_platform(platform_source, normal_platform_source)
    normal_bottom = compose_bottom_with_canonical_middle(platform, normal_middle)

    frozen_platform = tint_bottom_rails(
        platform, ice_middle.crop((PIECE_X, 0, PIECE_X + PIECE_W, 512))
    )
    frozen_upper = compose_bottom_with_canonical_middle(frozen_platform, ice_middle)
    ice_bottom = Image.new("RGBA", (CANVAS_W, 640), (0, 0, 0, 0))
    ice_bottom.alpha_composite(frozen_upper, dest=(0, 0))
    ice_bottom.alpha_composite(ice_fringe(platform_source), dest=(PIECE_X, 320))

    save(normal_top, "slot_top.png")
    save(ice_top, "slot_ice_top.png")
    save(normal_bottom, "slot_bottom.png")
    save(ice_bottom, "slot_ice_bottom.png")
    return metrics


if __name__ == "__main__":
    crown_metrics = build()
    preview()
    join_zoom_preview()
    qa(crown_metrics)
