"""Тайлы: камень и кора по маске открытых сторон (N=1, E=2, S=4, W=8) + шёлковый мост.
Клетка 128×128; над клеткой ещё 32 px запаса — туда торчит мох и травинки (пивот учитывает запас)."""
import math
from common import *

T = 128
HEAD = 32  # запас сверху под мох
N, E, S, W = 1, 2, 4, 8
STONE_D, STONE, STONE_L, STONE_RIM = "#20282c", "#2c363a", "#3a4549", "#5b6a68"
MOSS_D, MOSS, MOSS_L = "#2f5a36", "#4f8a48", "#8cc067"
BARK_D, BARK, BARK_L = "#3b2618", "#5e3d26", "#86603c"


def outline_path(mask, inset=0.0, r=26, over=0):
    """Контур клетки; углы скруглены там, где открыты обе соседние стороны.
    over — насколько вынести закрытые стороны за клетку (чтобы тушь не оставляла засечек на стыках)."""
    x0, y0, x1, y1 = 0 - (0 if mask & W else over), HEAD - (0 if mask & N else over), T + (0 if mask & E else over), HEAD + T + (0 if mask & S else over)
    i = inset
    xa = x0 + (i if mask & W else 0); xb = x1 - (i if mask & E else 0)
    ya = y0 + (i if mask & N else 0); yb = y1 - (i if mask & S else 0)
    rr = lambda a, b: max(r - i, 2) if (mask & a and mask & b) else 0
    tl, tr, br, bl = rr(N, W), rr(N, E), rr(S, E), rr(S, W)
    return (f"M {xa + tl} {ya} L {xb - tr} {ya} " + (f"Q {xb} {ya} {xb} {ya + tr} " if tr else "") +
            f"L {xb} {yb - br} " + (f"Q {xb} {yb} {xb - br} {yb} " if br else "") +
            f"L {xa + bl} {yb} " + (f"Q {xa} {yb} {xa} {yb - bl} " if bl else "") +
            f"L {xa} {ya + tl} " + (f"Q {xa} {ya} {xa + tl} {ya} " if tl else "") + "Z")


def open_edges(mask, r=26):
    """Только открытые стороны — по ним идёт контур тушью."""
    d = outline_path(mask)
    # Проще: обводим весь контур, а закрытые стороны потом перекрываем заливкой (см. ниже).
    return d


def pebbles(seed, x0, y0, x1, y1, n, cols):
    rnd = random.Random(seed)
    out = ""
    for _ in range(n):
        r = rnd.uniform(7, 16)
        cx = rnd.uniform(x0 + r, x1 - r); cy = rnd.uniform(y0 + r, y1 - r)
        out += f'<path d="{blob(cx, cy, r, 7, 0.25, seed=rnd.randint(0, 9999), squash=rnd.uniform(0.55, 0.85))}" fill="{rnd.choice(cols)}"/>'
    for _ in range(2):
        x = rnd.uniform(x0 + 10, x1 - 30); y = rnd.uniform(y0 + 10, y1 - 30)
        out += f'<path d="M {x:.0f} {y:.0f} l {rnd.uniform(6, 14):.0f} {rnd.uniform(6, 12):.0f} l {rnd.uniform(-4, 8):.0f} {rnd.uniform(8, 14):.0f}" fill="none" stroke="{STONE_D}" stroke-width="2.5" stroke-linecap="round"/>'
    return out


