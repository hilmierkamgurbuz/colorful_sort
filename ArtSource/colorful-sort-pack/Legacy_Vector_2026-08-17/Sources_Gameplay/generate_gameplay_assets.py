#!/usr/bin/env python3
"""Generate the modular gameplay sprite set from editable SVG masters.

Every sprite uses a 512 px logical cell and is intended for a 512 PPU import.
The SVG files are the editable source of truth. PNGs are rendered with macOS
SVG support, normalized to transparent RGBA, and tagged with import hints.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw, ImageFilter, PngImagePlugin


SOURCE_DIR = Path(__file__).resolve().parent
OUTPUT_DIR = SOURCE_DIR.parents[1] / "Gameplay"
LOGICAL_CELL_PX = 512
RECOMMENDED_PPU = 512


@dataclass(frozen=True)
class Asset:
    width: int
    height: int
    pivot: str
    body: str


DEFS = """
  <defs>
    <linearGradient id="slotMain" x1="0" y1="0" x2="0.9" y2="1">
      <stop offset="0" stop-color="#4B477F"/>
      <stop offset="0.52" stop-color="#39366B"/>
      <stop offset="1" stop-color="#2A2858"/>
    </linearGradient>
    <linearGradient id="slotRecess" x1="0" y1="0" x2="0.85" y2="1">
      <stop offset="0" stop-color="#38366B"/>
      <stop offset="0.62" stop-color="#2E2C5B"/>
      <stop offset="1" stop-color="#24234D"/>
    </linearGradient>
    <linearGradient id="iceMain" x1="0" y1="0" x2="0.85" y2="1">
      <stop offset="0" stop-color="#E1F8FF" stop-opacity="0.98"/>
      <stop offset="0.42" stop-color="#A8E9FB" stop-opacity="0.96"/>
      <stop offset="1" stop-color="#59B9E4" stop-opacity="0.98"/>
    </linearGradient>
    <linearGradient id="iceFacet" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FFFFFF" stop-opacity="0.9"/>
      <stop offset="0.55" stop-color="#BCEFFF" stop-opacity="0.72"/>
      <stop offset="1" stop-color="#67C7EC" stop-opacity="0.9"/>
    </linearGradient>
    <linearGradient id="coverMain" x1="0" y1="0" x2="0.9" y2="1">
      <stop offset="0" stop-color="#E3C7B8"/>
      <stop offset="0.48" stop-color="#C9A99C"/>
      <stop offset="1" stop-color="#AB877D"/>
    </linearGradient>
    <linearGradient id="coverSoft" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#DCC0B2"/>
      <stop offset="0.58" stop-color="#C4A397"/>
      <stop offset="1" stop-color="#A98278"/>
    </linearGradient>
    <linearGradient id="mysteryMain" x1="0" y1="0" x2="0.9" y2="1">
      <stop offset="0" stop-color="#444250"/>
      <stop offset="0.46" stop-color="#34323F"/>
      <stop offset="1" stop-color="#24232D"/>
    </linearGradient>
    <radialGradient id="shadowFade" cx="50%" cy="50%" r="50%">
      <stop offset="0" stop-color="#17142F" stop-opacity="0.72"/>
      <stop offset="0.62" stop-color="#17142F" stop-opacity="0.4"/>
      <stop offset="1" stop-color="#17142F" stop-opacity="0"/>
    </radialGradient>
    <filter id="iceGlow" x="-30%" y="-50%" width="160%" height="200%">
      <feGaussianBlur stdDeviation="13"/>
    </filter>
  </defs>
"""


ASSETS: dict[str, Asset] = {
    "slot_top": Asset(
        640,
        320,
        "Bottom Center (0.5, 0.0)",
        """
  <path d="M80 320V171C80 124 111 91 154 91c22 0 42 9 58 25 12-35 43-58 80-58 30 0 58 17 76 43 18-26 46-43 76-43 37 0 68 23 80 58 16-16 36-25 58-25 43 0 78 33 78 80v149Z" fill="#17142F" opacity="0.58" transform="translate(12 14)"/>
  <path d="M64 320V160c0-49 34-83 81-83 23 0 44 9 60 26 14-37 48-61 87-61 31 0 59 16 76 43 17-27 45-43 76-43 39 0 73 24 87 61 16-17 37-26 60-26 47 0 85 34 85 83v160Z" fill="url(#slotMain)" stroke="#211D45" stroke-width="18" stroke-linejoin="round"/>
  <path d="M91 320V174c0-35 23-58 56-58 24 0 42 12 57 34 12-43 43-68 86-68 34 0 58 18 78 51 20-33 44-51 78-51 43 0 74 25 86 68 15-22 33-34 57-34 33 0 60 23 60 58v146Z" fill="url(#slotRecess)"/>
  <path d="M84 159c0-37 26-62 62-62 22 0 42 10 57 29 17-42 47-64 88-64 31 0 57 15 77 46 20-31 46-46 77-46 41 0 71 22 88 64 15-19 35-29 57-29 32 0 57 20 62 51" fill="none" stroke="#6963A1" stroke-width="13" stroke-linecap="round" opacity="0.88"/>
  <path d="M567 154v166H533V169c0-26-10-43-31-52 32 3 55 16 65 37Z" fill="#211F4B" opacity="0.78"/>
