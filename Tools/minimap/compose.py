"""Compose drone-survey minimaps from Unity top-down bakes (Tools/minimap/raw/<Day>.png, 16 px/m over X -4..124, Z -4..84).
Adds a photo grade, faint 10 m survey grid, layout labels, north arrow + scale bar -> Assets/_Game/Resources/UI/Aerial_<Day>.jpg.
Also writes the corner-widget bezel (MinimapBezel.png, transparent centre)."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
L = json.loads((ROOT / "Tools/layout/site_layout.json").read_text())
OUT = ROOT / "Assets/_Game/Resources/UI"
X0, Z0, W, H = -4, -4, 128, 88
S = 12  # output px/m (1536 x 1056)
FONT = ROOT / "Assets/_Game/Resources/Fonts/BarlowCondensed-SemiBold.ttf"


def px(x, z):
    return (x - X0) * S, (H - (z - Z0)) * S


def font(size):
    try:
        return ImageFont.truetype(str(FONT), size)
    except OSError:
        return ImageFont.load_default()


def halo_text(d, xy, text, f, fill=(255, 255, 255, 235)):
    x, y = xy
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            d.text((x + dx, y + dy), text, font=f, fill=(0, 0, 0, 150), anchor="mm")
    d.text((x, y), text, font=f, fill=fill, anchor="mm")


def compose(day):
    im = Image.open(ROOT / f"Tools/minimap/raw/{day}.png").convert("RGB").resize((W * S, H * S), Image.LANCZOS)
    # Drone-photo grade: a touch more contrast/saturation, warm highlights, slight sharpening, vignette.
    im = ImageEnhance.Contrast(im).enhance(1.12)
    im = ImageEnhance.Color(im).enhance(1.08)
    im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2))
    vig = Image.new("L", im.size, 0)
    ImageDraw.Draw(vig).ellipse([-im.width * .25, -im.height * .3, im.width * 1.25, im.height * 1.3], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(160))
    im = Image.composite(im, ImageEnhance.Brightness(im).enhance(0.72), vig).convert("RGBA")

    ov = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for x in range(0, 121, 10):   # survey grid every 10 m, stronger every 50 m
        a = 60 if x % 50 == 0 else 26
        d.line([px(x, Z0), px(x, Z0 + H)], fill=(255, 255, 255, a), width=1)
    for z in range(0, 81, 10):
        a = 60 if z % 50 == 0 else 26
        d.line([px(X0, z), px(X0 + W, z)], fill=(255, 255, 255, a), width=1)
    # Site fence + overhead line + walkway, drawn thin like a survey overlay.
    pw, ph = L["parcel"]
    d.rectangle([px(0, ph), px(pw, 0)], outline=(80, 200, 120, 200), width=3)
    line = L["power_line"]
    d.line([px(-4, line["y"]), px(124, line["y"])], fill=(255, 70, 50, 210), width=3)
    halo_text(d, px(100, line["y"] + 2.2), "OVERHEAD 13 kV · 10 ft MAC", font(20), (255, 140, 120, 240))
    d.line([px(*p) for p in L["walkway"]], fill=(255, 150, 0, 170), width=3)
    f = font(21)
    for r in L["rects"]:
        if r["w"] * r["h"] < 12:
            continue
        halo_text(d, px(r["x"] + r["w"] / 2, r["y"] + r["h"] / 2), r["label"].upper(), f)
    im = Image.alpha_composite(im, ov)

    d = ImageDraw.Draw(im)
    # North arrow + scale bar, bottom-right.
    cx, cy = im.width - 70, im.height - 150
    d.polygon([(cx, cy - 44), (cx - 18, cy + 10), (cx, cy), (cx + 18, cy + 10)], fill=(255, 255, 255, 235), outline=(0, 0, 0))
    halo_text(d, (cx, cy + 32), "N", font(30))
    bx, by = im.width - 60 - 20 * S, im.height - 60
    d.rectangle([bx, by, bx + 10 * S, by + 10], fill=(255, 255, 255, 235))
    d.rectangle([bx + 10 * S, by, bx + 20 * S, by + 10], fill=(20, 20, 20, 235), outline=(255, 255, 255))
    halo_text(d, (bx, by - 18), "0", font(20)); halo_text(d, (bx + 10 * S, by - 18), "10", font(20)); halo_text(d, (bx + 20 * S, by - 18), "20 m", font(20))
    title = f"LOBLOLLY CREEK LIFT STATION · DRONE SURVEY · {day.upper()}"
    halo_text(d, (24 + font(22).getlength(title) / 2, im.height - 30), title, font(22), (255, 205, 60, 240))
    im.convert("RGB").save(OUT / f"Aerial_{day}.jpg", quality=84)


def bezel(n=512, rim=26):
    b = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(b)
    d.rounded_rectangle([0, 0, n - 1, n - 1], radius=34, fill=(28, 30, 32, 255))
    d.rounded_rectangle([rim, rim, n - 1 - rim, n - 1 - rim], radius=10, fill=(0, 0, 0, 0))
    d.rounded_rectangle([rim - 3, rim - 3, n - rim + 2, n - rim + 2], radius=12, outline=(70, 74, 78, 255), width=3)
    y = (255, 196, 30, 255)
    for (x, yy, sx, sy) in [(0, 0, 1, 1), (n, 0, -1, 1), (0, n, 1, -1), (n, n, -1, -1)]:   # yellow rubber corners
        d.polygon([(x, yy), (x + sx * 110, yy), (x + sx * 110, yy + sy * rim), (x + sx * rim, yy + sy * rim), (x + sx * rim, yy + sy * 110), (x, yy + sy * 110)], fill=y)
    for sx, sy in [(1, 1), (-1, 1), (1, -1), (-1, -1)]:   # screws
        cx, cy = (n / 2 + sx * (n / 2 - 13)), (n / 2 + sy * (n / 2 - 13))
        d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=(60, 60, 62, 255), outline=(20, 20, 20, 255))
    d.rectangle([n / 2 - 22, 0, n / 2 + 22, rim], fill=(200, 45, 35, 255))
    halo_text(d, (n / 2, rim / 2 + 1), "N", font(24))
    b.save(OUT / "MinimapBezel.png")


if __name__ == "__main__":
    for day in ["Mon", "Tue", "Wed", "Thu", "Fri"]:
        compose(day)
    bezel()
    print("ok")
