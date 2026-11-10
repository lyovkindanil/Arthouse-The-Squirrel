"""Объекты: гнездо, воспоминание, Сердце, святилище, очаг Края, камень, гусеница, запас, кокон, свечение.
Цветовой код как в прототипе: голубое = подбери, тёплое = безопасно, красное = опасно, оранжевое = Край."""
import math
from common import *

def ow(w=3.5):
    return f'stroke="{INK}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"'

BARK_D, BARK, BARK_L = "#3b2618", "#5e3d26", "#86603c"
MOSS_D, MOSS, MOSS_L = "#2f5a36", "#4f8a48", "#8cc067"


def nest():
    """Гнездо: чаша из веток с пухом, внутри тёплый свет. 2 × 1 ед."""
    w, h = 256, 128
    rnd = random.Random(4)
    twigs = ""
    for i in range(16):
        a = rnd.uniform(-0.5, 0.5); y = rnd.uniform(70, 110); x = rnd.uniform(30, 226)
        L = rnd.uniform(40, 80)
        x2, y2 = x + math.cos(a) * L, y + math.sin(a) * L * 0.4
        twigs += f'<path d="M {x:.0f} {y:.0f} L {x2:.0f} {y2:.0f}" {ow(7)}/><path d="M {x:.0f} {y:.0f} L {x2:.0f} {y2:.0f}" stroke="{rnd.choice([BARK, BARK_L, "#6e4a2c"])}" stroke-width="3.5" stroke-linecap="round"/>'
    defs = radial("warm", [(0, WARM, 0.9), (0.6, WARM, 0.35), (1, WARM, 0)])
    body = (f'<ellipse cx="128" cy="66" rx="92" ry="34" fill="url(#warm)"/>'
            f'<path d="M 22 74 C 26 116 230 116 234 74 C 200 92 56 92 22 74 Z" fill="{BARK}" {ow()}/>'
            f'<ellipse cx="128" cy="74" rx="96" ry="18" fill="#2a1a10" {ow()}/>'
            f'<ellipse cx="128" cy="76" rx="76" ry="11" fill="#f3d9a6" opacity="0.85"/>'
            + "".join(f'<path d="{blob(x, 72, 9, 7, 0.3, seed=x)}" fill="#fff0d0" opacity="0.9"/>' for x in (80, 104, 150, 172))
            + twigs)
    return svg(w, h, body, defs)


def memory(frame=0):
    """Воспоминание: светящийся голубым осенний лист — кусочек настоящего леса. ~0.5 ед. + ореол."""
    w = h = 96
    defs = radial("g", [(0, PICKUP, 0.8), (0.5, PICKUP, 0.25), (1, PICKUP, 0)])
    pts = []
    for i in range(10):  # «кленовый» контур: пять зубцов
        a = -math.pi / 2 + i / 10 * math.tau
        r = 30 if i % 2 == 0 else 15
        pts.append((48 + math.cos(a) * r, 46 + math.sin(a) * r))
    leaf = smooth_closed(pts, 0.12)
    body = (f'<circle cx="48" cy="48" r="46" fill="url(#g)"/>'
            f'<path d="{leaf}" fill="#c9f6ff" {ow(3)}/>'
            f'<path d="M 48 46 L 48 18 M 48 46 L 22 36 M 48 46 L 74 36 M 48 46 L 32 70 M 48 46 L 64 70 M 48 46 L 48 86" stroke="#5cc8e6" stroke-width="2.5" stroke-linecap="round" fill="none"/>'
            f'<circle cx="48" cy="46" r="5" fill="#ffffff"/>')
    return svg(w, h, body, defs)


def heart():
    """Сердце колыбели: светящееся семечко-сердце в колыбельке из корней. ~1.5 ед."""
    w = h = 192
    defs = radial("g", [(0, HEART, 0.9), (0.5, HEART, 0.3), (1, HEART, 0)])
    body = (f'<circle cx="96" cy="92" r="92" fill="url(#g)"/>'
            f'<path d="M 96 150 C 40 112 36 62 66 52 C 82 46 92 56 96 66 C 100 56 110 46 126 52 C 156 62 152 112 96 150 Z" fill="{HEART}" {ow(4)}/>'
            f'<path d="M 70 70 C 64 86 70 104 84 118" fill="none" stroke="#ffd0de" stroke-width="5" stroke-linecap="round"/>'
            f'<path d="M 30 150 C 60 186 132 186 162 150" fill="none" {ow(14)}/>'
            f'<path d="M 30 150 C 60 186 132 186 162 150" fill="none" stroke="{BARK_L}" stroke-width="7" stroke-linecap="round"/>')
    return svg(w, h, body, defs)


