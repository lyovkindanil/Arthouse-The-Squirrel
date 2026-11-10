"""Пробные ассеты в «живописном» духе: без контура, объём светом, мягкая дымка."""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageFilter
from paint import *

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)


def grass_fringe(w, y, seed, n=60, hmin=10, hmax=34):
    rnd = random.Random(seed)
    s = ""
    for _ in range(n):
        x = rnd.uniform(6, w - 6); hh = rnd.uniform(hmin, hmax); lean = rnd.uniform(-10, 10); bw = rnd.uniform(3, 6)
        s += f'<path d="M {x - bw:.0f} {y + 6} Q {x + lean * 0.3:.0f} {y - hh * 0.6:.0f} {x + lean:.0f} {y - hh:.0f} Q {x + lean * 0.4 + 2:.0f} {y - hh * 0.5:.0f} {x + bw:.0f} {y + 6} Z"/>'
    return s


def platform(w=640, h=300, seed=1):
    """Парящий кусок земли: камень-основание, сверху мох и трава, снизу корни."""
    rnd = random.Random(seed)
    top = 70
    pts = [(10, top + 20), (w * 0.25, top + 4), (w * 0.5, top), (w * 0.75, top + 6), (w - 10, top + 22),
           (w * 0.9, top + 90), (w * 0.72, h - 70 + rnd.uniform(-20, 20)), (w * 0.55, h - 20), (w * 0.4, h - 60), (w * 0.22, h - 90), (w * 0.08, top + 80)]
    rock = paint(w, h, closed(pts, 0.14), "#8a9aa0", "#4d5c66", "#1e2732", rim="#c9e6e0", volume=26, seed=seed, stroke=0.2, stroke_angle=-70)
    # Грани камня: несколько тёмных клиньев снизу.
    facets = "".join(closed([(x, h - 40 - rnd.uniform(0, 30)), (x + rnd.uniform(30, 60), top + 70 + rnd.uniform(0, 30)), (x + rnd.uniform(70, 110), h - 60 - rnd.uniform(0, 40))], 0.05) for x in (90, 230, 380, 470))
    fac = paint(w, h, facets, "#566772", "#34424d", "#172029", volume=10, seed=seed + 1, stroke=0.15)
    fac.putalpha(Image.fromarray((np.asarray(fac)[..., 3] * 0.55 * (np.asarray(rock)[..., 3] > 0)).astype(np.uint8)))
    moss_pts = [(4, top + 26), (w * 0.2, top - 2), (w * 0.5, top - 8), (w * 0.8, top), (w - 4, top + 28)]
    drip = []
    for i in range(12, 0, -1):
        x = i * w / 13
        drip.append((x, top + 34 + (rnd.uniform(14, 44) if i % 2 else rnd.uniform(0, 10))))
    moss = paint(w, h, closed(moss_pts + drip, 0.2), "#b9e07a", "#4f9a4a", "#1f5038", rim="#ecffc0", volume=12, seed=seed + 2, stroke=0.22, stroke_angle=10)
    blades_d = paint(w, h, grass_fringe(w, top + 6, seed + 3, 46, 14, 40), "#7fc85e", "#3f8a44", "#1f5038", volume=3, seed=seed + 3, stroke=0.1)
    blades_l = paint(w, h, grass_fringe(w, top + 10, seed + 4, 40, 8, 26), "#e0ff9a", "#9bd866", "#4f9a4a", volume=3, seed=seed + 4, stroke=0.1)
    roots = "".join(f'<path d="M {x} {h - 80} q {rnd.uniform(-14, 14):.0f} 30 {rnd.uniform(-8, 8):.0f} {rnd.uniform(40, 70):.0f} l 5 0 q 6 -30 4 -70 Z"/>' for x in (180, 300, 420))
    rt = paint(w, h, roots, "#b08a5a", "#6a4a30", "#2a1a12", volume=3, seed=seed + 5)
    return over(canvas(w, h), rt, rock, fac, blades_d, moss, blades_l)


def boulder(w=260, h=220, seed=5):
    rock = paint(w, h, blob(w / 2, h * 0.58, 92, 9, 0.2, seed, 0.8), "#93a4a6", "#516068", "#1c2530", rim="#d2efe6", volume=24, seed=seed, stroke=0.22, stroke_angle=-60)
    cap = paint(w, h, blob(w * 0.42, h * 0.3, 62, 8, 0.25, seed + 1, 0.42), "#bfe37f", "#4f9a4a", "#1f5038", rim="#ecffc0", volume=10, seed=seed + 1, stroke=0.2)
    return over(canvas(w, h), rock, cap)


