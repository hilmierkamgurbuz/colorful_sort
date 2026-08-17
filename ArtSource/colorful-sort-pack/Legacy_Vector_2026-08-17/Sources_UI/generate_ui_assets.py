#!/usr/bin/env python3
"""Generate the Colorful Sort UI SVG masters and transparent PNG sprites.

The artwork is intentionally text-free. Every visible base color comes from the
locked project palette below. SVG files remain editable; PNG files are rendered
from those masters with macOS ImageIO (`sips`) for deterministic output.
"""

from __future__ import annotations

import math
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


PALETTE = {
    "purple": "#7259FF",
    "outline": "#4237A1",
    "shadow": "#2A1D65",
    "yellow": "#FEC901",
    "orange": "#F08A00",
    "green": "#00D44C",
    "green_bevel": "#00A936",
    "red": "#FE3C00",
    "red_bevel": "#C91A13",
    "lavender": "#9A99FF",
    "cream": "#FFF6D6",
}

PURPLE = PALETTE["purple"]
OUTLINE = PALETTE["outline"]
SHADOW = PALETTE["shadow"]
YELLOW = PALETTE["yellow"]
ORANGE = PALETTE["orange"]
GREEN = PALETTE["green"]
GREEN_BEVEL = PALETTE["green_bevel"]
RED = PALETTE["red"]
RED_BEVEL = PALETTE["red_bevel"]
LAVENDER = PALETTE["lavender"]
CREAM = PALETTE["cream"]

SCRIPT_DIR = Path(__file__).resolve().parent
GENERATED_ROOT = SCRIPT_DIR.parents[1]
PNG_ROOT = GENERATED_ROOT / "UI"
SVG_ROOT = SCRIPT_DIR / "SVG"


def n(value: float) -> str:
    rounded = round(value, 3)
    if rounded == int(rounded):
        return str(int(rounded))
    return f"{rounded:.3f}".rstrip("0").rstrip(".")


def attrs(**items: object) -> str:
    rendered: list[str] = []
    for key, value in items.items():
        if value is None:
            continue
        key = key.rstrip("_").replace("_", "-")
        if isinstance(value, float):
            value = n(value)
        rendered.append(f'{key}="{value}"')
    return " ".join(rendered)


def rect(
    x: float,
    y: float,
    width: float,
    height: float,
    radius: float,
    fill: str,
    *,
    stroke: str | None = None,
    stroke_width: float | None = None,
    opacity: float | None = None,
    role: str | None = None,
) -> str:
    return (
        "<rect "
        + attrs(
            id=role,
            x=n(x),
            y=n(y),
            width=n(width),
            height=n(height),
            rx=n(radius),
            fill=fill,
            stroke=stroke,
            stroke_width=n(stroke_width) if stroke_width else None,
            opacity=opacity,
        )
        + "/>"
    )


def circle(
    cx: float,
    cy: float,
    radius: float,
    fill: str,
    *,
    stroke: str | None = None,
    stroke_width: float | None = None,
    opacity: float | None = None,
    role: str | None = None,
) -> str:
    return (
        "<circle "
        + attrs(
            id=role,
            cx=n(cx),
            cy=n(cy),
            r=n(radius),
            fill=fill,
            stroke=stroke,
            stroke_width=n(stroke_width) if stroke_width else None,
            opacity=opacity,
        )
        + "/>"
    )


def ellipse(
    cx: float,
    cy: float,
    rx: float,
    ry: float,
    fill: str,
    *,
    opacity: float | None = None,
    role: str | None = None,
) -> str:
    return (
        "<ellipse "
        + attrs(id=role, cx=n(cx), cy=n(cy), rx=n(rx), ry=n(ry), fill=fill, opacity=opacity)
        + "/>"
    )


