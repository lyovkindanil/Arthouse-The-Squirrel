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
        b = pb[name]; v = pose.get(key, (0, 0)); dx, dy, dz = v if len(v) == 3 else (0, v[0], v[1])
        b.location = b.bone.matrix_local.to_3x3().inverted() @ Vector((dx, dy, dz)); b.keyframe_insert("location", frame=frame)
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
def ik(upper, lower, ty, tz, body, knee, A=None, parent=0.0):
    """returns pitch for upper and lower bone so that the wrist/ankle reaches (ty, tz); knee: +1 bends to the tail, -1 to the head"""
    (ay, az), L1, L2, r1, r2, _ = _geo(upper, lower)
    if A: ay, az = A                                       # joint moved by the spine: A is given, parent = its pitch
    ay += body[0]; az += body[1]
    dy, dz = ty - ay, tz - az; D = min(_m.hypot(dy, dz), (L1 + L2) * 0.995)
    base = _m.atan2(dz, dy); al = _m.acos((L1 * L1 + D * D - L2 * L2) / (2 * L1 * D))
    a1 = base + knee * al                                  # direction of the upper bone
    by, bz = ay + L1 * _m.cos(a1), az + L1 * _m.sin(a1)
    a2 = _m.atan2(az + D * _m.sin(base) - bz, ay + D * _m.cos(base) - by)
    def wrap(x): return (x + 180) % 360 - 180
    u = wrap(_m.degrees(a1 - r1)); l = wrap(_m.degrees(a2 - r2) - u)
    return wrap(u - parent), l
def _rot(v, deg):
    c, s_ = _m.cos(_m.radians(deg)), _m.sin(_m.radians(deg)); return (v[0] * c - v[1] * s_, v[0] * s_ + v[1] * c)
def shoulder(side, sp, ch):
    """position of the shoulder joint when the spine and the chest are pitched by sp and ch"""
    S = arm.data.bones["spine"].head_local; C = arm.data.bones["chest"].head_local; A = arm.data.bones["arm_upper" + side].head_local
    a = _rot((A.y - C.y, A.z - C.z), ch); a = _rot((C.y - S.y + a[0], C.z - S.z + a[1]), sp)
    return (S.y + a[0], S.z + a[1])
PRESS = 0.12                                               # how far the body is pulled to the trunk
def stride(u):
    """u in 0..1: 0..0.7 the paw grips and the body pulls past it, 0.7..1 quick reach up. returns (travel 0..1, lift 0..1)"""
    u %= 1.0
    if u < 0.7: return u / 0.7, 0.0
    k = (u - 0.7) / 0.3; return 1 - ease(k), _m.sin(pi * k)
def head_press(P, p, amount):
    """the front of the body leans into the trunk on every pull; returns (press 0..1, spine, chest)"""
    press = 0.5 + 0.5 * sin(2 * p - 0.4)
    sp = 5 * press * amount; ch = 10 * press * amount
    P["spine"] = sp; P["chest"] = ch
    # the neck carries the head down to the bark, the head itself keeps looking up the trunk and turns a little
    P["neck"] = (26 * press * amount - 2, -5 * sin(p))
    P["head"] = (-4 * press * amount + 2 + 3 * sin(4 * p), -11 * sin(p - 0.5))
    return press, sp, ch
def climb(t):
    p = 2 * pi * t; P = {}
    P["root"] = -90; P["root_loc"] = (0.0, 0.95)
    body = (-0.022 * cos(2 * p + 0.6), -PRESS + 0.012 * cos(2 * p))   # surge up with every pull, breathing off the trunk
    P["hips_loc"] = body
    press, sp, ch = head_press(P, p, 1.0)
    for side, o in ((".L", 0.0), (".R", 0.5)):
        tr, lift = stride(t + o)
        (sy, sz), *_ , wrist = _geo("arm_upper" + side, "arm_lower" + side)
        u, l = ik("arm_upper" + side, "arm_lower" + side, sy - 0.20 + 0.30 * tr, wrist[1] + 0.07 * lift, body, +1,
                  A=shoulder(side, sp, ch), parent=sp + ch)
        P["arm_upper" + side] = u; P["arm_lower" + side] = l
        P["paw_front" + side] = -(u + l + sp + ch) * 0.85 + 30 * lift
        tr, lift = stride(t + o + 0.5)                     # diagonal pairs
        (hy, hz), *_ , ankle = _geo("thigh" + side, "shin" + side)
        u, l = ik("thigh" + side, "shin" + side, hy - 0.16 + 0.30 * tr, ankle[1] + 0.06 * lift, body, -1)
        P["thigh" + side] = u; P["shin" + side] = l
        P["paw_hind" + side] = -(u + l) * 0.85 + 25 * lift
        P["ear" + side] = -4 - 12 * press + 6 * sin(4 * p + o * 6)   # ears lay back when the head presses in
    for i, b in enumerate(TAIL, 1):
        P[b] = (-10, -6, -2, 2, 3, 2, 1, 1, 1, 1)[i - 1] + (1.4 + 0.3 * i) * sin(2 * p - 0.5 * i)
    return P

