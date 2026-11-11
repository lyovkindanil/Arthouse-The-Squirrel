# Adds base actions to the rig: run (loop), tail_attack (one shot), climb (loop, wall on the right).
# All poses are planar: every angle is a rotation about the side axis (armature X), in degrees.
#   +angle: a leg swings back, the body/head pitches nose-down, the tail curls up and forward.
# usage: python anims.py <in.blend> <out.blend>
import bpy, sys, math
from mathutils import Matrix, Vector
SRC, DST = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=SRC)
scn = bpy.context.scene; arm = bpy.data.objects["SquirrelRig"]; pb = arm.pose.bones
TAIL = [f"tail.{i:02d}" for i in range(1, 11)]
sin, cos, pi = math.sin, math.cos, math.pi
def pos(x): return max(0.0, x)
def smooth(x): x = min(1, max(0, x)); return x * x * (3 - 2 * x)

def apply(pose, frame, prev):
    """pose: {bone: deg, 'hips_loc': (dy, dz), 'root_loc': (dy, dz), 'chest_scale': s}"""
    for b in pb:
        R = b.bone.matrix_local.to_3x3()
        q = (R.inverted() @ Matrix.Rotation(math.radians(pose.get(b.name, 0.0)), 3, 'X') @ R)
        e = q.to_euler('XYZ', prev[b.name]) if b.name in prev else q.to_euler('XYZ')
        b.rotation_euler = e; prev[b.name] = e.copy()
        b.keyframe_insert("rotation_euler", frame=frame)
    for name, key in (("hips", "hips_loc"), ("root", "root_loc")):
        b = pb[name]; dy, dz = pose.get(key, (0, 0))
        b.location = b.bone.matrix_local.to_3x3().inverted() @ Vector((0, dy, dz)); b.keyframe_insert("location", frame=frame)
    s = pose.get("chest_scale", 1.0); pb["chest"].scale = (s, 1, s); pb["chest"].keyframe_insert("scale", frame=frame)

def bake(name, n, fn, loop):
    old = bpy.data.actions.get(name)
    if old: bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data.action = act; prev = {}
    for f in range(1, n + 2 if loop else n + 1):
        apply(fn(((f - 1) / n) if loop else (f - 1) / (n - 1)), f, prev)
    act["loop"] = loop
    return act

# ---------------------------------------------------------------- run: bounding gallop, 16 frames
def run(t):
    p = 2 * pi * t; ext = cos(p)                      # +1 stretched in the air, -1 gathered
    P = {}
    sp = 3 - 9 * ext; ch = 2 - 7 * ext
    P["spine"] = sp; P["chest"] = ch
    P["hips"] = -(sp + ch) * 0.55 + 6 * sin(p) - 4
    P["neck"] = -ch * 0.6 - 4; P["head"] = -sp * 0.3 + 4 * ext
    P["hips_loc"] = (-0.03 * ext, 0.03 + 0.07 * cos(p - 0.5))
    for side, o in ((".L", 0.0), (".R", 0.35)):
        a = p + 0.25 + o
        P["arm_upper" + side] = -50 * cos(a) - 12
        P["arm_lower" + side] = 55 * pos(-sin(a)) + 12 * pos(cos(a)) * 0 - 18 * pos(cos(a))
        P["paw_front" + side] = 30 * pos(-sin(a)) - 25 * pos(cos(a))
        h = p - 0.35 + o
        P["thigh" + side] = 48 * cos(h) + 4
        P["shin" + side] = 45 * pos(sin(h)) - 22 * pos(cos(h))
        P["paw_hind" + side] = 40 * pos(cos(h)) - 15 * pos(sin(h))
        P["ear" + side] = -14 - 9 * cos(p - 0.9 - o)
    for i, b in enumerate(TAIL, 1):
        P[b] = -3.5 + (3.0 + 0.35 * i) * sin(p - 0.55 * i + 0.6)
    P["chest_scale"] = 1.0
    return P