""",
    ),
    "slot_cell_repeat": Asset(
        640,
        512,
        "Center (0.5, 0.5)",
        """
  <rect x="76" y="0" width="512" height="512" fill="#17142F" opacity="0.52"/>
  <rect x="64" y="0" width="512" height="512" fill="url(#slotMain)" stroke="#211D45" stroke-width="18"/>
  <rect x="94" y="23" width="442" height="452" rx="48" fill="url(#slotRecess)" stroke="#292755" stroke-width="10"/>
  <path d="M104 64Q104 38 132 33H500" fill="none" stroke="#5F5997" stroke-width="12" stroke-linecap="round" opacity="0.76"/>
  <path d="M104 453H506q30 0 30-31v34q0 37-38 37H137q-33 0-33-40Z" fill="#201E48" opacity="0.92"/>
  <path d="M552 36v431q0 28-28 28h-24c25-11 31-31 31-59V57c0-21 7-29 21-21Z" fill="#201E49" opacity="0.82"/>
""",
    ),
    "slot_bottom": Asset(
        640,
        320,
        "Top Center (0.5, 1.0)",
        """
  <path d="M80 8h512v177c0 73-51 111-128 111H208c-77 0-128-38-128-111Z" fill="#17142F" opacity="0.58" transform="translate(12 12)"/>
  <path d="M64 0h512v175c0 68-48 105-120 105H184c-72 0-120-37-120-105Z" fill="url(#slotMain)" stroke="#211D45" stroke-width="18" stroke-linejoin="round"/>
  <path d="M94 0h442v155c0 50-34 73-87 73H191c-53 0-97-23-97-73Z" fill="url(#slotRecess)"/>
  <path d="M104 24v126c0 38 23 58 68 63" fill="none" stroke="#625C99" stroke-width="12" stroke-linecap="round" opacity="0.74"/>
  <path d="M100 210c18 32 50 47 96 47h248c54 0 87-18 102-53-10 47-45 68-105 68H198c-54 0-87-19-98-62Z" fill="#201E47" opacity="0.9"/>
  <path d="M235 244h170c26 0 45 13 49 36H186c4-23 23-36 49-36Z" fill="#6B63A4" opacity="0.66"/>
""",
    ),
    "slot_shadow": Asset(
        768,
        256,
        "Center (0.5, 0.5)",
        """
  <ellipse cx="384" cy="138" rx="330" ry="92" fill="url(#shadowFade)"/>
  <ellipse cx="358" cy="116" rx="220" ry="44" fill="#302B59" opacity="0.14"/>
""",
    ),
    "ice_frost_band": Asset(
        704,
        320,
        "Center (0.5, 0.5)",
        """
  <path d="M74 107Q118 71 164 94q45 21 85-9 46-34 92 2 43 34 89 1 43-31 87 0 40 29 85 3 45-26 80 17l-2 99q-39 25-83 8-43-16-84 8-44 26-88 2-46-25-89 2-45 29-91 2-40-24-84-3-47 23-89-4Z" fill="#62C9EC" opacity="0.3" filter="url(#iceGlow)"/>
  <path d="M70 95Q116 62 161 88q44 25 87-8 46-35 92 2 43 34 90 0 42-31 87 0 39 29 84 2 43-26 72 17l-3 96-47 28-54-18-48 25-58-21-53 27-55-24-53 28-53-23-56 24-51-26Z" fill="url(#iceMain)" stroke="#4A92C5" stroke-width="12" stroke-linejoin="round"/>
  <path d="M91 101q35-22 70-2 45 26 90-7 43-32 84 0 43 33 91 1 42-29 86 2 39 26 76 4" fill="none" stroke="#F2FDFF" stroke-width="16" stroke-linecap="round" opacity="0.92"/>
  <path d="M112 128l47-17 37 18-43 84-36-15Zm172-22 47 8 28 75-51 31-30-72Zm181 7 45-7 41 21-42 81-48-6Z" fill="#FFFFFF" opacity="0.28"/>
  <path d="M203 120l31 8-21 79-35 12Zm184-1 27 5-17 90-38 20Z" fill="#4BB3DD" opacity="0.36"/>
