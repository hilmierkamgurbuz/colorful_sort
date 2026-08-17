#!/usr/bin/env python3
"""Normalize two one-piece ImageGen slot renders for Unity.

The active artwork is never assembled from modular top/middle/bottom pieces.
Each final sprite comes from one complete built-in ImageGen render. Processing
is restricted to white/checker matte extraction, alpha edge decontamination,
crop and one premultiplied resize onto the requested transparent canvas.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import (
    binary_erosion,
    binary_fill_holes,
    distance_transform_edt,
    label,
)
from scipy.signal import find_peaks


HERE = Path(__file__).resolve().parent
ACTIVE = HERE.parents[2] / "Gameplay"
ACTIVE.mkdir(parents=True, exist_ok=True)


def largest_component(mask: np.ndarray) -> np.ndarray:
    labels, count = label(mask)
    if count == 0:
        raise RuntimeError("ImageGen subject mask is empty")
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    return labels == int(sizes.argmax())


def extract_light_matte(path: Path) -> Image.Image:
    """Remove ImageGen's light/checker matte without painting new RGB."""
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.uint8)
    # Both raw renders have a near-white neutral matte, while every subject
    # pixel is substantially darker or more chromatic. Threshold 15 cleanly
    # separates the one connected slot render from the backdrop.
    signal = 255 - rgb.min(axis=2).astype(np.int16)
    subject = binary_fill_holes(largest_component(signal > 15))

    # Preserve a one-pixel inward antialias. Boundary RGB is borrowed from the
    # nearest deeper pixel of the same ImageGen render, eliminating white or
    # checker contamination without synthesizing vector edges or new artwork.
    distance_in = distance_transform_edt(subject)
    alpha = np.clip((distance_in - 0.15) / 1.50, 0.0, 1.0)
    deep = binary_erosion(subject, iterations=2)
    if not deep.any():
        deep = subject
    _, nearest = distance_transform_edt(~deep, return_indices=True)
    output_rgb = rgb.copy()
    boundary = subject & (alpha < 0.999)
    output_rgb[boundary] = rgb[nearest[0][boundary], nearest[1][boundary]]
    output_rgb[~subject] = 0

    output_alpha = np.rint(alpha * 255.0).astype(np.uint8)
    rgba = np.dstack((output_rgb, output_alpha))
    rgba[output_alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def premultiplied_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    source = np.asarray(image, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premul = source[..., :3] * alpha[..., None]
    planes: list[np.ndarray] = []
    for plane in (*np.moveaxis(premul, 2, 0), alpha):
        planes.append(
            np.asarray(
                Image.fromarray(plane, mode="F").resize(size, Image.Resampling.LANCZOS),
                dtype=np.float32,
            )
        )
    out_alpha = np.clip(planes[3], 0.0, 1.0)
    out_premul = np.stack(planes[:3], axis=2)
    out_rgb = np.zeros_like(out_premul)
    visible = out_alpha > (2.0 / 255.0)
    out_rgb[visible] = out_premul[visible] / out_alpha[visible, None]
    pixels = np.rint(
        np.dstack((np.clip(out_rgb, 0.0, 1.0), out_alpha)) * 255.0
    ).astype(np.uint8)
    pixels[pixels[..., 3] <= 2] = 0
    return Image.fromarray(pixels, "RGBA")


def normalize_one_piece(
    raw_name: str,
    final_name: str,
    canvas_size: tuple[int, int],
    object_size: tuple[int, int],
    xy: tuple[int, int],
) -> Image.Image:
    extracted = extract_light_matte(HERE / raw_name)
    bbox = extracted.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"No subject in {raw_name}")
    # One crop + one resize of the complete render; no part compositing.
    complete = extracted.crop(bbox)
    complete = premultiplied_resize(complete, object_size)
    result = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    result.alpha_composite(complete, dest=xy)
    result.save(ACTIVE / final_name, format="PNG", optimize=True)
    return result


def preview_full(normal: Image.Image, frozen: Image.Image) -> None:
    sheet = Image.new("RGBA", (1328, 2048), (19, 15, 53, 255))
    sheet.alpha_composite(normal, dest=(16, 192))
    sheet.alpha_composite(frozen, dest=(672, 32))
    sheet.save(HERE / "SlotComplete2Cell_FullPreview.png", format="PNG", optimize=True)


def preview_phone(normal: Image.Image, frozen: Image.Image) -> None:
    normal_small = premultiplied_resize(normal, (64, 166))
    frozen_small = premultiplied_resize(frozen, (64, 198))
    sheet = Image.new("RGBA", (224, 230), (19, 15, 53, 255))
    sheet.alpha_composite(normal_small, dest=(40, 32))
    sheet.alpha_composite(frozen_small, dest=(120, 16))
    sheet.save(HERE / "SlotComplete2Cell_PhonePreview.png", format="PNG", optimize=True)


def groove_score(image: Image.Image) -> np.ndarray:
    array = np.asarray(image.convert("RGB"), dtype=np.float32)
    center = array[:, 190:450].mean(axis=(1, 2))
    from scipy.ndimage import gaussian_filter1d

    return gaussian_filter1d(center, 10.0) - gaussian_filter1d(center, 1.0)


def detect_grooves(image: Image.Image, y_min: int, y_max: int) -> list[float]:
    # Groove darkness relative to a vertically smoothed cavity profile.
    score = groove_score(image)
    peaks, properties = find_peaks(
        score[y_min:y_max], distance=300, prominence=0.35
    )
    candidates = sorted(
        [
            (float(score[y_min + peak]), int(y_min + peak))
            for peak in peaks
        ],
        reverse=True,
    )
    centers: list[float] = []
    for _, y in candidates[:2]:
        # Subpixel parabolic peak center for stable 512 px normalization.
        left, middle, right = score[y - 1], score[y], score[y + 1]
        denominator = left - 2.0 * middle + right
        offset = 0.0 if abs(float(denominator)) < 1e-8 else 0.5 * (left - right) / denominator
        centers.append(float(y + np.clip(offset, -0.5, 0.5)))
    return sorted(centers)


def cubic_hermite(
    x: np.ndarray,
    x0: float,
    x1: float,
    y0: float,
    y1: float,
    slope0: float = 1.0,
    slope1: float = 1.0,
) -> np.ndarray:
    span = x1 - x0
    t = (x - x0) / span
    t2 = t * t
    t3 = t2 * t
    h00 = 2.0 * t3 - 3.0 * t2 + 1.0
    h10 = t3 - 2.0 * t2 + t
    h01 = -2.0 * t3 + 3.0 * t2
    h11 = t3 - t2
    return h00 * y0 + h10 * span * slope0 + h01 * y1 + h11 * span * slope1


def warp_complete_render(
    image: Image.Image,
    first_source: float,
    second_source: float,
    first_target: float,
    crown_knot: float = 320.0,
) -> Image.Image:
    """C1-smooth vertical normalization of one complete ImageGen render."""
    height, width = image.height, image.width
    samples_per_pixel = 8
    source_axis = np.linspace(
        0.0, height - 1.0, (height - 1) * samples_per_pixel + 1, dtype=np.float64
    )
    destination_axis = source_axis.copy()
    first_segment = (source_axis > crown_knot) & (source_axis < first_source)
    second_segment = (source_axis >= first_source) & (source_axis < second_source)
    destination_axis[first_segment] = cubic_hermite(
        source_axis[first_segment], crown_knot, first_source, crown_knot, first_target
    )
    destination_axis[second_segment] = cubic_hermite(
        source_axis[second_segment], first_source, second_source, first_target, second_source
    )
    if np.any(np.diff(destination_axis) <= 0.0):
        raise RuntimeError("Non-monotonic groove normalization map")

    destination_rows = np.arange(height, dtype=np.float64)
    source_rows = np.interp(destination_rows, destination_axis, source_axis)
    # These protected ranges remain exactly byte-identical.
    source_rows[destination_rows <= crown_knot] = destination_rows[destination_rows <= crown_knot]
    bottom_start = int(np.ceil(second_source))
    source_rows[bottom_start:] = destination_rows[bottom_start:]

    rgba = np.asarray(image, dtype=np.float32) / 255.0
    alpha = rgba[..., 3]
    premul = rgba[..., :3] * alpha[..., None]
    y0 = np.floor(source_rows).astype(np.int32)
    y1 = np.minimum(y0 + 1, height - 1)
    fraction = (source_rows - y0).astype(np.float32)
    warped_alpha = alpha[y0] * (1.0 - fraction[:, None]) + alpha[y1] * fraction[:, None]
    warped_premul = (
        premul[y0] * (1.0 - fraction[:, None, None])
        + premul[y1] * fraction[:, None, None]
    )
    warped_rgb = np.zeros_like(warped_premul)
    visible = warped_alpha > (2.0 / 255.0)
    warped_rgb[visible] = warped_premul[visible] / warped_alpha[visible, None]
    pixels = np.rint(
        np.dstack((np.clip(warped_rgb, 0.0, 1.0), np.clip(warped_alpha, 0.0, 1.0)))
        * 255.0
    ).astype(np.uint8)
    pixels[pixels[..., 3] <= 2] = 0
    return Image.fromarray(pixels, "RGBA")


def normalize_groove_spacing(
    image: Image.Image,
    y_min: int,
    y_max: int,
    target_spacing: float = 512.0,
) -> tuple[Image.Image, dict[str, float | list[float] | int]]:
    before = detect_grooves(image, y_min, y_max)
    if len(before) != 2:
        raise RuntimeError(f"Expected two grooves, got {before}")
    first_source, second_source = before
    first_target = second_source - target_spacing
    warped = image
    after = before
    # Correct the tiny detector/resampling residual while always warping from
    # the same original complete render. Eight fixed iterations are deterministic.
    for _ in range(8):
        warped = warp_complete_render(
            image, first_source, second_source, first_target, crown_knot=320.0
        )
        after = detect_grooves(warped, y_min, y_max)
        spacing_error = (after[1] - after[0]) - target_spacing
        first_target += spacing_error
    warped = warp_complete_render(
        image, first_source, second_source, first_target, crown_knot=320.0
    )
    after = detect_grooves(warped, y_min, y_max)
    return warped, {
        "groove_centers_before": before,
        "groove_centers_after": after,
        "groove_spacing_before": float(before[1] - before[0]),
        "groove_spacing_after": float(after[1] - after[0]),
        "target_spacing": target_spacing,
        "crown_protected_through_y": 320,
        "bottom_protected_from_y": int(np.ceil(second_source)),
        "first_offset_in_center": float(after[0] - 320.0),
        "second_offset_in_bottom": float(after[1] - 832.0),
        "offset_mismatch": float((after[0] - 320.0) - (after[1] - 832.0)),
    }


def continuity_metrics(
    image: Image.Image, y_min: int, y_max: int, grooves: list[float]
) -> dict[str, float | int | list[int]]:
    rgba = np.asarray(image, dtype=np.float32)
    alpha = rgba[..., 3]
    luma = rgba[..., :3].mean(axis=2)
    rows = np.arange(y_min, y_max)
    center_luma = luma[rows, 320]

    left_centers: list[float] = []
    right_centers: list[float] = []
    missing = 0
    for index, y in enumerate(rows):
        left_x = np.arange(110, 190)
        right_x = np.arange(450, 530)
        left_w = np.clip(luma[y, 110:190] - center_luma[index], 0.0, None)
        right_w = np.clip(luma[y, 450:530] - center_luma[index], 0.0, None)
        if left_w.sum() <= 1.0 or right_w.sum() <= 1.0:
            missing += 1
            continue
        left_centers.append(float((left_x * left_w).sum() / left_w.sum()))
        right_centers.append(float((right_x * right_w).sum() / right_w.sum()))

    # Center-surface row differences, excluding the two intentional grooves.
    center_rgb = rgba[y_min:y_max, 300:340, :3].mean(axis=1)
    row_delta = np.abs(np.diff(center_rgb, axis=0)).max(axis=1)
    keep = np.ones_like(row_delta, dtype=bool)
    for groove in grooves:
        local = int(round(groove - y_min))
        keep[max(0, local - 20) : min(len(keep), local + 20)] = False
    filtered = row_delta[keep]

    left = np.asarray(left_centers)
    right = np.asarray(right_centers)
    rail_keep = np.ones(len(left), dtype=bool)
    for groove in grooves:
        local = int(round(groove - y_min))
        rail_keep[max(0, local - 20) : min(len(rail_keep), local + 20)] = False
    step_keep = rail_keep[:-1] & rail_keep[1:]
    left_kept = left[rail_keep]
    right_kept = right[rail_keep]
    left_steps = np.abs(np.diff(left))[step_keep]
    right_steps = np.abs(np.diff(right))[step_keep]
    return {
        "groove_centers_y": grooves,
        "rail_rows_checked": int(len(rows)),
        "rail_missing_rows": int(missing),
        "left_highlight_center_mean": float(left_kept.mean()),
        "left_highlight_center_range": float(left_kept.max() - left_kept.min()),
        "left_highlight_p99_adjacent_step": float(np.percentile(left_steps, 99)),
        "left_highlight_max_adjacent_step": float(left_steps.max(initial=0.0)),
        "right_highlight_center_mean": float(right_kept.mean()),
        "right_highlight_center_range": float(right_kept.max() - right_kept.min()),
        "right_highlight_p99_adjacent_step": float(np.percentile(right_steps, 99)),
        "right_highlight_max_adjacent_step": float(right_steps.max(initial=0.0)),
        "cavity_non_groove_row_delta_p95": float(np.percentile(filtered, 95)),
        "cavity_non_groove_row_delta_max": float(filtered.max(initial=0.0)),
    }


def image_qa(image: Image.Image) -> dict[str, int | list[int] | str]:
    array = np.asarray(image)
    alpha = array[..., 3]
    partial = (alpha > 0) & (alpha < 255)
    chroma = array[..., :3].max(axis=2) - array[..., :3].min(axis=2)
    near_white_neutral = partial & (array[..., :3].min(axis=2) > 220) & (chroma < 20)
    corners = [
        int(alpha[0, 0]),
        int(alpha[0, -1]),
        int(alpha[-1, 0]),
        int(alpha[-1, -1]),
    ]
    return {
        "mode": image.mode,
        "width": image.width,
        "height": image.height,
        "alpha_min": int(alpha.min()),
        "alpha_max": int(alpha.max()),
        "corner_alpha": corners,
        "hidden_rgb_max": int(array[alpha == 0, :3].max(initial=0)),
        "partial_alpha_neutral_white_pixels": int(near_white_neutral.sum()),
    }


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build() -> None:
    normal_before_warp = normalize_one_piece(
        "slot_complete_2cell_raw.png",
        "slot_complete_2cell.png",
        (640, 1664),
        (420, 1634),
        (110, 15),
    )
    frozen_before_warp = normalize_one_piece(
        "slot_ice_complete_2cell_raw.png",
        "slot_ice_complete_2cell.png",
        (640, 1984),
        (420, 1861),
        (110, 6),
    )

    normal, normal_normalization = normalize_groove_spacing(
        normal_before_warp, 180, 1250
    )
    frozen, frozen_normalization = normalize_groove_spacing(
        frozen_before_warp, 180, 1300
    )
    normal.save(ACTIVE / "slot_complete_2cell.png", format="PNG", optimize=True)
    frozen.save(ACTIVE / "slot_ice_complete_2cell.png", format="PNG", optimize=True)
    preview_full(normal, frozen)
    preview_phone(normal, frozen)

    normal_grooves = detect_grooves(normal, 180, 1250)
    frozen_grooves = detect_grooves(frozen, 180, 1300)

    def protected_delta(
        before: Image.Image, after: Image.Image, start: int, end: int
    ) -> int:
        a = np.asarray(before, dtype=np.int16)[start:end]
        b = np.asarray(after, dtype=np.int16)[start:end]
        return int(np.abs(a - b).max(initial=0))

    normal_bottom_start = int(normal_normalization["bottom_protected_from_y"])
    frozen_bottom_start = int(frozen_normalization["bottom_protected_from_y"])
    report = {
        "normal": {
            **image_qa(normal),
            **continuity_metrics(normal, 120, 1370, normal_grooves),
            **normal_normalization,
            "crown_protected_rgba_max_delta": protected_delta(
                normal_before_warp, normal, 0, 321
            ),
            "bottom_protected_rgba_max_delta": protected_delta(
                normal_before_warp, normal, normal_bottom_start, normal.height
            ),
            "sha256": sha256(ACTIVE / "slot_complete_2cell.png"),
        },
        "frozen": {
            **image_qa(frozen),
            **continuity_metrics(frozen, 110, 1380, frozen_grooves),
            **frozen_normalization,
            "crown_protected_rgba_max_delta": protected_delta(
                frozen_before_warp, frozen, 0, 321
            ),
            "bottom_protected_rgba_max_delta": protected_delta(
                frozen_before_warp, frozen, frozen_bottom_start, frozen.height
            ),
            "sha256": sha256(ACTIVE / "slot_ice_complete_2cell.png"),
        },
        "unity": {
            "pixels_per_unit": 512,
            "suggested_pivot": [0.5, 0.0],
            "sprite_mode": "Single",
            "mesh_type": "Full Rect",
            "sRGB": True,
            "alpha_is_transparency": True,
            "tiled_center_top_down": [160, 320, 320, 512],
            "normal_tiled_border_lbrt": [160, 832, 160, 320],
            "frozen_tiled_border_lbrt": [160, 1152, 160, 320],
        },
    }
    (HERE / "SlotComplete2Cell_QA.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    build()