def path(
    data: str,
    *,
    fill: str = "none",
    stroke: str | None = None,
    stroke_width: float | None = None,
    opacity: float | None = None,
    linecap: str | None = None,
    linejoin: str | None = None,
    fill_rule: str | None = None,
    role: str | None = None,
) -> str:
    return (
        "<path "
        + attrs(
            id=role,
            d=data,
            fill=fill,
            stroke=stroke,
            stroke_width=n(stroke_width) if stroke_width else None,
            opacity=opacity,
            stroke_linecap=linecap,
            stroke_linejoin=linejoin,
            fill_rule=fill_rule,
        )
        + "/>"
    )


def polygon(
    points: list[tuple[float, float]],
    fill: str,
    *,
    stroke: str | None = None,
    stroke_width: float | None = None,
    opacity: float | None = None,
    role: str | None = None,
) -> str:
    value = " ".join(f"{n(x)},{n(y)}" for x, y in points)
    return (
        "<polygon "
        + attrs(
            id=role,
            points=value,
            fill=fill,
            stroke=stroke,
            stroke_width=n(stroke_width) if stroke_width else None,
            opacity=opacity,
            stroke_linejoin="round",
        )
        + "/>"
    )


def svg_document(name: str, width: int, height: int, body: list[str]) -> str:
    palette_comment = ", ".join(f"{key} {value}" for key, value in PALETTE.items())
    return "\n".join(
        [
            '<?xml version="1.0" encoding="UTF-8"?>',
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
            f'viewBox="0 0 {width} {height}" fill="none" shape-rendering="geometricPrecision">',
            f"  <title>{name}</title>",
            "  <desc>Text-free editable UI master with a transparent canvas.</desc>",
            f"  <!-- Locked palette: {palette_comment} -->",
            '  <g id="artwork" stroke-linejoin="round">',
            *(f"    {item}" for item in body),
            "  </g>",
            "</svg>",
            "",
        ]
    )


PATH_TOKEN = re.compile(r"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")


def hex_rgb(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    return tuple(int(value[index : index + 2], 16) for index in (0, 2, 4))  # type: ignore[return-value]


def arc_segment(
    start: tuple[float, float],
    rx: float,
    ry: float,
    rotation: float,
    large_arc: int,
    sweep: int,
    end: tuple[float, float],
) -> list[tuple[float, float]]:
    """Sample one absolute SVG elliptical-arc segment."""
    x1, y1 = start
    x2, y2 = end
    rx, ry = abs(rx), abs(ry)
    if rx == 0 or ry == 0 or (x1 == x2 and y1 == y2):
        return [end]

    phi = math.radians(rotation % 360)
    cos_phi, sin_phi = math.cos(phi), math.sin(phi)
    dx, dy = (x1 - x2) / 2, (y1 - y2) / 2
    x1p = cos_phi * dx + sin_phi * dy
    y1p = -sin_phi * dx + cos_phi * dy

    scale = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry)
    if scale > 1:
        factor = math.sqrt(scale)
        rx *= factor
        ry *= factor

    numerator = max(
        0.0,
        rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p,
    )
    denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p
    coefficient = 0.0 if denominator == 0 else math.sqrt(numerator / denominator)
    if large_arc == sweep:
        coefficient *= -1
    cxp = coefficient * (rx * y1p / ry)
    cyp = coefficient * (-ry * x1p / rx)
    cx = cos_phi * cxp - sin_phi * cyp + (x1 + x2) / 2
    cy = sin_phi * cxp + cos_phi * cyp + (y1 + y2) / 2

    def vector_angle(ux: float, uy: float, vx: float, vy: float) -> float:
        dot = ux * vx + uy * vy
        length = math.hypot(ux, uy) * math.hypot(vx, vy)
        ratio = max(-1.0, min(1.0, dot / length)) if length else 1.0
        angle = math.acos(ratio)
        return -angle if ux * vy - uy * vx < 0 else angle

    ux, uy = (x1p - cxp) / rx, (y1p - cyp) / ry
    vx, vy = (-x1p - cxp) / rx, (-y1p - cyp) / ry
    theta = vector_angle(1, 0, ux, uy)
    delta = vector_angle(ux, uy, vx, vy)
    if not sweep and delta > 0:
        delta -= math.tau
    elif sweep and delta < 0:
        delta += math.tau

    sample_count = max(12, int(abs(delta) * 18))
    samples: list[tuple[float, float]] = []
    for index in range(1, sample_count + 1):
        angle = theta + delta * index / sample_count
        x = cx + cos_phi * rx * math.cos(angle) - sin_phi * ry * math.sin(angle)
        y = cy + sin_phi * rx * math.cos(angle) + cos_phi * ry * math.sin(angle)
        samples.append((x, y))
    return samples