""",
    ),
    "ice_crystal_left": Asset(
        256,
        448,
        "Top Center (0.5, 1.0)",
        """
  <path d="M58 43 194 62l-29 224-76 116-48-143Z" fill="#59BDE6" opacity="0.28" filter="url(#iceGlow)"/>
  <path d="M58 43 194 62l-29 224-76 116-48-143Z" fill="url(#iceMain)" stroke="#4A92C5" stroke-width="12" stroke-linejoin="round"/>
  <path d="M67 63 122 76 88 355 56 254Z" fill="#F4FDFF" opacity="0.65"/>
  <path d="m122 76 62-4-27 205-69 78Z" fill="#66C8ED" opacity="0.52"/>
  <path d="M70 64 181 79" fill="none" stroke="#FFFFFF" stroke-width="13" stroke-linecap="round" opacity="0.82"/>
""",
    ),
    "ice_crystal_center": Asset(
        256,
        512,
        "Top Center (0.5, 1.0)",
        """
  <path d="M54 48h148l-22 279-52 137-57-139Z" fill="#58BDE6" opacity="0.28" filter="url(#iceGlow)"/>
  <path d="M54 48h148l-22 279-52 137-57-139Z" fill="url(#iceMain)" stroke="#4A92C5" stroke-width="12" stroke-linejoin="round"/>
  <path d="M68 65h58l2 349-43-99Z" fill="#F5FDFF" opacity="0.64"/>
  <path d="m128 66 60 1-21 254-39 93Z" fill="#52B9E2" opacity="0.48"/>
  <path d="M70 67h112" fill="none" stroke="#FFFFFF" stroke-width="14" stroke-linecap="round" opacity="0.84"/>
""",
    ),
    "ice_crystal_right": Asset(
        256,
        448,
        "Top Center (0.5, 1.0)",
        """
  <path d="M62 62 198 43l17 216-48 143-76-116Z" fill="#59BDE6" opacity="0.28" filter="url(#iceGlow)"/>
  <path d="M62 62 198 43l17 216-48 143-76-116Z" fill="url(#iceMain)" stroke="#4A92C5" stroke-width="12" stroke-linejoin="round"/>
  <path d="m72 73 62 3 34 279-69-78Z" fill="#F4FDFF" opacity="0.64"/>
  <path d="m134 76 55-13 10 191-32 101Z" fill="#55BCE5" opacity="0.5"/>
  <path d="M75 79 186 64" fill="none" stroke="#FFFFFF" stroke-width="13" stroke-linecap="round" opacity="0.82"/>
""",
    ),
    "cover_top_cap": Asset(
        704,
        320,
        "Content Seam (0.5, 0.2625)",
        """
  <rect x="77" y="78" width="576" height="174" rx="49" fill="#4A3442" opacity="0.45"/>
  <rect x="64" y="64" width="576" height="172" rx="48" fill="url(#coverMain)" stroke="#74545A" stroke-width="14"/>
  <path d="M91 127q0-38 42-38h438q38 0 42 35" fill="none" stroke="#F0D7C8" stroke-width="18" stroke-linecap="round" opacity="0.86"/>
  <path d="M78 190h548v18q0 28-32 28H110q-32 0-32-28Z" fill="#967168" opacity="0.88"/>
  <path d="M585 83q41 7 41 46v67q0 27-30 35h-34q28-18 28-48v-73q0-17-5-27Z" fill="#89645F" opacity="0.62"/>
""",
    ),
    "cover_cell_repeat": Asset(
        640,
        512,
        "Center (0.5, 0.5)",
        """
  <rect x="133" y="0" width="400" height="512" fill="#4A3442" opacity="0.42"/>
  <rect x="120" y="0" width="400" height="512" fill="url(#coverSoft)" stroke="#79585C" stroke-width="12"/>
  <path d="M144 28h320q29 0 29 29" fill="none" stroke="#EBD3C5" stroke-width="14" stroke-linecap="round" opacity="0.76"/>
  <path d="M132 450h376v33q0 29-30 29H162q-30 0-30-29Z" fill="#947067" opacity="0.9"/>
  <path d="M474 20h34v444q0 25-25 32h-30q21-15 21-45Z" fill="#8B675F" opacity="0.7"/>
  <path d="M165 85v315" fill="none" stroke="#E5CABC" stroke-width="10" stroke-linecap="round" opacity="0.35"/>
