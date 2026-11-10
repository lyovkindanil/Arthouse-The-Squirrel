"""Белка: позы для простого покадрового аниматора. Смотрит вправо, пивот — у ног (низ по центру)."""
import math
from common import *

BW, BH = 320, 352  # рабочий холст; потом обрезаем по общему габариту всех поз
OX, OY = 160 - 106, 352 - 160  # ноги белки — в низ по центру холста
STROKE = 3.5


def ribbon(spine, widths, jitter_out=0.0, seed=0):
    """Толстая изогнутая лента по «хребту» — для хвоста. Наружный край чуть пушистый."""
    rnd = random.Random(seed)
    left, right = [], []
    for i, (x, y) in enumerate(spine):
        a = spine[max(i - 1, 0)]; b = spine[min(i + 1, len(spine) - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        w = widths[i]
        wo = w * (1 + rnd.uniform(-jitter_out, jitter_out))
        left.append((x + nx * wo, y + ny * wo))
        right.append((x - nx * w, y - ny * w))
    pts = left + right[::-1]
    return smooth_closed(pts, 0.16)


TAIL_W = [9, 16, 22, 26, 27, 26, 23, 19, 14, 8]


def tail_spine(curl, length=148):
    n = len(TAIL_W)
    pts, x, y = [(0.0, 0.0)], 0.0, 0.0
    step = length / (n - 1)
    for i in range(1, n):
        t = (i - 0.5) / (n - 1)
        th = math.radians(172 - 250 * curl * t ** 1.35)
        x += math.cos(th) * step
        y -= math.sin(th) * step  # экранная y вниз
        pts.append((x, y))
    return pts


def pose(tail=0, tilt=0, sx=1.0, sy=1.0, lift=0, hind=(0, 0), front=(0, 0), hind_rot=0, ear=0,
         eyes="open", tail_curl=1.0, whole_rot=0, dx=0):
    # Хвост строится по дуге: начинается назад-влево и закручивается вверх и вперёд.
    # tail_curl = 1 — полный завиток (стоит), меньше — хвост вытянут назад (бег, прыжок).
    spine = tail_spine(tail_curl)
    tail_d = ribbon(spine, TAIL_W, 0.12, seed=3)
    stripe = ribbon(spine[1:-2], [w * 0.36 for w in TAIL_W[1:-2]], 0.0)

    o = f'stroke="{INK}" stroke-width="{STROKE}" stroke-linejoin="round" stroke-linecap="round"'
    def ow(w): return f'stroke="{INK}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"'
    hx, hy = hind; fx, fy = front
    body = f'''
    <g transform="translate({OX + dx} {OY - lift}) rotate({whole_rot} 104 112)">
      <g transform="translate(80 112) rotate({tail})">
        <path d="{tail_d}" fill="{SQ_FUR_D}" {o}/>
        <path d="{stripe}" fill="{SQ_FUR}" opacity="0.9"/>
      </g>
      <g transform="translate(106 118) rotate({tilt}) scale({sx} {sy}) translate(-106 -118)">
        <!-- дальняя задняя лапа -->
        <ellipse cx="{98 + hx * 0.6}" cy="{150 + hy * 0.6}" rx="11" ry="4.5" fill="{SQ_FUR_D}" {o}/>
        <path d="{blob(106, 118, 29, 10, 0.06, seed=5, squash=0.82)}" fill="{SQ_FUR}" {o}/>
        <ellipse cx="117" cy="126" rx="13" ry="15" fill="{SQ_CREAM}"/>
        <g transform="rotate({hind_rot} 92 128)">
          <ellipse cx="92" cy="128" rx="18" ry="16" fill="{SQ_FUR}" {o}/>
          <path d="M 82 132 Q 90 120 101 126" fill="none" stroke="{SQ_FUR_D}" stroke-width="2.5" stroke-linecap="round"/>
        </g>
        <ellipse cx="{102 + hx}" cy="{151 + hy}" rx="14" ry="5" fill="{SQ_FUR}" {o}/>
        <path d="M {124 + fx * 0.5} 132 Q {126 + fx} {142 + fy * 0.5} {128 + fx} {150 + fy}" fill="none" {ow(9)}/>
        <path d="M {124 + fx * 0.5} 132 Q {126 + fx} {142 + fy * 0.5} {128 + fx} {150 + fy}" fill="none" stroke="{SQ_FUR}" stroke-width="3.5" stroke-linecap="round"/>
        <!-- голова -->
        <g transform="rotate({ear} 127 76)">
          <path d="M 120 80 Q 120 58 128 44 Q 131 40 133 46 Q 140 62 139 80 Z" fill="{SQ_FUR_D}" {o}/>
          <path d="M 128 44 L 124 34 M 130 44 L 131 32 M 131 46 L 137 36" {ow(2.5)}/>
          <path d="M 125 74 Q 128 58 130 52" fill="none" stroke="{SQ_FUR_L}" stroke-width="3" stroke-linecap="round"/>
        </g>
        <path d="{blob(132, 92, 20, 9, 0.05, seed=8)}" fill="{SQ_FUR}" {o}/>
        <ellipse cx="148" cy="98" rx="11" ry="8.5" fill="{SQ_FUR}" {o}/>
        <ellipse cx="146" cy="103" rx="9" ry="5" fill="{SQ_CREAM}"/>
        <ellipse cx="158" cy="95.5" rx="3.6" ry="3" fill="{INK}"/>
        {eye(eyes)}
      </g>
    </g>'''
    return svg(BW, BH, body)


def eye(kind):
    if kind == "closed":
        return f'<path d="M 133 88 Q 138 92 143 88" fill="none" stroke="{INK}" stroke-width="3" stroke-linecap="round"/>'
    if kind == "hurt":
        return f'<path d="M 134 84 L 142 92 M 142 84 L 134 92" stroke="{INK}" stroke-width="3" stroke-linecap="round"/>'
    return (f'<ellipse cx="138" cy="88" rx="4.8" ry="5.8" fill="{INK}"/>'
            f'<circle cx="139.8" cy="86" r="1.7" fill="#fff8e8"/>')


POSES = {
    "idle_0": dict(),
    "idle_1": dict(sy=0.97, sx=1.02, tail=4, ear=-3),
    "idle_blink": dict(sy=0.97, sx=1.02, tail=4, eyes="closed"),
    # Бег: 4 кадра (контакт, сжатие, толчок, полёт).
    "run_0": dict(tilt=-6, sx=1.08, sy=0.94, tail=-16, tail_curl=0.55, hind=(-14, 0), front=(10, 0), hind_rot=-12),
    "run_1": dict(tilt=-2, sx=0.96, sy=1.0, tail=-10, tail_curl=0.6, hind=(4, -2), front=(-4, -4), lift=2, hind_rot=6),
    "run_2": dict(tilt=-10, sx=1.12, sy=0.9, tail=-20, tail_curl=0.5, hind=(16, -4), front=(-10, -2), lift=6, hind_rot=18),
    "run_3": dict(tilt=-4, sx=1.04, sy=0.96, tail=-14, tail_curl=0.55, hind=(-6, -8), front=(14, -10), lift=10, hind_rot=-4),
    "jump": dict(tilt=-22, sx=0.94, sy=1.08, tail=24, tail_curl=0.5, hind=(-12, 4), front=(8, -14), ear=-14, lift=4, hind_rot=-20),
    "fall": dict(tilt=10, sx=1.02, sy=0.98, tail=-28, tail_curl=0.85, hind=(10, -6), front=(10, -2), ear=10, lift=4, hind_rot=10),
    "land": dict(tilt=4, sx=1.16, sy=0.84, tail=8, hind=(-4, 0), front=(4, 0)),
    # На стене: белка повёрнута головой вверх, брюхом к стене справа.
    "cling": dict(whole_rot=-78, tail=-30, tail_curl=0.8, hind=(4, 0), front=(0, 4), dx=-14, lift=96, eyes="open"),
    "hurt": dict(tilt=14, sx=0.92, sy=1.06, tail=30, ear=18, eyes="hurt", lift=6),
    "attack": dict(tilt=-4, tail=70, tail_curl=0.7, sx=1.04, sy=0.96, front=(8, -2)),
    "heal": dict(sy=0.95, sx=1.04, tail=10, eyes="closed", ear=-6),
}


def build():
    """Каждый кадр обрезается по себе; пивот (в долях) пишется в манифест — им пользуется импорт в Unity.
    Пивот обычных поз — точка земли под белкой, у «стены» — низ кадра под серединой тела."""
    manifest = {}
    for i, (n, p) in enumerate(POSES.items()):
        im = grain(render(pose(**p)), 0.05, seed=i * 7 + 1)
        x0, y0, x1, y1 = im.getbbox()
        x0, y0, x1, y1 = max(x0 - 2, 0), max(y0 - 2, 0), min(x1 + 2, BW), min(y1 + 2, BH)
        fx, fy = BW / 2, BH - 2  # точка земли на рабочем холсте
        if n == "cling":
            fx, fy = (x0 + x1) / 2, y1
        y1 = max(y1, int(fy))
        crop = im.crop((x0, y0, x1, y1))
        save(crop, f"Squirrel/{n}.png")
        manifest[n] = {"pivot": [round((fx - x0) / (x1 - x0), 4), round((y1 - fy) / (y1 - y0), 4)]}
    write_manifest("Squirrel", manifest)
    return manifest


if __name__ == "__main__":
    for k, v in build().items(): print(k, v)
