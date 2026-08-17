#!/usr/bin/env python3
"""Validate the generated asset pack and build its manifest/contact sheet.

This script never modifies source references. It reads the final PNG exports,
writes AssetManifest.json, qa_report.json, and preview_contact_sheet.png at the
GeneratedAssets root, and exits non-zero when the package contract is broken.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


PACK_ROOT = Path(__file__).resolve().parents[1]
ASSET_GROUPS = ("Backgrounds", "UI", "Gameplay")

EXPECTED_BACKGROUND = {
    "Backgrounds/menu_background_blocks.png": (1440, 2560),
    "Backgrounds/gameplay_background_purple.png": (1440, 2560),
    "Backgrounds/gameplay_background_puzzle_silhouettes.png": (1440, 2560),
    "Backgrounds/gameplay_block_pattern_512.png": (512, 512),
}

EXPECTED_UI = {
    **{f"UI/Buttons/level_button_{state}.png": (1024, 384) for state in ("normal", "pressed", "disabled")},
    **{f"UI/Buttons/square_green_{state}.png": (320, 320) for state in ("normal", "pressed", "disabled")},
    **{
        f"UI/Buttons/wide_{color}_{state}.png": (896, 256)
        for color in ("lavender", "green", "red")
        for state in ("normal", "pressed", "disabled")
    },
    **{f"UI/Buttons/close_shell_{state}.png": (256, 256) for state in ("normal", "pressed", "disabled")},
    "UI/HUD/hud_pill_9slice.png": (640, 192),
    "UI/HUD/coin.png": (192, 192),
    "UI/HUD/heart.png": (192, 192),
    "UI/HUD/plus.png": (160, 160),
    **{f"UI/Settings/settings_shell_{state}.png": (256, 256) for state in ("normal", "pressed", "disabled")},
    "UI/Settings/gear.png": (192, 192),
    "UI/Popups/popup_body_9slice.png": (1024, 1280),
    "UI/Popups/popup_header_9slice.png": (1024, 320),
    **{f"UI/Icons/{name}.png": (192, 192) for name in ("close", "profile", "restart", "sound", "vibration", "off_slash")},
}

EXPECTED_GAMEPLAY = {
    "Gameplay/slot_top.png": (640, 320),
    "Gameplay/slot_cell_repeat.png": (640, 512),
    "Gameplay/slot_bottom.png": (640, 320),
    "Gameplay/slot_ice_top.png": (640, 320),
    "Gameplay/slot_ice_middle.png": (640, 512),
    "Gameplay/slot_ice_bottom.png": (640, 640),
    "Gameplay/slot_complete_2cell.png": (640, 1664),
    "Gameplay/slot_ice_complete_2cell.png": (640, 1984),
    "Gameplay/slot_shadow.png": (768, 256),
    "Gameplay/ice_frost_band.png": (704, 320),
    "Gameplay/ice_crystal_left.png": (256, 448),
    "Gameplay/ice_crystal_center.png": (256, 512),
    "Gameplay/ice_crystal_right.png": (256, 448),
    "Gameplay/cover_top_cap.png": (704, 320),
    "Gameplay/cover_cell_repeat.png": (640, 512),
    "Gameplay/cover_bottom_cap.png": (640, 512),
    "Gameplay/cover_separator.png": (640, 160),
    "Gameplay/mystery_face_overlay.png": (640, 640),
    "Gameplay/question_mark_decal.png": (640, 640),
}

EXPECTED = EXPECTED_BACKGROUND | EXPECTED_UI | EXPECTED_GAMEPLAY

PIVOTS = {
    "Gameplay/slot_top.png": [0.5, 0.0],
    "Gameplay/slot_cell_repeat.png": [0.5, 0.5],
    "Gameplay/slot_bottom.png": [0.5, 1.0],
    "Gameplay/slot_ice_top.png": [0.5, 0.0],
    "Gameplay/slot_ice_middle.png": [0.5, 0.5],
    "Gameplay/slot_ice_bottom.png": [0.5, 1.0],
    "Gameplay/slot_complete_2cell.png": [0.5, 0.0],
    "Gameplay/slot_ice_complete_2cell.png": [0.5, 0.0],
    "Gameplay/slot_shadow.png": [0.5, 0.5],
    "Gameplay/ice_frost_band.png": [0.5, 0.5],
    "Gameplay/ice_crystal_left.png": [0.5, 1.0],
    "Gameplay/ice_crystal_center.png": [0.5, 1.0],
    "Gameplay/ice_crystal_right.png": [0.5, 1.0],
    "Gameplay/cover_top_cap.png": [0.5, 0.0],
    "Gameplay/cover_cell_repeat.png": [0.5, 0.5],
    "Gameplay/cover_bottom_cap.png": [0.5, 0.5],
    "Gameplay/cover_separator.png": [0.5, 0.5],
    "Gameplay/mystery_face_overlay.png": [0.5, 0.5],
    "Gameplay/question_mark_decal.png": [0.5, 0.5],
}

NINE_SLICE = {
    "UI/HUD/hud_pill_9slice.png": [72, 56, 72, 72],
    "UI/Popups/popup_body_9slice.png": [96, 112, 96, 136],
    "UI/Popups/popup_header_9slice.png": [112, 72, 112, 88],
}
for state in ("normal", "pressed", "disabled"):
    NINE_SLICE[f"UI/Buttons/level_button_{state}.png"] = [128, 88, 128, 104]
    NINE_SLICE[f"UI/Buttons/square_green_{state}.png"] = [72, 72, 72, 88]
    for color in ("lavender", "green", "red"):
        NINE_SLICE[f"UI/Buttons/wide_{color}_{state}.png"] = [88, 64, 88, 80]


SLOT_JOIN_BAND_HEIGHT = 112
SLOT_JOIN_RAIL_ALPHA_THRESHOLD = 64
SLOT_JOIN_CAVITY_ALPHA_THRESHOLD = 192
SLOT_JOIN_SIDE_ZONES = ((64, 192), (448, 576))
SLOT_JOIN_CAVITY_X = (192, 448)
SLOT_JOIN_THRESHOLDS = {
    "rail_outer_edge_p95_px": 4.0,
    "rail_alpha_mae": 0.025,
    "cavity_rgb_mae_255": 8.0,
    "cavity_luminance_mae_255": 6.0,
    "min_cavity_overlap_fraction": 0.90,
}

COMPLETE_SLOT_ASSETS = {
    "Gameplay/slot_complete_2cell.png": {
        "variant": "normal",
        "logical_cell_count": 2,
        "optional_tiled_border": [160, 832, 160, 320],
    },
    "Gameplay/slot_ice_complete_2cell.png": {
        "variant": "integrated_ice",
        "logical_cell_count": 2,
        "optional_tiled_border": [160, 1152, 160, 320],
    },
}
COMPLETE_SLOT_PROVENANCE = {
    "summary": "full rerender / no compositing / no nonuniform warp",
    "generation": "one complete blank-canvas OpenAI built-in ImageGen render per sprite",
    "allowed_processing": [
        "same-raw matte and alpha cleanup",
        "one complete-subject crop",
        "one whole-render uniform isotropic affine scale",
        "transparent canvas padding",
    ],
    "artwork_compositing": False,
    "nonuniform_warp": False,
    "regional_or_piecewise_mapping": False,
    "modular_sprite_inputs": False,
    "canonical_processor": "Sources/ImageGen/Gameplay/process_slot_complete_2cell_full_rerender_strict.py",
    "prompt_and_source_record": "Sources/ImageGen/Gameplay/SlotComplete2Cell_FullRerender_Prompts.md",
    "source_qa_record": "Sources/ImageGen/Gameplay/SlotComplete2Cell_FullRerender_QA.json",
}
COMPLETE_SLOT_ALPHA_THRESHOLD = 64
COMPLETE_SLOT_MAIN_COMPONENT_MIN_FRACTION = 0.995
COMPLETE_SLOT_MAX_ENCLOSED_HOLE_PIXELS = 0
COMPLETE_SLOT_GROOVE_MIN_CONTRAST_255 = 8.0
COMPLETE_SLOT_GROOVE_MAX_WIDTH_PX = 32


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
        "/Library/Fonts/Arial.ttf",
    ]
    for candidate in candidates:
        try:
            return ImageFont.truetype(candidate, size=size)
        except OSError:
            pass
    return ImageFont.load_default()


def checkerboard(width: int, height: int, cell: int = 18) -> Image.Image:
    canvas = Image.new("RGBA", (width, height), "#241943")
    draw = ImageDraw.Draw(canvas)
    colors = ("#34265A", "#40306B")
    for y in range(0, height, cell):
        for x in range(0, width, cell):
            draw.rectangle((x, y, x + cell, y + cell), fill=colors[((x // cell) + (y // cell)) % 2])
    return canvas


def validate_and_manifest() -> tuple[list[dict], list[str]]:
    errors: list[str] = []
    found = {
        path.relative_to(PACK_ROOT).as_posix(): path
        for group in ASSET_GROUPS
        for path in sorted((PACK_ROOT / group).rglob("*.png"))
        if not path.name.startswith("preview_")
    }
    missing = sorted(set(EXPECTED) - set(found))
    unexpected = sorted(set(found) - set(EXPECTED))
    if missing:
        errors.append(f"Missing assets: {missing}")
    if unexpected:
        errors.append(f"Unexpected assets: {unexpected}")

    assets: list[dict] = []
    for relative in sorted(found):
        path = found[relative]
        with Image.open(path) as image:
            image.load()
            dimensions = list(image.size)
            mode = image.mode
            expected_size = EXPECTED.get(relative)
            if expected_size and image.size != expected_size:
                errors.append(f"{relative}: expected {expected_size}, got {image.size}")
            alpha_range = None
            transparent_bbox = None
            if "A" in image.getbands():
                alpha = image.getchannel("A")
                alpha_range = list(alpha.getextrema())
                transparent_bbox = list(alpha.getbbox() or (0, 0, 0, 0))
                intentionally_translucent = {
                    "Gameplay/slot_shadow.png",
                    "Backgrounds/gameplay_block_pattern_512.png",
                }
                if relative.startswith(("UI/", "Gameplay/")) and relative not in intentionally_translucent:
                    if alpha_range[0] != 0 or alpha_range[1] != 255:
                        errors.append(f"{relative}: alpha must include 0 and 255, got {alpha_range}")
            elif relative.startswith(("UI/", "Gameplay/")):
                errors.append(f"{relative}: expected RGBA transparency, got {mode}")

        group = relative.split("/", 1)[0]
        record = {
            "path": relative,
            "group": group,
            "dimensions": dimensions,
            "color_mode": mode,
            "sRGB": True,
            "sha256": sha256(path),
            "pivot": PIVOTS.get(relative, [0.5, 0.5]),
            "pixels_per_unit": 512 if group == "Gameplay" else 100,
            "wrap_mode": "Repeat" if relative.endswith("gameplay_block_pattern_512.png") else "Clamp",
            "filter_mode": "Bilinear",
            "mip_maps": False,
            "visual_source": (
                "OpenAI built-in ImageGen"
                if relative.startswith(("UI/", "Gameplay/"))
                or relative.endswith("menu_background_blocks.png")
                or relative.endswith("gameplay_background_puzzle_silhouettes.png")
                else "deterministic raster"
            ),
        }
        if alpha_range is not None:
            record["alpha_range"] = alpha_range
            record["opaque_content_bbox"] = transparent_bbox
        if relative in COMPLETE_SLOT_ASSETS:
            complete_slot = COMPLETE_SLOT_ASSETS[relative]
            record["variant"] = complete_slot["variant"]
            record["logical_cell_count"] = complete_slot["logical_cell_count"]
            record["recommended_usage"] = "Preferred whole 2-cell SpriteRenderer asset"
            record["nine_slice_recommended"] = False
            record["optional_tiled_border"] = complete_slot["optional_tiled_border"]
            record["optional_tiled_usage"] = (
                "Advanced only: SpriteRenderer Draw Mode Tiled in exact 512 px cell increments"
            )
            record["production_provenance"] = COMPLETE_SLOT_PROVENANCE
            record["legacy_modular_alternative"] = (
                ["slot_top", "slot_cell_repeat", "slot_bottom"]
                if complete_slot["variant"] == "normal"
                else ["slot_ice_top", "slot_ice_middle", "slot_ice_bottom"]
            )
        if relative in NINE_SLICE:
            record["border"] = NINE_SLICE[relative]
            record["sprite_type"] = "Sliced"
        else:
            record["sprite_type"] = "Simple"
        if relative.endswith("question_mark_decal.png"):
            record["expected_glyph"] = "?"
        assets.append(record)
    return assets, errors


def _alpha_composite_at(canvas: Image.Image, sprite: Image.Image, xy: tuple[int, int]) -> None:
    canvas.alpha_composite(sprite.convert("RGBA"), xy)


def _percentile(values: list[float], percentile: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    rank = (len(ordered) - 1) * percentile
    lower = math.floor(rank)
    upper = math.ceil(rank)
    if lower == upper:
        return float(ordered[lower])
    weight = rank - lower
    return float(ordered[lower] * (1.0 - weight) + ordered[upper] * weight)


def _slot_join_broad_band_metrics(upper: Image.Image, lower: Image.Image) -> dict[str, object]:
    """Compare the visible structure on both sides of one assembled slot join.

    The upper band is flipped vertically so row zero in both samples is nearest
    the seam. This matches the mirrored transition used by the periodic middle
    strip while still tolerating small antialiasing and tonal variation.
    """
    band_height = SLOT_JOIN_BAND_HEIGHT
    upper = upper.convert("RGBA")
    lower = lower.convert("RGBA")
    if upper.width != lower.width or min(upper.height, lower.height) < band_height:
        return {
            "passed": False,
            "violations": ["incompatible_join_dimensions"],
            "upper_dimensions": list(upper.size),
            "lower_dimensions": list(lower.size),
        }

    width = upper.width
    upper_band = upper.crop(
        (0, upper.height - band_height, width, upper.height)
    ).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    lower_band = lower.crop((0, 0, width, band_height))
    upper_pixels = list(upper_band.get_flattened_data())
    lower_pixels = list(lower_band.get_flattened_data())

    outer_edge_errors: list[float] = []
    valid_geometry_rows = 0
    for y in range(band_height):
        offset = y * width
        upper_alpha = [pixel[3] for pixel in upper_pixels[offset : offset + width]]
        lower_alpha = [pixel[3] for pixel in lower_pixels[offset : offset + width]]
        upper_visible = [x for x, alpha in enumerate(upper_alpha) if alpha >= SLOT_JOIN_RAIL_ALPHA_THRESHOLD]
        lower_visible = [x for x, alpha in enumerate(lower_alpha) if alpha >= SLOT_JOIN_RAIL_ALPHA_THRESHOLD]
        if not upper_visible or not lower_visible:
            continue
        valid_geometry_rows += 1
        outer_edge_errors.extend(
            (
                abs(upper_visible[0] - lower_visible[0]),
                abs(upper_visible[-1] - lower_visible[-1]),
            )
        )

    rail_alpha_delta = 0.0
    rail_alpha_samples = 0
    for y in range(band_height):
        offset = y * width
        for start_x, end_x in SLOT_JOIN_SIDE_ZONES:
            for x in range(start_x, end_x):
                rail_alpha_delta += abs(
                    upper_pixels[offset + x][3] - lower_pixels[offset + x][3]
                ) / 255.0
                rail_alpha_samples += 1
    rail_alpha_mae = rail_alpha_delta / max(1, rail_alpha_samples)

    cavity_rgb_delta = 0.0
    cavity_luminance_delta = 0.0
    cavity_overlap_pixels = 0
    cavity_start, cavity_end = SLOT_JOIN_CAVITY_X
    for y in range(band_height):
        offset = y * width
        for x in range(cavity_start, cavity_end):
            upper_pixel = upper_pixels[offset + x]
            lower_pixel = lower_pixels[offset + x]
            if (
                upper_pixel[3] < SLOT_JOIN_CAVITY_ALPHA_THRESHOLD
                or lower_pixel[3] < SLOT_JOIN_CAVITY_ALPHA_THRESHOLD
            ):
                continue
            cavity_overlap_pixels += 1
            cavity_rgb_delta += sum(
                abs(upper_pixel[channel] - lower_pixel[channel])
                for channel in range(3)
            )
            upper_luminance = (
                0.2126 * upper_pixel[0]
                + 0.7152 * upper_pixel[1]
                + 0.0722 * upper_pixel[2]
            )
            lower_luminance = (
                0.2126 * lower_pixel[0]
                + 0.7152 * lower_pixel[1]
                + 0.0722 * lower_pixel[2]
            )
            cavity_luminance_delta += abs(upper_luminance - lower_luminance)

    expected_cavity_pixels = band_height * (cavity_end - cavity_start)
    cavity_overlap_fraction = cavity_overlap_pixels / expected_cavity_pixels
    cavity_rgb_mae = cavity_rgb_delta / max(1, cavity_overlap_pixels * 3)
    cavity_luminance_mae = cavity_luminance_delta / max(1, cavity_overlap_pixels)
    rail_outer_edge_p95 = _percentile(outer_edge_errors, 0.95)

    violations: list[str] = []
    if valid_geometry_rows != band_height:
        violations.append("rail_visibility_rows")
    if rail_outer_edge_p95 > SLOT_JOIN_THRESHOLDS["rail_outer_edge_p95_px"]:
        violations.append("rail_outer_edge_p95_px")
    if rail_alpha_mae > SLOT_JOIN_THRESHOLDS["rail_alpha_mae"]:
        violations.append("rail_alpha_mae")
    if cavity_rgb_mae > SLOT_JOIN_THRESHOLDS["cavity_rgb_mae_255"]:
        violations.append("cavity_rgb_mae_255")
    if cavity_luminance_mae > SLOT_JOIN_THRESHOLDS["cavity_luminance_mae_255"]:
        violations.append("cavity_luminance_mae_255")
    if cavity_overlap_fraction < SLOT_JOIN_THRESHOLDS["min_cavity_overlap_fraction"]:
        violations.append("cavity_overlap_fraction")

    return {
        "passed": not violations,
        "band_height_px_each_side": band_height,
        "rail_outer_edge_p95_px": round(rail_outer_edge_p95, 3),
        "rail_outer_edge_max_px": round(max(outer_edge_errors, default=0.0), 3),
        "rail_alpha_mae": round(rail_alpha_mae, 6),
        "cavity_rgb_mae_255": round(cavity_rgb_mae, 3),
        "cavity_luminance_mae_255": round(cavity_luminance_mae, 3),
        "cavity_overlap_fraction": round(cavity_overlap_fraction, 6),
        "valid_geometry_rows": valid_geometry_rows,
        "violations": violations,
    }


def _complete_slot_silhouette_metrics(image: Image.Image) -> dict[str, object]:
    """Validate that a whole-slot export is one vertically continuous silhouette."""
    source_mode = image.mode
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    alpha_range = list(alpha.getextrema())
    mask = alpha.point(
        [
            255 if value >= COMPLETE_SLOT_ALPHA_THRESHOLD else 0
            for value in range(256)
        ]
    )
    mask_pixels = list(mask.get_flattened_data())
    visible_pixels = sum(1 for value in mask_pixels if value)
    bbox = mask.getbbox()
    if not bbox or visible_pixels == 0:
        return {
            "passed": False,
            "source_mode": source_mode,
            "alpha_range": alpha_range,
            "violations": ["empty_alpha_silhouette"],
        }

    seed_index = next(index for index, value in enumerate(mask_pixels) if value)
    seed = (seed_index % mask.width, seed_index // mask.width)
    foreground = mask.copy()
    ImageDraw.floodfill(foreground, seed, 128, thresh=0)
    foreground_histogram = foreground.histogram()
    main_component_pixels = foreground_histogram[128]
    stray_component_pixels = foreground_histogram[255]
    main_component_fraction = main_component_pixels / visible_pixels

    background = ImageOps.invert(mask)
    padded_background = Image.new("L", (mask.width + 2, mask.height + 2), 255)
    padded_background.paste(background, (1, 1))
    ImageDraw.floodfill(padded_background, (0, 0), 0, thresh=0)
    enclosed_hole_pixels = padded_background.histogram()[255]

    left, top, right, bottom = bbox
    empty_rows_inside_bbox = 0
    for y in range(top, bottom):
        offset = y * mask.width
        if not any(mask_pixels[offset + left : offset + right]):
            empty_rows_inside_bbox += 1

    violations: list[str] = []
    if source_mode != "RGBA":
        violations.append("source_mode_rgba")
    if alpha_range[0] != 0 or alpha_range[1] != 255:
        violations.append("alpha_range_0_255")
    if main_component_fraction < COMPLETE_SLOT_MAIN_COMPONENT_MIN_FRACTION:
        violations.append("main_component_fraction")
    if enclosed_hole_pixels > COMPLETE_SLOT_MAX_ENCLOSED_HOLE_PIXELS:
        violations.append("enclosed_alpha_holes")
    if empty_rows_inside_bbox:
        violations.append("vertical_silhouette_gaps")

    return {
        "passed": not violations,
        "source_mode": source_mode,
        "alpha_range": alpha_range,
        "alpha_threshold_255": COMPLETE_SLOT_ALPHA_THRESHOLD,
        "content_bbox": list(bbox),
        "visible_pixels": visible_pixels,
        "main_component_fraction": round(main_component_fraction, 6),
        "stray_component_pixels": stray_component_pixels,
        "enclosed_hole_pixels": enclosed_hole_pixels,
        "empty_rows_inside_bbox": empty_rows_inside_bbox,
        "violations": violations,
    }


def _complete_slot_groove_metrics(image: Image.Image) -> dict[str, object]:
    """Detect one restrained horizontal groove in each of two 512 px cells."""
    image = image.convert("RGBA")
    if image.width < SLOT_JOIN_CAVITY_X[1] or image.height < 1216:
        return {
            "passed": False,
            "expected_groove_bands": 2,
            "detected_groove_bands": 0,
            "violations": ["insufficient_dimensions_for_two_cells"],
        }
    pixels = list(image.get_flattened_data())
    width = image.width
    cavity_start, cavity_end = SLOT_JOIN_CAVITY_X
    cell_starts = (320, 832)
    cell_results: list[dict[str, object]] = []
    total_bands = 0

    for cell_index, cell_start in enumerate(cell_starts):
        window_start = cell_start + 128
        window_end = cell_start + 384
        row_luminance: list[float] = []
        for y in range(window_start, window_end):
            luminance_sum = 0.0
            visible = 0
            offset = y * width
            for x in range(cavity_start, cavity_end):
                pixel = pixels[offset + x]
                if pixel[3] < SLOT_JOIN_CAVITY_ALPHA_THRESHOLD:
                    continue
                luminance_sum += (
                    0.2126 * pixel[0]
                    + 0.7152 * pixel[1]
                    + 0.0722 * pixel[2]
                )
                visible += 1
            row_luminance.append(luminance_sum / max(1, visible))

        baseline = _percentile(row_luminance, 0.5)
        candidate_rows = [
            index
            for index, luminance in enumerate(row_luminance)
            if baseline - luminance >= COMPLETE_SLOT_GROOVE_MIN_CONTRAST_255
        ]
        groups: list[list[int]] = []
        for row in candidate_rows:
            if not groups or row > groups[-1][-1] + 2:
                groups.append([row])
            else:
                groups[-1].append(row)

        bands: list[dict[str, object]] = []
        for group in groups:
            band_width = group[-1] - group[0] + 1
            if band_width > COMPLETE_SLOT_GROOVE_MAX_WIDTH_PX:
                continue
            peak_index = min(group, key=lambda index: row_luminance[index])
            bands.append(
                {
                    "center_y": window_start + (group[0] + group[-1]) / 2.0,
                    "width_px": band_width,
                    "peak_contrast_255": round(
                        baseline - row_luminance[peak_index], 3
                    ),
                }
            )

        total_bands += len(bands)
        cell_results.append(
            {
                "cell_index": cell_index,
                "window_y": [window_start, window_end],
                "baseline_luminance_255": round(baseline, 3),
                "detected_bands": bands,
                "passed": len(bands) == 1,
            }
        )

    violations: list[str] = []
    if total_bands != 2:
        violations.append("exactly_two_groove_bands")
    if any(not bool(cell["passed"]) for cell in cell_results):
        violations.append("one_groove_per_logical_cell")
    return {
        "passed": not violations,
        "expected_groove_bands": 2,
        "detected_groove_bands": total_bands,
        "minimum_contrast_255": COMPLETE_SLOT_GROOVE_MIN_CONTRAST_255,
        "maximum_band_width_px": COMPLETE_SLOT_GROOVE_MAX_WIDTH_PX,
        "cells": cell_results,
        "violations": violations,
    }


def structural_tests() -> tuple[dict, list[str]]:
    errors: list[str] = []
    results: dict[str, object] = {}

    pattern = Image.open(PACK_ROOT / "Backgrounds/gameplay_block_pattern_512.png").convert("RGBA")
    pattern_edges_match = (
        pattern.crop((0, 0, 1, pattern.height)).tobytes()
        == pattern.crop((pattern.width - 1, 0, pattern.width, pattern.height)).tobytes()
        and pattern.crop((0, 0, pattern.width, 1)).tobytes()
        == pattern.crop((0, pattern.height - 1, pattern.width, pattern.height)).tobytes()
    )
    results["tile_pattern_edges_byte_identical"] = pattern_edges_match
    if not pattern_edges_match:
        errors.append("gameplay_block_pattern_512.png does not wrap seamlessly")

    slot_top = Image.open(PACK_ROOT / "Gameplay/slot_top.png").convert("RGBA")
    slot_cell = Image.open(PACK_ROOT / "Gameplay/slot_cell_repeat.png").convert("RGBA")
    slot_bottom = Image.open(PACK_ROOT / "Gameplay/slot_bottom.png").convert("RGBA")
    slot_min_alpha = 255
    slot_edges_match = (
        slot_top.crop((0, slot_top.height - 1, slot_top.width, slot_top.height)).tobytes()
        == slot_cell.crop((0, 0, slot_cell.width, 1)).tobytes()
        and slot_cell.crop((0, 0, slot_cell.width, 1)).tobytes()
        == slot_cell.crop((0, slot_cell.height - 1, slot_cell.width, slot_cell.height)).tobytes()
        and slot_cell.crop((0, slot_cell.height - 1, slot_cell.width, slot_cell.height)).tobytes()
        == slot_bottom.crop((0, 0, slot_bottom.width, 1)).tobytes()
    )
    for count in range(1, 7):
        column = Image.new("RGBA", (640, 640 + count * 512), (0, 0, 0, 0))
        _alpha_composite_at(column, slot_top, (0, 0))
        for index in range(count):
            _alpha_composite_at(column, slot_cell, (0, 320 + index * 512))
        _alpha_composite_at(column, slot_bottom, (0, 320 + count * 512))
        seams = [320 + index * 512 for index in range(count)] + [320 + count * 512]
        for seam_y in seams:
            band = column.getchannel("A").crop((112, max(0, seam_y - 1), 528, min(column.height, seam_y + 2)))
            slot_min_alpha = min(slot_min_alpha, band.getextrema()[0])
    results["slot_repeat_1_to_6_min_seam_alpha"] = slot_min_alpha
    results["slot_seam_edges_byte_identical"] = slot_edges_match
    if slot_min_alpha == 0:
        errors.append("slot repeat assembly contains a transparent seam")
    if not slot_edges_match:
        errors.append("slot module edge pixels are not byte-identical")

    slot_ice_top = Image.open(PACK_ROOT / "Gameplay/slot_ice_top.png").convert("RGBA")
    slot_ice_middle = Image.open(PACK_ROOT / "Gameplay/slot_ice_middle.png").convert("RGBA")
    slot_ice_bottom = Image.open(PACK_ROOT / "Gameplay/slot_ice_bottom.png").convert("RGBA")
    slot_ice_min_alpha = 255
    slot_ice_edges_match = (
        slot_ice_top.crop((0, slot_ice_top.height - 1, slot_ice_top.width, slot_ice_top.height)).tobytes()
        == slot_ice_middle.crop((0, 0, slot_ice_middle.width, 1)).tobytes()
        and slot_ice_middle.crop((0, 0, slot_ice_middle.width, 1)).tobytes()
        == slot_ice_middle.crop(
            (0, slot_ice_middle.height - 1, slot_ice_middle.width, slot_ice_middle.height)
        ).tobytes()
        and slot_ice_middle.crop(
            (0, slot_ice_middle.height - 1, slot_ice_middle.width, slot_ice_middle.height)
        ).tobytes()
        == slot_ice_bottom.crop((0, 0, slot_ice_bottom.width, 1)).tobytes()
    )
    for count in range(1, 7):
        height = 320 + count * 512 + 640
        column = Image.new("RGBA", (640, height), (0, 0, 0, 0))
        _alpha_composite_at(column, slot_ice_top, (0, 0))
        for index in range(count):
            _alpha_composite_at(column, slot_ice_middle, (0, 320 + index * 512))
        _alpha_composite_at(column, slot_ice_bottom, (0, 320 + count * 512))
        seams = [320 + index * 512 for index in range(count)] + [320 + count * 512]
        for seam_y in seams:
            band = column.getchannel("A").crop((112, max(0, seam_y - 1), 528, min(column.height, seam_y + 2)))
            slot_ice_min_alpha = min(slot_ice_min_alpha, band.getextrema()[0])
    results["slot_ice_repeat_1_to_6_min_seam_alpha"] = slot_ice_min_alpha
    results["slot_ice_seam_edges_byte_identical"] = slot_ice_edges_match
    if slot_ice_min_alpha == 0:
        errors.append("integrated ice slot repeat assembly contains a transparent seam")
    if not slot_ice_edges_match:
        errors.append("integrated ice slot module edge pixels are not byte-identical")

    slot_join_pairs = {
        "normal_top_to_middle": (slot_top, slot_cell),
        "normal_middle_to_bottom": (slot_cell, slot_bottom),
        "ice_top_to_middle": (slot_ice_top, slot_ice_middle),
        "ice_middle_to_bottom": (slot_ice_middle, slot_ice_bottom),
    }
    slot_join_metrics = {
        name: _slot_join_broad_band_metrics(upper, lower)
        for name, (upper, lower) in slot_join_pairs.items()
    }
    slot_join_passed = all(
        bool(metrics["passed"]) for metrics in slot_join_metrics.values()
    )
    results["slot_join_broad_band_continuity"] = {
        "passed": slot_join_passed,
        "comparison": "upper bottom band mirrored from seam vs lower top band",
        "rail_alpha_threshold_255": SLOT_JOIN_RAIL_ALPHA_THRESHOLD,
        "cavity_alpha_threshold_255": SLOT_JOIN_CAVITY_ALPHA_THRESHOLD,
        "side_zones_x": [list(zone) for zone in SLOT_JOIN_SIDE_ZONES],
        "cavity_x": list(SLOT_JOIN_CAVITY_X),
        "thresholds": SLOT_JOIN_THRESHOLDS,
        "joins": slot_join_metrics,
    }
    for name, metrics in slot_join_metrics.items():
        if metrics["passed"]:
            continue
        errors.append(
            "slot broad-band discontinuity at "
            f"{name}: {', '.join(metrics['violations'])}"
        )

    complete_slot_results: dict[str, object] = {}
    for relative, specification in COMPLETE_SLOT_ASSETS.items():
        path = PACK_ROOT / relative
        if not path.exists():
            complete_slot_results[relative] = {
                "passed": False,
                "variant": specification["variant"],
                "violations": ["missing_complete_slot_asset"],
            }
            continue
        with Image.open(path) as source:
            source.load()
            silhouette = _complete_slot_silhouette_metrics(source)
            grooves = _complete_slot_groove_metrics(source)
        passed = bool(silhouette["passed"]) and bool(grooves["passed"])
        complete_slot_results[relative] = {
            "passed": passed,
            "variant": specification["variant"],
            "logical_cell_count": specification["logical_cell_count"],
            "silhouette": silhouette,
            "grooves": grooves,
        }
        if not passed:
            violations = list(silhouette["violations"]) + list(grooves["violations"])
            errors.append(
                f"complete 2-cell slot integrity failed for {relative}: "
                f"{', '.join(violations)}"
            )
    results["complete_2cell_slot_integrity"] = {
        "passed": all(
            bool(asset["passed"]) for asset in complete_slot_results.values()
        ),
        "preferred_usage": "whole SpriteRenderer asset; no modular joins",
        "assets": complete_slot_results,
    }

    cover_cap = Image.open(PACK_ROOT / "Gameplay/cover_top_cap.png").convert("RGBA")
    cover_cell = Image.open(PACK_ROOT / "Gameplay/cover_cell_repeat.png").convert("RGBA")
    cover_bottom = Image.open(PACK_ROOT / "Gameplay/cover_bottom_cap.png").convert("RGBA")
    cover_separator = Image.open(PACK_ROOT / "Gameplay/cover_separator.png").convert("RGBA")
    cover_min_alpha = 255
    cover_edges_match = (
        cover_cell.crop((0, 0, cover_cell.width, 1)).tobytes()
        == cover_cell.crop((0, cover_cell.height - 1, cover_cell.width, cover_cell.height)).tobytes()
        and cover_cell.crop((0, cover_cell.height - 1, cover_cell.width, cover_cell.height)).tobytes()
        == cover_bottom.crop((0, 0, cover_bottom.width, 1)).tobytes()
    )
    for count in range(1, 7):
        height = 320 + count * 512
        column = Image.new("RGBA", (704, height), (0, 0, 0, 0))
        _alpha_composite_at(column, cover_cap, (0, 0))
        for index in range(max(0, count - 1)):
            _alpha_composite_at(column, cover_cell, (32, 320 + index * 512))
        _alpha_composite_at(column, cover_bottom, (32, 320 + (count - 1) * 512))
        for index in range(1, count):
            _alpha_composite_at(column, cover_separator, (32, 240 + index * 512))
        seams = [320 + index * 512 for index in range(count)]
        for seam_y in seams:
            band = column.getchannel("A").crop((144, max(0, seam_y - 1), 560, min(column.height, seam_y + 2)))
            column_coverage = min(
                max(band.getpixel((x, y)) for y in range(band.height))
                for x in range(band.width)
            )
            cover_min_alpha = min(cover_min_alpha, column_coverage)
    results["cover_repeat_1_to_6_min_seam_alpha"] = cover_min_alpha
    results["cover_repeat_to_bottom_edges_byte_identical"] = cover_edges_match
    if cover_min_alpha == 0:
        errors.append("cover repeat assembly contains a transparent seam")
    if not cover_edges_match:
        errors.append("cover repeat and bottom-cap edge pixels are not byte-identical")

    mystery = Image.open(PACK_ROOT / "Gameplay/mystery_face_overlay.png")
    question = Image.open(PACK_ROOT / "Gameplay/question_mark_decal.png")
    mystery_overlay_matches = mystery.size == question.size == (640, 640)
    results["mystery_overlay_canvas_match"] = mystery_overlay_matches
    if not mystery_overlay_matches:
        errors.append("mystery face and question decal canvases do not match")

    results["nine_slice_entries_in_manifest"] = len(NINE_SLICE)
    for relative, border in NINE_SLICE.items():
        expected_size = EXPECTED[relative]
        left, bottom, right, top = border
        if left + right >= expected_size[0] or top + bottom >= expected_size[1]:
            errors.append(f"Invalid 9-slice border for {relative}: {border}")
    return results, errors


def build_contact_sheet(assets: list[dict]) -> Path:
    columns = 5
    card_w, card_h = 420, 330
    margin, gap = 36, 18
    header_h = 90
    groups = {name: [a for a in assets if a["group"] == name] for name in ASSET_GROUPS}
    total_h = margin
    for name in ASSET_GROUPS:
        rows = math.ceil(len(groups[name]) / columns)
        total_h += header_h + rows * card_h + gap
    total_w = margin * 2 + columns * card_w
    sheet = Image.new("RGB", (total_w, total_h), "#17102C")
    draw = ImageDraw.Draw(sheet)
    title_font, label_font, small_font = font(38, True), font(21, True), font(17)
    y = margin
    for group_name in ASSET_GROUPS:
        draw.text((margin, y + 12), group_name, fill="#FFF6D6", font=title_font)
        y += header_h
        for index, record in enumerate(groups[group_name]):
            col, row = index % columns, index // columns
            x = margin + col * card_w
            cy = y + row * card_h
            tile = checkerboard(card_w - 20, 236)
            with Image.open(PACK_ROOT / record["path"]) as source:
                sprite = source.convert("RGBA")
                sprite.thumbnail((card_w - 64, 214), Image.Resampling.LANCZOS)
                px = (tile.width - sprite.width) // 2
                py = (tile.height - sprite.height) // 2
                tile.alpha_composite(sprite, (px, py))
            sheet.paste(tile.convert("RGB"), (x + 10, cy + 4))
            stem = Path(record["path"]).stem
            if len(stem) > 31:
                stem = stem[:29] + "..."
            draw.text((x + 12, cy + 248), stem, fill="#FFFFFF", font=label_font)
            size_label = f'{record["dimensions"][0]} x {record["dimensions"][1]}  {record["color_mode"]}'
            draw.text((x + 12, cy + 280), size_label, fill="#BDB3E9", font=small_font)
        y += math.ceil(len(groups[group_name]) / columns) * card_h + gap
    output = PACK_ROOT / "preview_contact_sheet.png"
    sheet.save(output, "PNG", optimize=True)
    return output


def main() -> None:
    assets, errors = validate_and_manifest()
    structure, structural_errors = structural_tests()
    errors.extend(structural_errors)
    manifest = {
        "name": "Colorful Sort 2D Asset Pack",
        "version": 7,
        "reference_resolution": [1440, 2560],
        "gameplay_cell_pixels": 512,
        "recommended_gameplay_ppu": 512,
        "asset_count": len(assets),
        "assembly_recipes": {
            "preferred_complete_2cell_slots": {
                "production_provenance": COMPLETE_SLOT_PROVENANCE,
                "normal": {
                    "path": "Gameplay/slot_complete_2cell.png",
                    "dimensions": [640, 1664],
                    "pivot": [0.5, 0.0],
                    "pixels_per_unit": 512,
                    "logical_cell_count": 2,
                    "optional_tiled_border": [160, 832, 160, 320],
                },
                "integrated_ice": {
                    "path": "Gameplay/slot_ice_complete_2cell.png",
                    "dimensions": [640, 1984],
                    "pivot": [0.5, 0.0],
                    "pixels_per_unit": 512,
                    "logical_cell_count": 2,
                    "optional_tiled_border": [160, 1152, 160, 320],
                },
                "usage": "Fixed 2-cell: Simple. Variable cell count: Tiled in exact 512px steps with the listed border; do not use free Sliced stretch",
            },
            "slot_column_top_left_pixels": {
                "status": "legacy_optional_for_variable_height_columns",
                "top": [0, 0],
                "cell_i": "[0, 320 + i*512]",
                "bottom": "[0, 320 + cellCount*512]",
                "optional_shadow": "center under the completed column",
                "canonical_join_bands": "top lower 160px and bottom upper 128px reuse the matching normal middle rail/cavity profile",
            },
            "slot_column_unity_units": {
                "status": "legacy_optional_for_variable_height_columns",
                "bottom_attach": [0, 0],
                "middle_center_k": "[0, 0.5 + k], k=0..cellCount-1",
                "top_attach": "[0, cellCount]",
                "bottom_feature": "integrated front-facing 2x2 stud seat",
            },
            "integrated_ice_slot_top_left_pixels": {
                "status": "legacy_optional_for_variable_height_columns",
                "top": [0, 0],
                "middle_i": "[0, 320 + i*512]",
                "bottom_with_ice": "[0, 320 + cellCount*512]",
                "bottom_feature": "integrated front-facing 2x2 stud seat with frost shelf and three icicles underneath",
                "canonical_join_bands": "top lower 160px and bottom upper 128px reuse the matching ice middle rail/cavity profile",
            },
            "cover_column_top_left_pixels": {
                "cap": [0, 0],
                "repeat_cell_i": "[32, 320 + i*512], i=0..cellCount-2",
                "bottom_cell": "[32, 320 + (cellCount-1)*512]",
                "separator_i": "[32, 240 + i*512], i=1..cellCount-1",
            },
            "cover_column_unity_units": {
                "bottom_center": [0, 0.5],
                "repeat_center_k": "[0, 1.5 + k], k=0..cellCount-2",
                "top_bottom_pivot": "[0, cellCount]",
            },
            "ice_crystal_top_pivot_offsets_unity_units": {
                "left": [-0.33984375, -0.41015625],
                "center": [0.0, -0.37109375],
                "right": [0.33984375, -0.41015625],
            },
            "mystery_overlay": "mystery_face_overlay and question_mark_decal share the same 640x640 canvas and pivot; overlay 1:1",
        },
        "palette": {
            "purple": "#7259FF",
            "dark_outline": "#4237A1",
            "yellow": "#FEC901",
            "orange": "#F08A00",
            "green": "#00D44C",
            "red": "#FE3C00",
            "lavender": "#9A99FF",
            "ice_light": "#B9E3F7",
            "ice_mid": "#92D7F2",
            "cover_beige": "#C7A89C",
            "mystery": "#2A2835",
        },
        "assets": assets,
    }
    (PACK_ROOT / "AssetManifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    contact_sheet = build_contact_sheet(assets)
    report = {
        "passed": not errors,
        "asset_count": len(assets),
        "expected_asset_count": len(EXPECTED),
        "contact_sheet": contact_sheet.name,
        "structural_tests": structure,
        "errors": errors,
    }
    (PACK_ROOT / "qa_report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