""",
    ),
    "cover_separator": Asset(
        640,
        160,
        "Center (0.5, 0.5)",
        """
  <rect x="122" y="51" width="416" height="78" rx="24" fill="#4A3442" opacity="0.46"/>
  <rect x="112" y="36" width="416" height="78" rx="22" fill="url(#coverMain)" stroke="#74545A" stroke-width="10"/>
  <path d="M137 56h344" fill="none" stroke="#F0D7C8" stroke-width="11" stroke-linecap="round" opacity="0.72"/>
  <path d="M124 87h392v13q0 14-18 14H142q-18 0-18-14Z" fill="#916D65" opacity="0.92"/>
""",
    ),
    "mystery_face_overlay": Asset(
        640,
        640,
        "Center (0.5, 0.5)",
        """
  <rect x="82" y="84" width="512" height="512" rx="82" fill="#15141D" opacity="0.58"/>
  <rect x="64" y="64" width="512" height="512" rx="78" fill="url(#mysteryMain)" stroke="#191821" stroke-width="20"/>
  <path d="M104 201v-42q0-55 57-55h320" fill="none" stroke="#686573" stroke-width="18" stroke-linecap="round" opacity="0.74"/>
  <path d="M100 492h369q63 0 63-63v43q0 60-60 60H160q-60 0-60-40Z" fill="#1C1B24" opacity="0.94"/>
  <path d="M513 130v338q0 48-49 56h-42q70-26 70-93V164q0-23 21-34Z" fill="#1D1C26" opacity="0.72"/>
""",
    ),
    "question_mark_decal": Asset(
        640,
        640,
        "Center (0.5, 0.5)",
        """
  <g transform="translate(149 155) scale(0.64)">
  <path d="M116 177C116 91 178 48 262 48c91 0 146 49 146 125 0 81-64 110-111 137-34 20-46 42-46 81" fill="none" stroke="#101018" stroke-width="130" stroke-linecap="round" stroke-linejoin="round" opacity="0.58" transform="translate(10 14)"/>
  <circle cx="261" cy="456" r="65" fill="#101018" opacity="0.58" transform="translate(10 14)"/>
  <path d="M116 177C116 91 178 48 262 48c91 0 146 49 146 125 0 81-64 110-111 137-34 20-46 42-46 81" fill="none" stroke="#1A1923" stroke-width="130" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M116 177C116 91 178 48 262 48c91 0 146 49 146 125 0 81-64 110-111 137-34 20-46 42-46 81" fill="none" stroke="#F1EFEA" stroke-width="78" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="261" cy="456" r="65" fill="#1A1923"/>
  <circle cx="261" cy="456" r="39" fill="#F1EFEA"/>
  <path d="M132 158c10-54 55-84 117-84" fill="none" stroke="#FFFFFF" stroke-width="19" stroke-linecap="round" opacity="0.82"/>
  <circle cx="247" cy="443" r="12" fill="#FFFFFF" opacity="0.78"/>
  </g>
""",
    ),
}


def svg_document(name: str, asset: Asset) -> str:
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="{asset.width}" height="{asset.height}" viewBox="0 0 {asset.width} {asset.height}">
  <title>{name}</title>
  <metadata>LogicalCellPixels={LOGICAL_CELL_PX}; RecommendedPixelsPerUnit={RECOMMENDED_PPU}; Pivot={asset.pivot}</metadata>
{DEFS}{asset.body}</svg>
"""