def parse_path(data: str) -> list[tuple[list[tuple[float, float]], bool]]:
    tokens = PATH_TOKEN.findall(data)
    index = 0
    command: str | None = None
    current = (0.0, 0.0)
    start = current
    active: list[tuple[float, float]] = []
    subpaths: list[tuple[list[tuple[float, float]], bool]] = []

    def number() -> float:
        nonlocal index
        value = float(tokens[index])
        index += 1
        return value

    def flush(closed: bool = False) -> None:
        nonlocal active
        if active:
            subpaths.append((active, closed))
            active = []

    while index < len(tokens):
        if tokens[index].isalpha():
            command = tokens[index]
            index += 1
            if command in {"Z", "z"}:
                if active and active[-1] != start:
                    active.append(start)
                flush(True)
                current = start
                command = None
                continue

        if command == "M":
            flush(False)
            current = (number(), number())
            start = current
            active = [current]
            command = "L"
        elif command == "L":
            current = (number(), number())
            active.append(current)
        elif command == "H":
            current = (number(), current[1])
            active.append(current)
        elif command == "V":
            current = (current[0], number())
            active.append(current)
        elif command == "C":
            control1 = (number(), number())
            control2 = (number(), number())
            end = (number(), number())
            x0, y0 = current
            for step in range(1, 25):
                t = step / 24
                inv = 1 - t
                x = (
                    inv**3 * x0
                    + 3 * inv * inv * t * control1[0]
                    + 3 * inv * t * t * control2[0]
                    + t**3 * end[0]
                )
                y = (
                    inv**3 * y0
                    + 3 * inv * inv * t * control1[1]
                    + 3 * inv * t * t * control2[1]
                    + t**3 * end[1]
                )
                active.append((x, y))
            current = end
        elif command == "A":
            rx, ry, rotation = number(), number(), number()
            large_arc, sweep = int(number()), int(number())
            end = (number(), number())
            active.extend(arc_segment(current, rx, ry, rotation, large_arc, sweep, end))
            current = end
        elif command is None:
            continue
        else:
            raise ValueError(f"Unsupported SVG path command {command!r} in {data!r}")

    flush(False)
    return subpaths