# ---------------------------------------------------------------- climb_hug: the squirrel embraces a thin trunk
# The belly lies on the trunk, the legs go around it. The far legs are hidden by the "Trunk" holdout cylinder.
HUG = 0.25
def limb(pitch, roll):                                     # roll: spread to the side (about the body axis), then swing along the trunk
    return Matrix.Rotation(math.radians(pitch), 3, 'X') @ Matrix.Rotation(math.radians(roll), 3, 'Y')
def climb_hug(t):
    p = 2 * pi * t; P = {}
    P["root"] = -90; P["root_loc"] = (0.0, 0.95)
    P["hips_loc"] = (-0.02 * cos(2 * p + 0.6), -HUG + 0.01 * cos(2 * p))
    press, sp, ch = head_press(P, p, 0.8)
    for side, o, out in ((".L", 0.0, -1), (".R", 0.5, 1)):          # out: sign of the roll that spreads this leg outwards
        tr, lift = stride(t + o)
        P["arm_upper" + side] = limb(-38 + 52 * tr - (sp + ch), out * (52 + 22 * lift))
        P["arm_lower" + side] = limb(8 + 10 * tr, out * (-58 + 10 * lift))
        P["paw_front" + side] = limb(-10 + 25 * lift, out * -25)
        tr, lift = stride(t + o + 0.5)
        P["thigh" + side] = limb(-34 + 48 * tr, out * (56 + 20 * lift))
        P["shin" + side] = limb(26 - 8 * tr, out * (-62 + 10 * lift))
        P["paw_hind" + side] = limb(-6 + 20 * lift, out * -25)
        P["ear" + side] = -4 - 12 * press + 6 * sin(4 * p + o * 6)
    for i, b in enumerate(TAIL, 1):
        P[b] = (-12, -8, -3, 2, 3, 2, 1, 1, 1, 1)[i - 1] + (1.4 + 0.3 * i) * sin(2 * p - 0.5 * i)
    return P

# ---------------------------------------------------------------- tail_spin: the squirrel whirls around and hits with the tail
def tail_spin(t):
    P = {}
    def env(a, b, c, d, x): return ease((x - a) / (b - a)) * (1 - ease((x - c) / (d - c)))
    wind = env(0.0, 0.16, 0.16, 0.3, t)
    turn = ease((t - 0.14) / 0.5)                          # 0 -> 1 : one full turn, fastest in the middle
    speed = sin(pi * min(1, max(0, (t - 0.14) / 0.5)))     # how fast she turns right now
    settle = env(0.6, 0.72, 0.72, 1.0, t)
    P["root"] = (0, -22 * wind + 360 * turn)
    P["root_loc"] = (0.3 * (1 - cos(2 * pi * turn)), 0)    # she turns about her hips, so the tail lands in front and stays in the frame
    P["hips_loc"] = (0, -0.05 * wind + 0.05 * speed - 0.025 * settle)   # crouch, little hop, land
    P["hips"] = -5 * wind + 4 * speed; P["spine"] = (4 * wind - 3 * speed, 8 * speed); P["chest"] = (3 * wind - 3 * speed, 8 * speed)
    P["neck"] = (0, 10 * speed); P["head"] = (4 * wind, 14 * speed - 8 * settle)       # the head leads the turn
    for s_ in (".L", ".R"):
        P["arm_upper" + s_] = 6 * wind - 14 * speed; P["arm_lower" + s_] = 10 * wind + 22 * speed
        P["thigh" + s_] = -10 * wind + 10 * speed; P["shin" + s_] = 14 * wind + 16 * speed
        P["ear" + s_] = -16 * speed + 5 * wind
    pc = yc = 0.0; Wprev = Matrix.Identity(3)
    for i, b in enumerate(TAIL, 1):
        d = 0.012 * i
        flat = env(0.1, 0.3, 0.62, 0.95, t - d * 0.3) * 0.7
        sp_i = sin(pi * min(1, max(0, (t - d - 0.14) / 0.5)))
        over = env(0.56, 0.7, 0.7, 0.98, t - d)            # after the stop the tail swings on and curls
        pc += FLAT[i - 1] * flat + CURL_P[i - 1] * over * 0.8
        yc += -(3 + 0.9 * i) * sp_i + (5 + 1.2 * i) * over + 3 * wind   # drags behind while turning
        W = Matrix.Rotation(math.radians(yc), 3, 'Z') @ Matrix.Rotation(math.radians(pc), 3, 'X')
        P[b] = Wprev.inverted() @ W; Wprev = W
    return P

# ---------------------------------------------------------------- flips: a jump, one fast turn in the air, the tail hits
HIT_DIR = (82, 102, 120, 137, 152, 166, 180, 195, 212, 228)    # tail thrown forward in an arc over the back
HIT = local(HIT_DIR)
def _flight(t, a=0.16, b=0.72):
    k = min(1, max(0, (t - a) / (b - a)))
    return k, sin(pi * k)                                   # progress of the flight, height 0..1
