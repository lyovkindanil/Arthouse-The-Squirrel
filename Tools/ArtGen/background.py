"""Фон Мшистой колыбели: 3 плана для параллакса, бесшовные по горизонтали.
Тихие холодные тона, чтобы белка (тёплая) и опасности (красные) читались поверх."""
import math
from common import *

BW, BH = 2048, 1152  # 16 × 9 ед.


def wrap(fn, x, *a, **k):
    """Рисуем элемент ещё и со сдвигом на ширину — так края стыкуются."""
    return fn(x, *a, **k) + fn(x - BW, *a, **k) + fn(x + BW, *a, **k)


def spiral(cx, cy, r, turns=2.2, w=14, color="#000", stem_to=None):
    """Завиток папоротника — сквозной мотив колыбели (сон, свернувшаяся белка)."""
    pts = []
    n = 60
    for i in range(n + 1):
        t = i / n
        a = t * turns * math.tau
        rr = r * (1 - t) ** 1.2
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return f'<path d="{smooth_open(pts[::-1] + [(cx + r * 1.1, cy + r * 1.6), (cx + r * 1.2, stem_to or cy + r * 3.5)])}" fill="none" stroke="{color}" stroke-width="{w}" stroke-linecap="round"/>'


def far():
    rnd = random.Random(1)
    defs = linear("sky", [(0, "#251d3a", 1), (0.55, "#173033", 1), (1, "#10201f", 1)])
    defs += radial("haze", [(0, "#5a8a7a", 0.35), (1, "#5a8a7a", 0)])
    body = f'<rect width="{BW}" height="{BH}" fill="url(#sky)"/>'
    body += f'<ellipse cx="{BW * 0.5}" cy="{BH * 0.62}" rx="{BW * 0.7}" ry="{BH * 0.35}" fill="url(#haze)"/>'
    # Спящее «око» в небе: закрытое веко с ресницами. Очень тихо, почти фон.
    def eye(x):
        lashes = "".join(f'<path d="M {x + dx} {300 + 0.0016 * dx * dx * 1.2:.0f} l {dx * 0.12:.0f} 34" stroke="#3d4a6a" stroke-width="6" stroke-linecap="round"/>' for dx in range(-150, 151, 50))
        return (f'<path d="M {x - 190} 290 Q {x} 410 {x + 190} 290" fill="none" stroke="#3d4a6a" stroke-width="10" stroke-linecap="round"/>'
                f'<path d="M {x - 190} 290 Q {x} 170 {x + 190} 290 Q {x} 410 {x - 190} 290 Z" fill="#2a2c4a" opacity="0.6"/>{lashes}')
    body += wrap(eye, 1400)
    # Далёкие гигантские грибы и холмы мха.
    def shroom(x, h, w, col):
        return (f'<path d="M {x - w * 0.08} {BH} L {x - w * 0.06} {BH - h} L {x + w * 0.06} {BH - h} L {x + w * 0.08} {BH} Z" fill="{col}"/>'
                f'<path d="M {x - w / 2} {BH - h + 10} Q {x} {BH - h - w * 0.55} {x + w / 2} {BH - h + 10} Z" fill="{col}"/>')
    for x, h, w in ((300, 620, 360), (900, 480, 260), (1650, 700, 420)):
        body += wrap(shroom, x, h, w, "#1c3a3a")
    hills = " ".join(f"L {x} {BH - 220 - 60 * math.sin(x / BW * math.tau * 3) - 30 * math.sin(x / BW * math.tau * 5 + 1):.0f}" for x in range(0, BW + 1, 32))
    body += f'<path d="M 0 {BH} {hills} L {BW} {BH} Z" fill="#183230"/>'
    # Споры — тёплые бледные точки (не голубые: голубое = подбери).
    for _ in range(70):
        x, y, r = rnd.uniform(0, BW), rnd.uniform(0, BH * 0.8), rnd.uniform(1.5, 4)
        body += wrap(lambda xx: f'<circle cx="{xx:.0f}" cy="{y:.0f}" r="{r:.1f}" fill="#e8dca8" opacity="{rnd.uniform(0.15, 0.45):.2f}"/>', x)
    return svg(BW, BH, body, defs)


def mid():
    rnd = random.Random(2)
    body = ""
    col = "#132a29"
    # Свисающие корни сверху.
    for i in range(14):
        x = i * BW / 14 + rnd.uniform(-30, 30)
        L = rnd.uniform(160, 460)
        pts = [(x + rnd.uniform(-20, 20) * k, k * L / 4) for k in range(5)]
        d = smooth_open([(x, -10)] + pts[1:])
        body += wrap(lambda xx, d=d, x=x: f'<path d="{d}" transform="translate({xx - x:.0f} 0)" fill="none" stroke="{col}" stroke-width="{rnd.uniform(8, 22):.0f}" stroke-linecap="round"/>', x)
    body += f'<path d="M 0 0 L {BW} 0 L {BW} 60 ' + " ".join(f"L {x} {60 + 30 * math.sin(x / BW * math.tau * 4):.0f}" for x in range(BW, -1, -32)) + f' Z" fill="{col}"/>'
    # Завитки папоротников снизу.
    for x, r in ((250, 90), (760, 60), (1180, 110), (1720, 75)):
        body += wrap(lambda xx, r=r: spiral(xx, BH - 260 - r, r, w=r * 0.16, color="#17322f", stem_to=BH), x)
    ground = " ".join(f"L {x} {BH - 150 - 40 * math.sin(x / BW * math.tau * 2 + 0.5) - 20 * math.sin(x / BW * math.tau * 7):.0f}" for x in range(0, BW + 1, 32))
    body += f'<path d="M 0 {BH} {ground} L {BW} {BH} Z" fill="#11241f"/>'
    return svg(BW, BH, body)


def near():
    rnd = random.Random(3)
    body = ""
    # Крупные листья и травы по низу — тёмные, почти силуэт.
    for i in range(22):
        x = i * BW / 22 + rnd.uniform(-20, 20)
        h = rnd.uniform(120, 300); lean = rnd.uniform(-80, 80)
        d = f"M {x - 30} {BH} C {x - 30} {BH - h * 0.5} {x + lean - 26} {BH - h * 0.9} {x + lean} {BH - h} C {x + lean + 22} {BH - h * 0.8} {x + 34} {BH - h * 0.45} {x + 30} {BH} Z"
        body += wrap(lambda xx, d=d, x=x: f'<path d="{d}" transform="translate({xx - x:.0f} 0)" fill="#0b1815"/>', x)
    mounds = " ".join(f"L {x} {BH - 70 - 30 * math.sin(x / BW * math.tau * 3 + 2):.0f}" for x in range(0, BW + 1, 32))
    body += f'<path d="M 0 {BH} {mounds} L {BW} {BH} Z" fill="#0a1512"/>'
    return svg(BW, BH, body)


def build():
    save(grain(render(far()), 0.04, seed=1), "Background/bg_far.png")
    save(grain(render(mid()), 0.04, seed=2), "Background/bg_mid.png")
    save(grain(render(near()), 0.04, seed=3), "Background/bg_near.png")


if __name__ == "__main__":
    build()
