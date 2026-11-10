"""«Живописный» рендер: силуэт (SVG-путь) → объёмная форма без контура.
Объём берётся из размытого силуэта (псевдовысота), свет — сверху слева, плюс мазки и контровой свет."""
import io, math, random
import cairosvg
import numpy as np
from PIL import Image, ImageFilter


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255.0


def mask_of(w, h, shapes):
    """shapes — SVG-фрагмент (пути/эллипсы без заливки; зальём белым)."""
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}"><g fill="#fff" stroke="none">{shapes}</g></svg>'
    im = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg.encode()))).convert("RGBA")
    return np.asarray(im)[..., 3].astype(np.float32) / 255.0


def blur(a, r):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255.0


def strokes(w, h, seed, length=18, angle=-35):
    """Шум, вытянутый вдоль направления — имитация длинных мазков кисти."""
    rng = np.random.default_rng(seed)
    big = int(max(w, h) * 1.5)
    # Узкий по одной оси шум, растянутый по другой, потом поворот.
    n = rng.random((big // 5 + 2, big // 40 + 2)).astype(np.float32)
    im = Image.fromarray((n * 255).astype(np.uint8)).resize((big, big), Image.BICUBIC).filter(ImageFilter.GaussianBlur(2))
    im = im.rotate(angle + 90, resample=Image.BICUBIC)
    x0, y0 = (big - w) // 2, (big - h) // 2
    a = np.asarray(im.crop((x0, y0, x0 + w, y0 + h))).astype(np.float32) / 255.0
    return (a - a.mean()) / (a.std() + 1e-5)


def paint(w, h, shapes, light, mid, dark, rim=None, volume=14, seed=0, light_dir=(-0.6, -0.8), stroke=0.12, steps=5, stroke_angle=-35):
    """Возвращает RGBA-картинку формы. light/mid/dark — три тона (свет, основной, тень)."""
    m = mask_of(w, h, shapes)
    hgt = blur(m, volume) * m
    gy, gx = np.gradient(hgt)
    k = volume * 0.9
    lx, ly = light_dir
    lit = (-(gx * lx + gy * ly)) * k  # >0 — к свету
    lit = lit + (hgt - 0.6) * 0.5      # середина формы чуть светлее краёв
    lit = lit + strokes(w, h, seed, angle=stroke_angle) * stroke * 0.45
    lit = np.clip(lit * 0.9 + 0.5, 0, 1)
    # Мягкая постеризация: тона ложатся пятнами, как мазки, а не идеальным градиентом.
    q = np.floor(lit * steps) / steps
    lit = blur(q * 0.75 + lit * 0.25, 1.6)
    L, M, D = hexrgb(light), hexrgb(mid), hexrgb(dark)
    t = lit[..., None]
    col = np.where(t < 0.5, D + (M - D) * (t * 2), M + (L - M) * ((t - 0.5) * 2))
    # Лёгкий разброс оттенка — тёплые и холодные пятна.
    hue = blur(np.random.default_rng(seed + 9).random((h, w)).astype(np.float32), 22) - 0.5
    col[..., 0] += hue * 0.10; col[..., 2] -= hue * 0.10
    if rim is not None:
        sh = np.roll(np.roll(m, int(-lx * 5), axis=1), int(-ly * 5), axis=0)
        band = np.clip(m - sh, 0, 1)
        band = blur(band, 1.5)[..., None] * 0.9
        col = col * (1 - band) + hexrgb(rim) * band
    a = blur(m, 0.8)
    out = np.dstack([np.clip(col, 0, 1), a])
    return Image.fromarray((out * 255).astype(np.uint8))


def glow(w, h, cx, cy, r, color, strength=1.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
    a = np.clip(1 - d, 0, 1) ** 2 * strength
    out = np.dstack([np.broadcast_to(hexrgb(color), (h, w, 3)), a])
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def over(base, *layers):
    for l in layers:
        base = Image.alpha_composite(base, l)
    return base


def canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def blob(cx, cy, r, n=9, jitter=0.18, seed=0, squash=1.0):
    rnd = random.Random(seed)
    pts = [(cx + math.cos(i / n * math.tau) * r * (1 + rnd.uniform(-jitter, jitter)),
            cy + math.sin(i / n * math.tau) * r * squash * (1 + rnd.uniform(-jitter, jitter))) for i in range(n)]
    return closed(pts)


def closed(pts, k=0.18):
    n = len(pts)
    d = f"M {pts[0][0]:.1f} {pts[0][1]:.1f} "
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        d += (f"C {p1[0] + (p2[0] - p0[0]) * k:.1f} {p1[1] + (p2[1] - p0[1]) * k:.1f} "
              f"{p2[0] - (p3[0] - p1[0]) * k:.1f} {p2[1] - (p3[1] - p1[1]) * k:.1f} {p2[0]:.1f} {p2[1]:.1f} ")
    return f'<path d="{d}Z"/>'
