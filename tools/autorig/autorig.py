# Auto-rig per modelli umanoidi statici in A-pose (Tripo): scheletro con nomi stile Mixamo (senza prefisso),
# pesi automatici tramite una copia "rimeshata" (manifold) e trasferimento, animazioni procedurali opzionali.
# Uso: python -I autorig.py -- in.fbx out.fbx target_tris mode(runner|main) preview_prefix
import bpy, sys, math, json
import numpy as np
from mathutils import Vector, Quaternion, Euler, Matrix

import os
_a = sys.argv[sys.argv.index("--") + 1:]
src, dst, target, mode, prev = _a[0], _a[1], int(_a[2]), _a[3], _a[4]
run_bvh = _a[5] if len(_a) > 5 else None  # corsa da motion capture (BVH CMU) al posto di quella procedurale
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
for o in list(bpy.context.scene.objects):
    if o is not mesh:
        bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh.name = "Body"
mesh.data.name = "Body"

# --- riduzione
tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
if tris > target:
    m = mesh.modifiers.new("dec", "DECIMATE"); m.ratio = target / tris; m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
bpy.ops.object.shade_smooth()

V = np.array([v.co[:] for v in mesh.data.vertices])
V[:, 2] -= V[:, 2].min()
for i, v in enumerate(mesh.data.vertices):
    v.co.z = V[i, 2]
H = V[:, 2].max()
X, Y, Z = V[:, 0], V[:, 1], V[:, 2]
FWD = Vector((0, -1, 0))  # il modello guarda -Y
print("H", H, "tris", sum(len(p.vertices) - 2 for p in mesh.data.polygons))

def band(z, w=0.01):
    return np.abs(Z - z * H) < w * H

def centroid(mask):
    if mask.sum() == 0:
        return None
    return V[mask].mean(axis=0)

# --- inforcatura: salendo, primo livello con vertici vicino a x=0
crotch = None
for z in np.arange(0.15, 0.65, 0.004):
    if (band(z, 0.004) & (np.abs(X) < 0.012 * H)).sum() > 0:
        crotch = z; break
print("crotch", crotch)

def side(s):
    return s * X > 0.008 * H

joints = {}
for s, pre in ((1, "Left"), (-1, "Right")):
    # gambe
    hipz = crotch + 0.045
    thigh = centroid(side(s) & band(crotch - 0.05, 0.015) & (s * X < 0.2 * H))
    shin = centroid(side(s) & band(0.10, 0.012) & (s * X < 0.2 * H))
    hip = Vector((thigh[0], thigh[1], hipz * H))
    ankle = Vector((shin[0], shin[1], 0.055 * H))
    kz = (hipz + 0.055) * 0.5
    kc = centroid(side(s) & band(kz, 0.012) & (s * X < 0.2 * H))
    knee = Vector((kc[0], kc[1] - 0.012 * H, kz * H))
    foot = side(s) & (Z < 0.06 * H) & (s * X < 0.2 * H)
    toe_y, heel_y = Y[foot].min(), Y[foot].max()
    toebase = Vector((ankle.x, heel_y + (toe_y - heel_y) * 0.72, 0.022 * H))
    toetip = Vector((ankle.x, toe_y, 0.022 * H))
    joints[pre + "UpLeg"] = hip; joints[pre + "Leg"] = knee; joints[pre + "Foot"] = ankle
    joints[pre + "ToeBase"] = toebase; joints[pre + "ToeEnd"] = toetip

    # braccia: spalla poco dentro il bordo esterno all'altezza delle spalle; mano = punto più laterale tra anca e petto
    zs = 0.815
    edge = (s * X[band(zs, 0.012) & side(s)]).max()
    sh = Vector((s * max(0.07 * H, edge - 0.035 * H), centroid(band(zs, 0.012) & (np.abs(X) < 0.08 * H))[1], zs * H))
    hm = side(s) & (Z > 0.3 * H) & (Z < 0.62 * H)
    tip = Vector(V[hm][np.argmax(s * X[hm])])
    L = (tip - sh).length
    d = (tip - sh).normalized()

    def section(p, dd, r):
        rel = V - np.array(p[:])
        along = rel @ np.array(dd[:])
        perp = np.linalg.norm(rel - np.outer(along, dd[:]), axis=1)
        msk = side(s) & (np.abs(along) < 0.012 * H) & (perp < r * H)
        c = centroid(msk)
        return Vector(c) if c is not None else p

    elbow = section(sh + d * L * 0.42, d, 0.04)
    d2 = (tip - elbow).normalized()
    wrist = section(elbow + d2 * L * 0.335, d2, 0.035)
    # punta delle dita: il punto più lontano oltre il polso, vicino alla linea polso-mano
    d3 = (tip - wrist).normalized()
    rel = V - np.array(wrist[:]); along = rel @ np.array(d3[:])
    perp = np.linalg.norm(rel - np.outer(along, d3[:]), axis=1)
    fm = side(s) & (along > 0) & (perp < 0.06 * H)
    if fm.sum() > 0:
        tip = Vector(V[fm][np.argmax(along[fm])])
    hand_dir = (tip - wrist).normalized()
    hl = (tip - wrist).length
    joints[pre + "Arm"] = sh; joints[pre + "ForeArm"] = elbow; joints[pre + "Hand"] = wrist
    joints[pre + "HandEnd"] = tip
    side_vec = Vector((s, 0, 0))
    for f, off in (("Index", 0.012), ("Middle", -0.002)):
        base = wrist + hand_dir * hl * 0.42 + FWD * off * H
        joints[f"{pre}Hand{f}1"] = base
        joints[f"{pre}Hand{f}2"] = base + hand_dir * hl * 0.22
        joints[f"{pre}Hand{f}3"] = base + hand_dir * hl * 0.40
        joints[f"{pre}Hand{f}End"] = base + hand_dir * hl * 0.55
    tdir = (hand_dir * 0.45 + FWD * 0.9).normalized()
    tb = wrist + hand_dir * hl * 0.15 + FWD * 0.012 * H
    joints[f"{pre}HandThumb1"] = tb
    joints[f"{pre}HandThumb2"] = tb + tdir * hl * 0.22
    joints[f"{pre}HandThumb3"] = tb + tdir * hl * 0.40
    joints[f"{pre}HandThumbEnd"] = tb + tdir * hl * 0.55
    joints[pre + "Shoulder"] = Vector((s * 0.025 * H, sh.y, (zs - 0.01) * H))