def tail_flip_side(t):
    """side flip: she jumps and rolls over her side (about the spine); the tail, thrown forward, whips around with the body"""
    P = {}
    def env(a, b, c, d, x): return ease((x - a) / (b - a)) * (1 - ease((x - c) / (d - c)))
    wind = env(0.0, 0.16, 0.16, 0.3, t); k, up = _flight(t)
    roll = 360 * ease(k); land = env(0.68, 0.8, 0.8, 1.0, t)
    h = 0.36                                               # height of the body axis above the paws
    r = math.radians(roll)
    P["root"] = Matrix.Rotation(r, 3, 'Y') @ Matrix.Rotation(math.radians(-8 * up), 3, 'X')
    P["root_loc"] = (-h * sin(r), 0.05 * up, h - h * cos(r) + 0.7 * up)
    P["hips_loc"] = (0, -0.06 * wind - 0.04 * land)
    P["hips"] = -6 * wind + 5 * land; P["spine"] = 5 * wind + 4 * up; P["chest"] = 4 * wind + 3 * up
    P["neck"] = 6 * up; P["head"] = 5 * wind + 8 * up
    for s_ in (".L", ".R"):
        P["arm_upper" + s_] = 6 * wind + 25 * up - 8 * land; P["arm_lower" + s_] = 10 * wind + 45 * up
        P["thigh" + s_] = -10 * wind - 30 * up + 8 * land; P["shin" + s_] = 16 * wind + 50 * up + 8 * land
        P["ear" + s_] = -22 * up + 5 * wind
    for i, b in enumerate(TAIL, 1):
        d = 0.012 * i
        back = env(0.0, 0.16, 0.16, 0.3, t - d * 0.5)
        thr = env(0.08, 0.26, 0.66, 0.98, t - d * 0.6)      # tail thrown forward while she rolls
        over = env(0.6, 0.74, 0.74, 0.98, t - d)
        P[b] = WIND_T[i - 1] * back + CONE[i - 1] * thr + 2.5 * over
    return P
CONE = local((95, 115, 130, 140, 146, 150, 154, 158, 164, 172))   # tail up and forward, almost straight: it sweeps a wide circle as she rolls
WIND_T = (-9, -4, -3, -2, -2, -1, 0, 0, 0, 0)
def tail_flip_front(t):
    """forward flip: she jumps, turns head over heels with the tail tucked, and slams the tail down in front on landing"""
    P = {}
    def env(a, b, c, d, x): return ease((x - a) / (b - a)) * (1 - ease((x - c) / (d - c)))
    wind = env(0.0, 0.16, 0.16, 0.3, t); k, up = _flight(t, 0.16, 0.68)
    ang = 360 * ease(k); land = env(0.64, 0.76, 0.78, 1.0, t)
    c = (-0.1, 0.36); rc = _rot(c, ang)                    # turn about the middle of the body, not about the paws
    P["root"] = ang
    P["root_loc"] = (c[0] - rc[0] + 0.1 * land, c[1] - rc[1] + 0.55 * up)
    P["hips_loc"] = (0, -0.06 * wind - 0.05 * land)
    P["hips"] = -6 * wind + 6 * land; P["spine"] = 5 * wind + 16 * up; P["chest"] = 4 * wind + 14 * up     # ball in the air
    P["neck"] = 10 * up; P["head"] = 5 * wind + 14 * up - 6 * land
    for s_ in (".L", ".R"):
        P["arm_upper" + s_] = 6 * wind + 30 * up - 12 * land; P["arm_lower" + s_] = 10 * wind + 50 * up
        P["thigh" + s_] = -10 * wind - 40 * up + 10 * land; P["shin" + s_] = 16 * wind + 60 * up + 8 * land
        P["ear" + s_] = -24 * up + 5 * wind - 10 * land
    for i, b in enumerate(TAIL, 1):
        d = 0.012 * i
        back = env(0.0, 0.16, 0.16, 0.3, t - d * 0.5)
        tuck = env(0.14, 0.34, 0.5, 0.68, t)               # tail wrapped tight while she turns
        slam = env(0.5, 0.7, 0.76, 1.0, t - d)             # and thrown over on landing
        P[b] = WIND_T[i - 1] * back + 8.5 * tuck + HIT[i - 1] * 0.93 * slam
    return P

# ================================================================ real squirrel behaviour
# Squirrels move in quick jerks and then freeze, so most tracks here are "snap and hold".
def track(t, keys, w=0.035):
    """keys: [(time 0..1, value)], held until the next key; the change itself takes w of the cycle. Loops."""
    keys = sorted(keys); t %= 1.0
    cur = keys[-1][1]
    for i, (kt, kv) in enumerate(keys):
        if t >= kt: cur = kv; nxt_t, nxt_v = (keys[i + 1] if i + 1 < len(keys) else (keys[0][0] + 1.0, keys[0][1]))
    if t < keys[0][0]: nxt_t, nxt_v = keys[0]
    k = (t - (nxt_t - w)) / w
    return cur + (nxt_v - cur) * ease(k) if k > 0 else cur
