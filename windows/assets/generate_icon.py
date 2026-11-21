"""Build the Meta Cue icon from simple vector geometry (requires Pillow)."""
from pathlib import Path
from io import BytesIO
import struct
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
ONE = [(29, 15), (34, 15), (34, 32), (38, 32), (38, 36),
       (26, 36), (26, 32), (30, 32), (30, 21), (26, 23), (26, 18)]
TAIL = [(29, 38), (35, 38), (32, 43)]


def render(size):
    scale = size * 8 / 64
    image = Image.new("RGBA", (size * 8, size * 8))
    draw = ImageDraw.Draw(image)

    def rounded(box, radius, fill):
        draw.rounded_rectangle(tuple(round(x * scale) for x in box),
                               radius=round(radius * scale), fill=fill)

    def polygon(points, fill):
        draw.polygon([(round(x * scale), round(y * scale)) for x, y in points], fill=fill)

    rounded((2, 2, 62, 62), 15, "#416A9A")
    rounded((3, 3, 61, 61), 14, "#183558")
    polygon(TAIL, "#B7F3FF")
    rounded((18, 8, 46, 39), 7, "#B7F3FF")
    polygon(ONE, "#183558")
    for x, color in [(10, "#87A7CE"), (26, "#50D9E7"), (42, "#87A7CE")]:
        rounded((x, 47, x + 12, 56), 2, color)
    return image.resize((size, size), Image.Resampling.LANCZOS)


svg = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
  <title>Meta Cue — taskbar number hint</title>
  <rect x="2" y="2" width="60" height="60" rx="15" fill="#416A9A"/>
  <rect x="3" y="3" width="58" height="58" rx="14" fill="#183558"/>
  <path d="M29 38H35L32 43Z" fill="#B7F3FF"/>
  <rect x="18" y="8" width="28" height="31" rx="7" fill="#B7F3FF"/>
  <path d="M29 15H34V32H38V36H26V32H30V21L26 23V18Z" fill="#183558"/>
  <g fill="#87A7CE"><rect x="10" y="47" width="12" height="9" rx="2"/>
    <rect x="42" y="47" width="12" height="9" rx="2"/></g>
  <rect x="26" y="47" width="12" height="9" rx="2" fill="#50D9E7"/>
</svg>
'''
(ROOT / "meta-cue.svg").write_text(svg, encoding="utf-8")
render(256).save(ROOT / "meta-cue.png")
frames = []
for size in SIZES:
    stream = BytesIO()
    render(size).save(stream, format="PNG")
    frames.append((size, stream.getvalue()))
offset = 6 + len(frames) * 16
directory = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
for size, data in frames:
    directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
    offset += len(data)
(ROOT / "meta-cue.ico").write_bytes(directory + b"".join(data for _, data in frames))

# Actual-size small icons on both light and dark tray-like backgrounds.
preview = Image.new("RGB", (640, 320), "#F4F7FB")
draw = ImageDraw.Draw(preview)
draw.rectangle((320, 0, 640, 320), fill="#171E2B")
for left in (0, 320):
    preview.paste(render(160), (left + 80, 24), render(160))
    x = left + 35
    for size in (16, 20, 24, 32, 48):
        icon = render(size)
        preview.paste(icon, (x, 245 - size // 2), icon)
        x += size + 18
preview.save(ROOT / "icon-preview.png")
print("Generated SVG, PNG and 9-resolution ICO in", ROOT)