def torso_y(z):
    c = centroid(band(z, 0.012) & (np.abs(X) < 0.08 * H))
    return c[1]

hz = crotch + 0.045
joints["Hips"] = Vector((0, torso_y(hz), hz * H))
neckz = 0.84
for name, f in (("Spine", 0.22), ("Spine1", 0.48), ("Spine2", 0.74)):
    z = hz + (neckz - hz) * f
    joints[name] = Vector((0, torso_y(z), z * H))
headz = neckz + (1.0 - neckz) * 0.30
head_c = centroid(Z > headz * H)
joints["Head"] = Vector((head_c[0] * 0, head_c[1], headz * H))
joints["HeadTop"] = Vector((0, head_c[1], H))
joints["Neck"] = Vector((0, (joints["Spine2"].y + joints["Head"].y) * 0.5, neckz * H))
for k in ("LeftShoulder", "RightShoulder"):
    joints[k].y = joints["Spine2"].y
print("JOINTS", {k: tuple(round(c / H, 3) for c in v) for k, v in joints.items() if not k.endswith("End") and "Hand" not in k[5:]})

# --- armatura
arm_data = bpy.data.armatures.new("Armature")
rig = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
UP = Vector((0, 0, 1))

def bone(name, head, tail, parent=None, roll_up=False):
    b = eb.new(name); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]; b.use_connect = False
    b.align_roll(UP if roll_up else FWD)
    return b

J = joints
bone("Hips", J["Hips"], J["Spine"])
bone("Spine", J["Spine"], J["Spine1"], "Hips")
bone("Spine1", J["Spine1"], J["Spine2"], "Spine")
bone("Spine2", J["Spine2"], J["Neck"], "Spine1")
bone("Neck", J["Neck"], J["Head"], "Spine2")
bone("Head", J["Head"], J["HeadTop"], "Neck")
for p in ("Left", "Right"):
    bone(p + "Shoulder", J[p + "Shoulder"], J[p + "Arm"], "Spine2")
    bone(p + "Arm", J[p + "Arm"], J[p + "ForeArm"], p + "Shoulder")
    bone(p + "ForeArm", J[p + "ForeArm"], J[p + "Hand"], p + "Arm")
    bone(p + "Hand", J[p + "Hand"], J[p + "HandMiddle1"], p + "ForeArm")
    for f in ("Index", "Middle", "Thumb"):
        bone(f"{p}Hand{f}1", J[f"{p}Hand{f}1"], J[f"{p}Hand{f}2"], p + "Hand", roll_up=(f == "Thumb"))
        bone(f"{p}Hand{f}2", J[f"{p}Hand{f}2"], J[f"{p}Hand{f}3"], f"{p}Hand{f}1", roll_up=(f == "Thumb"))
        bone(f"{p}Hand{f}3", J[f"{p}Hand{f}3"], J[f"{p}Hand{f}End"], f"{p}Hand{f}2", roll_up=(f == "Thumb"))
    bone(p + "UpLeg", J[p + "UpLeg"], J[p + "Leg"], "Hips")
    bone(p + "Leg", J[p + "Leg"], J[p + "Foot"], p + "UpLeg")
    bone(p + "Foot", J[p + "Foot"], J[p + "ToeBase"], p + "Leg", roll_up=True)
    bone(p + "ToeBase", J[p + "ToeBase"], J[p + "ToeEnd"], p + "Foot", roll_up=True)