def pulse(t, at, rise=0.02, fall=0.08):
    """a quick twitch at time `at`: up fast, down slower"""
    x = (t - at) % 1.0
    if x < rise: return ease(x / rise)
    if x < rise + fall: return 1 - ease((x - rise) / fall)
    return 0.0
def _psi(bone):
    b = arm.data.bones[bone]; return _m.degrees(_m.atan2(b.tail_local.z - b.head_local.z, b.tail_local.y - b.head_local.y))
def aim(P, chain, dirs, parent):
    """point the bones of a chain in the given side-view directions (deg: 0 = back, 90 = up, 180 = forward, -90 = down)"""
    cum = parent
    for b, d in zip(chain, dirs):
        want = (d - _psi(b) + 180) % 360 - 180
        P[b] = (want - cum + 180) % 360 - 180; cum = want
def tail_dirs(P, dirs, parent, add=None):
    cum = parent
    for i, b in enumerate(TAIL):
        want = dirs[i] - REST_DIR[i]
        P[b] = want - cum + (add[i] if add else 0); cum = want
def tail_pose(P, dirs, yaws, parent):
    """tail by side-view directions of its bones plus a sideways sweep (yaw, cumulative, + = to the camera side)"""
    Wprev = Matrix.Rotation(math.radians(parent), 3, 'X')   # the sweep is about the true vertical, however the hips are tilted
    for i, b in enumerate(TAIL):
        W = Matrix.Rotation(math.radians(yaws[i]), 3, 'Z') @ Matrix.Rotation(math.radians(dirs[i] - REST_DIR[i]), 3, 'X')
        P[b] = Wprev.inverted() @ W; Wprev = W
def mix(a, b, k): return [x + (y - x) * k for x, y in zip(a, b)]
# the tail keeps the shape the model was sculpted in (back from the rump, up, curl forward), only leaned to the back
SIT_TAIL = [d + 6 for d in REST_DIR]
NO_YAW = [0.0] * 10
def sit(P, lean=0.0, drop=0.0, head=0.0, head_yaw=0.0, arms=(-104, -138), breath=0.0, tail_add=None,
        sp=4.0, ch=8.0, nk=22.0, tail=None, yaws=None, hb=0.0, sb=0.0, cb=0.0):
    """sitting on the haunches. lean: forward pitch of the body; arms: side-view directions of upper and lower front leg"""
    hp = -58 + lean
    body = (0.04, -0.2 - drop)
    P["hips"] = hp; P["hips_loc"] = body
    H = arm.data.bones["hips"].head_local
    for s_, ty in ((".L", -0.10), (".R", -0.04)):
        T = arm.data.bones["thigh" + s_].head_local; a = _rot((T.y - H.y, T.z - H.z), hp)
        u, l = ik("thigh" + s_, "shin" + s_, ty, -0.765, body, -1, A=(H.y + a[0], H.z + a[1]), parent=hp)
        P["thigh" + s_] = u; P["shin" + s_] = l; P["paw_hind" + s_] = -(u + l + hp)
        aim(P, ["arm_upper" + s_, "arm_lower" + s_, "paw_front" + s_], (arms[0], arms[1], arms[1] - 25), hp + hb + sp + sb + ch + cb)
    P["spine"] = sp; P["chest"] = ch; P["chest_scale"] = 1 + 0.03 * breath
    P["hips.b"] = hb; P["spine.b"] = sb; P["chest.b"] = cb      # the extra back joints, used for curling up
    P["neck"] = (nk, head_yaw * 0.3); P["head"] = (-(hp + hb + sp + sb + ch + cb + nk) + head, head_yaw * 0.7)
    dirs = list(tail or SIT_TAIL)
    if tail_add: dirs = [d + x for d, x in zip(dirs, tail_add)]
    tail_pose(P, dirs, yaws or NO_YAW, hp)
    return P
def whip(t, times, amp=1.0, dur=0.11):
    """tail flicks the way squirrels do it: a quick wave that runs up the tail, strongest at the tip. returns extra direction per bone"""
    out = [0.0] * 10
    for a in times:
        x = (t - a) % 1.0
        if x < dur:
            e = sin(pi * x / dur)
            for i in range(10): out[i] += amp * (2.0 + 2.2 * i) * e * sin(2 * pi * x / dur - 0.5 * i)
    return out

def sit_alert(t):
    """stands up like a post and looks around: quick turns of the head, freezes, ear and tail twitches"""
    P = {}
    yaw = track(t, [(0.0, 0), (0.12, -38), (0.34, -38), (0.38, 12), (0.58, 12), (0.62, -20), (0.8, -20), (0.86, 0)])
    pitch = track(t, [(0.0, 0), (0.12, -6), (0.38, 4), (0.62, -14), (0.8, -14), (0.86, 0)])
    lean = track(t, [(0.0, 0), (0.6, -7), (0.82, -7), (0.9, 0)], w=0.06)                 # stretches up to look further
    add = whip(t, (0.22, 0.31, 0.7), 0.8)                                               # tail flicks
    sit(P, lean=lean, head=pitch, head_yaw=yaw, breath=0.5 - 0.5 * cos(4 * pi * t), tail_add=add)
    P["ear.L"] = 14 * pulse(t, 0.16) + 12 * pulse(t, 0.5) - 10 * pulse(t, 0.66)
    P["ear.R"] = 12 * pulse(t, 0.44) + 14 * pulse(t, 0.74) - 8 * pulse(t, 0.2)
    return P