def mushrooms(w=300, h=320, seed=7):
    """Светящиеся грибы: тёплый свет (не голубой — голубое в игре означает «подбери»)."""
    img = canvas(w, h)
    for cx, base, hh, r, s in ((190, 310, 190, 78, 0), (95, 312, 120, 52, 1), (250, 314, 70, 30, 2)):
        img = over(img, glow(w, h, cx, base - hh, r * 2.1, "#ffb060", 0.55))
        stem = paint(w, h, f'<path d="M {cx - r * 0.2} {base} C {cx - r * 0.26} {base - hh * 0.5} {cx - r * 0.12} {base - hh * 0.8} {cx - r * 0.1} {base - hh} L {cx + r * 0.1} {base - hh} C {cx + r * 0.16} {base - hh * 0.6} {cx + r * 0.24} {base - hh * 0.3} {cx + r * 0.22} {base} Z"/>',
                     "#fff1cf", "#d9b88a", "#7a5a48", volume=7, seed=seed + s)
        gills = paint(w, h, f'<ellipse cx="{cx}" cy="{base - hh + 4}" rx="{r * 0.9}" ry="{r * 0.2}"/>', "#ffd58a", "#c9773e", "#6a3324", volume=6, seed=seed + s + 10)
        cap = paint(w, h, f'<path d="M {cx - r} {base - hh + 4} C {cx - r} {base - hh - r * 0.95} {cx + r} {base - hh - r * 0.95} {cx + r} {base - hh + 4} C {cx + r * 0.5} {base - hh - 8} {cx - r * 0.5} {base - hh - 8} {cx - r} {base - hh + 4} Z"/>',
                    "#ffe9a8", "#f08a3c", "#8a2f2a", rim="#fff6d6", volume=16, seed=seed + s + 20, stroke=0.18)
        rnd = random.Random(seed + s)
        spots = "".join(f'<ellipse cx="{cx + rnd.uniform(-0.6, 0.6) * r:.0f}" cy="{base - hh - rnd.uniform(0.2, 0.6) * r:.0f}" rx="{rnd.uniform(4, 9):.0f}" ry="{rnd.uniform(3, 6):.0f}"/>' for _ in range(6))
        sp = paint(w, h, spots, "#ffffff", "#fff0c8", "#f2c98a", volume=3, seed=seed)
        img = over(img, stem, gills, cap, sp)
    return img


def fern(w=280, h=300, seed=9):
    """Папоротник с завитком — сквозной мотив колыбели."""
    img = canvas(w, h)
    rnd = random.Random(seed)
    for i, (ang, L) in enumerate(((-70, 250), (-110, 220), (-45, 190), (-135, 170))):
        a = math.radians(ang)
        bx, by = w / 2, h - 6
        leaves = ""
        for k in range(1, 12):
            t = k / 12
            px, py = bx + math.cos(a) * L * t + math.cos(a) * 30 * t * t * (1 if ang > -90 else -1), by + math.sin(a) * L * t + 60 * t * t
            ll = 46 * (1 - t) + 10
            for sgn in (-1, 1):
                na = a + sgn * 1.25
                ex, ey = px + math.cos(na) * ll, py + math.sin(na) * ll
                leaves += f'<path d="M {px:.0f} {py:.0f} Q {(px + ex) / 2 + 8:.0f} {(py + ey) / 2 - 8:.0f} {ex:.0f} {ey:.0f} Q {(px + ex) / 2 - 6:.0f} {(py + ey) / 2 + 8:.0f} {px:.0f} {py:.0f} Z"/>'
        frond = paint(w, h, leaves, "#c8f08a", "#4fa052", "#184a3a", rim="#f0ffc8" if i < 2 else None, volume=4, seed=seed + i, stroke=0.15)
        img = over(img, frond)
    return img