bpy.ops.object.mode_set(mode='OBJECT')

# --- pesi geometrici: distanza dai segmenti delle ossa (1/d^4), poi lisciatura lungo i lati della mesh
bones = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in arm_data.bones]
P = np.array([v.co[:] for v in mesh.data.vertices])
D = np.zeros((len(P), len(bones)))
for j, (n, h, t) in enumerate(bones):
    h = np.array(h[:]); t = np.array(t[:]); ab = t - h
    u = np.clip(((P - h) @ ab) / max(1e-9, ab @ ab), 0, 1)
    D[:, j] = np.linalg.norm(P - (h + np.outer(u, ab)), axis=1)
W = 1.0 / (D / H + 0.004) ** 4
W[D > D.min(axis=1, keepdims=True) * 2.2 + 0.01 * H] = 0
W /= W.sum(axis=1, keepdims=True)
edges = np.array([e.vertices[:] for e in mesh.data.edges])
deg = np.bincount(edges.ravel(), minlength=len(P)).astype(float)
for _ in range(4):
    acc = np.zeros_like(W)
    np.add.at(acc, edges[:, 0], W[edges[:, 1]]); np.add.at(acc, edges[:, 1], W[edges[:, 0]])
    nb = acc / np.maximum(deg, 1)[:, None]
    W = 0.5 * W + 0.5 * np.where(deg[:, None] > 0, nb, W)
idx = np.argsort(-W, axis=1)[:, :4]
groups = {n: mesh.vertex_groups.new(name=n) for n, _, _ in bones}
for i in range(len(P)):
    ws = W[i, idx[i]]; ws = ws / ws.sum()
    for j, w in zip(idx[i], ws):
        if w > 0.01:
            groups[bones[j][0]].add([i], float(w), 'REPLACE')
print("weights done", len(P))
mesh.parent = rig
am = mesh.modifiers.new("Armature", "ARMATURE"); am.object = rig

# --- animazioni (solo runner)
pb = rig.pose.bones
for p in pb:
    p.rotation_mode = 'QUATERNION'
bl = {b.name: b.length for b in arm_data.bones}
L1 = bl["LeftUpLeg"]; L2 = bl["LeftLeg"]
FPS = 30
bpy.context.scene.render.fps = FPS

def leg_len(hip, knee):
    a = math.radians(hip); b = math.radians(-knee)
    return L1 * math.cos(a) + L2 * math.cos(a - b)

def q(x=0, y=0, z=0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'YXZ').to_quaternion()

def apply(pose, hips_loc):
    for p in pb:
        rx, ry, rz = pose.get(p.name, (0, 0, 0))
        if p.name.startswith("Right"):
            ry, rz = -ry, -rz
        p.rotation_quaternion = q(rx, ry, rz)
        p.location = (0, 0, 0)
    pb["Hips"].location = hips_loc

def add(pose, name, x=0, y=0, z=0, both=False):
    for n in ((f"Left{name}", f"Right{name}") if both else (name,)):
        a = pose.get(n, (0, 0, 0)); pose[n] = (a[0] + x, a[1] + y, a[2] + z)

def ready(pose, claw=-22):
    add(pose, "Spine", 7); add(pose, "Spine1", 6); add(pose, "Spine2", 5); add(pose, "Neck", -8); add(pose, "Head", -7)
    add(pose, "Arm", 15, 0, -24, both=True); add(pose, "ForeArm", 32, both=True)
    for f in ("Index", "Middle"):
        for k in (1, 2, 3):
            add(pose, f"Hand{f}{k}", 0, 0, claw, both=True)
    return pose

def legs(pose, lh, lk, rh, rk, lf=8, rf=8):
    add(pose, "LeftUpLeg", lh); add(pose, "LeftLeg", lk); add(pose, "LeftFoot", lf)
    add(pose, "RightUpLeg", rh); add(pose, "RightLeg", rk); add(pose, "RightFoot", rf)
    return (L1 + L2) - max(leg_len(lh, lk), leg_len(rh, rk))

def smooth(a, b, t):
    t = max(0.0, min(1.0, t)); t = t * t * (3 - 2 * t)
    return a + (b - a) * t

def keyed(name, frames, fn, loop):
    act = bpy.data.actions.new(name)
    rig.animation_data_create(); rig.animation_data.action = act
    for f in range(frames + 1):
        pose, loc = fn(f / frames)
        apply(pose, loc)
        for p in pb:
            p.keyframe_insert("rotation_quaternion", frame=f)
        pb["Hips"].keyframe_insert("location", frame=f)
    act.use_fake_user = True
    print("action", name, frames)
    return act