def shrine():
    """Святилище «отталкивания»: камень-постамент с вырезанным знаком лапки и зелёным светом. 1.2 × 1.6 ед."""
    w, h = 160, 208
    defs = radial("g", [(0, "#8dff9c", 0.8), (1, "#8dff9c", 0)])
    body = (f'<ellipse cx="80" cy="70" rx="70" ry="66" fill="url(#g)"/>'
            f'<path d="M 30 206 L 40 140 L 120 140 L 130 206 Z" fill="#56635f" {ow()}/>'
            f'<path d="M 24 140 L 136 140 L 128 122 L 32 122 Z" fill="#6c7a74" {ow()}/>'
            f'<path d="M 44 150 L 50 196 M 112 150 L 106 196" stroke="#3d4744" stroke-width="3" stroke-linecap="round"/>'
            f'<g transform="translate(80 70) rotate(45)"><rect x="-26" y="-26" width="52" height="52" rx="8" fill="#b9ffc2" {ow(3)}/></g>'
            # Знак: лапка, отталкивающаяся от стены.
            f'<path d="M 66 52 L 66 90" stroke="#2f6b3a" stroke-width="5" stroke-linecap="round"/>'
            f'<path d="M 74 72 L 98 62 M 74 72 L 98 80" stroke="#2f6b3a" stroke-width="4" stroke-linecap="round"/>')
    return svg(w, h, body, defs)


def edge_front(frame=0):
    """Очаг Края: дыра в мире с рваными оранжевыми краями — отсюда ползёт пустота. ~2 ед."""
    w = h = 256
    defs = radial("g", [(0, EDGE_FRONT, 0.7), (0.55, EDGE_FRONT, 0.2), (1, EDGE_FRONT, 0)])
    rnd = random.Random(9 + frame)
    cracks = ""
    for i in range(9):
        a = i / 9 * math.tau + rnd.uniform(-0.2, 0.2)
        r0, r1 = 46, rnd.uniform(70, 100)
        mid = (r0 + r1) / 2
        p = [(128 + math.cos(a) * r0, 128 + math.sin(a) * r0), (128 + math.cos(a + 0.12) * mid, 128 + math.sin(a + 0.12) * mid), (128 + math.cos(a) * r1, 128 + math.sin(a) * r1)]
        cracks += f'<path d="M {p[0][0]:.0f} {p[0][1]:.0f} L {p[1][0]:.0f} {p[1][1]:.0f} L {p[2][0]:.0f} {p[2][1]:.0f}" fill="none" stroke="{EDGE_FRONT}" stroke-width="4" stroke-linecap="round" opacity="0.85"/>'
    body = (f'<circle cx="128" cy="128" r="126" fill="url(#g)"/>{cracks}'
            f'<path d="{blob(128, 128, 54, 12, 0.18, seed=11 + frame)}" fill="{EDGE_FRONT}" {ow(4)}/>'
            f'<path d="{blob(128, 128, 44, 12, 0.2, seed=21 + frame)}" fill="{VOID}"/>'
            + "".join(f'<circle cx="{128 + rnd.uniform(-22, 22):.0f}" cy="{128 + rnd.uniform(-22, 22):.0f}" r="{rnd.uniform(1.5, 3):.1f}" fill="#8f7aa8" opacity="0.7"/>' for _ in range(8)))
    return svg(w, h, body, defs)


def rock():
    """Камень, замуровавший гусеницу: светлее обычного и в трещинах — видно, что бьётся. 1 × 2 ед."""
    w, h = 136, 264
    body = (f'<path d="M 8 258 C 4 180 6 60 22 14 C 50 2 90 2 116 14 C 132 70 132 190 128 258 Z" fill="#9b8c76" {ow(4)}/>'
            f'<path d="M 24 240 C 20 170 22 70 34 26 C 46 20 60 18 72 18 C 50 60 44 150 46 240 Z" fill="#b7a98e" opacity="0.7"/>'
            f'<path d="M 64 18 L 56 70 L 78 104 L 60 150 L 74 190" fill="none" stroke="{INK}" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>'
            f'<path d="M 78 104 L 108 120 M 60 150 L 30 170 M 56 70 L 34 82" fill="none" stroke="{INK}" stroke-width="3.5" stroke-linecap="round"/>')
    return svg(w, h, body)