def nibble(t):
    """sits and gnaws a nut held in the front paws; stops for a moment to look up"""
    P = {}
    busy = 1 - track(t, [(0.0, 0), (0.62, 1), (0.8, 0)], w=0.05)                      # 1 = gnawing, 0 = pause, head up
    chew = sin(2 * pi * t * 10) * busy                                                # ten bites per cycle
    turn = sin(2 * pi * t * 2.5) * busy                                               # turns the nut in the paws
    yaw = track(t, [(0.0, 0), (0.66, -30), (0.78, -30), (0.82, 0)])
    sit(P, lean=10 * busy + 2, head=14 * busy + 2.5 * chew - 6 * (1 - busy), head_yaw=yaw,
        arms=(-150 + 4 * turn, 128 + 5 * chew + 6 * turn), breath=0.5 - 0.5 * cos(4 * pi * t),
        tail_add=[(0.4 + 0.25 * i) * sin(2 * pi * t - 0.4 * i) for i in range(1, 11)])
    P["ear.L"] = 5 * chew + 14 * pulse(t, 0.64); P["ear.R"] = 5 * chew + 12 * pulse(t, 0.7)
    return P

# calm sit, and curling up into a ball
BALL_TAIL = (-4, -3, -2, 0, 0, 1, 2, 3, 5, 8)                   # tail laid on the ground ...
BALL_YAW = (38, 68, 94, 116, 135, 151, 165, 177, 187, 195)    # ... and wrapped around the body on the camera side, tip over the nose
def rest_pose(P, c_body, c_head, c_flat, c_wrap, breath=0.0, head=0.0, head_yaw=0.0, tail_add=None):
    """0 = sitting hunched and calm, 1 = curled into a ball. The back stays arched like a dome, the nose goes down to the belly,
    the tail first lies down behind (c_flat), then sweeps around the body (c_wrap)"""
    arms = mix((-96, -86), (-62, -176), c_body)
    # six joints share the curl: the rump stays down, the back makes an even arc over it
    sit(P, lean=30 - 50 * c_body, drop=0.03 * c_body, head=head * (1 - c_head) + 6 + 100 * c_head, head_yaw=head_yaw * (1 - c_head), arms=arms, breath=breath,
        hb=30 * c_body, sp=10 + 22 * c_body, sb=32 * c_body, ch=12 + 20 * c_body, cb=30 * c_body, nk=14 + 30 * c_head,
        tail=mix(SIT_TAIL, BALL_TAIL, c_flat), yaws=mix(NO_YAW, BALL_YAW, c_wrap), tail_add=tail_add)
    for s_ in (".L", ".R"): P["ear" + s_] = -45 * c_head
    return P
def sit_idle(t):
    """sits quietly: breathes, looks to the side once, ear twitches, the tail tip sways"""
    P = {}
    yaw = track(t, [(0.0, 0), (0.45, -28), (0.7, -28), (0.76, 0)])
    rest_pose(P, 0, 0, 0, 0, breath=0.5 - 0.5 * cos(4 * pi * t), head=track(t, [(0.0, 0), (0.45, -5), (0.76, 0)]), head_yaw=yaw,
              tail_add=[(0.3 + 0.3 * i) * sin(2 * pi * t - 0.45 * i) for i in range(1, 11)])
    P["ear.L"] = 12 * pulse(t, 0.2) + 10 * pulse(t, 0.6); P["ear.R"] = 12 * pulse(t, 0.52) + 8 * pulse(t, 0.9)
    return P
def curl_up(t):
    """from the calm sit: sinks down, tucks the nose to the belly, wraps the tail around herself"""
    P = {}
    rest_pose(P, ease(t / 0.5), ease((t - 0.25) / 0.45), ease((t - 0.1) / 0.5), ease((t - 0.22) / 0.78))
    return P
def sleep_ball(t):
    """asleep in a ball: slow breathing, the tail tip stirs"""
    P = {}
    br = 0.5 - 0.5 * cos(2 * pi * t)
    rest_pose(P, 1, 1, 1, 1, breath=1.6 * br, tail_add=[0.25 * i * br for i in range(1, 11)])
    P["hips_loc"] = (P["hips_loc"][0], P["hips_loc"][1] + 0.008 * br)
    P["ear.L"] = -45 + 12 * pulse(t, 0.55, 0.03, 0.06)
    return P

def keyed(t, keys):
    """smooth interpolation through [(time, value)] keys, one shot"""
    keys = sorted(keys)
    if t <= keys[0][0]: return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1: return v0 + (v1 - v0) * ease((t - t0) / (t1 - t0))
    return keys[-1][1]