def idle(t):
    pose = ready({}, claw=-20 + 8 * math.sin(2 * math.pi * t))
    add(pose, "Spine1", 2 * math.sin(2 * math.pi * t)); add(pose, "Spine2", 2 * math.sin(2 * math.pi * t))
    twitch = math.exp(-((t - 0.62) / 0.03) ** 2)
    add(pose, "Head", 0, 10 * math.sin(2 * math.pi * t) + 22 * twitch, 5 * math.sin(4 * math.pi * t))
    add(pose, "Arm", 4 * math.sin(2 * math.pi * t + 1), both=True)
    drop = legs(pose, 12, -24, 12, -24)
    return pose, (0, -drop - 0.006 * H * (1 + math.sin(2 * math.pi * t)), 0)

def run(t):
    w = 2 * math.pi * t
    pose = ready({}, claw=-28)
    lh, rh = 15 + 42 * math.sin(w), 15 + 42 * math.sin(w + math.pi)
    lk = -(18 + 95 * max(0.0, math.cos(w)) ** 1.3); rk = -(18 + 95 * max(0.0, math.cos(w + math.pi)) ** 1.3)
    drop = legs(pose, lh, lk, rh, rk, 18 * math.sin(w), 18 * math.sin(w + math.pi))
    add(pose, "Spine", 9); add(pose, "Spine1", 7, 9 * math.sin(w)); add(pose, "Spine2", 5); add(pose, "Hips", 0, -6 * math.sin(w))
    add(pose, "Neck", -10); add(pose, "Head", -10)
    add(pose, "LeftArm", 22 + 50 * math.sin(w + math.pi), 0, -6); add(pose, "RightArm", 22 + 50 * math.sin(w), 0, -6)
    add(pose, "LeftForeArm", 22 + 25 * math.sin(w + math.pi)); add(pose, "RightForeArm", 22 + 25 * math.sin(w))
    bob = 0.018 * H * math.cos(4 * math.pi * (t - 0.08))
    return pose, (0, -drop + bob, 0)

ATT = [  # (t, spine x, spine1 y, rarm x, rarm z, rfore x, larm x, lfore x, fwd, lh, lk, rh, rk)
    (0.00, 0, 0, 0, 0, 0, 0, 0, 0.00, 12, -24, 12, -24),
    (0.20, 2, -22, 100, 30, 70, 35, 10, -0.01, 12, -24, 12, -24),
    (0.38, 20, 24, 10, -10, 12, 60, 28, 0.07, 30, -38, -8, -18),
    (0.55, 15, 18, -12, -22, 20, 45, 30, 0.06, 28, -36, -6, -20),
    (1.00, 0, 0, 0, 0, 0, 0, 0, 0.00, 12, -24, 12, -24),
]

def attack(t):
    for i in range(len(ATT) - 1):
        if ATT[i][0] <= t <= ATT[i + 1][0]:
            a, b = ATT[i], ATT[i + 1]; u = (t - a[0]) / (b[0] - a[0]); break
    v = [smooth(a[k], b[k], u) for k in range(len(a))]
    pose = ready({}, claw=-15)
    add(pose, "Spine", v[1]); add(pose, "Spine1", 0, v[2])
    add(pose, "RightArm", v[3], 0, v[4]); add(pose, "RightForeArm", v[5])
    add(pose, "LeftArm", v[6]); add(pose, "LeftForeArm", v[7])
    drop = legs(pose, v[9], v[10], v[11], v[12])
    return pose, (0, -drop - abs(v[8]) * 0.4, v[8] * H)

DEATH = [  # (t, hips x, hips y-loc, hips z-loc, spine x, head x, head y, arm x, arm z, hip, knee)
    (0.00, 0, 0.00, 0.00, 0, 0, 0, 0, 0, 12, -24),
    (0.17, -6, 0.00, -0.03, -14, -25, 0, -10, 28, 8, -20),
    (0.40, 10, -0.20, 0.02, 22, 10, 10, 40, 10, 45, -95),
    (0.68, 58, -0.33, 0.12, 12, -5, 35, 75, 20, 25, -60),
    (1.00, 86, -0.43, 0.16, 2, -18, 62, 25, 40, -4, -8),
]

def death(t):
    for i in range(len(DEATH) - 1):
        if DEATH[i][0] <= t <= DEATH[i + 1][0]:
            a, b = DEATH[i], DEATH[i + 1]; u = (t - a[0]) / (b[0] - a[0]); break
    v = [smooth(a[k], b[k], u) for k in range(len(a))]
    pose = ready({}, claw=-35)
    add(pose, "Hips", v[1]); add(pose, "Spine", v[4]); add(pose, "Head", v[5], v[6])
    add(pose, "Arm", v[7], 0, v[8], both=True)
    add(pose, "UpLeg", v[9], both=True); add(pose, "Leg", v[10], both=True)
    return pose, (0, v[2] * H, v[3] * H)


