"""Враги Мшистой колыбели. Единый код: красные глаза/пятна = опасность, тушь-контур как у белки."""
from common import *

def ow(w=3.5):
    return f'stroke="{INK}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"'
MOSS_D, MOSS, MOSS_L = "#2f5a36", "#4f8a48", "#8cc067"
BARK_D, BARK, BARK_L = "#3b2618", "#5e3d26", "#86603c"


def red_eye(cx, cy, r=5):
    return (f'<circle cx="{cx}" cy="{cy}" r="{r * 2.2}" fill="{DANGER_GLOW}" opacity="0.25"/>'
            f'<ellipse cx="{cx}" cy="{cy}" rx="{r}" ry="{r * 0.8}" fill="{DANGER}" stroke="{INK}" stroke-width="2"/>'
            f'<circle cx="{cx + r * 0.3}" cy="{cy - r * 0.25}" r="{r * 0.3}" fill="#ffe1d6"/>')


def crawler(frame):
    """Моховик: кочка мха на коротких лапках, под чёлкой мха — красные глаза. 1.2 × 0.8 ед."""
    w, h = 176, 120
    step = 6 if frame else -6
    legs = "".join(
        f'<path d="M {x} 96 L {x + (step if i % 2 else -step)} 114" {ow(7)} fill="none"/>'
        f'<path d="M {x} 96 L {x + (step if i % 2 else -step)} 114" stroke="{BARK}" stroke-width="3" fill="none" stroke-linecap="round"/>'
        for i, x in enumerate((44, 70, 104, 130)))
    sq = 1.0 if frame == 0 else 0.96
    tufts = "".join(f'<path d="M {x} {y} q 4 -12 10 -2" fill="none" stroke="{MOSS_L}" stroke-width="3" stroke-linecap="round"/>'
                    for x, y in ((50, 46), (78, 34), (106, 38), (126, 52)))
    body = f'''{legs}
    <g transform="translate(88 100) scale(1 {sq}) translate(-88 -100)">
      <path d="M 18 100 C 14 60 48 26 92 26 C 136 26 164 60 158 100 Z" fill="{MOSS}" {ow()}/>
      <path d="M 30 92 C 34 64 60 44 92 42 C 70 52 56 70 54 92 Z" fill="{MOSS_L}" opacity="0.45"/>
      {tufts}
      <path d="M 18 100 L 158 100 L 150 90 L 138 98 L 126 88 L 114 98 L 100 88 L 88 98 L 74 88 L 62 98 L 48 88 L 36 98 L 26 90 Z" fill="{MOSS_D}" {ow()}/>
      {red_eye(124, 78, 5.5)}{red_eye(142, 80, 4.5)}
    </g>'''
    return svg(w, h, body)


def moth(frame):
    """Моль: бледно-сиреневая, пушистая, на крыльях «глазки». 0.9 × 0.6 ед. (с крыльями шире)."""
    w, h = 176, 128
    lift = -18 if frame else 8
    wing = lambda s: (f'<g transform="translate(88 64) scale({s} 1) rotate({lift}) translate(-88 -64)">'
                      f'<path d="M 92 60 C 120 18 166 14 168 44 C 170 70 136 80 98 72 Z" fill="#b9addd" {ow()}/>'
                      f'<path d="M 96 72 C 126 80 150 92 140 112 C 128 124 104 100 94 78 Z" fill="#9c8fc6" {ow()}/>'
                      f'<circle cx="140" cy="44" r="10" fill="#6b5a99"/><circle cx="140" cy="44" r="4" fill="#efe6ff"/>'
                      f'<path d="M 104 62 C 124 42 146 34 160 40" fill="none" stroke="#e3dbff" stroke-width="2" opacity="0.7"/></g>')
    body = f'''{wing(1)}{wing(-1)}
      <path d="M 82 40 Q 70 18 60 14 M 94 40 Q 106 18 116 14" fill="none" {ow(2.5)}/>
      <path d="{blob(88, 72, 17, 9, 0.12, seed=2, squash=1.6)}" fill="#d9d0f0" {ow()}/>
      <path d="M 78 82 Q 88 86 98 82 M 78 94 Q 88 98 98 94" fill="none" stroke="#9c8fc6" stroke-width="2.5" stroke-linecap="round"/>
      {red_eye(81, 52, 4.5)}{red_eye(95, 52, 4.5)}'''
    return svg(w, h, body)


def spitter(frame):
    """Плевун: гриб-плевальщик. Шляпка с красными пятнами, под ней рот-дырочка. 1.1 × 1.1 ед."""
    w, h = 168, 168
    sq = (1.0, 1.0) if frame == 0 else (1.08, 0.88)
    body = f'''
    <g transform="translate(84 164) scale({sq[0]} {sq[1]}) translate(-84 -164)">
      <path d="M 62 164 C 58 130 60 100 66 84 L 102 84 C 108 100 110 130 106 164 Z" fill="#e8e0c4" {ow()}/>
      <path d="M 70 160 C 68 130 70 104 74 90" fill="none" stroke="#c7bb94" stroke-width="4" stroke-linecap="round"/>
      <ellipse cx="84" cy="108" rx="{9 if frame else 6}" ry="{10 if frame else 6}" fill="{INK}"/>
      <ellipse cx="84" cy="108" rx="{4 if frame else 2.5}" ry="{4 if frame else 2.5}" fill="#5b1b1b"/>
      <path d="M 10 86 C 10 40 46 14 84 14 C 122 14 158 40 158 86 C 130 94 38 94 10 86 Z" fill="#9fb84a" {ow()}/>
      <path d="M 26 74 C 30 44 56 28 84 26 C 60 38 46 56 44 80 Z" fill="#d6e486" opacity="0.5"/>
      <path d="M 16 86 C 44 96 124 96 152 86" fill="none" stroke="#6b7d2c" stroke-width="5" stroke-linecap="round"/>
      <ellipse cx="54" cy="52" rx="13" ry="9" fill="{DANGER}" stroke="{INK}" stroke-width="2.5"/>
      <ellipse cx="108" cy="42" rx="10" ry="7" fill="{DANGER}" stroke="{INK}" stroke-width="2.5"/>
      <ellipse cx="128" cy="68" rx="8" ry="6" fill="{DANGER}" stroke="{INK}" stroke-width="2.5"/>
      <ellipse cx="82" cy="70" rx="7" ry="5" fill="{DANGER}" stroke="{INK}" stroke-width="2.5"/>
    </g>'''
    return svg(w, h, body)


