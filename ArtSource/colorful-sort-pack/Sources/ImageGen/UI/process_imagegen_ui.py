#!/usr/bin/env python3
"""Normalize built-in ImageGen UI masters into Unity-ready PNG sprites.

Every visible source pixel comes from an ImageGen raster master. This script
only trims transparent padding, resizes, recolors the green face for alternate
button families, and derives pressed/disabled raster states. It creates no SVG
or vector geometry.
"""

from __future__ import annotations

import colorsys
from pathlib import Path

from PIL import Image, ImageEnhance


HERE = Path(__file__).resolve().parent
RAW = HERE / "Buttons"
PACK_ROOT = HERE.parents[2]
OUT = PACK_ROOT / "UI"


def load_trimmed(name: str) -> Image.Image:
    image = Image.open(RAW / name).convert("RGBA")
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= 3 else 0)
    bbox = mask.getbbox()
    if not bbox:
        raise RuntimeError(f"ImageGen master is empty: {name}")
    image = image.crop(bbox)
    alpha = image.getchannel("A").point(
        lambda value: 0 if value < 3 else (255 if value > 250 else value)
    )
    image.putalpha(alpha)
    return image


def fit(
    image: Image.Image,
    size: tuple[int, int],
    *,
    pad_x: float = 0.035,
    pad_y: float = 0.035,
    force_fill: bool = False,
) -> Image.Image:
    target_w, target_h = size
    inner_w = max(1, round(target_w * (1 - 2 * pad_x)))
    inner_h = max(1, round(target_h * (1 - 2 * pad_y)))
    if force_fill:
        resized = image.resize((inner_w, inner_h), Image.Resampling.LANCZOS)
    else:
        scale = min(inner_w / image.width, inner_h / image.height)
        resized = image.resize(
            (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
            Image.Resampling.LANCZOS,
        )
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (target_w - resized.width) // 2
    y = (target_h - resized.height) // 2
    canvas.alpha_composite(resized, (x, y))
    return canvas


def recolor_green_face(image: Image.Image, target_hex: str) -> Image.Image:
    tr = int(target_hex[1:3], 16) / 255
    tg = int(target_hex[3:5], 16) / 255
    tb = int(target_hex[5:7], 16) / 255
    target_h, target_s, _ = colorsys.rgb_to_hsv(tr, tg, tb)
    pixels = []
    for red, green, blue, alpha in image.getdata():
        if alpha == 0:
            pixels.append((red, green, blue, alpha))
            continue
        h, s, v = colorsys.rgb_to_hsv(red / 255, green / 255, blue / 255)
        # ImageGen master face is vivid green; purple rim/shadow remains unchanged.
        if 0.19 <= h <= 0.48 and s >= 0.28 and green > red * 1.05:
            if target_s < 0.7:
                out_s = min(0.52, max(0.36, target_s))
                out_v = min(1.0, v * 1.08)
            else:
                out_s = max(s, target_s * 0.82)
                out_v = v
            nr, ng, nb = colorsys.hsv_to_rgb(target_h, out_s, out_v)
            pixels.append((round(nr * 255), round(ng * 255), round(nb * 255), alpha))
        else:
            pixels.append((red, green, blue, alpha))
    result = Image.new("RGBA", image.size)
    result.putdata(pixels)
    return result


def pressed_state(normal: Image.Image) -> Image.Image:
    sprite = ImageEnhance.Brightness(normal).enhance(0.83)
    new_h = max(1, round(normal.height * 0.91))
    sprite = sprite.resize((normal.width, new_h), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", normal.size, (0, 0, 0, 0))
    canvas.alpha_composite(sprite, (0, normal.height - new_h))
    return canvas


def disabled_state(normal: Image.Image) -> Image.Image:
    alpha = normal.getchannel("A")
    rgb = normal.convert("RGB")
    rgb = ImageEnhance.Color(rgb).enhance(0.16)
    tint = Image.new("RGB", normal.size, "#8B84C8")
    rgb = Image.blend(rgb, tint, 0.34)
    rgb = ImageEnhance.Brightness(rgb).enhance(0.88)
    result = rgb.convert("RGBA")
    result.putalpha(alpha)
    return result


def save(path: Path, image: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, "PNG", optimize=True)


def save_states(stem: str, normal: Image.Image) -> None:
    directory = OUT / "Buttons"
    save(directory / f"{stem}_normal.png", normal)
    save(directory / f"{stem}_pressed.png", pressed_state(normal))
    save(directory / f"{stem}_disabled.png", disabled_state(normal))


def main() -> None:
    level_master = load_trimmed("level_button_imagegen_raw.png")
    square_master = load_trimmed("square_green_imagegen_raw.png")
    lavender_master = load_trimmed("wide_lavender_imagegen_raw.png")
    red_wide_master = load_trimmed("wide_red_imagegen_raw.png")
    close_master = load_trimmed("close_shell_imagegen_raw.png")
    settings_master = load_trimmed("settings_shell_imagegen_raw.png")
    hud_master = load_trimmed("hud_pill_imagegen_raw.png")
    body_master = load_trimmed("popup_body_imagegen_raw.png")
    header_master = load_trimmed("popup_header_imagegen_raw.png")

    level = fit(level_master, (1024, 384), pad_x=0.018, pad_y=0.045, force_fill=True)
    save_states("level_button", level)

    square = fit(square_master, (320, 320), pad_x=0.035, pad_y=0.035)
    save_states("square_green", square)

    wide_green = fit(level_master, (896, 256), pad_x=0.024, pad_y=0.045, force_fill=True)
    save_states("wide_green", wide_green)
    save_states("wide_lavender", fit(lavender_master, (896, 256), pad_x=0.024, pad_y=0.045, force_fill=True))
    save_states("wide_red", fit(red_wide_master, (896, 256), pad_x=0.024, pad_y=0.045, force_fill=True))

    close = fit(close_master, (256, 256), pad_x=0.03, pad_y=0.03)
    save_states("close_shell", close)

    settings = fit(settings_master, (256, 256), pad_x=0.03, pad_y=0.03)
    for state, image in {
        "normal": settings,
        "pressed": pressed_state(settings),
        "disabled": disabled_state(settings),
    }.items():
        save(OUT / "Settings" / f"settings_shell_{state}.png", image)

    save(OUT / "HUD" / "hud_pill_9slice.png", fit(hud_master, (640, 192), pad_x=0.02, pad_y=0.05, force_fill=True))
    save(OUT / "Popups" / "popup_body_9slice.png", fit(body_master, (1024, 1280), pad_x=0.02, pad_y=0.02, force_fill=True))
    save(OUT / "Popups" / "popup_header_9slice.png", fit(header_master, (1024, 320), pad_x=0.02, pad_y=0.04, force_fill=True))

    for path in sorted(OUT.rglob("*.png")):
        with Image.open(path) as image:
            if image.mode != "RGBA":
                raise RuntimeError(f"Expected RGBA: {path}")
            if image.getchannel("A").getbbox() is None:
                raise RuntimeError(f"Empty output: {path}")
        print(path.relative_to(PACK_ROOT))


if __name__ == "__main__":
    main()