def paw_swipe(t):
    """rears up a little on the haunches, pulls the near front paw back and swats forward and down, then drops back on all fours"""
    P = {}
    rear = keyed(t, [(0.0, 0), (0.22, 1), (0.3, 1), (0.44, 0.3), (0.58, 0.15), (0.95, 0)])               # how much the front is lifted
    hit = keyed(t, [(0.3, 0), (0.42, 1), (0.54, 1), (0.9, 0)])                   # lunge that comes with the swat
    hp = -40 * rear + 16 * hit
    body = (0.04 * rear - 0.09 * hit, -0.10 * rear + 0.02 * hit)
    P["hips"] = hp; P["hips_loc"] = body
    sp, ch = 3 * rear - 2 * hit, 5 * rear - 4 * hit
    P["spine"] = sp; P["chest"] = ch
    P["neck"] = (14 * rear - 4 * hit, 0); P["head"] = (10 * rear + 10 * hit, -8 * hit)
    H = arm.data.bones["hips"].head_local
    for s_ in (".L", ".R"):
        (hy, hz), *_ , ankle = _geo("thigh" + s_, "shin" + s_)
        T = arm.data.bones["thigh" + s_].head_local; a = _rot((T.y - H.y, T.z - H.z), hp)
        u, l = ik("thigh" + s_, "shin" + s_, ankle[0], ankle[1], body, -1, A=(H.y + a[0], H.z + a[1]), parent=hp)
        P["thigh" + s_] = u; P["shin" + s_] = l; P["paw_hind" + s_] = -(u + l + hp)
        P["ear" + s_] = 6 * rear - 26 * hit
    par = hp + sp + ch
    r_up, r_lo, r_pw = _psi("arm_upper.L"), _psi("arm_lower.L"), _psi("paw_front.L")
    # far paw: just tucks to the chest while she is up
    aim(P, ["arm_upper.L", "arm_lower.L", "paw_front.L"], (r_up - 22 * rear, r_lo - 62 * rear, r_pw - 40 * rear), par)
    # near paw: back and up (wind-up), then a fast arc forward and down, claws out
    up = keyed(t, [(0.0, r_up), (0.2, -40), (0.3, -34), (0.42, -172), (0.56, -140), (0.95, r_up)])
    lo = keyed(t, [(0.0, r_lo), (0.2, -250), (0.3, -262), (0.42, -178), (0.56, -120), (0.95, r_lo)])
    pw = keyed(t, [(0.0, r_pw), (0.2, -260), (0.3, -280), (0.42, -160), (0.56, -150), (0.95, r_pw)])
    aim(P, ["arm_upper.R", "arm_lower.R", "paw_front.R"], (up, lo, pw), par)
    cum = 0.0
    for i, b in enumerate(TAIL):                                               # the tail balances: lifts on the rear-up, snaps back on the hit
        dly = 0.012 * i
        want = (1.2 + 0.25 * i) * keyed(t - dly, [(0.0, 0), (0.24, 1), (0.42, -1.6), (0.6, 0.6), (0.95, 0)])
        P[b] = want - cum; cum = want
    return P