def moss_top(seed):
    """Мох на верхней стороне: волна с периодом в клетку (стыкуется с соседями) + капли и травинки."""
    rnd = random.Random(seed)
    pts = []
    for i in range(17):
        x = i * T / 16
        y = HEAD + 20 + 6 * math.sin(i / 16 * math.tau * 2 + 0.6) + 3 * math.sin(i / 16 * math.tau * 3)
        if 0 < i < 16 and rnd.random() < 0.25: y += rnd.uniform(6, 14)  # капля мха
        pts.append((x, y))
    d = f"M -2 {HEAD - 4} L {T + 2} {HEAD - 4} " + " ".join(f"L {x:.1f} {y:.1f}" for x, y in pts[::-1]) + " Z"
    # Верхняя «подушка» мха чуть выше края клетки — пушистая.
    bumps = ""
    for i in range(6):
        cx = 12 + i * 21 + rnd.uniform(-4, 4)
        bumps += f'<ellipse cx="{cx:.0f}" cy="{HEAD + 2}" rx="{rnd.uniform(11, 15):.0f}" ry="{rnd.uniform(5, 8):.0f}" fill="{MOSS}"/>'
    tufts = ""
    for _ in range(rnd.randint(2, 4)):
        x = rnd.uniform(14, T - 14); h = rnd.uniform(10, 24); lean = rnd.uniform(-6, 6)
        tufts += f'<path d="M {x:.0f} {HEAD + 2} q {lean:.0f} {-h * 0.6:.0f} {lean * 1.6:.0f} {-h:.0f}" fill="none" stroke="{MOSS_L}" stroke-width="3" stroke-linecap="round"/>'
    if rnd.random() < 0.35:  # крошечный светлячок-гриб — тёплая точка, не голубая (голубое = подбери)
        x = rnd.uniform(20, T - 20)
        tufts += (f'<path d="M {x:.0f} {HEAD + 4} l 0 -10" stroke="#e8e0c4" stroke-width="3"/>'
                  f'<ellipse cx="{x:.0f}" cy="{HEAD - 7}" rx="6" ry="3.5" fill="#f2b45a" stroke="{INK}" stroke-width="1.5"/>')
    return (f'<path d="{d}" fill="{MOSS}" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>'
            f'{bumps}<path d="M 0 {HEAD - 3} C 30 {HEAD - 8} 90 {HEAD - 8} {T} {HEAD - 3}" fill="none" stroke="{MOSS_L}" stroke-width="3" opacity="0.8"/>'
            f'{tufts}')


def ink_sides(mask):
    """Линия туши только по открытым сторонам (и скруглённым углам)."""
    x0, y0, x1, y1 = 0, HEAD, T, HEAD + T
    out = f'<path d="{outline_path(mask, 1.5)}" fill="none" stroke="{INK}" stroke-width="4"/>'
    # Закрытые стороны: стираем линию, рисуя поверх полосу заливки (она уйдёт под соседний тайл).
    return out


def stone_tile(mask, variant=0):
    seed = mask * 31 + variant * 7 + 1
    body = (f'<path d="{outline_path(mask)}" fill="{STONE_RIM}"/>'
            f'<path d="{outline_path(mask, 9)}" fill="{STONE}"/>'
            f'<clipPath id="c"><path d="{outline_path(mask, 9)}"/></clipPath>'
            f'<g clip-path="url(#c)">{pebbles(seed, 6, HEAD + 6, T - 6, HEAD + T - 6, 7, [STONE_L, STONE_D, "#323d41"])}</g>'
            f'{ink_sides(mask)}')
    # Закрытые стороны: заливаем тонкую кромку цветом камня, чтобы линия туши не шла по стыку.
    body = body.replace(f'{ink_sides(mask)}', '') + clip_ink(mask, f'<path d="{outline_path(mask, 1.5, over=12)}" fill="none" stroke="{INK}" stroke-width="4"/>')
    if mask & N: body += moss_top(seed)
    if mask & S:  # снизу свисают корешки
        rnd = random.Random(seed + 3)
        for _ in range(rnd.randint(0, 2)):
            x = rnd.uniform(16, T - 16); L = rnd.uniform(10, 26)
            body += f'<path d="M {x:.0f} {HEAD + T - 4} q 4 {L * 0.5:.0f} -2 {L:.0f}" fill="none" stroke="{BARK_L}" stroke-width="2.5" stroke-linecap="round"/>'
    return svg(T, T + HEAD + 24, body)  # +24 снизу — под корешки


