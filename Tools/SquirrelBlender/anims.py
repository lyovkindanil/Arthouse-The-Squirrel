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
    """pose: {bone: deg or (pitch, yaw) -- yaw turns about the vertical axis, + swings the tail to the camera side, 'hips_loc': (dy, dz), 'root_loc': (dy, dz), 'chest_scale': s}"""
    for b in pb:
        R = b.bone.matrix_local.to_3x3()
        v = pose.get(b.name, 0.0)
        if isinstance(v, Matrix): M = v
        else:
            pitch, yaw = v if isinstance(v, tuple) else (v, 0.0)
            M = Matrix.Rotation(math.radians(yaw), 3, 'Z') @ Matrix.Rotation(math.radians(pitch), 3, 'X')
        q = (R.inverted() @ M @ R)
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

# ---------------------------------------------------------------- tail attack: 28 frames, side sweep, two variants
REST_DIR = (20, 32, 45, 58, 69, 93, 120, 148, 196, 236)       # direction of each tail bone at rest
FLAT_DIR = (0, 0, 2, 3, 4, 5, 6, 8, 11, 15)                  # tail lowered and stretched for the sweep
def local(dirs):
    d = [x - r for x, r in zip(dirs, REST_DIR)]
    return [d[0]] + [d[i] - d[i - 1] for i in range(1, 10)]
FLAT = local(FLAT_DIR)
SWEEP = (60, 40, 24, 12, 8, 6, 5, 4, 3, 2)                     # yaw per bone at the hit: the base turns, the rest reaches forward
BACK = (-16, -10, -7, -5, -3, -2, -1, 0, 0, 0)                # wind-up to the far side
def ease(x): x = min(1, max(0, x)); return 0.5 - 0.5 * cos(pi * x)
CURL_P = (0, 0, 0, 0, 4, 10, 18, 26, 32, 34)                  # the tip rolls up into a hook after the hit
CURL_Y = (0, 0, 0, 0, 0, 4, 8, 12, 16, 18)
def tail_attack(t, side=1):
    """side=+1: the tail passes between the camera and the squirrel; -1: behind her"""
    P = {}
    def env(a, b, c, d, x): return ease((x - a) / (b - a)) * (1 - ease((x - c) / (d - c)))
    wind = env(0.0, 0.22, 0.22, 0.38, t)
    hit = env(0.22, 0.40, 0.58, 1.0, t)
    P["hips_loc"] = (-0.02 * wind + 0.01 * hit, -0.035 * wind - 0.03 * hit)
    P["hips"] = (-4 * wind + 5 * hit, side * (6 * wind - 10 * hit))          # the body turns into the swing
    P["spine"] = (3 * wind, side * (-3 * wind + 5 * hit)); P["chest"] = (2 * wind - 3 * hit, side * (-2 * wind + 5 * hit))
    P["neck"] = -3 * hit; P["head"] = (4 * wind + 5 * hit, 0)
    for s_ in (".L", ".R"):
        P["arm_upper" + s_] = 5 * wind - 9 * hit; P["arm_lower" + s_] = 8 * wind + 4 * hit
        P["thigh" + s_] = -8 * wind + 6 * hit; P["shin" + s_] = 12 * wind + 6 * hit
        P["ear" + s_] = -12 * hit + 5 * wind
    pc = yc = 0.0; Wprev = Matrix.Identity(3)
    for i, b in enumerate(TAIL, 1):
        d = 0.012 * i                                              # the wave runs from the base to the tip
        w = env(0.0, 0.22, 0.22, 0.38, t - d * 0.5)
        flat = env(0.2, 0.38, 0.62, 1.0, t - d * 0.3)
        h = env(0.22, 0.40, 0.58, 1.0, t - d)
        over = env(0.34, 0.46, 0.46, 0.64, t - d)                  # small overshoot of the base
        curl = env(0.40, 0.58, 0.66, 0.96, t - d * 0.5)
        pc += FLAT[i - 1] * flat + CURL_P[i - 1] * curl
        yc += side * (BACK[i - 1] * w + SWEEP[i - 1] * (h + 0.06 * over) + CURL_Y[i - 1] * curl)
        # the sweep stays horizontal: yaw about the vertical axis of the body, not of the bent tail
        W = Matrix.Rotation(math.radians(yc), 3, 'Z') @ Matrix.Rotation(math.radians(pc), 3, 'X')
        P[b] = Wprev.inverted() @ W; Wprev = W
    return P

