#!/usr/bin/env python3
"""Generate the deterministic gameplay background textures.

The generator intentionally has no project-specific runtime dependency beyond
NumPy and Pillow.  Re-running it always produces the same two Unity-ready PNGs:

* gameplay_background_purple.png -- 1440 x 2560 opaque RGB background
* gameplay_block_pattern_512.png -- 512 x 512 seamless RGBA overlay

The transparent pattern is drawn on an infinite, periodic lattice before it is
downsampled.  That gives edge-crossing motifs the same antialiasing on either
side of the tile boundary and avoids a visible seam with Repeat wrap mode.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


BACKGROUND_SIZE = (1440, 2560)
PATTERN_SIZE = 512
SUPERSAMPLE = 3

PALETTE = {
    "violet": (75, 44, 127),        # #4B2C7F
    "deep_violet": (58, 41, 103),  # #3A2967
    "accent": (114, 89, 255),      # #7259FF
    "lavender": (154, 153, 255),
    "ink": (35, 23, 65),
    "highlight": (210, 208, 255),
}


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    """Vectorized smoothstep that also supports reversed edges."""
    t = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def mix(a: np.ndarray, b: np.ndarray, amount: np.ndarray) -> np.ndarray:
    return a * (1.0 - amount[..., None]) + b * amount[..., None]


def generate_gameplay_background(output_path: Path) -> None:
    """Create a clean purple playfield with center light and dark vignette."""
    width, height = BACKGROUND_SIZE
    x = np.linspace(0.0, 1.0, width, dtype=np.float32)[None, :]
    y = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None]

    top = np.asarray(PALETTE["deep_violet"], dtype=np.float32)
    middle = np.asarray(PALETTE["violet"], dtype=np.float32)
    bottom = np.asarray((47, 27, 88), dtype=np.float32)
    accent = np.asarray(PALETTE["accent"], dtype=np.float32)
    deep_ink = np.asarray((27, 17, 49), dtype=np.float32)

    # A two-stage vertical blend keeps the top quiet for HUD readability and
    # gives the lower gameplay area a little more visual weight.
    upper_t = smoothstep(0.0, 0.48, y)
    lower_t = smoothstep(0.46, 1.0, y)
    base = top[None, None, :] * (1.0 - upper_t[..., None])
    base += middle[None, None, :] * upper_t[..., None]
    base = mix(base, np.broadcast_to(bottom, base.shape), lower_t * 0.78)

    # Broad center illumination, biased subtly toward the upper-left so the
    # background shares the UI kit's lighting direction.
    light_distance = ((x - 0.42) / 0.73) ** 2 + ((y - 0.39) / 0.60) ** 2
    center_light = np.exp(-2.05 * light_distance).astype(np.float32) * 0.33
    directional = np.clip(1.0 - (x * 0.82 + y * 0.50), 0.0, 1.0) ** 2
    light_amount = np.clip(center_light + directional * 0.055, 0.0, 0.38)
    base = mix(base, np.broadcast_to(accent, base.shape), light_amount)

    # A soft peripheral darkening frames gameplay without creating a hard ring.
    vignette_distance = ((x - 0.50) / 0.72) ** 2 + ((y - 0.46) / 0.78) ** 2
    vignette = smoothstep(0.40, 1.35, vignette_distance) * 0.46
    base = mix(base, np.broadcast_to(deep_ink, base.shape), vignette)

    # Very broad, deterministic tonal undulation prevents gradient banding while
    # remaining visually neutral behind the actual level geometry.
    cloud = (
        np.sin(x * np.float32(8.4) + y * np.float32(4.1))
        + 0.62 * np.sin(x * np.float32(3.2) - y * np.float32(8.7) + 1.8)
        + 0.34 * np.cos(x * np.float32(13.1) + y * np.float32(2.7) + 0.6)
    )
    cloud *= 0.62
    base += cloud[..., None]

    rng = np.random.default_rng(7259)
    dither = rng.uniform(-0.46, 0.46, (height, width, 1)).astype(np.float32)
    base += dither

    rgb = np.clip(np.rint(base), 0, 255).astype(np.uint8)
    Image.fromarray(rgb, mode="RGB").save(output_path, format="PNG", compress_level=9)


def sc(value: float) -> int:
    return int(round(value * SUPERSAMPLE))


def rgba(name: str, alpha: int) -> tuple[int, int, int, int]:
    return (*PALETTE[name], alpha)


def _rounded(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float], radius: float,
             fill: tuple[int, int, int, int], width: float = 0,
             outline: tuple[int, int, int, int] | None = None) -> None:
    scaled_box = tuple(sc(v) for v in box)
    draw.rounded_rectangle(
        scaled_box,
        radius=sc(radius),
        fill=fill,
        outline=outline,
        width=sc(width) if width else 1,
    )


def make_brick_stamp(scale: float, angle: float) -> Image.Image:
    """A softly beveled 2x2 toy-brick symbol."""
    width, height = 150.0, 112.0
    stamp = Image.new("RGBA", (sc(width), sc(height)), (*PALETTE["accent"], 0))
    draw = ImageDraw.Draw(stamp, "RGBA")

    _rounded(draw, (18, 32, 136, 102), 17, rgba("ink", 20))
    _rounded(draw, (14, 24, 132, 94), 17, rgba("violet", 34), 2.2, rgba("lavender", 18))
    _rounded(draw, (19, 28, 127, 88), 13, rgba("accent", 12))

    for cx in (47, 99):
        draw.ellipse((sc(cx - 16), sc(15), sc(cx + 16), sc(43)), fill=rgba("ink", 18))
        draw.ellipse((sc(cx - 16), sc(10), sc(cx + 16), sc(38)),
                     fill=rgba("violet", 35), outline=rgba("lavender", 19), width=sc(1.8))
        draw.arc((sc(cx - 11), sc(14), sc(cx + 11), sc(33)), 195, 328,
                 fill=rgba("highlight", 25), width=sc(2.2))

    draw.line((sc(31), sc(31), sc(113), sc(31)), fill=rgba("highlight", 20), width=sc(2.0))
    draw.arc((sc(19), sc(29), sc(127), sc(88)), 184, 274,
             fill=rgba("highlight", 15), width=sc(2.2))

    if scale != 1.0:
        stamp = stamp.resize(
            (max(1, int(stamp.width * scale)), max(1, int(stamp.height * scale))),
            Image.Resampling.LANCZOS,
        )
    return stamp.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)


def make_cube_stamp(scale: float, angle: float) -> Image.Image:
    """An isometric toy cube with brighter top/left faces."""
    width, height = 130.0, 126.0
    stamp = Image.new("RGBA", (sc(width), sc(height)), (*PALETTE["accent"], 0))
    draw = ImageDraw.Draw(stamp, "RGBA")

    top_face = [(65, 12), (116, 38), (65, 65), (14, 38)]
    left_face = [(14, 38), (65, 65), (65, 116), (14, 88)]
    right_face = [(65, 65), (116, 38), (116, 88), (65, 116)]
    shadow = [(20, 91), (66, 118), (112, 93), (112, 100), (66, 125), (20, 99)]

    draw.polygon([(sc(x), sc(y)) for x, y in shadow], fill=rgba("ink", 17))
    draw.polygon([(sc(x), sc(y)) for x, y in left_face], fill=rgba("accent", 28))
    draw.polygon([(sc(x), sc(y)) for x, y in right_face], fill=rgba("ink", 22))
    draw.polygon([(sc(x), sc(y)) for x, y in top_face], fill=rgba("lavender", 30))
    draw.line([(sc(x), sc(y)) for x, y in top_face + [top_face[0]]],
              fill=rgba("highlight", 18), width=sc(1.8), joint="curve")
    draw.line([(sc(x), sc(y)) for x, y in left_face + [left_face[0]]],
              fill=rgba("violet", 22), width=sc(1.6), joint="curve")
    draw.line((sc(65), sc(66), sc(65), sc(108)), fill=rgba("highlight", 13), width=sc(1.8))

    # A shallow stud on the cube's top face adds the plastic-block cue.
    draw.ellipse((sc(49), sc(27), sc(81), sc(45)), fill=rgba("ink", 13))
    draw.ellipse((sc(49), sc(23), sc(81), sc(41)),
                 fill=rgba("accent", 31), outline=rgba("highlight", 19), width=sc(1.5))

    if scale != 1.0:
        stamp = stamp.resize(
            (max(1, int(stamp.width * scale)), max(1, int(stamp.height * scale))),
            Image.Resampling.LANCZOS,
        )
    return stamp.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)


def make_stack_stamp(scale: float, angle: float) -> Image.Image:
    """Three offset rounded blocks, kept abstract enough for a background motif."""
    width, height = 146.0, 128.0
    stamp = Image.new("RGBA", (sc(width), sc(height)), (*PALETTE["accent"], 0))
    draw = ImageDraw.Draw(stamp, "RGBA")

    blocks = (
        (22, 69, 102, 113, 20),
        (47, 39, 127, 83, 26),
        (16, 13, 96, 57, 31),
    )
    for x0, y0, x1, y1, alpha in blocks:
        _rounded(draw, (x0 + 5, y0 + 7, x1 + 5, y1 + 7), 11, rgba("ink", 15))
        _rounded(draw, (x0, y0, x1, y1), 11, rgba("violet", alpha), 1.8,
                 rgba("lavender", 14))
        draw.line((sc(x0 + 12), sc(y0 + 7), sc(x1 - 12), sc(y0 + 7)),
                  fill=rgba("highlight", 16), width=sc(1.8))

    if scale != 1.0:
        stamp = stamp.resize(
            (max(1, int(stamp.width * scale)), max(1, int(stamp.height * scale))),
            Image.Resampling.LANCZOS,
        )
    return stamp.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)


def alpha_composite_center(canvas: Image.Image, stamp: Image.Image, center: tuple[float, float]) -> None:
    x = int(round(center[0] * SUPERSAMPLE - stamp.width / 2))
    y = int(round(center[1] * SUPERSAMPLE - stamp.height / 2))
    canvas.alpha_composite(stamp, dest=(x, y))


def generate_seamless_pattern(output_path: Path) -> None:
    """Create a seamless, low-opacity block-symbol overlay."""
    tile = PATTERN_SIZE
    logical_span = tile * 3
    canvas = Image.new(
        "RGBA",
        (logical_span * SUPERSAMPLE, logical_span * SUPERSAMPLE),
        (*PALETTE["accent"], 0),
    )

    # Positions are in the central logical tile. Several intentionally cross a
    # boundary; periodic lattice repetition carries their remaining pixels to
    # the opposite edge of the exported tile.
    motif_specs = (
        ("brick", (18.0, 92.0), 0.72, -14.0),
        ("cube", (229.0, 18.0), 0.68, 10.0),
        ("stack", (416.0, 105.0), 0.64, 15.0),
        ("cube", (510.0, 252.0), 0.74, -13.0),
        ("brick", (156.0, 269.0), 0.60, 19.0),
        ("stack", (351.0, 345.0), 0.72, -17.0),
        ("cube", (61.0, 453.0), 0.62, 17.0),
        ("brick", (283.0, 500.0), 0.58, -10.0),
        ("stack", (493.0, 487.0), 0.55, 8.0),
    )

    stamp_factories = {
        "brick": make_brick_stamp,
        "cube": make_cube_stamp,
        "stack": make_stack_stamp,
    }
    prepared = [
        (stamp_factories[kind](scale, angle), position)
        for kind, position, scale, angle in motif_specs
    ]

    # Draw periodic copies over a 3 x 3 tile field. One extra lattice step in
    # each direction supplies pixels for rotated stamps crossing the outer edge.
    for stamp, (base_x, base_y) in prepared:
        for tile_y in range(-2, 4):
            for tile_x in range(-2, 4):
                alpha_composite_center(
                    canvas,
                    stamp,
                    ((tile_x + 1) * tile + base_x, (tile_y + 1) * tile + base_y),
                )

    reduced = canvas.resize((logical_span, logical_span), Image.Resampling.LANCZOS)
    pattern = reduced.crop((tile, tile, tile * 2, tile * 2))

    # Neutral hidden color prevents dark/white fringe artifacts in texture
    # importers that interpolate straight-alpha RGB beyond the visible edge.
    pixels = np.asarray(pattern).copy()
    pixels[pixels[..., 3] == 0, :3] = np.asarray(PALETTE["accent"], dtype=np.uint8)

    # Keep the periodic drawing's natural transition while also making the
    # exported boundary texels exactly identical. This is helpful for engines
    # and validation tools that compare the first/last rows directly instead of
    # sampling the texture with wrap-aware bilinear filtering.
    vertical_edge = (
        pixels[:, 0].astype(np.uint16) + pixels[:, -1].astype(np.uint16) + 1
    ) // 2
    pixels[:, 0] = vertical_edge.astype(np.uint8)
    pixels[:, -1] = pixels[:, 0]
    horizontal_edge = (
        pixels[0].astype(np.uint16) + pixels[-1].astype(np.uint16) + 1
    ) // 2
    pixels[0] = horizontal_edge.astype(np.uint8)
    pixels[-1] = pixels[0]

    pattern = Image.fromarray(pixels, mode="RGBA")
    pattern.save(output_path, format="PNG", compress_level=9)


def composite_over_solid(pattern: np.ndarray, solid: tuple[int, int, int]) -> np.ndarray:
    alpha = pattern[..., 3:4].astype(np.float32) / 255.0
    foreground = pattern[..., :3].astype(np.float32)
    background = np.asarray(solid, dtype=np.float32)[None, None, :]
    return foreground * alpha + background * (1.0 - alpha)


def verify_outputs(background_path: Path, pattern_path: Path) -> None:
    with Image.open(background_path) as background_image:
        if background_image.size != BACKGROUND_SIZE or background_image.mode != "RGB":
            raise RuntimeError(
                f"Unexpected background: {background_image.size}, {background_image.mode}"
            )

    with Image.open(pattern_path) as pattern_image:
        if pattern_image.size != (PATTERN_SIZE, PATTERN_SIZE) or pattern_image.mode != "RGBA":
            raise RuntimeError(
                f"Unexpected pattern: {pattern_image.size}, {pattern_image.mode}"
            )
        pattern = np.asarray(pattern_image)

    alpha = pattern[..., 3]
    if not (4.0 <= np.count_nonzero(alpha) / alpha.size * 100.0 <= 45.0):
        raise RuntimeError("Pattern motif coverage is outside the intended subtle range")
    if int(alpha.max()) > 62:
        raise RuntimeError("Pattern opacity is too strong for a background overlay")
    if not np.array_equal(pattern[:, 0], pattern[:, -1]):
        raise RuntimeError("Pattern left/right boundary texels do not match")
    if not np.array_equal(pattern[0], pattern[-1]):
        raise RuntimeError("Pattern top/bottom boundary texels do not match")

    # Check the visual step across the wrap seam after compositing on the target
    # palette. Values are compared with ordinary one-pixel steps in the tile.
    composite = composite_over_solid(pattern, PALETTE["violet"])
    seam_x = float(np.abs(composite[:, 0] - composite[:, -1]).mean())
    seam_y = float(np.abs(composite[0, :] - composite[-1, :]).mean())
    internal_x = float(np.abs(composite[:, 1:] - composite[:, :-1]).mean())
    internal_y = float(np.abs(composite[1:, :] - composite[:-1, :]).mean())
    if seam_x > max(1.0, internal_x * 3.0) or seam_y > max(1.0, internal_y * 3.0):
        raise RuntimeError(
            f"Pattern seam discontinuity: x={seam_x:.3f}, y={seam_y:.3f}, "
            f"internal=({internal_x:.3f}, {internal_y:.3f})"
        )

    print(
        "Verified:",
        f"background={BACKGROUND_SIZE[0]}x{BACKGROUND_SIZE[1]} RGB;",
        f"pattern={PATTERN_SIZE}x{PATTERN_SIZE} RGBA;",
        f"coverage={np.count_nonzero(alpha) / alpha.size * 100.0:.2f}%;",
        f"max_alpha={int(alpha.max())};",
        f"seam_mean=({seam_x:.3f}, {seam_y:.3f})",
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="verify existing outputs without regenerating them",
    )
    args = parser.parse_args()

    generated_assets = Path(__file__).resolve().parents[2]
    output_dir = generated_assets / "Backgrounds"
    output_dir.mkdir(parents=True, exist_ok=True)
    background_path = output_dir / "gameplay_background_purple.png"
    pattern_path = output_dir / "gameplay_block_pattern_512.png"

    if not args.check_only:
        generate_gameplay_background(background_path)
        generate_seamless_pattern(pattern_path)
    verify_outputs(background_path, pattern_path)


if __name__ == "__main__":
    main()
