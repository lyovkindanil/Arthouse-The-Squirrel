# Sharpens the skin weights of the legs so that elbows, knees and wrists bend as joints, not as rubber hoses.
# usage: python weights.py <in.blend> <out.blend> [power=4]
import bpy, bmesh, sys
SRC, DST = sys.argv[1], sys.argv[2]; POW = float(sys.argv[3]) if len(sys.argv) > 3 else 4.0
bpy.ops.wm.open_mainfile(filepath=SRC)
body = bpy.data.objects["Squirrel"]; me = body.data
chains = [[n + s for n in names] for s in (".L", ".R") for names in (("arm_upper", "arm_lower", "paw_front"), ("thigh", "shin", "paw_hind"))]
bm = bmesh.new(); bm.from_mesh(me); dl = bm.verts.layers.deform.verify(); n = 0
for ch in chains:
    ids = [body.vertex_groups[g].index for g in ch]
    for v in bm.verts:
        d = v[dl]; w = [d.get(i, 0.0) for i in ids]; tot = sum(w)
        if tot <= 1e-6 or max(w) == tot: continue
        p = [x ** POW for x in w]; k = tot / sum(p)
        for i, x in zip(ids, p):
            if x * k > 1e-4: d[i] = x * k
            elif i in d: del d[i]
        n += 1
bm.to_mesh(me); bm.free(); print("sharpened verts", n)
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
