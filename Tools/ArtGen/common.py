"""Общие помощники генератора арта: SVG → PNG (cairosvg) + бумажное зерно."""
import io, os, math, random
import cairosvg
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Squirrel", "Art")
PPU = 128  # пикселей на единицу мира (клетка карты = 1 ед.)

# --- Палитра Мшистой колыбели ---
INK = "#1d1712"          # общий контур: тёплая тушь
SQ_FUR = "#e2843a"       # белка — тёплый оранжевый, контраст холодному фону
SQ_FUR_D = "#b45a24"
SQ_FUR_L = "#f4ad62"
SQ_CREAM = "#f6dcb0"
DANGER = "#ff3d3a"       # красный = опасность (глаза врагов, шипы)
DANGER_GLOW = "#ff6a4a"
PICKUP = "#8ff0ff"       # голубой = подбери (воспоминания)
WARM = "#ffc46b"         # тёплый = безопасно (гнёзда)
HEART = "#ff6f9a"
EDGE_FRONT = "#ff8a3a"   # очаг Края
EDGE_RING = "#c09aff"    # кольцо Края
VOID = "#0d0814"


def svg(w, h, body, defs=""):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">'
            f'<defs>{defs}</defs>{body}</svg>')


def render(svg_text, scale=1.0):
    png = cairosvg.svg2png(bytestring=svg_text.encode("utf-8"), scale=scale)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def grain(img, amount=0.07, seed=0, wrap=False):
    """Бумажное зерно: мягкий шум яркости только по непрозрачным пикселям."""
    rng = np.random.default_rng(seed)
    a = np.asarray(img).astype(np.float32) / 255.0
    h, w = a.shape[:2]
    n = rng.normal(0, 1, (h, w)).astype(np.float32)
    # Немного крупнее пикселя — похоже на фактуру бумаги, а не на цифровой шум.
    small = rng.normal(0, 1, (h // 4 + 2, w // 4 + 2)).astype(np.float32)
    big = np.asarray(Image.fromarray(((small * 0.25 + 0.5).clip(0, 1) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32) / 255.0 - 0.5
    noise = n * 0.5 + big * 2.0
    k = 1.0 + noise * amount
    a[..., :3] = (a[..., :3] * k[..., None]).clip(0, 1)
    return Image.fromarray((a * 255).astype(np.uint8))


def save(img, rel):
    path = os.path.join(ART, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


def radial(id_, stops):
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return f'<radialGradient id="{id_}" cx="50%" cy="50%" r="50%">{s}</radialGradient>'


def linear(id_, stops, x1=0, y1=0, x2=0, y2=1):
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return f'<linearGradient id="{id_}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{s}</linearGradient>'


def blob(cx, cy, r, n=9, jitter=0.18, seed=0, squash=1.0):
    """Неровный «вырезанный ножницами» круг — замкнутый сглаженный путь."""
    rnd = random.Random(seed)
    pts = []
    for i in range(n):
        t = i / n * math.tau
        rr = r * (1 + rnd.uniform(-jitter, jitter))
        pts.append((cx + math.cos(t) * rr, cy + math.sin(t) * rr * squash))
    return smooth_closed(pts)


def smooth_closed(pts, k=0.18):
    """Catmull-Rom → кубические Безье, замкнутый путь."""
    n = len(pts)
    d = f"M {pts[0][0]:.1f} {pts[0][1]:.1f} "
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        c1 = (p1[0] + (p2[0] - p0[0]) * k * 1.0, p1[1] + (p2[1] - p0[1]) * k)
        c2 = (p2[0] - (p3[0] - p1[0]) * k, p2[1] - (p3[1] - p1[1]) * k)
        d += f"C {c1[0]:.1f} {c1[1]:.1f} {c2[0]:.1f} {c2[1]:.1f} {p2[0]:.1f} {p2[1]:.1f} "
    return d + "Z"


def smooth_open(pts, k=0.18):
    n = len(pts)
    d = f"M {pts[0][0]:.1f} {pts[0][1]:.1f} "
    for i in range(n - 1):
        p0 = pts[max(i - 1, 0)]; p1 = pts[i]; p2 = pts[i + 1]; p3 = pts[min(i + 2, n - 1)]
        c1 = (p1[0] + (p2[0] - p0[0]) * k, p1[1] + (p2[1] - p0[1]) * k)
        c2 = (p2[0] - (p3[0] - p1[0]) * k, p2[1] - (p3[1] - p1[1]) * k)
        d += f"C {c1[0]:.1f} {c1[1]:.1f} {c2[0]:.1f} {c2[1]:.1f} {p2[0]:.1f} {p2[1]:.1f} "
    return d


def write_manifest(folder, data):
    """sprites.json рядом с картинками: пивоты и прочее для импортёра в Unity."""
    import json
    path = os.path.join(ART, folder, "sprites.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"ppu": PPU, "sprites": data}, f, ensure_ascii=False, indent=1)