def clip_ink(mask, ink):
    """Оставляем тушь только в полосе у открытых сторон."""
    x0, y0, x1, y1 = 0, HEAD, T, HEAD + T
    rects = []
    if mask & N: rects.append(f'<rect x="-4" y="{y0 - 4}" width="{T + 8}" height="30"/>')
    if mask & S: rects.append(f'<rect x="-4" y="{y1 - 26}" width="{T + 8}" height="30"/>')
    if mask & W: rects.append(f'<rect x="-4" y="{y0 - 4}" width="30" height="{T + 8}"/>')
    if mask & E: rects.append(f'<rect x="{x1 - 26}" y="{y0 - 4}" width="30" height="{T + 8}"/>')
    if not rects: return ""
    return (f'<clipPath id="cell"><rect x="0" y="{y0}" width="{T}" height="{T}"/></clipPath>'
            f'<clipPath id="ink"><path d="M0 0"/>{"".join(rects)}</clipPath><g clip-path="url(#cell)"><g clip-path="url(#ink)">{ink}</g></g>')


def bark_tile(mask, variant=0):
    """Кора: вертикальные волокна с периодом в клетку — бесшовно по вертикали."""
    seed = mask * 13 + variant * 5 + 2
    rnd = random.Random(seed)
    grain_lines = ""
    for i in range(7):
        x = 10 + i * 18
        ph = i * 1.7  # одинаково во всех тайлах — волокна стыкуются по вертикали
        pts = [(x + 4 * math.sin(ph + k / 8 * math.tau), HEAD + k * T / 8) for k in range(9)]
        grain_lines += f'<path d="{smooth_open(pts)}" fill="none" stroke="{BARK_D}" stroke-width="{rnd.uniform(3, 6):.1f}" stroke-linecap="round"/>'
        if i % 2 == 0:
            grain_lines += f'<path d="{smooth_open([(px + 6, py) for px, py in pts])}" fill="none" stroke="{BARK_L}" stroke-width="2" opacity="0.6"/>'
    knot = ""
    if variant == 1:
        knot = (f'<ellipse cx="{rnd.uniform(40, 88):.0f}" cy="{HEAD + rnd.uniform(40, 88):.0f}" rx="12" ry="18" fill="{BARK_D}" stroke="{INK}" stroke-width="2.5"/>')
    body = (f'<path d="{outline_path(mask, 0, 14)}" fill="{BARK_L}"/>'
            f'<path d="{outline_path(mask, 7, 14)}" fill="{BARK}"/>'
            f'<clipPath id="c"><path d="{outline_path(mask, 7, 14)}"/></clipPath><g clip-path="url(#c)">{grain_lines}{knot}</g>'
            + clip_ink(mask, f'<path d="{outline_path(mask, 1.5, 14, over=12)}" fill="none" stroke="{INK}" stroke-width="4"/>'))
    if mask & N: body += moss_top(seed + 100)
    return svg(T, T + HEAD + 24, body)


def silk_tile():
    """Шёлковый мост: несколько провисающих нитей, стыкуются по горизонтали."""
    body = ""
    for i, (y, sag, wd) in enumerate(((HEAD + 18, 10, 6), (HEAD + 30, 14, 5), (HEAD + 42, 8, 4))):
        d = f"M -2 {y} Q {T / 2} {y + sag} {T + 2} {y}"
        body += f'<path d="{d}" fill="none" stroke="{INK}" stroke-width="{wd + 4}"/><path d="{d}" fill="none" stroke="#f1eedc" stroke-width="{wd}"/>'
    body += f'<path d="M 0 {HEAD + 20} L {T} {HEAD + 20}" stroke="#ffffff" stroke-width="2" opacity="0.5"/>'
    return svg(T, T + HEAD + 24, body)


def build():
    m = {}
    piv = [0.5, (24 + T / 2) / (T + HEAD + 24)]  # центр клетки
    for mask in range(16):
        for v in range(3 if mask == 0 else 2):
            save(grain(render(stone_tile(mask, v)), 0.06, seed=mask * 10 + v), f"Tiles/stone_{mask:02d}_{v}.png")
            m[f"stone_{mask:02d}_{v}"] = {"pivot": piv}
        for v in range(2):
            save(grain(render(bark_tile(mask, v)), 0.06, seed=300 + mask * 10 + v), f"Tiles/bark_{mask:02d}_{v}.png")
            m[f"bark_{mask:02d}_{v}"] = {"pivot": piv}
    save(grain(render(silk_tile()), 0.04, seed=7), "Tiles/silk.png"); m["silk"] = {"pivot": piv}
    write_manifest("Tiles", m)


if __name__ == "__main__":
    build()
