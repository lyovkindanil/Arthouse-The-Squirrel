"""Готовит позы белки из концепт-картинок к игре.

    python3 Tools/SquirrelSprites/prepare.py spirit

Берёт Tools/SquirrelSprites/src/<форма>/{idle,run,jump,cling}.png и poses.json оттуда же,
кладёт вырезанные спрайты и form.json в Assets/_Squirrel/Art/Squirrel/<форма>/.
Сборщик сцен (Squirrel → Собрать всё) читает form.json: масштаб, пивот и «якорь» тела каждой позы.

poses.json — для каждой позы, в пикселях исходной картинки:
  eye     — ширина глаза (по ней позы приводятся к одному размеру белки);
  torso   — центр туловища (вокруг него колышется хвост);
  tail    — основание хвоста (вокруг него хвост поворачивается при ударе);
  ground  — точка земли под белкой (для idle/run), либо
  center  — true: туловище ставится в центр хитбокса (прыжок), либо
  wall    — две точки [[x, y], [x, y]] на поверхности стены, за которую держатся лапы (для cling):
            картинка поворачивается, чтобы стена стала вертикальной, и зеркалится, если стена слева;
  key     — "black": фон чёрный, вырезать по яркости; иначе берётся готовая прозрачность;
  trunk   — true: убрать тёмный ствол дерева.
Нужны pillow, numpy, scipy.
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EYE_UNITS = 59 / 680.0        # ширина глаза в единицах мира: тело белки ≈ 0.9 ед.
HITBOX_CENTER_Y = 0.35        # центр хитбокса белки над её ногами
HITBOX_HALF_W = 0.4


def key_black(rgb):
    """Чёрный фон → прозрачность; свечение остаётся полупрозрачным и ярким."""
    bg = np.median(rgb[:24, :24].reshape(-1, 3), axis=0)
    lum = (rgb - bg).max(axis=2)
    soft = np.clip((lum - 0.04) / 0.8, 0, 1)
    hard = Image.fromarray(((lum > 0.45) * 255).astype(np.uint8))
    ff = hard.copy()
    h, w = lum.shape
    for p in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, h - 1), (w // 2, 0)):
        if ff.getpixel(p) == 0: ImageDraw.floodfill(ff, p, 128)
    sil = np.asarray(ff) != 128   # силуэт вместе с тёмными частями внутри (глаз)
    core = np.asarray(Image.fromarray((sil * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(7)).filter(ImageFilter.GaussianBlur(2.5))).astype(np.float32) / 255
    a = np.maximum(soft, core)
    col = (rgb - bg * (1 - a[..., None])) / np.maximum(a[..., None], 0.04)
    a[a < 0.02] = 0
    return np.clip(col, 0, 1), a


def remove_trunk(rgb, a):
    """Убирает ствол: самое большое тёмное пятно, плюс всё, что целиком сидит на нём (листья, искры)."""
    dark = (a > 0.5) & (rgb.mean(axis=2) < 0.42)
    lab, k = ndi.label(dark)
    if k == 0: return a
    sizes = ndi.sum(dark, lab, range(1, k + 1))
    trunk = lab == (int(np.argmax(sizes)) + 1)
    trunk = ndi.binary_closing(trunk, iterations=10)
    trunk = ndi.binary_fill_holes(trunk)
    trunk = ndi.binary_dilation(trunk, iterations=3)
    keep = (a > 0.05) & ~trunk
    lab2, k2 = ndi.label(keep)
    if k2:
        s2 = ndi.sum(keep, lab2, range(1, k2 + 1))
        keep = lab2 == (int(np.argmax(s2)) + 1)   # только белка, без оторванных кусочков
    m = ndi.gaussian_filter(keep.astype(np.float32), 1.2)
    return a * np.clip(m * 1.4, 0, 1)


def main(form):
    src = os.path.join(HERE, "src", form)
    out = os.path.join(ROOT, "Assets", "_Squirrel", "Art", "Squirrel", form)
    os.makedirs(out, exist_ok=True)
    poses = json.load(open(os.path.join(src, "poses.json"), encoding="utf-8"))
    manifest = {"form": form, "poses": []}
    for name, p in poses.items():
        im = np.asarray(Image.open(os.path.join(src, name + ".png")).convert("RGBA")).astype(np.float32) / 255
        rgb, a = im[..., :3], im[..., 3]
        if p.get("key") == "black": rgb, a = key_black(rgb)
        if p.get("trunk"): a = remove_trunk(rgb, a)
        w = a.shape[1]
        torso = np.array(p["torso"], np.float32)
        tail = np.array(p.get("tail", p["torso"]), np.float32)
        ppu = p["eye"] / EYE_UNITS
        wall_x = None
        if "wall" in p:
            pad = 160   # поля, чтобы хвост не обрезало при повороте
            rgb = np.pad(rgb, ((pad, pad), (pad, pad), (0, 0))); a = np.pad(a, pad); w = a.shape[1]
            torso = torso + pad; tail = tail + pad
            (xa, ya), (xb, yb) = sorted([[q[0] + pad, q[1] + pad] for q in p["wall"]], key=lambda q: q[1])
            ang = np.degrees(np.arctan2(xb - xa, yb - ya))      # наклон стены от вертикали
            cx, cy = (xa + xb) / 2, (ya + yb) / 2
            rot = Image.fromarray((np.dstack([rgb, a]) * 255).astype(np.uint8), "RGBA").rotate(-ang, resample=Image.BICUBIC, center=(cx, cy))
            arr = np.asarray(rot).astype(np.float32) / 255
            rgb, a = arr[..., :3], arr[..., 3]
            t = np.radians(ang); c, s_ = np.cos(t), np.sin(t)
            turn = lambda q: np.array([cx + (q[0] - cx) * c - (q[1] - cy) * s_, cy + (q[0] - cx) * s_ + (q[1] - cy) * c], np.float32)
            torso, tail = turn(torso), turn(tail)
            wall_x = cx
            # Всё, что за стеной, — остатки ствола.
            side = -1 if torso[0] > wall_x else 1
            xs_ = np.arange(w)[None, :]
            a = a * np.clip((side * (wall_x - xs_) + 5) / 4.0, 0, 1)
        if wall_x is not None and wall_x < torso[0]:   # стена должна быть справа (белка смотрит вправо = к стене)
            rgb, a = rgb[:, ::-1], a[:, ::-1]
            torso[0] = w - 1 - torso[0]; tail[0] = w - 1 - tail[0]; wall_x = w - 1 - wall_x
        if "ground" in p: pivot = np.array(p["ground"], np.float32)
        elif wall_x is not None: pivot = np.array([wall_x - HITBOX_HALF_W * ppu, torso[1] + HITBOX_CENTER_Y * ppu])
        else: pivot = np.array([torso[0], torso[1] + HITBOX_CENTER_Y * ppu])
        ys, xs = np.where(a > 0.02)
        x0, x1, y0, y1 = max(xs.min() - 4, 0), min(xs.max() + 5, w), max(ys.min() - 4, 0), min(ys.max() + 5, a.shape[0])
        img = Image.fromarray((np.dstack([rgb, a])[y0:y1, x0:x1] * 255).astype(np.uint8), "RGBA")
        img.save(os.path.join(out, name + ".png"))
        cw, ch = x1 - x0, y1 - y0
        manifest["poses"].append({
            "name": name, "ppu": round(float(ppu), 2),
            "pivotX": round(float((pivot[0] - x0) / cw), 4), "pivotY": round(float((y1 - pivot[1]) / ch), 4),
            # якорь — центр туловища в единицах мира относительно пивота
            "anchorX": round(float((torso[0] - pivot[0]) / ppu), 3), "anchorY": round(float((pivot[1] - torso[1]) / ppu), 3),
            "tailX": round(float((tail[0] - pivot[0]) / ppu), 3), "tailY": round(float((pivot[1] - tail[1]) / ppu), 3),
        })
        print(name, img.size, manifest["poses"][-1])
    json.dump(manifest, open(os.path.join(out, "form.json"), "w", encoding="utf-8"), indent=1)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "spirit")