def render_svg_to_png(svg_path: Path, png_path: Path, width: int, height: int) -> None:
    """Rasterize the small SVG subset emitted by this generator with Pillow."""
    scale = 3
    size = (width * scale, height * scale)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    root = ET.parse(svg_path).getroot()
    artwork = next(element for element in root if element.tag.endswith("g"))

    def scaled_points(points: list[tuple[float, float]]) -> list[tuple[int, int]]:
        return [(round(x * scale), round(y * scale)) for x, y in points]

    def composite_mask(mask: Image.Image, color: str, opacity: float) -> None:
        nonlocal canvas
        red, green, blue = hex_rgb(color)
        if opacity < 1:
            mask = mask.point(lambda value: round(value * opacity))
        layer = Image.new("RGBA", size, (red, green, blue, 0))
        layer.putalpha(mask)
        canvas = Image.alpha_composite(canvas, layer)

    for element in artwork:
        tag = element.tag.rsplit("}", 1)[-1]
        fill = element.get("fill", "none")
        stroke = element.get("stroke")
        opacity = float(element.get("opacity", "1"))
        stroke_width = round(float(element.get("stroke-width", "0")) * scale)
        fill_mask = Image.new("L", size, 0)
        stroke_mask = Image.new("L", size, 0)
        fill_draw = ImageDraw.Draw(fill_mask)
        stroke_draw = ImageDraw.Draw(stroke_mask)

        if tag == "rect":
            x = float(element.get("x", "0")) * scale
            y = float(element.get("y", "0")) * scale
            w = float(element.get("width", "0")) * scale
            h = float(element.get("height", "0")) * scale
            radius = float(element.get("rx", "0")) * scale
            box = (round(x), round(y), round(x + w), round(y + h))
            if fill != "none":
                fill_draw.rounded_rectangle(box, radius=round(radius), fill=255)
            if stroke and stroke_width:
                stroke_draw.rounded_rectangle(
                    box,
                    radius=round(radius),
                    outline=255,
                    width=stroke_width,
                )
        elif tag in {"circle", "ellipse"}:
            cx = float(element.get("cx", "0")) * scale
            cy = float(element.get("cy", "0")) * scale
            rx = float(element.get("r", element.get("rx", "0"))) * scale
            ry = float(element.get("r", element.get("ry", "0"))) * scale
            box = (round(cx - rx), round(cy - ry), round(cx + rx), round(cy + ry))
            if fill != "none":
                fill_draw.ellipse(box, fill=255)
            if stroke and stroke_width:
                stroke_draw.ellipse(box, outline=255, width=stroke_width)
        elif tag == "polygon":
            points = [
                tuple(float(value) for value in pair.split(","))
                for pair in element.get("points", "").split()
            ]
            rendered = scaled_points(points)  # type: ignore[arg-type]
            if fill != "none":
                fill_draw.polygon(rendered, fill=255)
            if stroke and stroke_width:
                stroke_draw.line(rendered + [rendered[0]], fill=255, width=stroke_width, joint="curve")
        elif tag == "path":
            subpaths = parse_path(element.get("d", ""))
            if fill != "none":
                evenodd = element.get("fill-rule") == "evenodd"
                for points, _closed in subpaths:
                    rendered = scaled_points(points)
                    if len(rendered) < 3:
                        continue
                    if evenodd:
                        temporary = Image.new("L", size, 0)
                        ImageDraw.Draw(temporary).polygon(rendered, fill=255)
                        fill_mask = ImageChops.difference(fill_mask, temporary)
                    else:
                        ImageDraw.Draw(fill_mask).polygon(rendered, fill=255)
            if stroke and stroke_width:
                for points, closed in subpaths:
                    rendered = scaled_points(points)
                    if len(rendered) < 2:
                        continue
                    stroke_draw.line(rendered, fill=255, width=stroke_width, joint="curve")
                    if element.get("stroke-linecap") == "round" and not closed:
                        radius = stroke_width // 2
                        for x, y in (rendered[0], rendered[-1]):
                            stroke_draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=255)
        else:
            raise ValueError(f"Unsupported SVG element: {tag}")

        if fill != "none":
            composite_mask(fill_mask, fill, opacity)
        if stroke:
            composite_mask(stroke_mask, stroke, opacity)

    canvas.resize((width, height), Image.Resampling.LANCZOS).save(png_path, format="PNG")


def write_asset(category: str, name: str, width: int, height: int, body: list[str]) -> None:
    svg_dir = SVG_ROOT / category
    png_dir = PNG_ROOT / category
    svg_dir.mkdir(parents=True, exist_ok=True)
    png_dir.mkdir(parents=True, exist_ok=True)

    svg_path = svg_dir / f"{name}.svg"
    png_path = png_dir / f"{name}.png"
    svg_path.write_text(svg_document(name, width, height, body), encoding="utf-8")
    render_svg_to_png(svg_path, png_path, width, height)