def spore():
    w = h = 48
    body = (f'<circle cx="24" cy="24" r="22" fill="{DANGER_GLOW}" opacity="0.25"/>'
            f'<path d="{blob(24, 24, 11, 8, 0.2, seed=4)}" fill="#c6d65e" {ow(2.5)}/>'
            f'<circle cx="21" cy="21" r="3.5" fill="{DANGER}"/>')
    return svg(w, h, body)


def root_warden(frame):
    """Корневой сторож: пень-страж с корнями-руками и полой сердцевиной. Хитбокс 4 × 7 ед.
    Сердцевина остаётся отдельным объектом (она светится, когда по ней можно бить)."""
    w, h = 768, 1056  # 6 × 8.25 ед.
    cx = 384
    roots = "".join(
        f'<path d="{smooth_open([(cx + 150, 980 - i * 110), (cx + 250 + i * 10, 960 - i * 120 - (30 if frame else 0)), (cx + 330, 1000 - i * 90), (cx + 370, 1040 - i * 70)])}" fill="none" stroke="{INK}" stroke-width="30" stroke-linecap="round"/>'
        f'<path d="{smooth_open([(cx + 150, 980 - i * 110), (cx + 250 + i * 10, 960 - i * 120 - (30 if frame else 0)), (cx + 330, 1000 - i * 90), (cx + 370, 1040 - i * 70)])}" fill="none" stroke="{BARK}" stroke-width="20" stroke-linecap="round"/>'
        for i in range(4))
    grooves = "".join(f'<path d="M {cx - 200 + i * 55} 1040 C {cx - 210 + i * 55} 800 {cx - 180 + i * 50} 560 {cx - 170 + i * 48} 330" fill="none" stroke="{BARK_D}" stroke-width="6" opacity="0.7"/>' for i in range(8))
    body = f'''{roots}
      <path d="M {cx - 256} 1050 C {cx - 250} 800 {cx - 236} 520 {cx - 210} 300 C {cx - 120} 250 {cx + 120} 250 {cx + 210} 300 C {cx + 236} 520 {cx + 250} 800 {cx + 256} 1050 Z" fill="{BARK}" {ow(6)}/>
      {grooves}
      <path d="M {cx - 300} 300 C {cx - 340} 160 {cx - 160} 60 {cx} 80 C {cx + 180} 40 {cx + 360} 160 {cx + 300} 300 C {cx + 200} 340 {cx - 200} 340 {cx - 300} 300 Z" fill="{MOSS}" {ow(6)}/>
      <path d="M {cx - 250} 260 C {cx - 240} 170 {cx - 120} 110 {cx - 20} 116 C {cx - 120} 150 {cx - 180} 210 {cx - 190} 280 Z" fill="{MOSS_L}" opacity="0.45"/>
      <path d="M {cx - 280} 300 l 20 60 l 18 -40 l 22 70 l 16 -50 M {cx + 200} 310 l 14 56 l 20 -36 l 16 50" fill="none" stroke="{MOSS_D}" stroke-width="10" stroke-linecap="round"/>
      <ellipse cx="{cx + 25}" cy="634" rx="104" ry="118" fill="{BARK_D}" {ow(6)}/>
      <ellipse cx="{cx + 25}" cy="644" rx="78" ry="90" fill="#1a0f09"/>
      <path d="M {cx - 150} 360 Q {cx - 90} {330 if frame else 345} {cx - 30} 362" fill="none" {ow(10)}/>
      <path d="M {cx + 60} 362 Q {cx + 120} {330 if frame else 345} {cx + 180} 362" fill="none" {ow(10)}/>
      {red_eye(cx - 90, 392, 20)}{red_eye(cx + 120, 392, 20)}'''
    return svg(w, h, body)


def build():
    m = {}
    for f in (0, 1):
        save(grain(render(crawler(f)), 0.06, seed=10 + f), f"Enemies/crawler_{f}.png"); m[f"crawler_{f}"] = {"pivot": [0.5, 0.05]}
        save(grain(render(moth(f)), 0.05, seed=20 + f), f"Enemies/moth_{f}.png"); m[f"moth_{f}"] = {"pivot": [0.5, 0.5]}
        save(grain(render(spitter(f)), 0.06, seed=30 + f), f"Enemies/spitter_{f}.png"); m[f"spitter_{f}"] = {"pivot": [0.5, 0.02]}
        save(grain(render(root_warden(f)), 0.07, seed=40 + f), f"Enemies/root_warden_{f}.png"); m[f"root_warden_{f}"] = {"pivot": [0.5, 0.0]}
    save(grain(render(spore()), 0.05, seed=50), "Enemies/spore.png"); m["spore"] = {"pivot": [0.5, 0.5]}
    write_manifest("Enemies", m)


if __name__ == "__main__":
    build()