# ---------------------------------------------------------------- climb: 30 frames, trunk on the right
# Worked out in the rest frame (trunk = the ground plane, "up the trunk" = towards the head, -Y);
# the root bone then turns the whole pose upright. Paws are planted on the trunk with two-bone IK.
import math as _m
def _geo(upper, lower):
    A = arm.data.bones[upper].head_local; B = arm.data.bones[lower].head_local; C = arm.data.bones[lower].tail_local
    L1 = _m.hypot(B.y - A.y, B.z - A.z); L2 = _m.hypot(C.y - B.y, C.z - B.z)
    return (A.y, A.z), L1, L2, _m.atan2(B.z - A.z, B.y - A.y), _m.atan2(C.z - B.z, C.y - B.y), (C.y, C.z)
def ik(upper, lower, ty, tz, body, knee):
    """returns pitch for upper and lower bone so that the wrist/ankle reaches (ty, tz); knee: +1 bends to the tail, -1 to the head"""
    (ay, az), L1, L2, r1, r2, _ = _geo(upper, lower)
    ay += body[0]; az += body[1]
    dy, dz = ty - ay, tz - az; D = min(_m.hypot(dy, dz), (L1 + L2) * 0.995)
    base = _m.atan2(dz, dy); al = _m.acos((L1 * L1 + D * D - L2 * L2) / (2 * L1 * D))
    a1 = base + knee * al                                  # direction of the upper bone
    by, bz = ay + L1 * _m.cos(a1), az + L1 * _m.sin(a1)
    a2 = _m.atan2(az + D * _m.sin(base) - bz, ay + D * _m.cos(base) - by)
    def wrap(x): return (x + 180) % 360 - 180
    u = wrap(_m.degrees(a1 - r1)); l = wrap(_m.degrees(a2 - r2) - u)
    return u, l
PRESS = 0.12                                               # how far the body is pulled to the trunk
def stride(u):
    """u in 0..1: 0..0.7 the paw grips and the body pulls past it, 0.7..1 quick reach up. returns (travel 0..1, lift 0..1)"""
    u %= 1.0
    if u < 0.7: return u / 0.7, 0.0
    k = (u - 0.7) / 0.3; return 1 - ease(k), _m.sin(pi * k)
def climb(t):
    p = 2 * pi * t; P = {}
    P["root"] = -90; P["root_loc"] = (0.0, 0.95)
    body = (-0.022 * cos(2 * p + 0.6), -PRESS + 0.012 * cos(2 * p))   # surge up with every pull, breathing off the trunk
    P["hips_loc"] = body
    # the head lives: presses to the trunk while the body pulls up, lifts to look where to grab next,
    # and glances a little to the side once per cycle
    press = 0.5 + 0.5 * sin(2 * p - 0.4)
    P["neck"] = (12 * press - 4, -5 * sin(p))
    P["head"] = (16 * press - 8 + 3 * sin(4 * p), -11 * sin(p - 0.5))
    for side, o in ((".L", 0.0), (".R", 0.5)):
        tr, lift = stride(t + o)
        (sy, sz), *_ , wrist = _geo("arm_upper" + side, "arm_lower" + side)
        u, l = ik("arm_upper" + side, "arm_lower" + side, sy - 0.20 + 0.30 * tr, wrist[1] + 0.07 * lift, body, +1)
        P["arm_upper" + side] = u; P["arm_lower" + side] = l
        P["paw_front" + side] = -(u + l) * 0.85 + 30 * lift
        tr, lift = stride(t + o + 0.5)                     # diagonal pairs
        (hy, hz), *_ , ankle = _geo("thigh" + side, "shin" + side)
        u, l = ik("thigh" + side, "shin" + side, hy - 0.16 + 0.30 * tr, ankle[1] + 0.06 * lift, body, -1)
        P["thigh" + side] = u; P["shin" + side] = l
        P["paw_hind" + side] = -(u + l) * 0.85 + 25 * lift
        P["ear" + side] = -4 - 12 * press + 6 * sin(4 * p + o * 6)   # ears lay back when the head presses in
    for i, b in enumerate(TAIL, 1):
        P[b] = (-10, -6, -2, 2, 3, 2, 1, 1, 1, 1)[i - 1] + (1.4 + 0.3 * i) * sin(2 * p - 0.5 * i)
    return P

bake("run", 16, run, True); bake("tail_attack_front", 28, lambda t: tail_attack(t, 1), False); bake("tail_attack_back", 28, lambda t: tail_attack(t, -1), False); bake("climb", 30, climb, True)
arm.animation_data.action = bpy.data.actions["idle_test"]
for b in pb: b.location = (0, 0, 0); b.rotation_euler = (0, 0, 0); b.scale = (1, 1, 1)
scn.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