def squirrel(w=300, h=280, seed=11):
    """Белка в том же приёме: объём светом, пушистый хвост, без контура."""
    def tail_shape():
        pts_l, pts_r = [], []
        n = 12; x, y = 108.0, 214.0; step = 24
        spine = [(x, y)]
        for i in range(1, n):
            t = (i - 0.5) / (n - 1)
            th = math.radians(165 - 250 * t ** 1.3)
            x += math.cos(th) * step; y -= math.sin(th) * step
            spine.append((x, y))
        widths = [10, 22, 32, 40, 44, 45, 43, 38, 32, 25, 17, 8]
        rnd = random.Random(seed)
        for i, (px, py) in enumerate(spine):
            a = spine[max(i - 1, 0)]; b = spine[min(i + 1, n - 1)]
            dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy)
            nx, ny = -dy / L, dx / L
            wv = widths[i] * (1 + rnd.uniform(-0.12, 0.12))
            pts_l.append((px + nx * wv, py + ny * wv)); pts_r.append((px - nx * widths[i], py - ny * widths[i]))
        return closed(pts_l + pts_r[::-1], 0.16)
    tail = paint(w, h, tail_shape(), "#ffc27a", "#d86a28", "#6a2416", rim="#ffe9bf", volume=20, seed=seed, stroke=0.3, stroke_angle=60)
    far_leg = paint(w, h, '<ellipse cx="150" cy="262" rx="22" ry="9"/>', "#c86a2c", "#8a3f1c", "#4a1c10", volume=5, seed=seed + 1)
    body = paint(w, h, blob(160, 212, 50, 10, 0.05, seed, 0.86), "#ffb86a", "#e07a30", "#7a2c16", rim="#ffe2b0", volume=22, seed=seed + 2, stroke=0.2)
    belly = paint(w, h, '<ellipse cx="182" cy="226" rx="22" ry="27"/>', "#fff4dc", "#f6d7a8", "#d49a66", volume=10, seed=seed + 3)
    haunch = paint(w, h, '<ellipse cx="138" cy="228" rx="31" ry="28"/>', "#ffb86a", "#dc742c", "#7a2c16", rim="#ffe2b0", volume=14, seed=seed + 4, stroke=0.2)
    foot = paint(w, h, '<ellipse cx="156" cy="264" rx="26" ry="9"/>', "#ffc890", "#e08a44", "#7a3418", volume=5, seed=seed + 5)
    arm = paint(w, h, '<path d="M 190 222 q 14 12 14 34 l 10 0 q 0 -28 -12 -42 Z"/>', "#ffc080", "#e07a30", "#7a2c16", volume=4, seed=seed + 6)
    ear = paint(w, h, '<path d="M 186 150 Q 186 112 200 88 Q 206 80 210 92 Q 222 120 220 150 Z"/>', "#ffb86a", "#c85a22", "#6a2416", rim="#ffe2b0", volume=6, seed=seed + 7)
    head = paint(w, h, blob(206, 166, 35, 9, 0.04, seed + 1) + '<ellipse cx="236" cy="176" rx="20" ry="15"/>', "#ffc27a", "#e88436", "#8a3418", rim="#ffe9bf", volume=18, seed=seed + 8, stroke=0.18)
    cheek = paint(w, h, '<ellipse cx="232" cy="184" rx="17" ry="10"/>', "#fff6e2", "#f8dcb0", "#dca672", volume=6, seed=seed + 9)
    eye = paint(w, h, '<ellipse cx="216" cy="158" rx="8" ry="9.5"/>', "#3a2a22", "#1a1210", "#0a0706", volume=4, seed=1)
    shine = paint(w, h, '<circle cx="219" cy="154" r="3"/><circle cx="213" cy="162" r="1.4"/>', "#ffffff", "#ffffff", "#e8f4ff", volume=1)
    nose = paint(w, h, '<ellipse cx="254" cy="171" rx="6" ry="5"/>', "#5a3a30", "#2a1a16", "#120a08", volume=3)
    return over(canvas(w, h), tail, far_leg, body, belly, haunch, foot, arm, ear, head, cheek, eye, shine, nose)


def memory_leaf(w=160, h=160, seed=3):
    pts = [(80 + math.cos(-math.pi / 2 + i / 10 * math.tau) * (44 if i % 2 == 0 else 22), 78 + math.sin(-math.pi / 2 + i / 10 * math.tau) * (44 if i % 2 == 0 else 22)) for i in range(10)]
    leaf = paint(w, h, closed(pts, 0.12), "#ffffff", "#a8f2ff", "#3aa8d8", rim="#ffffff", volume=8, seed=seed, stroke=0.1)
    return over(canvas(w, h), glow(w, h, 80, 80, 80, "#8ff0ff", 0.9), leaf)


