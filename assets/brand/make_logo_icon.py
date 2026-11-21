#!/usr/bin/env python3
"""ViewLab logo v2 — mark is the real app icon (assets/xr-viewlab-icon.png),
wordmark is Segoe UI Semibold outlines via fontTools. Emits SVG masters + PNGs."""
import base64
import io

import resvg_py
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.ttLib import TTFont
from PIL import Image

FONT = r"C:\Windows\Fonts\segoeuib.ttf"
ICON = r"F:\AI-Projects\ViewLab\assets\xr-viewlab-icon.png"
OUT = r"F:\AI-Projects\ViewLab-Website\brand"
INK = "#E8E8E8"
INK_DARK = "#17191C"
ACCENT = "#C90012"

font = TTFont(FONT)
UPM = font["head"].unitsPerEm
glyphs = font.getGlyphSet()
cmap = font.getBestCmap()
hmtx = font["hmtx"]

# ---- icon: crop to content, downscale for embedding ----
icon = Image.open(ICON).convert("RGBA")
a = icon.getchannel("A")
bbox = a.getbbox()
icon_c = icon.crop(bbox)
# embed at 192px tall for crispness
icon_e = icon_c.copy()
icon_e.thumbnail((1024, 1024), Image.LANCZOS)
buf = io.BytesIO()
icon_e.save(buf, "PNG")
ICON_B64 = base64.b64encode(buf.getvalue()).decode()
ICON_W, ICON_H = icon_e.size
# displayed size in logo: height 58, keep aspect
DISP_H = 64.0
DISP_W = DISP_H * ICON_W / ICON_H
ICON_X = 46.0
ICON_Y = (76 - DISP_H) / 2
ICON_ASPECT = ICON_W / ICON_H

# ---- wordmark ----
TEXT = "ViewLab"
TRACK = 2.0
bp = BoundsPen(glyphs)
glyphs[cmap[ord("V")]].draw(bp)
cap_h = bp.bounds[3] - bp.bounds[1]
SCALE = 50.0 / cap_h
CAP_PX = cap_h * SCALE
BASELINE_Y = (76 + CAP_PX) / 2 - 2

parts = []
pen_x = ICON_X + DISP_W + 18
for c in TEXT:
    gname = cmap[ord(c)]
    pen = SVGPathPen(glyphs)
    glyphs[gname].draw(pen)
    d = pen.getCommands()
    if d:
        parts.append(
            f'<path d="{d}" transform="translate({pen_x:.2f} {BASELINE_Y:.2f}) '
            f'scale({SCALE:.6f} {-SCALE:.6f})"/>'
        )
    pen_x += hmtx[gname][0] * SCALE + TRACK
text_svg = "\n  ".join(parts)
text_right = pen_x - TRACK
print(f"icon {ICON_W}x{ICON_H} disp {DISP_W:.0f}x{DISP_H} text right {text_right:.0f}")


def logo_svg(ink):
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 450 76" width="450" height="76">\n'
        f'  <image href="data:image/png;base64,{ICON_B64}" x="{ICON_X:.1f}" y="{ICON_Y:.1f}" '
        f'width="{DISP_W:.1f}" height="{DISP_H:.1f}"/>\n'
        f'  <g fill="{ink}">\n  {text_svg}\n  </g>\n'
        "</svg>\n"
    )


def emit(svg_text, prefix):
    with open(f"{OUT}/{prefix}.svg", "w", encoding="utf-8") as f:
        f.write(svg_text)
    for w, h, suf in [(450, 76, "450x76"), (225, 38, "225x38"), (113, 19, "113x19")]:
        png = resvg_py.svg_to_bytes(svg_text, width=w, height=h)
        im = Image.open(io.BytesIO(png)).convert("RGBA")
        if im.size != (w, h):
            im = im.crop((0, 0, w, h))
        im.save(f"{OUT}/{prefix}-{suf}.png")
        print(prefix, suf, im.size)


emit(logo_svg(INK), "viewlab-logo")
emit(logo_svg(INK_DARK), "viewlab-logo-dark")

# favicon: the app icon itself on a dark rounded tile
def png_b64(im):
    b = io.BytesIO()
    im.save(b, "PNG")
    return base64.b64encode(b.getvalue()).decode()

fav_icon = icon_c.copy()
fav_icon.thumbnail((512, 512), Image.LANCZOS)
fiw = fav_icon.size[0] / fav_icon.size[1]
# fit inside 60px box preserving aspect
fh = 56.0
fw = min(60.0, fh * fiw)
if fw > 60.0:
    fw = 60.0; fh = fw / fiw
fx = (76 - fw) / 2; fy = (76 - fh) / 2
fav_svg = (
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 76 76" width="76" height="76">\n'
    '  <rect width="76" height="76" rx="14" fill="#101114"/>\n'
    f'  <image href="data:image/png;base64,{png_b64(fav_icon)}" x="{fx:.1f}" y="{fy:.1f}" width="{fw:.1f}" height="{fh:.1f}"/>\n'
    "</svg>\n"
)
with open(f"{OUT}/favicon.svg", "w", encoding="utf-8") as f:
    f.write(fav_svg)
for s in [16, 32, 64, 180]:
    png = resvg_py.svg_to_bytes(fav_svg, width=s, height=s)
    with open(f"{OUT}/favicon-{s}.png", "wb") as f:
        f.write(png)
print("favicon done")
