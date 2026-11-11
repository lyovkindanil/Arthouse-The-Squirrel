# Packs rendered frames (render_sprites.py output) into sprite sheets for the game.
# python pack_unity.py <out_dir> <frame=256> <scale> <frames_dir>[:offx,offy] ...
#   out_dir  - Assets/_Squirrel/Art/Squirrel/<form>3d; gets <action>.png sheets and clips.json
#   scale    - game units per model unit (how big the squirrel is in the game)
#   offx,offy - where the pivot of this clip sits relative to the squirrel's feet point, game units
# The Unity side (GreyboxBuilder.Art.cs) reads clips.json and cuts the sheets into sprites.
import sys, os, json, glob, numpy as np
from PIL import Image
out, frame, scale = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
COLS = 8; os.makedirs(out, exist_ok=True); clips = []
def shrink(im, size):                                   # premultiplied resize: no dark fringe around the fur
    a = np.asarray(im.convert("RGBA"), dtype=np.float32) / 255.0
    a[..., :3] *= a[..., 3:4]
    ch = [Image.fromarray(a[..., i]).resize((size, size), Image.LANCZOS) for i in range(4)]
    b = np.clip(np.stack([np.asarray(c) for c in ch], -1), 0, 1)
    al = np.maximum(b[..., 3:4], 1e-4); b[..., :3] = np.clip(b[..., :3] / al, 0, 1)
    return Image.fromarray((b * 255 + 0.5).astype(np.uint8), "RGBA")
for arg in sys.argv[4:]:
    d, _, off = arg.partition(":"); off = [float(v) for v in off.split(",")] if off else [0.0, 0.0]
    meta = json.load(open(os.path.join(d, "meta.json"))); name = meta["action"]
    files = sorted(glob.glob(os.path.join(d, name + "_*.png")))
    size = int(round(frame * meta["size"] / 512))       # wide actions keep their bigger frame
    rows = -(-len(files) // COLS); sheet = Image.new("RGBA", (size * COLS, size * rows))
    for i, f in enumerate(files): sheet.paste(shrink(Image.open(f), size), ((i % COLS) * size, (i // COLS) * size))
    sheet.save(os.path.join(out, name + ".png"), optimize=True)
    ppu = meta["pixelsPerUnit"] * size / meta["size"] / scale
    clips.append({"name": name, "frames": len(files), "cols": COLS, "size": size, "fps": meta["fps"], "loop": meta["loop"],
                  "ppu": round(ppu, 3), "pivotX": round(meta["pivot"][0], 4), "pivotY": round(meta["pivot"][1], 4),
                  "offsetX": off[0], "offsetY": off[1]})
    print(name, len(files), sheet.size, "ppu %.2f" % ppu)
json.dump({"form": os.path.basename(out.rstrip("/")), "clips": clips}, open(os.path.join(out, "clips.json"), "w"), indent=1)