class Painter:
    """Small deterministic supersampled renderer for the authored SVG shapes."""

    scale = 3

    def __init__(self, width: int, height: int) -> None:
        self.width = width
        self.height = height
        self.image = Image.new(
            "RGBA", (width * self.scale, height * self.scale), (0, 0, 0, 0)
        )
        self.draw = ImageDraw.Draw(self.image, "RGBA")

    @classmethod
    def pt(cls, point: tuple[float, float]) -> tuple[int, int]:
        return tuple(round(value * cls.scale) for value in point)  # type: ignore[return-value]

    @classmethod
    def box(cls, box: tuple[float, float, float, float]) -> tuple[int, int, int, int]:
        return tuple(round(value * cls.scale) for value in box)  # type: ignore[return-value]

    @staticmethod
    def color(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
        return (*ImageColor.getrgb(value), alpha)

    def rounded(
        self,
        box: tuple[float, float, float, float],
        radius: float,
        fill: str,
        alpha: int = 255,
        outline: str | None = None,
        width: float = 1,
    ) -> None:
        self.draw.rounded_rectangle(
            self.box(box),
            radius=round(radius * self.scale),
            fill=self.color(fill, alpha),
            outline=self.color(outline) if outline else None,
            width=round(width * self.scale),
        )

    def ellipse(
        self,
        box: tuple[float, float, float, float],
        fill: str,
        alpha: int = 255,
        outline: str | None = None,
        width: float = 1,
    ) -> None:
        self.draw.ellipse(
            self.box(box),
            fill=self.color(fill, alpha),
            outline=self.color(outline) if outline else None,
            width=round(width * self.scale),
        )

    def polygon(
        self,
        points: list[tuple[float, float]],
        fill: str,
        alpha: int = 255,
        outline: str | None = None,
        width: float = 1,
    ) -> None:
        scaled = [self.pt(point) for point in points]
        self.draw.polygon(scaled, fill=self.color(fill, alpha))
        if outline:
            self.draw.line(
                scaled + [scaled[0]],
                fill=self.color(outline),
                width=round(width * self.scale),
                joint="curve",
            )

    def line(
        self,
        points: list[tuple[float, float]],
        fill: str,
        width: float,
        alpha: int = 255,
        rounded: bool = True,
    ) -> None:
        scaled = [self.pt(point) for point in points]
        line_width = round(width * self.scale)
        self.draw.line(
            scaled,
            fill=self.color(fill, alpha),
            width=line_width,
            joint="curve",
        )
        if rounded:
            radius = line_width // 2
            for x, y in (scaled[0], scaled[-1]):
                self.draw.ellipse(
                    (x - radius, y - radius, x + radius, y + radius),
                    fill=self.color(fill, alpha),
                )

    def new_mask(self) -> tuple[Image.Image, ImageDraw.ImageDraw]:
        mask = Image.new("L", self.image.size, 0)
        return mask, ImageDraw.Draw(mask)

    def mask_rounded(
        self,
        draw: ImageDraw.ImageDraw,
        box: tuple[float, float, float, float],
        radius: float,
        fill: int = 255,
    ) -> None:
        draw.rounded_rectangle(
            self.box(box), radius=round(radius * self.scale), fill=fill
        )

    def mask_ellipse(
        self,
        draw: ImageDraw.ImageDraw,
        box: tuple[float, float, float, float],
        fill: int = 255,
    ) -> None:
        draw.ellipse(self.box(box), fill=fill)

    def fill_mask(
        self,
        mask: Image.Image,
        top: str,
        bottom: str | None = None,
        alpha: int = 255,
    ) -> None:
        if bottom is None:
            layer = Image.new("RGBA", self.image.size, self.color(top, alpha))
        else:
            top_rgb = ImageColor.getrgb(top)
            bottom_rgb = ImageColor.getrgb(bottom)
            layer = Image.new("RGBA", self.image.size)
            gradient = ImageDraw.Draw(layer)
            total = max(1, self.image.height - 1)
            for y in range(self.image.height):
                t = y / total
                rgb = tuple(
                    round(top_rgb[i] * (1 - t) + bottom_rgb[i] * t)
                    for i in range(3)
                )
                gradient.line(
                    (0, y, self.image.width, y), fill=(*rgb, alpha), width=1
                )
        self.image.alpha_composite(Image.composite(layer, Image.new("RGBA", self.image.size), mask))

    def glow_ellipse(
        self,
        box: tuple[float, float, float, float],
        fill: str,
        alpha: int,
        blur: float,
    ) -> None:
        layer = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer, "RGBA")
        draw.ellipse(self.box(box), fill=self.color(fill, alpha))
        layer = layer.filter(ImageFilter.GaussianBlur(blur * self.scale))
        self.image.alpha_composite(layer)

    def finish(self) -> Image.Image:
        return self.image.resize(
            (self.width, self.height), Image.Resampling.LANCZOS
        ).convert("RGBA")


