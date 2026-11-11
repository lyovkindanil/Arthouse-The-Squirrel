# Renders an action of SquirrelRig to transparent PNG frames + meta.json (pivot, pixels per unit).
# blender -b squirrel_rig.blend -P render_sprites.py -- <action> <out_dir> [size=512] [step=1] [samples=24]
import bpy, sys, json, os
a = sys.argv[sys.argv.index("--") + 1:]
action, out = a[0], a[1]
size = int(a[2]) if len(a) > 2 else 512
step = int(a[3]) if len(a) > 3 else 1
samples = int(a[4]) if len(a) > 4 else 24
if len(a) > 5: bpy.ops.wm.open_mainfile(filepath=a[5])   # only for running without the blender binary
scn = bpy.context.scene; arm = bpy.data.objects["SquirrelRig"]; cam = scn.camera
act = bpy.data.actions[action]; arm.animation_data.action = act
scn.render.engine = "CYCLES"; scn.cycles.samples = samples; scn.cycles.device = "CPU"
scn.render.film_transparent = True; scn.render.resolution_x = scn.render.resolution_y = size
scn.render.image_settings.file_format = "PNG"; scn.render.image_settings.color_mode = "RGBA"
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
frames = list(range(f0, f1, step))            # last key equals the first one in a loop, so it is skipped
os.makedirs(out, exist_ok=True)
for n, f in enumerate(frames):
    scn.frame_set(f)
    scn.render.filepath = os.path.join(out, f"{action}_{n:03d}.png")
    bpy.ops.render.render(write_still=True)
ppu = size / cam.data.ortho_scale
root = arm.matrix_world @ arm.data.bones["root"].head_local   # point on the ground under the body
px = size / 2 + (cam.location.y - root.y) * ppu               # camera looks along +X: image right = -Y
py = size / 2 + (root.z - cam.location.z) * ppu               # from the bottom edge
json.dump({"action": action, "frames": len(frames), "fps": scn.render.fps / step, "size": size,
           "pixelsPerUnit": ppu, "pivot": [px / size, py / size]}, open(os.path.join(out, "meta.json"), "w"), indent=1)
print("rendered", len(frames))