def jump(t):
    """a full jump in place (the game moves her): crouch, push off, fly up with the paws tucked, level out at the top,
    fall nose first reaching for the ground, land on the front paws, then the hind ones.
    Frames (of 36): 0-6 crouch, 6-10 push off, 10-18 rise, 18-22 top, 22-30 fall, 30-35 landing."""
    P = {}
    hp = keyed(t, [(0, 0), (0.17, 5), (0.27, -27), (0.45, -30), (0.58, -6), (0.72, 22), (0.84, 12), (0.93, 0)])
    dz = keyed(t, [(0, 0), (0.17, -0.10), (0.27, 0.05), (0.34, 0), (0.82, 0), (0.89, -0.08), (1, 0)])
    dy = keyed(t, [(0, 0), (0.17, 0.03), (0.27, -0.04), (0.4, 0), (0.84, 0), (0.9, -0.03), (1, 0)])
    body = (dy, dz)
    sp = keyed(t, [(0, 0), (0.17, 8), (0.27, -5), (0.45, -4), (0.58, 9), (0.72, 2), (0.89, 8), (1, 0)])
    ch = keyed(t, [(0, 0), (0.17, 8), (0.27, -7), (0.45, -5), (0.58, 9), (0.72, 0), (0.89, 6), (1, 0)])
    P["hips"] = hp; P["hips_loc"] = body; P["spine"] = sp; P["chest"] = ch
    P["neck"] = -0.3 * hp; P["head"] = -0.35 * hp + keyed(t, [(0, 0), (0.17, 8), (0.3, -6), (0.72, 6), (0.89, 10), (1, 0)])
    gf = keyed(t, [(0.16, 1), (0.22, 0), (0.8, 0), (0.85, 1)])            # front paws on the ground
    gh = keyed(t, [(0.25, 1), (0.3, 0), (0.84, 0), (0.9, 1)])             # hind paws on the ground
    fU = keyed(t, [(0.18, 0), (0.3, 28), (0.5, 28), (0.62, -15), (0.75, -55), (0.85, -35)])
    fL = keyed(t, [(0.18, 0), (0.3, -95), (0.5, -95), (0.62, -55), (0.75, 8), (0.85, 0)])
    hT = keyed(t, [(0.25, 20), (0.34, 55), (0.48, 50), (0.6, -30), (0.74, -38), (0.86, -15)])
    hS = keyed(t, [(0.25, -10), (0.34, -30), (0.48, -25), (0.6, 42), (0.74, 30), (0.86, 5)])
    hP = keyed(t, [(0.25, 30), (0.34, 70), (0.48, 60), (0.6, 0), (0.86, 0)])
    H = arm.data.bones["hips"].head_local
    for s_, o in ((".L", 0.0), (".R", 6.0)):
        A = shoulder(s_, sp, ch); a = _rot((A[0] - H.y, A[1] - H.z), hp); par = hp + sp + ch
        (sy, sz), *_ , wrist = _geo("arm_upper" + s_, "arm_lower" + s_)
        u, l = ik("arm_upper" + s_, "arm_lower" + s_, wrist[0], wrist[1], body, +1, A=(H.y + a[0], H.z + a[1]), parent=par)
        P["arm_upper" + s_] = gf * u + (1 - gf) * (fU + o); P["arm_lower" + s_] = gf * l + (1 - gf) * (fL - o)
        P["paw_front" + s_] = gf * -(u + l + par) + (1 - gf) * -20
        (hy, hz), *_ , ankle = _geo("thigh" + s_, "shin" + s_)
        T = arm.data.bones["thigh" + s_].head_local; a = _rot((T.y - H.y, T.z - H.z), hp)
        u, l = ik("thigh" + s_, "shin" + s_, ankle[0], ankle[1], body, -1, A=(H.y + a[0], H.z + a[1]), parent=hp)
        P["thigh" + s_] = gh * u + (1 - gh) * (hT - o); P["shin" + s_] = gh * l + (1 - gh) * hS
        P["paw_hind" + s_] = gh * -(u + l + hp) + (1 - gh) * hP
        P["ear" + s_] = keyed(t, [(0, 0), (0.17, 8), (0.3, -24), (0.55, -10), (0.72, -28), (0.9, 6), (1, 0)])
    cum = 0.0
    for i, b in enumerate(TAIL):                                          # the tail trails: down behind her on the way up, up above her on the way down
        dly = 0.012 * i
        k = keyed(t - dly, [(0, 0), (0.17, 1.5), (0.3, -3.6), (0.48, -3.0), (0.62, 0.5), (0.76, 3.2), (0.88, 2.0), (1, 0)])
        want = k * (1 + 0.9 * i) - 0.6 * hp * (1 if i == 0 else 0)
        P[b] = want - cum; cum = want
    return P

def tail_flag(t):
    """alarm: stands tense on all fours and whips the tail in sharp series, the way squirrels scold"""
    P = {}
    flicks = [0.08, 0.18, 0.28, 0.55, 0.65]
    f = sum(pulse(t, a, 0.025, 0.07) for a in flicks)
    jerk = sum(pulse(t, a, 0.02, 0.05) for a in flicks)                               # the whole body twitches with each flick
    tense = track(t, [(0.0, 1), (0.42, 0.6), (0.5, 1), (0.8, 0.5), (0.92, 1)], w=0.06)
    body = (0.0, -0.05 * tense - 0.012 * jerk)
    P["hips_loc"] = body
    sp, ch = -3 * tense, -5 * tense - 2 * jerk
    P["spine"] = sp; P["chest"] = ch
    P["neck"] = -8 * tense; P["head"] = (6 * tense + 3 * jerk, track(t, [(0.0, 0), (0.4, -25), (0.5, 0), (0.78, -15), (0.9, 0)]))
    for s_ in (".L", ".R"):
        (sy, sz), *_ , wrist = _geo("arm_upper" + s_, "arm_lower" + s_)
        u, l = ik("arm_upper" + s_, "arm_lower" + s_, wrist[0] - 0.03, wrist[1], body, +1, A=shoulder(s_, sp, ch), parent=sp + ch)
        P["arm_upper" + s_] = u; P["arm_lower" + s_] = l; P["paw_front" + s_] = -(u + l + sp + ch)
        (hy, hz), *_ , ankle = _geo("thigh" + s_, "shin" + s_)
        u, l = ik("thigh" + s_, "shin" + s_, ankle[0], ankle[1], body, -1)
        P["thigh" + s_] = u; P["shin" + s_] = l; P["paw_hind" + s_] = -(u + l)
        P["ear" + s_] = -10 * tense + 10 * jerk
    wv = whip(t, flicks, 1.0)
    cum = 0.0
    for i, b in enumerate(TAIL):
        want = 3.0 * tense + wv[i] + (0.6 + 0.2 * i) * sin(2 * pi * 2 * t - 0.5 * i)   # raised a little, flicks, idle sway
        P[b] = want - cum; cum = want
    return P