def _slot_top(p: Painter) -> None:
    # Combined silhouettes avoid internal outlines where the two toy-like studs meet.
    shadow, d = p.new_mask()
    p.mask_rounded(d, (78, 91, 590, 348), 86)
    p.mask_ellipse(d, (159, 57, 329, 227))
    p.mask_ellipse(d, (339, 57, 509, 227))
    d.rectangle(p.box((78, 176, 590, 348)), fill=255)
    p.fill_mask(shadow, "#17142F", alpha=148)

    outline, d = p.new_mask()
    p.mask_rounded(d, (55, 68, 585, 340), 92)
    p.mask_ellipse(d, (136, 33, 338, 235))
    p.mask_ellipse(d, (330, 33, 532, 235))
    d.rectangle(p.box((55, 170, 585, 340)), fill=255)
    p.fill_mask(outline, "#211D45")

    main, d = p.new_mask()
    p.mask_rounded(d, (64, 77, 576, 340), 82)
    p.mask_ellipse(d, (145, 42, 329, 226))
    p.mask_ellipse(d, (339, 42, 523, 226))
    d.rectangle(p.box((64, 164, 576, 340)), fill=255)
    p.fill_mask(main, "#4B477F", "#2A2858")

    recess, d = p.new_mask()
    p.mask_rounded(d, (91, 113, 549, 340), 60)
    p.mask_ellipse(d, (169, 74, 311, 216))
    p.mask_ellipse(d, (357, 74, 499, 216))
    d.rectangle(p.box((91, 176, 549, 340)), fill=255)
    p.fill_mask(recess, "#39376D", "#24234D")
    p.line([(92, 154), (116, 115), (169, 98), (222, 102)], "#6A64A2", 13, 214)
    p.line([(205, 78), (251, 61), (292, 78)], "#716AA8", 11, 190)
    p.polygon([(533, 123), (560, 151), (560, 340), (523, 340), (523, 173)], "#211F4B", 190)


def _slot_cell(p: Painter) -> None:
    p.rounded((77, 0, 589, 512), 0, "#17142F", 138)
    p.rounded((55, -8, 585, 520), 0, "#211D45")
    p.rounded((64, -1, 576, 513), 0, "#454175")
    p.rounded((94, 22, 536, 478), 48, "#302E60", outline="#292755", width=10)
    p.line([(110, 75), (110, 62), (135, 40), (494, 40)], "#625C99", 12, 190)
    p.polygon([(104, 438), (506, 438), (526, 420), (526, 455), (500, 493), (136, 493), (104, 463)], "#201E48", 232)
    p.polygon([(531, 42), (552, 29), (552, 455), (528, 486), (498, 493), (531, 435)], "#201E49", 205)


def _slot_bottom(p: Painter) -> None:
    p.rounded((77, -8, 589, 292), 72, "#17142F", 148)
    p.rounded((77, -8, 589, 112), 0, "#17142F", 148)
    p.rounded((55, -18, 585, 290), 78, "#211D45")
    p.rounded((55, -18, 585, 112), 0, "#211D45")
    p.rounded((64, -10, 576, 280), 68, "#413D75")
    p.rounded((64, -10, 576, 112), 0, "#413D75")
    p.rounded((94, -18, 536, 228), 58, "#2C2A59")
    p.rounded((94, -18, 536, 96), 0, "#2C2A59")
    p.line([(106, 21), (106, 148), (123, 190), (171, 211)], "#635D9B", 12, 190)
    p.polygon([(100, 205), (181, 244), (459, 244), (546, 199), (529, 244), (474, 272), (183, 272), (125, 244)], "#201E47", 226)
    p.rounded((186, 241, 454, 288), 24, "#6B63A4", 168)


def _slot_shadow(p: Painter) -> None:
    p.glow_ellipse((70, 70, 698, 190), "#17142F", 150, 14)
    p.ellipse((148, 89, 588, 177), "#302B59", 36)


def _ice_band(p: Painter) -> None:
    p.glow_ellipse((61, 62, 648, 257), "#69D5F3", 90, 10)
    points = [
        (70, 105), (116, 72), (161, 91), (204, 74), (248, 89),
        (294, 68), (340, 87), (386, 72), (430, 92), (475, 72),
        (517, 91), (559, 76), (602, 94), (634, 111), (630, 199),
        (583, 225), (529, 207), (481, 232), (423, 211), (370, 238),
        (315, 214), (262, 242), (209, 219), (153, 243), (102, 217),
    ]
    outline = [(x, y + 9) for x, y in points]
    p.polygon(outline, "#4A92C5")
    p.polygon(points, "#A8E9FB", outline="#4A92C5", width=8)
    p.line(points[:14], "#F2FDFF", 15, 226)
    p.polygon([(110, 122), (158, 102), (195, 130), (153, 217), (119, 198)], "#FFFFFF", 65)
    p.polygon([(281, 106), (330, 111), (357, 190), (309, 220), (279, 148)], "#FFFFFF", 60)
    p.polygon([(465, 113), (510, 103), (551, 127), (509, 209), (462, 201)], "#FFFFFF", 55)
    p.polygon([(206, 119), (238, 128), (215, 207), (179, 220)], "#43AFDB", 72)
    p.polygon([(389, 119), (417, 126), (398, 214), (359, 235)], "#43AFDB", 70)


