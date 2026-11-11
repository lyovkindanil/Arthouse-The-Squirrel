# Subtle looping motion of separate strands only (tail tufts, ear tufts, whiskers, mane).
# A vertex moves if it belongs to a thin strand; the weight grows from the strand root to its tip,
# so strands bend like hair while the body stays rigid.
# usage: python fur.py <in.blend> <out.blend> [strength=0.05] [debug.png]
import bpy, sys, numpy as np
from mathutils.bvhtree import BVHTree
from mathutils import Vector
from scipy.spatial import cKDTree
a = sys.argv
SRC, DST = a[1], a[2]; STR = float(a[3]) if len(a) > 3 else 0.05; DBG = a[4] if len(a) > 4 else None
THIN, REACH = 0.035, 0.10          # strand thickness limit; distance from the root at which weight reaches 1
bpy.ops.wm.open_mainfile(filepath=SRC)
scn = bpy.context.scene; body = bpy.data.objects["Squirrel"]; me = body.data
old = body.modifiers.get("FurFlutter")
if old: body.modifiers.remove(old)
n = len(me.vertices)
co = np.empty(n * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
no = np.empty(n * 3, np.float32); me.vertices.foreach_get("normal", no); no = no.reshape(-1, 3)
polys = [tuple(p.vertices) for p in me.polygons]
bvh = BVHTree.FromPolygons([Vector(c) for c in co.tolist()], polys)
thick = np.full(n, 9.0, np.float32)
for i in range(n):
    c = Vector(co[i].tolist()); d = -Vector(no[i].tolist())
    hit = bvh.ray_cast(c + d * 1e-4, d, 0.5)
    if hit[0] is not None: thick[i] = hit[3]
core = thick > THIN * 1.6
# clean the "core" set: a core vertex must have mostly core neighbours within 0.02
tree_all = cKDTree(co)
cnt = np.array([core[ix].mean() for ix in tree_all.query_ball_point(co[::1], 0.02)]) if False else None
dist, _ = cKDTree(co[core]).query(co, k=1)
w = np.clip(dist / REACH, 0, 1) ** 1.5
w[thick > THIN * 1.6] = 0
# regions: bone groups of the Armature weights
def grp(names):
    out = np.zeros(n, np.float32); ids = {body.vertex_groups[x].index for x in names if x in body.vertex_groups}
    for v in me.vertices:
        for g in v.groups:
            if g.group in ids: out[v.index] += g.weight
    return out
tail = grp([f"tail.{i:02d}" for i in range(1, 11)]); ears = grp(["ear.L", "ear.R"])
head = grp(["head"]); mane = grp(["neck", "chest"])
region = np.clip(tail + ears + 0.8 * head + 0.6 * mane, 0, 1)
w *= region
print("moving verts %.1f%%, mean w %.3f" % (100 * (w > 0.05).mean(), w.mean()))
vg = body.vertex_groups.get("fur_tips") or body.vertex_groups.new(name="fur_tips")
vg.remove(range(n))
q = np.round(w * 25).astype(int)
for k in range(1, 26):
    idx = np.nonzero(q == k)[0]
    if len(idx): vg.add(idx.tolist(), k / 25, 'REPLACE')
tex = bpy.data.textures.get("FurNoise") or bpy.data.textures.new("FurNoise", "CLOUDS")
tex.noise_scale = 0.45; tex.noise_depth = 0; tex.cloud_type = 'COLOR'
emp = bpy.data.objects.get("FurWind")
if emp is None:
    emp = bpy.data.objects.new("FurWind", None); scn.collection.objects.link(emp); emp.empty_display_size = 0.2
    for i, expr in ((0, "0.07*cos(2*pi*frame/60)"), (1, "0.05*sin(4*pi*frame/60)"), (2, "0.07*sin(2*pi*frame/60)")):
        dr = emp.driver_add("location", i).driver; dr.type = 'SCRIPTED'; dr.expression = expr
for fc in emp.animation_data.drivers:
    fc.driver.expression = ("0.2*cos(2*pi*frame/60)", "0.12*sin(4*pi*frame/60)", "0.2*sin(2*pi*frame/60)")[fc.array_index]
mod = body.modifiers.new("FurFlutter", "DISPLACE")
mod.texture = tex; mod.texture_coords = 'OBJECT'; mod.texture_coords_object = emp
mod.direction = 'RGB_TO_XYZ'; mod.space = 'LOCAL'; mod.mid_level = 0.5; mod.strength = STR; mod.vertex_group = "fur_tips"
bpy.context.view_layer.objects.active = body
while body.modifiers.find("FurFlutter") > 0: bpy.ops.object.modifier_move_up(modifier="FurFlutter")
if DBG:
    from PIL import Image
    S = 900; im = np.zeros((S, S, 3), np.uint8); im[:] = (30, 30, 36)
    order = np.argsort(co[:, 0])[::-1]               # nearest to the side camera (-X) drawn last
    px = ((0.15 - co[order, 1]) / 2.4 * S + S / 2).astype(int).clip(0, S - 1)
    py = (S / 2 - (co[order, 2] - 0.0) / 2.4 * S).astype(int).clip(0, S - 1)
    ww = w[order]; col = np.stack([90 + 165 * ww, 90 + 60 * ww - 60 * ww, 100 - 80 * ww], 1).astype(np.uint8)
    im[py, px] = col; Image.fromarray(im).save(DBG)
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