def sniff(t):
    """nose to the ground: lowers the front, sniffs along the ground in little jerks, lifts the head to check around"""
    P = {}
    down = track(t, [(0.0, 0), (0.1, 1), (0.62, 1), (0.72, 0)], w=0.09)
    sn = sin(2 * pi * t * 12) * down                                                  # quick sniffing
    reach = track(t, [(0.1, 0), (0.28, 1), (0.46, -0.6), (0.62, 0)], w=0.07) * down  # works the nose forward and back
    look = track(t, [(0.0, 0), (0.76, -32), (0.9, -32), (0.95, 0)])
    body = (-0.03 * down - 0.03 * reach, -0.05 * down)
    P["hips_loc"] = body
    sp, ch = 8 * down, 17 * down
    P["spine"] = sp; P["chest"] = ch; P["hips"] = -3 * down
    P["neck"] = (36 * down + 5 * reach, 0); P["head"] = (20 * down - 8 * reach + 2 * sn - 5 * (1 - down), look)
    for s_ in (".L", ".R"):
        (sy, sz), *_ , wrist = _geo("arm_upper" + s_, "arm_lower" + s_)
        u, l = ik("arm_upper" + s_, "arm_lower" + s_, wrist[0], wrist[1], body, +1, A=shoulder(s_, sp - 3 * down, ch), parent=sp + ch - 3 * down)
        P["arm_upper" + s_] = u; P["arm_lower" + s_] = l; P["paw_front" + s_] = -(u + l + sp + ch - 3 * down)
        (hy, hz), *_ , ankle = _geo("thigh" + s_, "shin" + s_)
        H = arm.data.bones["hips"].head_local; T = arm.data.bones["thigh" + s_].head_local; a = _rot((T.y - H.y, T.z - H.z), -3 * down)
        u, l = ik("thigh" + s_, "shin" + s_, ankle[0], ankle[1], body, -1, A=(H.y + a[0], H.z + a[1]), parent=-3 * down)
        P["thigh" + s_] = u; P["shin" + s_] = l; P["paw_hind" + s_] = -(u + l - 3 * down)
    P["ear.L"] = -8 * down + 3 * sn + 14 * pulse(t, 0.8); P["ear.R"] = -8 * down - 3 * sn + 12 * pulse(t, 0.86)
    for i, b in enumerate(TAIL, 1):
        P[b] = 1.5 * down + (1.0 + 0.2 * i) * sin(2 * pi * 2 * t - 0.5 * i) + (3 - 0.2 * i) * pulse(t - 0.006 * i, 0.82, 0.03, 0.1)
    return P

bake("run", 16, run, True); bake("tail_attack_front", 28, lambda t: tail_attack(t, 1), False); bake("tail_attack_back", 28, lambda t: tail_attack(t, -1), False); bake("climb", 30, climb, True)
bake("climb_hug", 30, climb_hug, True)["holdout"] = "Trunk"; bake("tail_spin", 28, tail_spin, False)
bake("sit_alert", 90, sit_alert, True); bake("nibble", 60, nibble, True); bake("tail_flag", 60, tail_flag, True); bake("sniff", 90, sniff, True)
bake("paw_swipe", 24, paw_swipe, False); bake("jump", 36, jump, False)
bake("sit_idle", 60, sit_idle, True); bake("curl_up", 40, curl_up, False); bake("sleep_ball", 90, sleep_ball, True)
bake("tail_flip_side", 22, tail_flip_side, False)["wide"] = 1.25; bake("tail_flip_front", 24, tail_flip_front, False)["wide"] = 1.25
if "Trunk" not in bpy.data.objects:
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=0.2, depth=5, location=(0, -0.3 - 0.2, 0.2))
    tr = bpy.context.active_object; tr.name = "Trunk"; tr.display_type = 'WIRE'
    m = bpy.data.materials.new("TrunkHoldout"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); h = nt.nodes.new("ShaderNodeHoldout"); nt.links.new(h.outputs[0], o.inputs[0])
    tr.data.materials.append(m); tr.hide_render = True
for name in ("run", "jump", "paw_swipe", "tail_flag", "tail_attack_front", "tail_attack_back", "tail_spin", "tail_flip_side", "tail_flip_front"):
    bpy.data.actions[name]["fur"] = 1.5                 # strands stir stronger in fast actions (read by render_sprites.py)
# how the game carries her while the clip plays in place: the strands lag against this motion (read by render_sprites.py)
bpy.data.actions["run"]["virt_vel"] = (0.0, -3.5, 0.0)
bpy.data.actions["climb"]["virt_vel"] = (0.0, 0.0, 1.0); bpy.data.actions["climb_hug"]["virt_vel"] = (0.0, 0.0, 1.0)
bpy.data.actions["jump"]["virt_z"] = [1.3 * math.sin(math.pi * min(1, max(0, (i - 9) / 21))) for i in range(36)]
arm.animation_data.action = bpy.data.actions["idle_test"]
for b in pb: b.location = (0, 0, 0); b.rotation_euler = (0, 0, 0); b.scale = (1, 1, 1)
scn.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=DST, compress=True); print("saved")
