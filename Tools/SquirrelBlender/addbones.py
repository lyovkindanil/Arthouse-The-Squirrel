# Splits hips, spine and chest in two (adds hips.b, spine.b, chest.b) so the back can curl smoothly.
# Old animations are not affected: with the new bones at rest the body moves exactly as before.
# usage: python addbones.py <in.blend> <out.blend>
import bpy, bmesh, sys
SRC, DST = sys.argv[1], sys.argv[2]
bpy.ops.wm.open_mainfile(filepath=SRC)
arm = bpy.data.objects["SquirrelRig"]; body = bpy.data.objects["Squirrel"]
if "spine.b" in arm.data.bones: sys.exit("already split")
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT'); eb = arm.data.edit_bones
info = {}
for name, move in (("hips", ["spine"]), ("spine", ["chest"]), ("chest", ["neck", "arm_upper.L", "arm_upper.R"])):
    b = eb[name]; h, t = b.head.copy(), b.tail.copy(); mid = (h + t) / 2
    nb = eb.new(name + ".b"); nb.head = mid; nb.tail = t; nb.roll = b.roll
    b.tail = mid; nb.parent = b; nb.use_connect = True
    for c in move:
        conn = eb[c].use_connect; eb[c].parent = nb; eb[c].use_connect = conn
    info[name] = (h, t)
bpy.ops.object.mode_set(mode='OBJECT')
for b in arm.pose.bones: b.rotation_mode = 'XYZ'
me = body.data; bm = bmesh.new(); bm.from_mesh(me); dl = bm.verts.layers.deform.verify()
def sm(x): x = min(1, max(0, x)); return x * x * (3 - 2 * x)
for name, (h, t) in info.items():
    g = body.vertex_groups[name].index; gb = (body.vertex_groups.get(name + ".b") or body.vertex_groups.new(name=name + ".b")).index
    ax = t - h; L2 = ax.length_squared; n = 0
    for v in bm.verts:
        d = v[dl]; w = d.get(g, 0.0)
        if w <= 0: continue
        s = (v.co - h).dot(ax) / L2                  # 0 at the head of the old bone, 1 at its tail
        k = sm((s - 0.3) / 0.4)                      # blend over the middle of the bone
        if k > 0:
            d[gb] = w * k
            if k < 1: d[g] = w * (1 - k)
            else: del d[g]
            n += 1
    print(name, "moved verts", n)
bm.to_mesh(me); bm.free()
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved, bones", len(arm.data.bones))