# --- corsa da motion capture (BVH CMU): retarget per direzione delle ossa, loop senza cuciture, sul posto
def mocap_run(path, extra):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from bvh import BVH
    b = BVH(path); P, G = b.fk(); I = b.idx
    P0, G0 = P[0].copy(), G[0].copy()  # fotogramma 0 = T-pose che guarda davanti
    ground = min(P0[I["LeftFoot"], 2], P0[I["RightFoot"], 2])
    k = (J["Hips"].z - J["LeftFoot"].z) / (P0[I["Hips"], 2] - ground)
    # direzione di corsa -> davanti (-Y), solo per i fotogrammi di moto
    xy = P[1:, I["Hips"], :2]; d = xy[-1] - xy[0]
    ang = math.atan2(-1.0, 0.0) - math.atan2(d[1], d[0])
    c, s_ = math.cos(ang), math.sin(ang); Rz = np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1]])
    P[1:] = P[1:] @ Rz.T; G[1:] = np.einsum("ab,fjbc->fjac", Rz, G[1:])
    nf = b.nf
    # loop: due pose simili a distanza di un ciclo
    feat_j = [I[n] for n in ("LeftFoot", "RightFoot", "LeftHand", "RightHand", "LeftLeg", "RightLeg")]
    F = (P[:, feat_j] - P[:, I["Hips"]][:, None]).reshape(nf, -1)
    V = np.vstack([np.zeros((1, F.shape[1])), np.diff(F, axis=0)])
    B = 16; best = None
    for L in range(50, 100):
        for st in range(2, nf - L - B):
            cost = np.linalg.norm(F[st] - F[st + L]) + 4.0 * np.linalg.norm(V[st] - V[st + L])
            if best is None or cost < best[0]: best = (cost, st, L)
    _, st, L = best
    # velocità di corsa (orizzontale) nel tratto scelto, in unità del modello al secondo
    hip = P[:, I["Hips"]]
    speed = np.linalg.norm(hip[st + L, :2] - hip[st, :2]) / (L * b.dt) * k
    print("MOCAP loop start", st, "len", L, "frames =", round(L * b.dt, 3), "s; speed", round(speed, 3), "model-units/s")

    rest = {bn.name: np.array(bn.matrix_local.to_3x3()) for bn in arm_data.bones}
    rdir = {bn.name: np.array((bn.tail_local - bn.head_local).normalized()) for bn in arm_data.bones}
    FWDv = np.array([0, -1, 0.]); UPv = np.array([0, 0, 1.])
    MAP = {"Hips": ("d", "Hips"), "Spine": ("d", "LowerBack"), "Spine1": ("d", "Spine"), "Spine2": ("d", "Spine1"),
           "Neck": ("d", "Neck"), "Head": ("d", "Head")}
    for side in ("Left", "Right"):
        MAP[side + "Shoulder"] = ("v", side + "Shoulder", side + "Arm", FWDv)
        MAP[side + "Arm"] = ("v", side + "Arm", side + "ForeArm", FWDv)
        MAP[side + "ForeArm"] = ("v", side + "ForeArm", side + "Hand", FWDv)
        MAP[side + "Hand"] = ("d", side + "Hand")
        MAP[side + "UpLeg"] = ("v", side + "UpLeg", side + "Leg", FWDv)
        MAP[side + "Leg"] = ("v", side + "Leg", side + "Foot", FWDv)
        MAP[side + "Foot"] = ("v", side + "Foot", side + "ToeBase", UPv)
        MAP[side + "ToeBase"] = ("d", side + "ToeBase")

    def frame(p, s2):
        y = p / np.linalg.norm(p); z = s2 - (s2 @ y) * y; z /= np.linalg.norm(z); x = np.cross(y, z)
        return np.stack([x, y, z], axis=1)

    trend = np.polyfit(np.arange(nf), hip[:, 0], 1), np.polyfit(np.arange(nf), hip[:, 1], 1)
    def retarget(f):
        Wp = {}; out = {}
        for bn in arm_data.bones:
            n = bn.name; Rb = rest[n]
            m = MAP.get(n)
            if m is None:
                Wd = (Wp[bn.parent.name] @ rest[bn.parent.name].T @ Rb)  # dita: seguono la mano
            elif m[0] == "d":
                j = I[m[1]]; Wd = G[f, j] @ G0[j].T @ Rb
            else:
                j, cj, sec = I[m[1]], I[m[2]], m[3]
                ps = P[f, cj] - P[f, j]; ss = G[f, j] @ G0[j].T @ sec
                Wd = frame(ps, ss) @ frame(rdir[n], sec).T @ Rb
            if bn.parent is None: basis = Rb.T @ Wd
            else: basis = Rb.T @ rest[bn.parent.name] @ Wp[bn.parent.name].T @ Wd
            Wp[n] = Wd; out[n] = Matrix(basis.tolist()).to_quaternion()
        sway = np.array([hip[f, 0] - np.polyval(trend[0], f), hip[f, 1] - np.polyval(trend[1], f), hip[f, 2] - P0[I["Hips"], 2]]) * k
        out["__loc"] = Vector((Rb_h := rest["Hips"]).T @ sway)
        return out

    def sample(t):  # t in fotogrammi sorgente (frazionario), dentro [st, st+L] con dissolvenza della cucitura
        def at(x):
            a = int(math.floor(x)); u = x - a
            pa, pb = retarget(a), retarget(min(a + 1, nf - 1))
            return {n: (pa[n].slerp(pb[n], u) if n != "__loc" else pa[n].lerp(pb[n], u)) for n in pa}
        rel = t - st
        cur = at(t)
        if rel < B:
            w = rel / B; w = w * w * (3 - 2 * w)
            nxt = at(t + L)
            cur = {n: (nxt[n].slerp(cur[n], w) if n != "__loc" else nxt[n].lerp(cur[n], w)) for n in cur}
        return cur

    n_out = max(8, round(L * b.dt * FPS))
    act = bpy.data.actions.new("Run")
    rig.animation_data_create(); rig.animation_data.action = act
    claw = q(0, 0, -26)
    for kf in range(n_out + 1):
        pose = sample(st + L * (kf % n_out) / n_out)
        for p in pb:
            qq = pose.get(p.name, Quaternion())
            if "HandIndex" in p.name or "HandMiddle" in p.name:
                qq = q(0, 0, 26 if p.name.startswith("Right") else -26)
            key = p.name.replace("Left", "").replace("Right", "") if p.name.startswith(("Left", "Right")) else p.name
            if key in extra:
                ex, ey, ez = extra[key]
                qq = qq @ (q(ex, -ey, -ez) if p.name.startswith("Right") else q(ex, ey, ez))
            p.rotation_quaternion = qq; p.location = (0, 0, 0)
            p.keyframe_insert("rotation_quaternion", frame=kf)
        pb["Hips"].location = pose["__loc"]
        pb["Hips"].keyframe_insert("location", frame=kf)
    act.use_fake_user = True
    if os.environ.get("RIGDEBUG"):
        bpy.context.scene.frame_set(0); bpy.context.view_layer.update()
        hp = rig.pose.bones["Hips"].head
        src = st
        for n in ("LeftArm", "LeftForeArm", "LeftHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "Head"):
            tv = (rig.pose.bones[n].head - hp)
            sv = (P[src, I[n]] - P[src, I["Hips"]]) * k
            print("DBG", n, "tgt", tuple(round(x, 3) for x in tv), "src", tuple(round(x, 3) for x in sv))
    json.dump({"run_speed_model_units": speed, "loop_seconds": L * b.dt, "frames": n_out}, open(prev + "_run.json", "w"))
    print("action Run (mocap)", n_out)


# --- animazioni da file FBX (Mixamo, scheletro "mixamorig:", "Without Skin"): retarget sulle nostre ossa
def fbx_motion(path):
    """Legge un FBX di animazione: posizioni (nf, nj, 3), rotazioni globali (nf, nj, 3, 3), riposo, indici, fps."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == 'ARMATURE'][0]
    act = src.animation_data.action
    f0, f1 = [int(round(x)) for x in act.frame_range]
    names = [b.name.split(":")[-1] for b in src.data.bones]
    idx = {n: i for i, n in enumerate(names)}
    def ortho(m):
        m = np.array(m); u, _, vt = np.linalg.svd(m); return u @ vt
    mw = src.matrix_world
    P0 = np.array([(mw @ b.head_local)[:] for b in src.data.bones])
    G0 = np.array([ortho((mw @ b.matrix_local).to_3x3()) for b in src.data.bones])
    Ps, Gs = [], []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        Ps.append([(mw @ pb.head)[:] for pb in src.pose.bones])
        Gs.append([ortho((mw @ pb.matrix).to_3x3()) for pb in src.pose.bones])
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.actions.remove(act)
    bpy.context.scene.frame_set(0)
    return np.array(Ps), np.array(Gs), P0, G0, idx, fps

RIG_REST = None
def align_motion(P, G, P0, G0, I):
    """Ruota la sorgente perché guardi -Y (dal riposo: piede -> punta)."""
    fw = np.zeros(3)
    for sd in ("Left", "Right"):
        fw += P0[I[sd + "ToeBase"]] - P0[I[sd + "Foot"]]
    fw[2] = 0; fw /= np.linalg.norm(fw)
    ang = math.atan2(-1.0, 0.0) - math.atan2(fw[1], fw[0])
    c, s_ = math.cos(ang), math.sin(ang); Rz = np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1]])
    return P @ Rz.T, np.einsum("ab,fjbc->fjac", Rz, G), P0 @ Rz.T, np.einsum("ab,jbc->jac", Rz, G0)

UPPER = {"Spine1", "Spine2", "Neck", "Head"} | {f"{sd}{b}" for sd in ("Left", "Right") for b in
         ("Shoulder", "Arm", "ForeArm", "Hand", "HandIndex1", "HandIndex2", "HandIndex3", "HandMiddle1", "HandMiddle2",
          "HandMiddle3", "HandThumb1", "HandThumb2", "HandThumb3")}

def retarget_clip(name, path, hips_mode, extra=None, upper=None):
    """hips_mode: 'loop' (toglie la deriva), 'pin' (bacino fermo in orizzontale), 'keep' (tiene lo spostamento).
    upper: secondo FBX in loop da cui prendere busto alto, testa e braccia (fase normalizzata sul ciclo delle gambe)."""
    P, G, P0, G0, I, fps = fbx_motion(path)
    nf = len(P)
    U = None
    if upper:
        Pu, Gu, P0u, G0u, Iu, _ = fbx_motion(upper)
        Pu, Gu, P0u, G0u = align_motion(Pu, Gu, P0u, G0u, Iu)
        U = (Pu, Gu, P0u, G0u, Iu)
    # direzione "davanti" della sorgente dal riposo (piede -> punta) e rotazione verso -Y
    fw = np.zeros(3)
    for sd in ("Left", "Right"):
        fw += P0[I[sd + "ToeBase"]] - P0[I[sd + "Foot"]]
    fw[2] = 0; fw /= np.linalg.norm(fw)
    ang = math.atan2(-1.0, 0.0) - math.atan2(fw[1], fw[0])
    c, s_ = math.cos(ang), math.sin(ang); Rz = np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1]])
    P = P @ Rz.T; P0 = P0 @ Rz.T
    G = np.einsum("ab,fjbc->fjac", Rz, G); G0 = np.einsum("ab,jbc->jac", Rz, G0)
    ground = min(P0[I["LeftFoot"], 2], P0[I["RightFoot"], 2])
    k = (J["Hips"].z - J["LeftFoot"].z) / (P0[I["Hips"], 2] - ground)
    rest = {bn.name: np.array(bn.matrix_local.to_3x3()) for bn in arm_data.bones}
    rdir = {bn.name: np.array((bn.tail_local - bn.head_local).normalized()) for bn in arm_data.bones}
    FWDv = np.array([0, -1, 0.]); UPv = np.array([0, 0, 1.])
    MAP = {n: ("d", n) for n in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")}
    for sd in ("Left", "Right"):
        MAP[sd + "Shoulder"] = ("v", sd + "Shoulder", sd + "Arm", FWDv)
        MAP[sd + "Arm"] = ("v", sd + "Arm", sd + "ForeArm", FWDv)
        MAP[sd + "ForeArm"] = ("v", sd + "ForeArm", sd + "Hand", FWDv)
        MAP[sd + "Hand"] = ("d", sd + "Hand")
        MAP[sd + "UpLeg"] = ("v", sd + "UpLeg", sd + "Leg", FWDv)
        MAP[sd + "Leg"] = ("v", sd + "Leg", sd + "Foot", FWDv)
        MAP[sd + "Foot"] = ("v", sd + "Foot", sd + "ToeBase", UPv)
        MAP[sd + "ToeBase"] = ("d", sd + "ToeBase")
        for fg in ("Index", "Middle", "Thumb"):
            for kk in (1, 2, 3):
                if f"{sd}Hand{fg}{kk}" in I:
                    MAP[f"{sd}Hand{fg}{kk}"] = ("d", f"{sd}Hand{fg}{kk}")

    def frame(p, s2):
        y = p / np.linalg.norm(p); z = s2 - (s2 @ y) * y; z /= np.linalg.norm(z); x = np.cross(y, z)
        return np.stack([x, y, z], axis=1)

    hip = P[:, I["Hips"]]
    tx = np.polyfit(np.arange(nf), hip[:, 0], 1) if nf > 1 else None
    ty = np.polyfit(np.arange(nf), hip[:, 1], 1) if nf > 1 else None
    act = bpy.data.actions.new(name)
    rig.animation_data_create(); rig.animation_data.action = act
    step = fps / FPS
    n_out = max(1, int(round((nf - 1) / step)))
    for kf in range(n_out + 1):
        f = min(nf - 1, int(round(kf * step)))
        Wp = {}
        for bn in arm_data.bones:
            n = bn.name; Rb = rest[n]; m = MAP.get(n)
            if U is not None and n in UPPER:
                SP, SG, S0, SI = U[0], U[1], U[3], U[4]
                sf = int(round(f / max(1, nf - 1) * (len(SP) - 1))) % len(SP)
            else:
                SP, SG, S0, SI, sf = P, G, G0, I, f
            if m is None or m[1] not in SI:
                Wd = Wp[bn.parent.name] @ rest[bn.parent.name].T @ Rb
            elif m[0] == "d":
                j = SI[m[1]]; Wd = SG[sf, j] @ S0[j].T @ Rb
            else:
                j, cj, sec = SI[m[1]], SI[m[2]], m[3]
                Wd = frame(SP[sf, cj] - SP[sf, j], SG[sf, j] @ S0[j].T @ sec) @ frame(rdir[n], sec).T @ Rb
            basis = Rb.T @ Wd if bn.parent is None else Rb.T @ rest[bn.parent.name] @ Wp[bn.parent.name].T @ Wd
            Wp[n] = Wd
            pbn = pb[n]; qq = Matrix(basis.tolist()).to_quaternion()
            if extra and n.replace("Left", "").replace("Right", "") in extra:
                ex, ey, ez = extra[n.replace("Left", "").replace("Right", "")]
                qq = qq @ (q(ex, -ey, -ez) if n.startswith("Right") else q(ex, ey, ez))
            pbn.rotation_quaternion = qq; pbn.location = (0, 0, 0)
            pbn.keyframe_insert("rotation_quaternion", frame=kf)
        off = hip[f] - P0[I["Hips"]]
        if hips_mode == "loop":
            off[0] = hip[f, 0] - np.polyval(tx, f); off[1] = hip[f, 1] - np.polyval(ty, f)
        elif hips_mode == "pin":
            off[0] = hip[f, 0] - hip[0, 0]; off[1] = hip[f, 1] - hip[0, 1]
            off[:2] *= 0.25  # un accenno di affondo, ma il nemico resta dove lo mette il NavMesh
        pb["Hips"].location = Vector(rest["Hips"].T @ (off * k))
        pb["Hips"].keyframe_insert("location", frame=kf)
    act.use_fake_user = True
    speed = (np.linalg.norm(hip[-1, :2] - hip[0, :2]) / ((nf - 1) / fps) * k) if nf > 1 else 0
    print("action", name, "(fbx)", n_out, "frames, src speed", round(float(speed), 3), "model-units/s")
    return speed

CLIPS = json.load(open(run_bvh)) if (run_bvh and run_bvh.endswith(".json")) else None
if CLIPS:
    info = {}
    for c in CLIPS:
        info[c["name"]] = retarget_clip(c["name"], c["file"], c.get("hips", "loop"), c.get("extra"), c.get("upper"))
    json.dump(info, open(prev + "_clips.json", "w"))
    rig.animation_data.action = None
elif mode == "runner":
    keyed("Idle", 60, idle, True)
    if run_bvh and run_bvh.endswith(".bvh"):
        # Runner: un filo più curvo in avanti e testa che guarda avanti, artigli chiusi (vedi mocap_run)
        mocap_run(run_bvh, {"Spine": (5, 0, 0), "Spine1": (4, 0, 0), "Neck": (-4, 0, 0), "Head": (-4, 0, 0),
                            "Arm": (0, 0, 12), "ForeArm": (0, 0, 8)})
    else:
        keyed("Run", 18, run, True)
    keyed("Attack", 21, attack, False)
    keyed("Death", 36, death, False)
    rig.animation_data.action = None
apply({}, (0, 0, 0))

# --- export
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=dst, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    bake_anim=(mode == "runner" or bool(CLIPS)), bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, armature_nodetype='NULL',
    mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
json.dump({k: [c for c in v] for k, v in joints.items()}, open(prev + "_joints.json", "w"))

# --- anteprime: posa a riposo con le ossa, e alcuni fotogrammi delle animazioni
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'MATERIAL'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x = 500; sc.render.resolution_y = 600
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = H * 1.15; sc.world = bpy.data.worlds.new("w")

def shot(name, ang):
    a = math.radians(ang); d = 3 * H
    cam.location = (d * math.sin(a), -d * math.cos(a), H * 0.5); cam.rotation_euler = (math.radians(90), 0, a)
    sc.render.filepath = f"{prev}_{name}.png"; bpy.ops.render.render(write_still=True)

shot("rest_front", 0)
if mode == "runner":
    for act, frames in (("Run", (0, 3, 6, 9, 12, 15)), ("Attack", (6, 9)), ("Death", (12, 36)), ("Idle", (0,))):
        if act not in bpy.data.actions:
            continue
        rig.animation_data.action = bpy.data.actions[act]
        for f in frames:
            sc.frame_set(f); shot(f"{act}_{f}", 90)
else:
    # prova di deformazione: braccia giù, gamba avanti, busto girato
    pb["LeftArm"].rotation_quaternion = q(20, 0, -45); pb["RightArm"].rotation_quaternion = q(60, 0, -30)
    pb["RightForeArm"].rotation_quaternion = q(70); pb["LeftUpLeg"].rotation_quaternion = q(40)
    pb["LeftLeg"].rotation_quaternion = q(-60); pb["Spine1"].rotation_quaternion = q(0, 20, 0)
    bpy.context.view_layer.update(); shot("posed_front", 20); shot("posed_side", 90)
