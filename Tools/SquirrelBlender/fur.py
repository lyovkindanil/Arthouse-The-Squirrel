# Adds a subtle looping fur flutter: vertex group "fur_tips" (how much a vertex sticks out of the smoothed
# body) + Displace modifier driven by a noise texture whose coordinates travel in a 60-frame circle.
# usage: python fur.py <in.blend> <out.blend> [strength=0.03]
import bpy, sys, math, numpy as np
a = sys.argv
SRC, DST = a[1], a[2]; STR = float(a[3]) if len(a) > 3 else 0.03
bpy.ops.wm.open_mainfile(filepath=SRC)
scn = bpy.context.scene; body = bpy.data.objects["Squirrel"]; me = body.data
n = len(me.vertices)
co = np.empty(n * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
no = np.empty(n * 3, np.float32); me.vertices.foreach_get("normal", no); no = no.reshape(-1, 3)
# smoothed copy -> protrusion
dup = body.copy(); dup.data = me.copy(); scn.collection.objects.link(dup)
for m in list(dup.modifiers): dup.modifiers.remove(m)
sm = dup.modifiers.new("s", "SMOOTH"); sm.factor = 1.0; sm.iterations = 40
ev = dup.evaluated_get(bpy.context.evaluated_depsgraph_get()); sco = np.empty(n * 3, np.float32)
ev.data.vertices.foreach_get("co", sco); sco = sco.reshape(-1, 3)
bpy.data.objects.remove(dup)
d = np.linalg.norm(co - sco, axis=1)
hi = np.percentile(d, 97); w = np.clip(d / hi, 0, 1) ** 1.3
print("protrusion p50/p97", float(np.percentile(d, 50)), float(hi), "mean w", float(w.mean()))
vg = body.vertex_groups.get("fur_tips") or body.vertex_groups.new(name="fur_tips")
# bucket weights to keep the number of API calls small
q = np.round(w * 20).astype(int)
for k in range(1, 21):
    idx = np.nonzero(q == k)[0]
    if len(idx): vg.add(idx.tolist(), k / 20, 'REPLACE')
tex = bpy.data.textures.new("FurNoise", "CLOUDS"); tex.noise_scale = 0.16; tex.noise_depth = 1; tex.cloud_type = 'COLOR'
emp = bpy.data.objects.new("FurWind", None); scn.collection.objects.link(emp); emp.empty_display_size = 0.2
for i, expr in ((0, "0.07*cos(2*pi*frame/60)"), (1, "0.05*sin(4*pi*frame/60)"), (2, "0.07*sin(2*pi*frame/60)")):
    dr = emp.driver_add("location", i).driver; dr.type = 'SCRIPTED'; dr.expression = expr
mod = body.modifiers.new("FurFlutter", "DISPLACE")
mod.texture = tex; mod.texture_coords = 'OBJECT'; mod.texture_coords_object = emp
mod.direction = 'RGB_TO_XYZ'; mod.space = 'LOCAL'; mod.mid_level = 0.5; mod.strength = STR; mod.vertex_group = "fur_tips"
# displace in rest space, before the armature
bpy.context.view_layer.objects.active = body
while body.modifiers.find("FurFlutter") > 0: bpy.ops.object.modifier_move_up(modifier="FurFlutter")
print([m.name for m in body.modifiers])
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