def raised_button(
    width: int,
    height: int,
    *,
    x: float,
    y: float,
    button_width: float,
    button_height: float,
    radius: float,
    face: str,
    bevel: str,
    state: str,
) -> list[str]:
    if state not in {"normal", "pressed", "disabled"}:
        raise ValueError(f"Unknown state: {state}")

    pressed_shift = 18 if state == "pressed" else 0
    shadow_offset = 12 if state == "pressed" else 28
    y += pressed_shift
    surface_opacity = 0.58 if state == "disabled" else 1.0
    if state == "disabled":
        face = LAVENDER
        bevel = PURPLE

    inset = max(10, round(min(button_width, button_height) * 0.045))
    bevel_depth = max(22, round(button_height * 0.18))
    inner_radius = max(12, radius - inset)

    items = [
        rect(
            x + 4,
            y + shadow_offset,
            button_width - 8,
            button_height,
            radius,
            SHADOW,
            opacity=0.95,
            role="shadow",
        ),
        rect(x, y, button_width, button_height, radius, OUTLINE, role="outline"),
        rect(
            x + inset,
            y + inset,
            button_width - inset * 2,
            button_height - inset * 2,
            inner_radius,
            bevel,
            opacity=surface_opacity,
            role="bevel",
        ),
        rect(
            x + inset,
            y + inset,
            button_width - inset * 2,
            button_height - inset * 2 - bevel_depth,
            inner_radius,
            face,
            opacity=surface_opacity,
            role="face",
        ),
    ]
    if state != "disabled":
        items.append(
            path(
                f"M {n(x + radius * 0.78)} {n(y + inset * 1.8)} "
                f"H {n(x + button_width - radius * 0.78)}",
                stroke=CREAM,
                stroke_width=max(5, round(height * 0.025)),
                opacity=0.34,
                linecap="round",
                role="highlight",
            )
        )
    return items


def level_button(state: str) -> list[str]:
    return raised_button(
        1024,
        384,
        x=56,
        y=34,
        button_width=912,
        button_height=286,
        radius=108,
        face=GREEN,
        bevel=GREEN_BEVEL,
        state=state,
    )


def hud_pill() -> list[str]:
    return [
        rect(28, 46, 584, 126, 52, SHADOW, opacity=0.92, role="shadow"),
        rect(24, 24, 592, 132, 52, OUTLINE, role="outline"),
        rect(36, 36, 568, 108, 42, LAVENDER, role="lower_bevel"),
        rect(36, 36, 568, 82, 42, CREAM, role="face"),
        path(
            "M 82 51 H 558",
            stroke=CREAM,
            stroke_width=8,
            opacity=0.48,
            linecap="round",
            role="highlight",
        ),
    ]


def coin_icon() -> list[str]:
    return [
        circle(96, 104, 76, SHADOW, opacity=0.95, role="shadow"),
        circle(96, 92, 78, OUTLINE, role="outline"),
        circle(96, 92, 65, ORANGE, role="rim"),
        circle(96, 86, 52, YELLOW, role="face"),
        circle(78, 65, 13, CREAM, opacity=0.52, role="shine"),
        path(
            "M 126 66 C 103 48 66 59 62 88 C 58 117 88 133 116 117 "
            "M 116 117 L 107 105 M 116 117 L 101 121",
            stroke=ORANGE,
            stroke_width=10,
            linecap="round",
            linejoin="round",
            role="emboss",
        ),
    ]


