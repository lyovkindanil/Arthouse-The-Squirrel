# Renders an action of SquirrelRig to transparent PNG frames + meta.json (pivot, pixels per unit).
# blender -b squirrel_rig.blend -P render_sprites.py -- <action> <out_dir> [size=512] [step=2] [samples=24]
import bpy, sys, json, os
a = sys.argv[sys.argv.index("--") + 1:]
action, out = a[0], a[1]
size = int(a[2]) if len(a) > 2 else 512
step = int(a[3]) if len(a) > 3 else 2
samples = int(a[4]) if len(a) > 4 else 24
if len(a) > 5: bpy.ops.wm.open_mainfile(filepath=a[5])   # only for running without the blender binary
scn = bpy.context.scene; arm = bpy.data.objects["SquirrelRig"]; cam = scn.camera
act = bpy.data.actions[action]; arm.animation_data.action = act
scn.render.engine = "CYCLES"; scn.cycles.samples = samples; scn.cycles.device = "CPU"
scn.render.film_transparent = True; scn.render.resolution_x = scn.render.resolution_y = size
scn.render.image_settings.file_format = "PNG"; scn.render.image_settings.color_mode = "RGBA"
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
loop = bool(act.get("loop", True))
fur = bpy.data.objects["Squirrel"].modifiers.get("FurFlutter")   # fast moves need a stronger stir of the strands to be seen at all
if fur: fur.strength = 0.05 * float(act.get("fur", 1.0))
wide = float(act.get("wide", 1.0))                       # big moves get a bigger frame at the same pixels per unit
size = int(round(size * wide)); cam.data.ortho_scale *= wide
scn.render.resolution_x = scn.render.resolution_y = size
for name in ("Trunk",):                                  # objects that only cut the picture (far legs behind the trunk)
    if name in bpy.data.objects: bpy.data.objects[name].hide_render = act.get("holdout") != name
frames = list(range(f0, f1 if loop else f1 + 1, step))   # in a loop the last key equals the first one, so it is skipped
wind = bpy.data.objects.get("FurWind")                  # strand noise must repeat with the loop
if wind and wind.animation_data:
    n = (f1 - f0) if loop else 60; r = 0.2 * (n / 60) ** 0.5   # short loops still get a visible stir
    for fc in wind.animation_data.drivers:
        fc.driver.expression = (f"{r}*cos(2*pi*frame/{n})", f"{r*0.6}*sin(4*pi*frame/{n})", f"{r}*sin(2*pi*frame/{n})")[fc.array_index]
os.makedirs(out, exist_ok=True)

# ---- strands react to motion: they lag behind when the body jerks and swing on after it stops ---------------------
# For every strand vertex a small spring follows "minus its velocity" (air drag). The body itself is never touched.
# Actions rendered in place can tell how they travel in the game: custom props virt_vel (units per second, constant)
# and virt_z (height per frame), so that a run streams the fur back and a jump presses it down and then lifts it.
import numpy as np
body = bpy.data.objects["Squirrel"]; LAG = float(act.get("lag", 1.0))
def evaluated():
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    c = np.empty(len(ev.data.vertices) * 3, np.float32); ev.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)
offsets = None
if LAG > 0 and "fur_tips" in body.vertex_groups:
    gi = body.vertex_groups["fur_tips"].index; w = np.zeros(len(body.data.vertices), np.float32)
    for v in body.data.vertices:
        for g in v.groups:
            if g.group == gi: w[v.index] = g.weight
    sel = np.nonzero(w > 0.03)[0]; ws = w[sel][:, None]
    vv = np.array(act.get("virt_vel", (0.0, 0.0, 0.0)), np.float32) / scn.render.fps * step
    vz = list(act.get("virt_z", []))
    def virt(i):                                           # where the game would have carried her by frame i
        p = vv * i
        if vz: p = p + np.array((0, 0, vz[min(max(i * step, 0), len(vz) - 1)]), np.float32)
        return p
    pos = []
    for i, f in enumerate(frames):
        scn.frame_set(f); pos.append(evaluated()[sel] + virt(i))
    K, W0, Z, CLAMP = 0.42 * LAG, 0.85, 0.42, 0.11         # drag, spring frequency (rad/frame), damping, max offset
    o = np.zeros_like(pos[0]); ov = np.zeros_like(o); offsets = [None] * len(frames)
    for cycle in range(2 if loop else 1):                  # a loop is run twice so that it joins itself
        for i in range(len(frames)):
            if i == 0 and not loop: vel = np.zeros_like(o)
            elif i == 0: vel = pos[0] - pos[-1] + vv * len(frames)   # wrap of the loop: same pose again, only the travel differs
            else: vel = pos[i] - pos[i - 1]
            tgt = -K * vel
            for _ in range(3):                             # sub-steps keep the spring stable
                acc = W0 * W0 * (tgt - o) - 2 * Z * W0 * ov
                ov += acc / 3; o += ov / 3
            n_ = np.linalg.norm(o, axis=1, keepdims=True); o *= np.minimum(1, CLAMP / np.maximum(n_, 1e-6))
            offsets[i] = (o * ws).copy()
    ro = bpy.data.objects.new("SquirrelRender", body.data.copy()); scn.collection.objects.link(ro)
    body.hide_render = True
    print("strand lag: verts", len(sel), "max offset %.3f" % max(float(np.abs(x).max()) for x in offsets))
for n, f in enumerate(frames):
    scn.frame_set(f)
    if offsets is not None:
        c = evaluated().copy(); c[sel] += offsets[n]
        ro.data.vertices.foreach_set("co", c.ravel()); ro.data.update()
    scn.render.filepath = os.path.join(out, f"{action}_{n:03d}.png")
    bpy.ops.render.render(write_still=True)
if offsets is not None:
    body.hide_render = False; bpy.data.objects.remove(ro)
ppu = size / cam.data.ortho_scale
scn.frame_set(f0)
root = arm.matrix_world @ arm.pose.bones["root"].head          # point on the ground (or on the wall) under the body
px = size / 2 + (cam.location.y - root.y) * ppu               # camera looks along +X: image right = -Y
py = size / 2 + (root.z - cam.location.z) * ppu               # from the bottom edge
json.dump({"action": action, "frames": len(frames), "loop": loop, "fps": scn.render.fps / step, "size": size,
           "pixelsPerUnit": ppu, "pivot": [px / size, py / size]}, open(os.path.join(out, "meta.json"), "w"), indent=1)
print("rendered", len(frames))
