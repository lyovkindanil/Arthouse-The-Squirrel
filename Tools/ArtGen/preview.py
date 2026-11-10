"""Превью: кусок настоящей карты Мшистой колыбели, собранный из новых тайлов и спрайтов (не для игры)."""
import json, os, sys
from PIL import Image
from common import ART, ROOT, PPU

def load_map():
    lines = open(os.path.join(ROOT, "Assets/_Squirrel/Levels/MossCradle.txt"), encoding="utf-8").read().splitlines()
    rows = lines[lines.index("map") + 1:]
    rows = [r for r in rows if r]
    H = len(rows); W = max(len(r) for r in rows)
    cell = lambda x, y: rows[H - 1 - y][x] if 0 <= y < H and 0 <= x < len(rows[H - 1 - y]) else "#"
    return cell, W, H

def sprite(folder, name):
    return Image.open(os.path.join(ART, folder, name + ".png")), json.load(open(os.path.join(ART, folder, "sprites.json")))["sprites"].get(name, {"pivot": [0.5, 0.5]})["pivot"]

def main(x0, y0, w, h, out, scale=0.5, squirrel_at=None):
    cell, W, H = load_map()
    px = PPU
    img = Image.new("RGBA", (w * px, h * px))
    # Фон: три плана (в превью без сдвига)
    for layer in ("bg_far", "bg_mid", "bg_near"):
        bg = Image.open(os.path.join(ART, "Background", layer + ".png")).resize((w * px, int(w * px * 1152 / 2048)))
        img.alpha_composite(bg, (0, h * px - bg.height))
    solid = lambda c: c in "#B"
    def put(im, piv, wx, wy):  # wx, wy — мировые координаты точки пивота
        X = int((wx - x0) * px - piv[0] * im.width); Y = int((y0 + h - wy) * px - (1 - piv[1]) * im.height)
        img.alpha_composite(im, (0, 0), ) if False else img.paste(im, (X, Y), im) if False else _ac(img, im, X, Y)
    for y in range(y0 + h - 1, y0 - 2, -1):
        for x in range(x0 - 1, x0 + w + 1):
            c = cell(x, y)
            if c in "#B=":
                if c == "=": im, piv = sprite("Tiles", "silk")
                else:
                    m = (0 if solid(cell(x, y + 1)) else 1) | (0 if solid(cell(x + 1, y)) else 2) | (0 if solid(cell(x, y - 1)) else 4) | (0 if solid(cell(x - 1, y)) else 8)
                    kind = "stone" if c == "#" else "bark"
                    v = (x * 7 + y * 13) % (3 if (kind == "stone" and m == 0) else 2)
                    im, piv = sprite("Tiles", f"{kind}_{m:02d}_{v}")
                put(im, piv, x + 0.5, y + 0.5)
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            c = cell(x, y)
            spec = {"N": ("Objects", "nest", 0), "M": ("Objects", "memory", 0.5), "H": ("Objects", "heart", 0.5), "A": ("Objects", "shrine", 0),
                    "c": ("Enemies", "crawler_0", 0), "m": ("Enemies", "moth_0", 0.5), "p": ("Enemies", "spitter_0", 0), "*": ("Objects", "edge_front_0", 0.5),
                    "C": ("Objects", "caterpillar_0", 0), "K": ("Objects", "stash", 0), "x": ("Objects", "cocoon", 0)}.get(c)
            if spec:
                im, piv = sprite(spec[0], spec[1]); put(im, piv, x + 0.5, y + spec[2])
            if c == "R" and cell(x, y - 1) != "R":
                im, piv = sprite("Objects", "rock"); put(im, piv, x + 0.5, y + 1)
    if squirrel_at:
        for i, (name, sx, sy) in enumerate(squirrel_at):
            gy = int(sy)
            while not solid(cell(int(sx), gy - 1)) and gy > 0: gy -= 1  # ставим на землю под точкой
            im, piv = sprite("Squirrel", name); put(im, piv, sx, gy if sy == int(sy) else sy)
    img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    img.convert("RGB").save(out)

def _ac(base, im, X, Y):
    layer = Image.new("RGBA", base.size)
    layer.paste(im, (X, Y))
    base.alpha_composite(layer)

if __name__ == "__main__":
    out = sys.argv[1]
    main(int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), out, float(sys.argv[6]),
         squirrel_at=[(a.split(":")[0], float(a.split(":")[1]), float(a.split(":")[2])) for a in sys.argv[7:]])