def heart_icon() -> list[str]:
    heart = (
        "M 96 158 C 82 143 34 113 28 76 C 22 39 65 18 96 52 "
        "C 127 18 170 39 164 76 C 158 113 110 143 96 158 Z"
    )
    return [
        path(heart, fill=SHADOW, opacity=0.95, role="shadow"),
        path(
            "M 96 149 C 82 135 42 108 36 76 C 31 48 65 33 96 66 "
            "C 127 33 161 48 156 76 C 150 108 110 135 96 149 Z",
            fill=RED,
            stroke=OUTLINE,
            stroke_width=12,
            role="heart",
        ),
        path(
            "M 53 68 C 58 51 75 49 84 61",
            stroke=CREAM,
            stroke_width=10,
            opacity=0.52,
            linecap="round",
            role="shine",
        ),
    ]


def plus_icon() -> list[str]:
    return [
        circle(80, 88, 64, SHADOW, opacity=0.95, role="shadow"),
        circle(80, 78, 64, OUTLINE, role="outline"),
        circle(80, 78, 52, GREEN_BEVEL, role="bevel"),
        circle(80, 72, 48, GREEN, role="face"),
        path(
            "M 80 43 V 101 M 51 72 H 109",
            stroke=OUTLINE,
            stroke_width=25,
            linecap="round",
            role="glyph_outline",
        ),
        path(
            "M 80 43 V 101 M 51 72 H 109",
            stroke=CREAM,
            stroke_width=13,
            linecap="round",
            role="glyph",
        ),
    ]


def settings_shell(state: str) -> list[str]:
    return raised_button(
        256,
        256,
        x=28,
        y=20,
        button_width=200,
        button_height=196,
        radius=54,
        face=PURPLE,
        bevel=OUTLINE,
        state=state,
    )


def gear_path(cx: float, cy: float, outer: float, root: float, hole: float) -> str:
    points: list[tuple[float, float]] = []
    steps = 32
    for index in range(steps):
        angle = -math.pi / 2 + index * math.tau / steps
        phase = index % 4
        radius = outer if phase in {0, 1} else root
        points.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius))
    outer_path = "M " + " L ".join(f"{n(x)} {n(y)}" for x, y in points) + " Z"
    hole_path = (
        f"M {n(cx + hole)} {n(cy)} "
        f"A {n(hole)} {n(hole)} 0 1 0 {n(cx - hole)} {n(cy)} "
        f"A {n(hole)} {n(hole)} 0 1 0 {n(cx + hole)} {n(cy)} Z"
    )
    return outer_path + " " + hole_path


def gear_icon() -> list[str]:
    data = gear_path(96, 94, 72, 55, 24)
    return [
        path(data, fill=SHADOW, opacity=0.9, fill_rule="evenodd", role="shadow"),
        path(
            gear_path(96, 86, 72, 55, 24),
            fill=CREAM,
            stroke=OUTLINE,
            stroke_width=10,
            fill_rule="evenodd",
            role="gear",
        ),
        path(
            "M 63 52 C 76 40 93 37 108 41",
            stroke=CREAM,
            stroke_width=7,
            opacity=0.45,
            linecap="round",
            role="shine",
        ),
    ]


def popup_body() -> list[str]:
    return [
        rect(50, 54, 924, 1178, 88, SHADOW, opacity=0.98, role="shadow"),
        rect(42, 26, 940, 1180, 88, OUTLINE, role="outline"),
        rect(60, 44, 904, 1144, 72, PURPLE, role="panel"),
        rect(
            78,
            62,
            868,
            1108,
            58,
            "none",
            stroke=LAVENDER,
            stroke_width=8,
            opacity=0.76,
            role="inner_border",
        ),
        path(
            "M 126 82 H 898",
            stroke=CREAM,
            stroke_width=8,
            opacity=0.18,
            linecap="round",
            role="highlight",
        ),
    ]


def popup_header() -> list[str]:
    return [
        rect(48, 64, 928, 220, 82, SHADOW, opacity=0.96, role="shadow"),
        rect(38, 24, 948, 242, 86, OUTLINE, role="outline"),
        rect(54, 40, 916, 210, 70, ORANGE, role="orange_bevel"),
        rect(54, 40, 916, 160, 70, YELLOW, role="yellow_face"),
        path(
            "M 126 62 H 898",
            stroke=CREAM,
            stroke_width=9,
            opacity=0.42,
            linecap="round",
            role="highlight",
        ),
    ]


