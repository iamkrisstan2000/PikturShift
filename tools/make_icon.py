"""Generates the PikturShift app icon: a dark rounded tile with a photo frame and a
convert-arrows badge. Two masters are drawn - a detailed one for 64 px and up, and a
simplified, heavier one for 48 px and below, where thin detail turns to mush - then every
size is rendered from the right master into a multi-size .ico plus PNGs."""
from PIL import Image, ImageDraw
import os

S = 1024

TILE_TOP = (46, 54, 33, 255)
TILE_BOT = (20, 23, 16, 255)
INK = (247, 248, 243, 255)
LIME = (170, 255, 41, 255)
SCREEN = (28, 33, 21, 255)


def rounded_tile(img: Image.Image, d: ImageDraw.ImageDraw) -> None:
    """Dark tile with a vertical gradient, an inner rim and a transparent surround."""
    radius = 210
    grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(TILE_TOP[i] + (TILE_BOT[i] - TILE_TOP[i]) * t)
                                            for i in range(3)) + (255,))

    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)
    d.rounded_rectangle([6, 6, S - 7, S - 7], radius=radius - 6, outline=(255, 255, 255, 26), width=6)


def convert_badge(d: ImageDraw.ImageDraw, cx: int, cy: int, r: int, ring: int) -> None:
    """Dark disc with a lime ring and two opposing arrows, i.e. the convert glyph.
    Every arrow corner is kept inside the ring by deriving the geometry from `inner`."""
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(23, 27, 18, 255), outline=LIME, width=ring)

    inner = r - ring
    gap, shaft = inner * 0.44, inner * 0.23
    head_h, head_in, head_out, tail = inner * 0.36, inner * 0.44, inner * 0.84, inner * 0.66

    for direction in (1, -1):
        y = cy + direction * gap
        x_tail = cx - direction * tail
        x_neck = cx + direction * (head_in - shaft * 0.4)
        d.polygon([(x_tail, y - shaft), (x_neck, y - shaft),
                   (x_neck, y + shaft), (x_tail, y + shaft)], fill=INK)
        d.polygon([(cx + direction * head_in, y - head_h),
                   (cx + direction * head_out, y),
                   (cx + direction * head_in, y + head_h)], fill=INK)


def render(detail: bool) -> Image.Image:
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded_tile(img, d)

    if detail:
        # photo frame with a tiny landscape
        fx0, fy0, fx1, fy1 = 190, 214, 822, 742
        d.rounded_rectangle([fx0, fy0, fx1, fy1], radius=86, fill=INK)
        ix0, iy0, ix1, iy1 = fx0 + 60, fy0 + 60, fx1 - 60, fy1 - 60
        d.rounded_rectangle([ix0, iy0, ix1, iy1], radius=42, fill=SCREEN)
        d.ellipse([ix0 + 66, iy0 + 54, ix0 + 150, iy0 + 138], fill=LIME)
        d.polygon([(ix0 + 24, iy1 - 24), (ix0 + 190, iy1 - 214), (ix0 + 336, iy1 - 24)], fill=(170, 255, 41, 236))
        d.polygon([(ix0 + 236, iy1 - 24), (ix0 + 372, iy1 - 170), (ix0 + 486, iy1 - 24)],
                  fill=(214, 240, 200, 226))
        convert_badge(d, 784, 690, 150, 16)
    else:
        # 48 px and below: no badge and no landscape - a heavier frame with big
        # convert arrows inside it is the only thing that survives the downscale.
        fx0, fy0, fx1, fy1 = 156, 186, 868, 792
        d.rounded_rectangle([fx0, fy0, fx1, fy1], radius=108, fill=INK)
        ix0, iy0, ix1, iy1 = fx0 + 84, fy0 + 84, fx1 - 84, fy1 - 84
        d.rounded_rectangle([ix0, iy0, ix1, iy1], radius=52, fill=SCREEN)

        cx, cy = 512, 490
        gap, shaft, head_h, head_in, head_out, tail = 118, 60, 94, 148, 246, 186
        for direction in (1, -1):
            y = cy + direction * gap
            d.polygon([(cx - direction * tail, y - shaft), (cx + direction * (head_in - 24), y - shaft),
                       (cx + direction * (head_in - 24), y + shaft), (cx - direction * tail, y + shaft)],
                      fill=LIME)
            d.polygon([(cx + direction * head_in, y - head_h),
                       (cx + direction * head_out, y),
                       (cx + direction * head_in, y + head_h)], fill=LIME)

    return img


def main() -> None:
    detailed = render(detail=True)
    compact = render(detail=False)

    out_dir = os.path.join(os.path.dirname(__file__), "..", "src", "PikturShift", "Assets")
    os.makedirs(out_dir, exist_ok=True)

    sizes = [256, 128, 64, 48, 40, 32, 24, 20, 16]
    frames = [(detailed if s >= 128 else compact).resize((s, s), Image.LANCZOS) for s in sizes]

    ico_path = os.path.abspath(os.path.join(out_dir, "app.ico"))
    frames[0].save(ico_path, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[1:])
    detailed.resize((256, 256), Image.LANCZOS).save(os.path.join(out_dir, "app-256.png"))
    compact.resize((32, 32), Image.LANCZOS).save(os.path.join(out_dir, "app-32.png"))
    print("written", ico_path)


if __name__ == "__main__":
    main()