# ---------------------------------------------------------------- tail attack: 20 frames, one shot
REST_DIR = (20, 32, 45, 58, 69, 93, 120, 148, 196, 236)       # direction of each tail bone at rest
HIT_DIR = (82, 102, 120, 137, 152, 166, 180, 195, 212, 228)    # arc over the back, tip lands in front of the nose
WIND_DIR = (2, 8, 16, 26, 38, 55, 78, 104, 140, 176)           # pulled back and low before the hit
def local(dirs):
    d = [x - r for x, r in zip(dirs, REST_DIR)]
    return [d[0]] + [d[i] - d[i - 1] for i in range(1, 10)]
HIT, WIND = local(HIT_DIR), local(WIND_DIR)
def tail_attack(t):
    P = {}
    def env(a, b, c, d, x):                     # 0 -> 1 between a..b, hold, 1 -> 0 between c..d
        return smooth((x - a) / (b - a)) * (1 - smooth((x - c) / (d - c)))
    wind = env(0.0, 0.2, 0.2, 0.34, t)          # wind-up
    body = env(0.2, 0.36, 0.5, 0.95, t)         # lunge
    P["hips_loc"] = (0.04 * wind - 0.05 * body, -0.05 * wind - 0.04 * body)
    P["hips"] = -7 * wind + 10 * body
    P["spine"] = 5 * wind - 4 * body; P["chest"] = 4 * wind - 6 * body
    P["neck"] = -4 * body; P["head"] = 6 * wind + 12 * body
    for side in (".L", ".R"):
        P["arm_upper" + side] = 6 * wind - 18 * body; P["arm_lower" + side] = 10 * wind + 8 * body
        P["thigh" + side] = -10 * wind + 14 * body; P["shin" + side] = 14 * wind - 8 * body
        P["ear" + side] = -30 * body + 6 * wind
    for i, b in enumerate(TAIL, 1):
        d = 0.011 * i                           # the whip travels from the base to the tip
        w = env(0.0, 0.2, 0.2, 0.32, t - d * 0.4)
        s = env(0.2, 0.33, 0.48, 0.97, t - d)
        over = env(0.3, 0.42, 0.42, 0.62, t - d)   # overshoot right after the hit
        P[b] = WIND[i - 1] * w + HIT[i - 1] * s + 2.5 * over
    return P

# ---------------------------------------------------------------- climb: 30 frames, wall on the right
def climb(t):
    p = 2 * pi * t; P = {}
    P["root"] = -90; P["root_loc"] = (0.0, 0.95)
    P["hips_loc"] = (-0.025 * cos(2 * p), 0.0)           # along the body: two pulls per cycle
    P["hips"] = 9 + 2.5 * sin(2 * p); P["spine"] = 2 + 3 * sin(p); P["chest"] = 2 - 3 * sin(p)
    P["neck"] = 2; P["head"] = 6 + 3 * sin(2 * p + 0.5)   # looks up along the trunk and a little outwards
    for side, o in ((".L", 0.0), (".R", pi)):
        a = p + o
        P["arm_upper" + side] = -30 - 30 * sin(a)        # reach up along the wall
        P["arm_lower" + side] = 22 + 34 * pos(cos(a))    # lift the paw while it travels up
        P["paw_front" + side] = -30 + 22 * pos(cos(a))
        h = p + o + pi                                   # diagonal pairs
        P["thigh" + side] = -38 - 24 * sin(h)
        P["shin" + side] = 46 + 24 * pos(cos(h))
        P["paw_hind" + side] = -18 + 18 * pos(cos(h))
        P["ear" + side] = -8 + 4 * sin(2 * p + o)
    for i, b in enumerate(TAIL, 1):
        P[b] = (-9, 2, 6, 6, 3, 1, 1, 1, 1, 1)[i - 1] + (1.6 + 0.3 * i) * sin(p - 0.5 * i)
    return P

bake("run", 16, run, True); bake("tail_attack", 20, tail_attack, False); bake("climb", 30, climb, True)
arm.animation_data.action = bpy.data.actions["idle_test"]
for b in pb: b.location = (0, 0, 0); b.rotation_euler = (0, 0, 0); b.scale = (1, 1, 1)
scn.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