def background(w=1920, h=1080, seed=21):
    """Глубина дымкой: дальние стволы светлее и синее, ближние — темнее. Лучи света сверху слева."""
    rng = np.random.default_rng(seed)
    yy = np.linspace(0, 1, h)[:, None]
    top, mid, bot = hexrgb("#2b2350"), hexrgb("#2f6f72"), hexrgb("#123033")
    col = np.where(yy[..., None] < 0.55, top + (mid - top) * (yy[..., None] / 0.55), mid + (bot - mid) * ((yy[..., None] - 0.55) / 0.45))
    col = np.broadcast_to(col, (h, w, 3)).copy()
    col += (blur(rng.random((h, w)).astype(np.float32), 40)[..., None] - 0.5) * 0.12
    img = Image.fromarray((np.clip(np.dstack([col, np.ones((h, w))]), 0, 1) * 255).astype(np.uint8), "RGBA")
    img = over(img, glow(w, h, w * 0.3, h * 0.25, h * 0.9, "#9be8c8", 0.5))
    rnd = random.Random(seed)
    for depth, (lt, md, dk, n, wd) in enumerate((("#5a9a98", "#3f7f84", "#2f666e", 7, 60), ("#3f8078", "#285a5c", "#163c40", 6, 110), ("#2a5a50", "#143530", "#081a1a", 4, 190))):
        shapes = ""
        for i in range(n):
            x = (i + rnd.uniform(0.1, 0.9)) * w / n; ww = wd * rnd.uniform(0.7, 1.3); lean = rnd.uniform(-60, 60)
            shapes += f'<path d="M {x - ww:.0f} {h} C {x - ww * 0.5:.0f} {h * 0.6:.0f} {x - ww * 0.4 + lean:.0f} {h * 0.3:.0f} {x - ww * 0.45 + lean:.0f} -20 L {x + ww * 0.45 + lean:.0f} -20 C {x + ww * 0.4 + lean:.0f} {h * 0.3:.0f} {x + ww * 0.5:.0f} {h * 0.6:.0f} {x + ww:.0f} {h} Z"/>'
        hills = [(0, h)] + [(x, h - (120 + depth * 50) - 50 * math.sin(x / w * 9 + depth * 2)) for x in range(0, w + 1, 120)] + [(w, h)]
        shapes += '<path d="M ' + " L ".join(f"{x:.0f} {y:.0f}" for x, y in hills) + ' Z"/>'
        layer = paint(w, h, shapes, lt, md, dk, volume=30 + depth * 10, seed=seed + depth, stroke=0.2, stroke_angle=-8)
        img = over(img, layer)
        # Дымка между планами.
        fog = np.zeros((h, w, 4), np.float32); fog[..., :3] = hexrgb("#7fd0c0")
        fog[..., 3] = blur(rng.random((h, w)).astype(np.float32), 60) * np.linspace(0.05, 0.5, h)[:, None] * (0.8 - depth * 0.2)
        img = over(img, Image.fromarray((np.clip(fog, 0, 1) * 255).astype(np.uint8), "RGBA"))
    # Лучи.
    rays = np.zeros((h, w), np.float32)
    xx, yv = np.meshgrid(np.arange(w), np.arange(h))
    u = (xx - yv * 0.55) / w
    for c, wdt in ((0.1, 0.03), (0.22, 0.05), (0.36, 0.025), (0.5, 0.04)):
        rays += np.exp(-((u - c) / wdt) ** 2)
    rays *= np.linspace(1, 0.1, h)[:, None] * 0.22
    ray = np.zeros((h, w, 4), np.float32); ray[..., :3] = hexrgb("#e8ffd8"); ray[..., 3] = rays
    img = over(img, Image.fromarray((np.clip(ray, 0, 1) * 255).astype(np.uint8), "RGBA"))
    return img


def scene():
    bg = background()
    W, H = bg.size
    def put(im, x, y, s=1.0):  # x, y — низ по центру
        if s != 1.0: im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
        layer = canvas(W, H); layer.paste(im, (int(x - im.width / 2), int(y - im.height)), im)
        return layer
    p1, p2, p3 = platform(760, 320, 1), platform(520, 260, 2), platform(900, 340, 3)
    img = over(bg, put(p2, 1480, 560), put(fern(), 1440, 388, 0.8), put(p3, 520, 1090), put(p1, 1380, 1000),
               put(boulder(), 250, 838, 1.0), put(mushrooms(), 700, 842, 0.9), put(fern(), 880, 840, 1.0), put(fern(seed=4), 1650, 742, 0.9),
               put(mushrooms(seed=8), 1560, 388, 0.5), put(squirrel(), 1180, 742, 0.62), put(memory_leaf(), 1500, 300, 0.8), put(boulder(seed=12), 1560, 772, 0.5))
    # Ближний план: тёмные листья по углам.
    fg = paint(W, H, f'<path d="M -40 {H} C 60 {H - 380} 260 {H - 300} 300 {H - 120} C 340 {H - 60} 380 {H} 380 {H} Z"/><path d="M {W + 40} {H} C {W - 100} {H - 300} {W - 300} {H - 200} {W - 340} {H} Z"/>', "#14403a", "#0a2422", "#041010", volume=30, seed=40)
    return over(img, fg)


if __name__ == "__main__":
    items = {"platform": platform(), "boulder": boulder(), "mushrooms": mushrooms(), "fern": fern(), "squirrel": squirrel(), "memory": memory_leaf()}
    for n, im in items.items(): im.save(os.path.join(OUT, n + ".png"))
    scene().convert("RGB").save(os.path.join(OUT, "scene.png"))