def caterpillar(frame=0):
    """Гусеница: мягкая, светло-зелёная, большие добрые глаза. 1.2 × 0.6 ед."""
    w, h = 168, 96
    hump = 10 if frame else 0
    segs = ""
    for i, x in enumerate((30, 52, 74, 96, 116)):
        y = 70 - (hump if i in (1, 2) else 0)
        segs += f'<circle cx="{x}" cy="{y}" r="{17 - abs(i - 2) * 1.2:.1f}" fill="{"#a6d65a" if i % 2 else "#94c64c"}" {ow(3)}/>'
    body = (segs +
            f'<circle cx="138" cy="60" r="20" fill="#b8e070" {ow(3)}/>'
            f'<ellipse cx="146" cy="56" rx="5" ry="6.5" fill="{INK}"/><circle cx="148" cy="54" r="2" fill="#fff"/>'
            f'<path d="M 140 42 q -4 -16 -12 -18 M 150 42 q 4 -16 12 -16" fill="none" {ow(2.5)}/>'
            f'<path d="M 144 70 q 6 4 12 0" fill="none" {ow(2.5)}/>')
    return svg(w, h, body)


def stash():
    """Запас гусеницы: листья и ягоды. 1 × 0.7 ед."""
    w, h = 136, 104
    body = (f'<path d="M 10 98 C 14 60 40 44 68 44 C 98 44 124 60 126 98 Z" fill="{MOSS}" {ow()}/>'
            f'<path d="M 30 92 C 34 70 50 60 68 58" fill="none" stroke="{MOSS_L}" stroke-width="3" stroke-linecap="round"/>'
            f'<circle cx="54" cy="44" r="14" fill="#c23a5a" {ow(3)}/><circle cx="50" cy="40" r="4" fill="#ffd0dc"/>'
            f'<circle cx="82" cy="38" r="17" fill="#d24868" {ow(3)}/><circle cx="77" cy="33" r="5" fill="#ffd0dc"/>'
            f'<circle cx="100" cy="58" r="12" fill="#b03050" {ow(3)}/>')
    return svg(w, h, body)


def cocoon():
    w, h = 72, 104
    body = (f'<path d="M 36 4 C 60 20 64 70 40 100 C 14 80 10 26 36 4 Z" fill="#8e8672" {ow(3)}/>'
            + "".join(f'<path d="M {16 + i * 2} {30 + i * 14} Q 36 {36 + i * 14} {56 - i * 2} {28 + i * 14}" fill="none" stroke="#5f584a" stroke-width="2.5"/>' for i in range(4)))
    return svg(w, h, body)


def glow_soft():
    """Мягкое белое свечение — красится в Unity цветом (голубым, тёплым, оранжевым)."""
    w = h = 256
    defs = radial("g", [(0, "#ffffff", 1), (0.25, "#ffffff", 0.55), (0.6, "#ffffff", 0.15), (1, "#ffffff", 0)])
    return svg(w, h, f'<circle cx="128" cy="128" r="128" fill="url(#g)"/>', defs)


def build():
    m = {}
    items = {
        "nest": (nest(), [0.5, 0.12]), "memory": (memory(), [0.5, 0.5]), "heart": (heart(), [0.5, 0.5]),
        "shrine": (shrine(), [0.5, 0.0]), "edge_front_0": (edge_front(0), [0.5, 0.5]), "edge_front_1": (edge_front(1), [0.5, 0.5]),
        "rock": (rock(), [0.5, 0.5]), "caterpillar_0": (caterpillar(0), [0.5, 0.05]), "caterpillar_1": (caterpillar(1), [0.5, 0.05]),
        "stash": (stash(), [0.5, 0.02]), "cocoon": (cocoon(), [0.5, 0.02]),
    }
    for i, (n, (s, piv)) in enumerate(items.items()):
        save(grain(render(s), 0.05, seed=100 + i), f"Objects/{n}.png")
        m[n] = {"pivot": piv}
    save(render(glow_soft()), "Objects/glow_soft.png"); m["glow_soft"] = {"pivot": [0.5, 0.5]}
    write_manifest("Objects", m)


if __name__ == "__main__":
    build()
