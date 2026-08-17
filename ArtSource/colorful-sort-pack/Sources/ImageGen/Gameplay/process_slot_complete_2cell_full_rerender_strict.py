#!/usr/bin/env python3
"""Strict raster-only export for complete one-piece ImageGen slot renders.

Final artwork reads exactly one complete ImageGen raw per sprite.  The only
allowed final operations are matte/alpha cleanup, one crop, one uniform
isotropic transform, and transparent padding.  There is no regional mapping,
piecewise warp, modular sprite read, artwork compositing, or vector drawing.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import (
    binary_closing,
    binary_erosion,
    binary_fill_holes,
    distance_transform_edt,
    gaussian_filter1d,
    label,
)


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DRAFT = ROOT / "GeneratedAssets" / "ImageGenDraft" / "Gameplay"
ACTIVE = ROOT / "GeneratedAssets" / "Gameplay"
DRAFT.mkdir(parents=True, exist_ok=True)
ACTIVE.mkdir(parents=True, exist_ok=True)

SPECS = {
    "normal": {
        "raw": "slot_complete_2cell_full_rerender_raw.png",
        "final": "slot_complete_2cell.png",
        "canvas": (640, 1664),
        "tiled_border_lbrt": [160, 832, 160, 320],
    },
    "frozen": {
        "raw": "slot_ice_complete_2cell_full_rerender_raw.png",
        "final": "slot_ice_complete_2cell.png",
        "canvas": (640, 1984),
        "tiled_border_lbrt": [160, 1152, 160, 320],
    },
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def largest_component(mask: np.ndarray) -> np.ndarray:
    labels, count = label(mask)
    if count == 0:
        raise RuntimeError("ImageGen subject mask is empty")
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    return labels == int(sizes.argmax())


def extract_subject(path: Path) -> tuple[Image.Image, dict[str, object]]:
    """Extract baked light/checker matte; preserve only ImageGen-source RGB."""
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.uint8)
    maximum = rgb.max(axis=2).astype(np.int16)
    minimum = rgb.min(axis=2).astype(np.int16)
    chroma = maximum - minimum

    # The checker matte is bright neutral.  Dark plastic and chromatic cyan ice
    # form one connected foreground component.  Filled interior highlights stay
    # inside that component and therefore are preserved by fill_holes.
    seed = (minimum < 222) | (chroma > 22)
    seed = binary_closing(seed, structure=np.ones((3, 3), dtype=bool))
    subject = binary_fill_holes(largest_component(seed))

    # One-pixel inward soft edge.  For partially transparent boundary pixels,
    # use the nearest interior RGB from this same raw render to remove the baked
    # white/checker fringe.  No new colors or geometry are painted.
    inward = distance_transform_edt(subject)
    alpha = np.clip((inward - 0.25) / 0.90, 0.0, 1.0)
    core = binary_erosion(subject, iterations=2)
    if not core.any():
        core = subject
    _, nearest = distance_transform_edt(~core, return_indices=True)
    cleaned_rgb = rgb.copy()
    boundary = subject & (alpha < 0.999)
    cleaned_rgb[boundary] = rgb[nearest[0][boundary], nearest[1][boundary]]
    cleaned_rgb[~subject] = 0
    alpha_u8 = np.rint(alpha * 255.0).astype(np.uint8)
    rgba = np.dstack((cleaned_rgb, alpha_u8))
    rgba[alpha_u8 == 0, :3] = 0
    image = Image.fromarray(rgba, "RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"No extracted subject in {path.name}")
    return image, {
        "raw_mode": "RGB",
        "raw_size": list(Image.open(path).size),
        "subject_bbox_raw_ltrb": list(bbox),
        "subject_bbox_raw_wh": [bbox[2] - bbox[0], bbox[3] - bbox[1]],
        "matte_rule": "(min_rgb < 222) OR (chroma > 22), largest connected component",
        "edge_cleanup": "nearest same-raw interior RGB on partial-alpha boundary",
    }


def _subpixel_peak(score: np.ndarray, index: int) -> float:
    left = float(score[index - 1])
    middle = float(score[index])
    right = float(score[index + 1])
    denominator = left - 2.0 * middle + right
    offset = 0.0 if abs(denominator) < 1e-9 else 0.5 * (left - right) / denominator
    return float(index + np.clip(offset, -0.5, 0.5))


def detect_grooves(image: Image.Image) -> list[float]:
    """Detect two recessed horizontal marks without modifying the artwork."""
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    ys, xs = np.where(alpha >= 128)
    if xs.size == 0:
        raise RuntimeError("Cannot detect grooves in an empty image")
    left, right = int(xs.min()), int(xs.max()) + 1
    top, bottom = int(ys.min()), int(ys.max()) + 1
    width, height = right - left, bottom - top
    x0 = left + int(round(width * 0.29))
    x1 = left + int(round(width * 0.71))

    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    luminance = (
        0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    )
    profile = luminance[:, x0:x1].mean(axis=1)
    # A groove is a thin dark row against a slowly varying cavity gradient.
    score = gaussian_filter1d(profile, 11.0) - gaussian_filter1d(profile, 1.25)
    y0 = top + int(round(height * 0.16))
    y1 = top + int(round(height * 0.76))
    local = [
        y
        for y in range(max(y0, 1), min(y1, image.height - 1))
        if score[y] >= score[y - 1] and score[y] > score[y + 1]
    ]
    local.sort(key=lambda y: float(score[y]), reverse=True)
    chosen: list[int] = []
    minimum_separation = max(64, int(round(height * 0.12)))
    for y in local:
        if all(abs(y - other) >= minimum_separation for other in chosen):
            chosen.append(y)
        if len(chosen) == 2:
            break
    if len(chosen) != 2:
        raise RuntimeError(f"Expected exactly two groove peaks; detected {chosen}")
    return sorted(_subpixel_peak(score, y) for y in chosen)


def uniform_isotropic_transform(
    cropped: Image.Image,
    canvas_size: tuple[int, int],
    scale: float,
) -> tuple[Image.Image, dict[str, object]]:
    """Apply one whole-image affine transform with identical X/Y scale."""
    canvas_width, canvas_height = canvas_size
    placed_width = cropped.width * scale
    placed_height = cropped.height * scale
    if placed_width > canvas_width or placed_height > canvas_height:
        raise RuntimeError(
            "Uniform 512-spacing scale does not fit canvas: "
            f"{placed_width:.3f}x{placed_height:.3f} into "
            f"{canvas_width}x{canvas_height}"
        )
    offset_x = (canvas_width - placed_width) / 2.0
    offset_y = (canvas_height - placed_height) / 2.0
    inverse = (
        1.0 / scale,
        0.0,
        -offset_x / scale,
        0.0,
        1.0 / scale,
        -offset_y / scale,
    )

    source = np.asarray(cropped, dtype=np.float32) / 255.0
    alpha = source[..., 3]
    premultiplied = source[..., :3] * alpha[..., None]
    transformed_planes: list[np.ndarray] = []
    for plane in (*np.moveaxis(premultiplied, 2, 0), alpha):
        rendered = Image.fromarray(plane.astype(np.float32), mode="F").transform(
            canvas_size,
            Image.Transform.AFFINE,
            inverse,
            resample=Image.Resampling.BICUBIC,
            fillcolor=0.0,
        )
        transformed_planes.append(np.asarray(rendered, dtype=np.float32))

    out_alpha = np.clip(transformed_planes[3], 0.0, 1.0)
    out_premultiplied = np.stack(transformed_planes[:3], axis=2)
    out_rgb = np.zeros_like(out_premultiplied)
    visible = out_alpha > (8.0 / 255.0)
    out_rgb[visible] = out_premultiplied[visible] / out_alpha[visible, None]
    pixels = np.rint(
        np.dstack((np.clip(out_rgb, 0.0, 1.0), out_alpha)) * 255.0
    ).astype(np.uint8)
    # Remove sub-4% bicubic ringing specks outside the main silhouette.
    pixels[pixels[..., 3] <= 8] = 0
    main_silhouette = largest_component(pixels[..., 3] > 8)
    pixels[~main_silhouette] = 0
    return Image.fromarray(pixels, "RGBA"), {
        "uniform_scale_x": scale,
        "uniform_scale_y": scale,
        "isotropic_scale_delta": 0.0,
        "resized_subject_float_wh": [placed_width, placed_height],
        "pad_offsets_float_ltrb": [
            offset_x,
            offset_y,
            canvas_width - offset_x - placed_width,
            canvas_height - offset_y - placed_height,
        ],
        "affine_forward_matrix": [[scale, 0.0, offset_x], [0.0, scale, offset_y]],
    }


def alpha_qa(image: Image.Image) -> dict[str, object]:
    rgba = np.asarray(image, dtype=np.uint8)
    alpha = rgba[..., 3]
    hidden = rgba[..., :3][alpha == 0]
    corners = [
        int(alpha[0, 0]),
        int(alpha[0, -1]),
        int(alpha[-1, 0]),
        int(alpha[-1, -1]),
    ]
    _, component_count = label(alpha > 8)
    return {
        "mode": image.mode,
        "size": list(image.size),
        "alpha_min": int(alpha.min()),
        "alpha_max": int(alpha.max()),
        "corner_alpha": corners,
        "hidden_rgb_max": int(hidden.max()) if hidden.size else 0,
        "alpha_components_gt8": int(component_count),
    }


def continuity_qa(image: Image.Image, grooves: list[float]) -> dict[str, object]:
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    ys, xs = np.where(alpha >= 128)
    top, bottom = int(ys.min()), int(ys.max()) + 1
    left_edges: list[int] = []
    right_edges: list[int] = []
    missing = 0
    # Measure the full straight-rail interval between crown and stud plate.
    y0 = max(top, int(round(top + 0.12 * (bottom - top))))
    y1 = min(bottom, int(round(grooves[1] + 0.28 * (bottom - top))))
    for y in range(y0, y1):
        row = np.flatnonzero(alpha[y] >= 128)
        if row.size == 0:
            missing += 1
            continue
        left_edges.append(int(row.min()))
        right_edges.append(int(row.max()))
    left_steps = np.abs(np.diff(left_edges)) if len(left_edges) > 1 else np.array([0])
    right_steps = np.abs(np.diff(right_edges)) if len(right_edges) > 1 else np.array([0])
    center_x = image.width // 2
    center_missing = int(np.count_nonzero(alpha[y0:y1, center_x] < 128))
    return {
        "rows_checked": y1 - y0,
        "rail_missing_rows": missing,
        "cavity_center_missing_rows": center_missing,
        "left_outer_edge_range_px": int(max(left_edges) - min(left_edges)),
        "right_outer_edge_range_px": int(max(right_edges) - min(right_edges)),
        "left_outer_edge_p99_adjacent_step_px": float(np.percentile(left_steps, 99)),
        "right_outer_edge_p99_adjacent_step_px": float(np.percentile(right_steps, 99)),
    }


def render_final(kind: str) -> tuple[Image.Image, dict[str, object]]:
    spec = SPECS[kind]
    raw_path = HERE / str(spec["raw"])
    extracted, extraction = extract_subject(raw_path)
    raw_grooves = detect_grooves(extracted)
    raw_spacing = raw_grooves[1] - raw_grooves[0]
    scale = 512.0 / raw_spacing
    bbox = extracted.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"No subject bbox for {kind}")
    cropped = extracted.crop(bbox)
    final, transform = uniform_isotropic_transform(
        cropped, tuple(spec["canvas"]), scale
    )
    final_grooves = detect_grooves(final)
    analytic_grooves = [
        (raw_y - bbox[1]) * scale + transform["pad_offsets_float_ltrb"][1]
        for raw_y in raw_grooves
    ]
    report = {
        "raw_file": raw_path.name,
        "raw_sha256": sha256(raw_path),
        "source_count": 1,
        "source_role": "one complete built-in ImageGen render",
        "extraction": extraction,
        "raw_groove_centers_y": raw_grooves,
        "raw_groove_spacing_px": raw_spacing,
        "target_groove_spacing_px": 512.0,
        "transform": transform,
        "analytic_final_groove_centers_y": analytic_grooves,
        "analytic_final_groove_spacing_px": analytic_grooves[1] - analytic_grooves[0],
        "detected_final_groove_centers_y": final_grooves,
        "detected_final_groove_spacing_px": final_grooves[1] - final_grooves[0],
        "alpha": alpha_qa(final),
        "continuity": continuity_qa(final, final_grooves),
        "unity": {
            "pixels_per_unit": 512,
            "suggested_pivot": [0.5, 0.0],
            "sprite_mode": "Single",
            "mesh_type": "Full Rect",
            "sRGB": True,
            "alpha_is_transparency": True,
            "tiled_center_top_down": [160, 320, 320, 512],
            "tiled_border_lbrt": spec["tiled_border_lbrt"],
        },
        "operations_allowlist": [
            "light/checker matte alpha cleanup",
            "one complete-subject crop",
            "one whole-render uniform isotropic affine scale",
            "transparent canvas padding",
        ],
        "operations_forbidden_and_absent": [
            "piecewise warp",
            "regional resize",
            "vertical spatial normalization",
            "modular sprite input",
            "artwork compositing",
            "vector replacement",
        ],
    }
    return final, report


def _fit(image: Image.Image, maximum: tuple[int, int]) -> Image.Image:
    scale = min(maximum[0] / image.width, maximum[1] / image.height)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    return image.resize(size, Image.Resampling.LANCZOS)


def make_previews(finals: dict[str, Image.Image]) -> None:
    # Full before/after provenance preview.
    sheet = Image.new("RGB", (1800, 1200), (28, 22, 70))
    draw = ImageDraw.Draw(sheet)
    columns = [30, 470, 930, 1370]
    labels = ["NORMAL RAW", "NORMAL FINAL", "FROZEN RAW", "FROZEN FINAL"]
    images = [
        Image.open(HERE / SPECS["normal"]["raw"]).convert("RGB"),
        finals["normal"],
        Image.open(HERE / SPECS["frozen"]["raw"]).convert("RGB"),
        finals["frozen"],
    ]
    for x, label_text, image in zip(columns, labels, images):
        draw.text((x, 20), label_text, fill=(255, 255, 255))
        shown = _fit(image, (400, 1120))
        if shown.mode == "RGBA":
            backing = Image.new("RGBA", shown.size, (48, 38, 102, 255))
            backing.alpha_composite(shown)
            shown = backing.convert("RGB")
        sheet.paste(shown, (x, 60))
    sheet.save(
        HERE / "SlotComplete2Cell_FullRerender_BeforeAfter.png",
        format="PNG",
        optimize=True,
    )

    # Actual 200% top-region inspection preview on a dark neutral backing.
    crops: list[Image.Image] = []
    for kind in ("normal", "frozen"):
        image = finals[kind]
        bbox = image.getchannel("A").getbbox()
        if bbox is None:
            raise RuntimeError("Empty final during top preview")
        top_height = max(1, round((bbox[3] - bbox[1]) * 0.35))
        crop = image.crop((bbox[0], bbox[1], bbox[2], bbox[1] + top_height))
        crops.append(crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.NEAREST))
    width = sum(crop.width for crop in crops) + 96
    height = max(crop.height for crop in crops) + 64
    top_sheet = Image.new("RGBA", (width, height), (28, 22, 70, 255))
    x = 32
    for crop in crops:
        top_sheet.alpha_composite(crop, dest=(x, 32))
        x += crop.width + 32
    top_sheet.save(
        HERE / "SlotComplete2Cell_FullRerender_Top200.png",
        format="PNG",
        optimize=True,
    )

    # Compatibility previews used by the existing pack documentation.
    full = Image.new("RGBA", (1328, 2048), (19, 15, 53, 255))
    full.alpha_composite(finals["normal"], dest=(16, 192))
    full.alpha_composite(finals["frozen"], dest=(672, 32))
    full.save(HERE / "SlotComplete2Cell_FullPreview.png", optimize=True)
    phone = Image.new("RGBA", (224, 230), (19, 15, 53, 255))
    phone.alpha_composite(_fit(finals["normal"], (64, 166)), dest=(40, 32))
    phone.alpha_composite(_fit(finals["frozen"], (64, 198)), dest=(120, 16))
    phone.save(HERE / "SlotComplete2Cell_PhonePreview.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--activate",
        action="store_true",
        help="also overwrite active Gameplay finals after visual QA",
    )
    parser.add_argument(
        "--only",
        choices=tuple(SPECS),
        help="process one variant during raw-fit QA",
    )
    args = parser.parse_args()

    finals: dict[str, Image.Image] = {}
    qa: dict[str, object] = {
        "processor": Path(__file__).name,
        "strict_pipeline": True,
        "active_overwrite_requested": bool(args.activate),
    }
    selected = SPECS.items() if args.only is None else [(args.only, SPECS[args.only])]
    for kind, spec in selected:
        final, report = render_final(kind)
        draft_path = DRAFT / str(spec["final"])
        final.save(draft_path, format="PNG", optimize=True)
        report["draft_file"] = str(draft_path.relative_to(ROOT))
        report["draft_sha256"] = sha256(draft_path)
        if args.activate:
            active_path = ACTIVE / str(spec["final"])
            final.save(active_path, format="PNG", optimize=True)
            report["active_file"] = str(active_path.relative_to(ROOT))
            report["active_sha256"] = sha256(active_path)
        finals[kind] = final
        qa[kind] = report

    if len(finals) == len(SPECS):
        make_previews(finals)
    qa_path = HERE / "SlotComplete2Cell_FullRerender_QA.json"
    qa_path.write_text(json.dumps(qa, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(qa, indent=2))


if __name__ == "__main__":
    main()