def _ice_crystal(p: Painter, kind: str) -> None:
    if kind == "left":
        poly = [(58, 43), (194, 62), (165, 286), (89, 402), (41, 259)]
        light = [(67, 63), (122, 76), (88, 355), (56, 254)]
        dark = [(122, 76), (184, 72), (157, 277), (88, 355)]
        top = [(70, 64), (181, 79)]
    elif kind == "center":
        poly = [(54, 48), (202, 48), (180, 327), (128, 464), (71, 325)]
        light = [(68, 65), (126, 65), (128, 414), (85, 315)]
        dark = [(128, 66), (188, 67), (167, 321), (128, 414)]
        top = [(70, 67), (182, 67)]
    else:
        poly = [(62, 62), (198, 43), (215, 259), (167, 402), (91, 286)]
        light = [(72, 73), (134, 76), (168, 355), (99, 277)]
        dark = [(134, 76), (189, 63), (199, 254), (167, 355)]
        top = [(75, 79), (186, 64)]
    xs = [point[0] for point in poly]
    ys = [point[1] for point in poly]
    p.glow_ellipse((min(xs) - 10, min(ys), max(xs) + 10, max(ys) + 18), "#69D5F3", 72, 7)
    p.polygon([(x + 5, y + 7) for x, y in poly], "#4A92C5", 235)
    p.polygon(poly, "#A8E9FB", outline="#4A92C5", width=8)
    p.polygon(light, "#F4FDFF", 166)
    p.polygon(dark, "#55BCE5", 124)
    p.line(top, "#FFFFFF", 13, 210)


def _cover_top(p: Painter) -> None:
    p.rounded((77, 78, 653, 252), 49, "#4A3442", 115)
    p.rounded((57, 57, 647, 243), 56, "#74545A")
    p.rounded((64, 64, 640, 236), 48, "#C9A99C")
    p.rounded((78, 78, 626, 203), 38, "#DCC0B2")
    p.line([(102, 117), (119, 96), (572, 96), (604, 119)], "#F0D7C8", 16, 220)
    p.polygon([(78, 188), (626, 188), (626, 207), (594, 236), (110, 236), (78, 207)], "#967168", 224)
    p.polygon([(584, 82), (617, 107), (617, 196), (594, 225), (562, 225), (590, 181), (590, 111)], "#89645F", 155)


def _cover_cell(p: Painter) -> None:
    p.rounded((133, 0, 533, 512), 0, "#4A3442", 110)
    p.rounded((112, -8, 528, 520), 0, "#74545A")
    p.rounded((120, -1, 520, 513), 0, "#C4A397")
    p.rounded((132, 12, 508, 462), 22, "#D1B2A4")
    p.line([(147, 48), (162, 29), (464, 29), (493, 54)], "#EBD3C5", 14, 194)
    p.polygon([(132, 449), (508, 449), (508, 483), (478, 512), (162, 512), (132, 483)], "#947067", 230)
    p.polygon([(474, 20), (508, 37), (508, 463), (482, 496), (452, 496), (474, 451)], "#8B675F", 178)
    p.line([(165, 85), (165, 400)], "#E5CABC", 10, 86)


def _cover_separator(p: Painter) -> None:
    p.rounded((122, 51, 538, 129), 24, "#4A3442", 118)
    p.rounded((104, 28, 536, 122), 28, "#74545A")
    p.rounded((112, 36, 528, 114), 22, "#C9A99C")
    p.line([(137, 56), (481, 56)], "#F0D7C8", 11, 184)
    p.polygon([(124, 86), (516, 86), (516, 101), (498, 114), (142, 114), (124, 101)], "#916D65", 232)


def _mystery(p: Painter) -> None:
    p.rounded((82, 84, 594, 596), 82, "#15141D", 148)
    p.rounded((53, 53, 587, 587), 90, "#191821")
    p.rounded((64, 64, 576, 576), 78, "#34323F")
    p.rounded((84, 84, 556, 532), 62, "#3B3947")
    p.line([(104, 201), (104, 159), (126, 124), (161, 104), (481, 104)], "#686573", 18, 188)
    p.polygon([(100, 476), (469, 476), (532, 413), (532, 472), (472, 532), (160, 532), (100, 492)], "#1C1B24", 242)
    p.polygon([(492, 130), (532, 157), (532, 468), (492, 516), (422, 524), (492, 431)], "#1D1C26", 184)


