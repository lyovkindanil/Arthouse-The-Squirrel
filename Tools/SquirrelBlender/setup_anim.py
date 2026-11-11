# Adds render setup (camera, light, world) and the test action "idle_test" to the rigged file.
import bpy, math, sys
SRC, DST = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=SRC)
scn = bpy.context.scene; arm = bpy.data.objects["SquirrelRig"]
scn.render.engine = "CYCLES"; scn.cycles.samples = 24; scn.cycles.device = "CPU"
scn.render.film_transparent = True
scn.render.resolution_x = scn.render.resolution_y = 512
scn.render.fps = 24; scn.frame_start = 1; scn.frame_end = 48
w = bpy.data.worlds.new("SpriteWorld"); scn.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[1].default_value = 2.2
cam = bpy.data.objects.new("SpriteCam", bpy.data.cameras.new("SpriteCam")); scn.collection.objects.link(cam); scn.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = 3.4
cam.location = (-6, 0.15, 0.15); cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
sun = bpy.data.objects.new("SpriteSun", bpy.data.lights.new("SpriteSun", "SUN")); sun.data.energy = 3
scn.collection.objects.link(sun); sun.rotation_euler = (math.radians(55), 0, math.radians(-60))

pb = arm.pose.bones
for b in pb: b.rotation_mode = 'XYZ'
arm.animation_data_create()
act = bpy.data.actions.new("idle_test"); act.use_fake_user = True
arm.animation_data.action = act
N = 48
for f in range(1, N + 2, 2):
    t = 2 * math.pi * (f - 1) / N
    for i in range(1, 11):
        b = pb[f"tail.{i:02d}"]
        b.rotation_euler = (math.radians(2.2) * math.sin(t - i * 0.45), 0, math.radians(1.0) * math.sin(t - i * 0.45 + 1.2))
        b.keyframe_insert("rotation_euler", frame=f)
    br = 0.5 - 0.5 * math.cos(t)          # breathing 0..1
    c = pb["chest"]; c.scale = (1 + 0.035 * br, 1 + 0.01 * br, 1 + 0.035 * br); c.keyframe_insert("scale", frame=f)
    s = pb["spine"]; s.rotation_euler = (math.radians(-1.2) * br, 0, 0); s.keyframe_insert("rotation_euler", frame=f)
    h = pb["head"]; h.rotation_euler = (math.radians(1.5) * math.sin(t - 0.8), 0, 0); h.keyframe_insert("rotation_euler", frame=f)
    for e, ph in (("ear.L", 0.3), ("ear.R", 0.9)):
        b = pb[e]; b.rotation_euler = (math.radians(3) * math.sin(t - ph), 0, 0); b.keyframe_insert("rotation_euler", frame=f)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True)
print("saved", DST)