def square_green(state: str) -> list[str]:
    return raised_button(
        320,
        320,
        x=36,
        y=24,
        button_width=248,
        button_height=244,
        radius=66,
        face=GREEN,
        bevel=GREEN_BEVEL,
        state=state,
    )


def wide_button(color: str, state: str) -> list[str]:
    choices = {
        "lavender": (LAVENDER, PURPLE),
        "green": (GREEN, GREEN_BEVEL),
        "red": (RED, RED_BEVEL),
    }
    face, bevel = choices[color]
    return raised_button(
        896,
        256,
        x=44,
        y=24,
        button_width=808,
        button_height=178,
        radius=70,
        face=face,
        bevel=bevel,
        state=state,
    )


def close_shell(state: str) -> list[str]:
    shift = 14 if state == "pressed" else 0
    shadow_dy = 9 if state == "pressed" else 24
    opacity = 0.58 if state == "disabled" else 1.0
    face = LAVENDER if state == "disabled" else RED
    bevel = PURPLE if state == "disabled" else RED_BEVEL
    cy = 108 + shift
    items = [
        circle(128, cy + shadow_dy, 94, SHADOW, opacity=0.95, role="shadow"),
        circle(128, cy, 100, OUTLINE, role="outline"),
        circle(128, cy, 87, bevel, opacity=opacity, role="bevel"),
        circle(128, cy - 10, 78, face, opacity=opacity, role="face"),
    ]
    if state != "disabled":
        items.append(
            path(
                "M 86 71 C 107 49 142 45 166 62",
                stroke=CREAM,
                stroke_width=8,
                opacity=0.4,
                linecap="round",
                role="highlight",
            )
        )
    return items


def close_icon() -> list[str]:
    data = "M 51 49 L 141 139 M 141 49 L 51 139"
    return [
        path(
            data,
            stroke=OUTLINE,
            stroke_width=42,
            linecap="round",
            role="glyph_outline",
        ),
        path(data, stroke=CREAM, stroke_width=24, linecap="round", role="glyph"),
    ]


def profile_icon() -> list[str]:
    return [
        circle(96, 62, 40, OUTLINE, role="head_outline"),
        circle(96, 62, 28, CREAM, role="head"),
        path(
            "M 33 163 C 35 116 61 96 96 96 C 131 96 157 116 159 163 Z",
            fill=OUTLINE,
            role="body_outline",
        ),
        path(
            "M 49 151 C 53 121 71 108 96 108 C 121 108 139 121 143 151 Z",
            fill=CREAM,
            role="body",
        ),
        path(
            "M 75 43 C 87 34 106 34 118 43",
            stroke=CREAM,
            stroke_width=7,
            opacity=0.42,
            linecap="round",
            role="shine",
        ),
    ]


def restart_icon() -> list[str]:
    curve = "M 145 77 C 128 39 77 29 46 60 C 18 88 31 137 69 151 C 98 162 127 149 140 126"
    arrow_outline = [(34, 39), (79, 43), (52, 84)]
    arrow = [(43, 50), (66, 52), (52, 72)]
    return [
        path(
            curve,
            stroke=OUTLINE,
            stroke_width=42,
            linecap="round",
            linejoin="round",
            role="curve_outline",
        ),
        path(
            curve,
            stroke=CREAM,
            stroke_width=23,
            linecap="round",
            linejoin="round",
            role="curve",
        ),
        polygon(arrow_outline, OUTLINE, role="arrow_outline"),
        polygon(arrow, CREAM, role="arrow"),
    ]