def _cubic(
    p0: tuple[float, float],
    p1: tuple[float, float],
    p2: tuple[float, float],
    p3: tuple[float, float],
    steps: int = 18,
) -> list[tuple[float, float]]:
    points = []
    for index in range(steps + 1):
        t = index / steps
        u = 1 - t
        points.append(
            (
                u**3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t**3 * p3[0],
                u**3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t**3 * p3[1],
            )
        )
    return points


def _question(p: Painter) -> None:
    curve = _cubic((116, 177), (116, 91), (178, 48), (262, 48))
    curve += _cubic((262, 48), (353, 48), (408, 97), (408, 173))[1:]
    curve += _cubic((408, 173), (408, 254), (344, 282), (297, 310))[1:]
    curve += _cubic((297, 310), (263, 330), (251, 352), (251, 391))[1:]
    scale = 0.64
    offset_x, offset_y = 149, 155
    transform = lambda point: (
        point[0] * scale + offset_x,
        point[1] * scale + offset_y,
    )
    transform_box = lambda box: (
        box[0] * scale + offset_x,
        box[1] * scale + offset_y,
        box[2] * scale + offset_x,
        box[3] * scale + offset_y,
    )
    curve = [transform(point) for point in curve]
    shadow = [(x + 10 * scale, y + 14 * scale) for x, y in curve]
    p.line(shadow, "#101018", 130 * scale, 148)
    p.ellipse(transform_box((206, 401, 336, 531)), "#101018", 148)
    p.line(curve, "#1A1923", 130 * scale)
    p.line(curve, "#F1EFEA", 78 * scale)
    p.ellipse(transform_box((196, 391, 326, 521)), "#1A1923")
    p.ellipse(transform_box((222, 417, 300, 495)), "#F1EFEA")
    highlight = _cubic((132, 158), (143, 103), (190, 74), (249, 74), 12)
    p.line([transform(point) for point in highlight], "#FFFFFF", 19 * scale, 208)
    p.ellipse(transform_box((235, 431, 259, 455)), "#FFFFFF", 198)


def draw_asset(name: str, asset: Asset) -> Image.Image:
    painter = Painter(asset.width, asset.height)
    if name == "slot_top":
        _slot_top(painter)
    elif name == "slot_cell_repeat":
        _slot_cell(painter)
    elif name == "slot_bottom":
        _slot_bottom(painter)
    elif name == "slot_shadow":
        _slot_shadow(painter)
    elif name == "ice_frost_band":
        _ice_band(painter)
    elif name.startswith("ice_crystal_"):
        _ice_crystal(painter, name.removeprefix("ice_crystal_"))
    elif name == "cover_top_cap":
        _cover_top(painter)
    elif name == "cover_cell_repeat":
        _cover_cell(painter)
    elif name == "cover_separator":
        _cover_separator(painter)
    elif name == "mystery_face_overlay":
        _mystery(painter)
    elif name == "question_mark_decal":
        _question(painter)
    else:
        raise KeyError(f"No raster renderer for {name}")
    return painter.finish()


def render_png(name: str, png_path: Path, asset: Asset) -> None:
    rgba = draw_asset(name, asset)
    if rgba.size != (asset.width, asset.height):
        raise RuntimeError(
            f"Unexpected render size for {png_path.name}: {rgba.size}, "
            f"expected {(asset.width, asset.height)}"
        )
    info = PngImagePlugin.PngInfo()
    info.add_text("LogicalCellPixels", str(LOGICAL_CELL_PX))
    info.add_text("RecommendedPixelsPerUnit", str(RECOMMENDED_PPU))
    info.add_text("PivotSuggestion", asset.pivot)
    rgba.save(png_path, format="PNG", compress_level=9, pnginfo=info)


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, asset in ASSETS.items():
        svg_path = SOURCE_DIR / f"{name}.svg"
        png_path = OUTPUT_DIR / f"{name}.png"
        svg_path.write_text(svg_document(name, asset), encoding="utf-8")
        render_png(name, png_path, asset)
        print(f"{name}: {asset.width}x{asset.height} | {asset.pivot}")


if __name__ == "__main__":
    main()