def sound_icon() -> list[str]:
    speaker_outline = [(28, 77), (57, 77), (96, 43), (96, 145), (57, 111), (28, 111)]
    speaker = [(39, 86), (63, 86), (84, 68), (84, 120), (63, 102), (39, 102)]
    return [
        polygon(speaker_outline, OUTLINE, role="speaker_outline"),
        polygon(speaker, CREAM, role="speaker"),
        path(
            "M 119 70 C 138 82 138 106 119 118 M 136 49 C 173 72 173 116 136 139",
            stroke=OUTLINE,
            stroke_width=27,
            linecap="round",
            role="waves_outline",
        ),
        path(
            "M 119 70 C 138 82 138 106 119 118 M 136 49 C 173 72 173 116 136 139",
            stroke=CREAM,
            stroke_width=13,
            linecap="round",
            role="waves",
        ),
    ]


def vibration_icon() -> list[str]:
    return [
        path(
            "M 34 49 C 19 65 19 81 34 96 C 19 111 19 127 34 143 "
            "M 158 49 C 173 65 173 81 158 96 C 173 111 173 127 158 143",
            stroke=OUTLINE,
            stroke_width=28,
            linecap="round",
            linejoin="round",
            role="waves_outline",
        ),
        path(
            "M 34 49 C 19 65 19 81 34 96 C 19 111 19 127 34 143 "
            "M 158 49 C 173 65 173 81 158 96 C 173 111 173 127 158 143",
            stroke=CREAM,
            stroke_width=14,
            linecap="round",
            linejoin="round",
            role="waves",
        ),
        rect(52, 29, 88, 136, 24, OUTLINE, role="phone_outline"),
        rect(64, 41, 64, 112, 16, CREAM, role="phone"),
        rect(77, 57, 38, 68, 8, PURPLE, role="screen"),
        circle(96, 140, 6, PURPLE, role="home_dot"),
    ]


def off_slash_icon() -> list[str]:
    data = "M 43 154 L 151 38"
    return [
        path(data, stroke=OUTLINE, stroke_width=38, linecap="round", role="slash_outline"),
        path(data, stroke=RED, stroke_width=22, linecap="round", role="slash"),
        path(
            "M 63 137 L 137 57",
            stroke=CREAM,
            stroke_width=5,
            opacity=0.35,
            linecap="round",
            role="highlight",
        ),
    ]


def generate() -> None:
    for state in ("normal", "pressed", "disabled"):
        write_asset("Buttons", f"level_button_{state}", 1024, 384, level_button(state))

    write_asset("HUD", "hud_pill_9slice", 640, 192, hud_pill())
    write_asset("HUD", "coin", 192, 192, coin_icon())
    write_asset("HUD", "heart", 192, 192, heart_icon())
    write_asset("HUD", "plus", 160, 160, plus_icon())

    for state in ("normal", "pressed", "disabled"):
        write_asset("Settings", f"settings_shell_{state}", 256, 256, settings_shell(state))
    write_asset("Settings", "gear", 192, 192, gear_icon())

    write_asset("Popups", "popup_body_9slice", 1024, 1280, popup_body())
    write_asset("Popups", "popup_header_9slice", 1024, 320, popup_header())

    for state in ("normal", "pressed", "disabled"):
        write_asset("Buttons", f"square_green_{state}", 320, 320, square_green(state))
        write_asset("Buttons", f"close_shell_{state}", 256, 256, close_shell(state))
        for color in ("lavender", "green", "red"):
            write_asset(
                "Buttons",
                f"wide_{color}_{state}",
                896,
                256,
                wide_button(color, state),
            )

    write_asset("Icons", "close", 192, 192, close_icon())
    write_asset("Icons", "profile", 192, 192, profile_icon())
    write_asset("Icons", "restart", 192, 192, restart_icon())
    write_asset("Icons", "sound", 192, 192, sound_icon())
    write_asset("Icons", "vibration", 192, 192, vibration_icon())
    write_asset("Icons", "off_slash", 192, 192, off_slash_icon())


if __name__ == "__main__":
    generate()
