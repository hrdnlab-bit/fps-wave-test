"""
Esporta il fucile sci-fi di M@xim (scifi_firearm.blend) come FBX per Unity: tre oggetti (SciRifle_Body, SciRifle_Trigger,
SciRifle_Mag) con trasformazioni applicate e una palette di pochi materiali (SR_*) che il builder ricrea in URP.
Uso: python -I export_scifi_rifle.py -- scifi_firearm.blend out.fbx
"""
import bpy, sys

a = sys.argv[sys.argv.index("--") + 1:]
blend_path, out_path = a[0], a[1]
bpy.ops.wm.open_mainfile(filepath=blend_path)
names = {'Cube': 'SciRifle_Body', 'Cube.002': 'SciRifle_Trigger', 'Cube.003': 'SciRifle_Mag'}
keep = [bpy.data.objects[n] for n in names]
for o in keep:
    if o.animation_data:
        o.animation_data_clear()
bpy.context.view_layer.update()
# Nel .blend il fucile è ruotato nella scena: si esprime tutto nel sistema locale del corpo (lì è allineato agli assi).
body_inv = bpy.data.objects['Cube'].matrix_world.inverted()
frames = {o.name: body_inv @ o.matrix_world for o in keep}
for o in keep:
    o.parent = None
    o.matrix_world = frames[o.name]
for o in list(bpy.context.scene.objects):
    if o not in keep:
        bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()
for o in keep:
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.select_set(False)
    o.name = names[o.name] if o.name in names else o.name
    o.data.name = o.name

# ---------------------------------------------------------------- mirino olografico
# Il "cannocchiale" originale è una scatola chiusa con uno schermino dietro: mirando si vede solo il retro.
# Si toglie (vertici del corpo sopra la slitta nel tratto del mirino) e si costruisce un olografico aperto:
# base, cornice con finestra, striscia luminosa e un vetro separato (SciRifle_Lens, il reticolo lo mette Unity).
import bmesh, mathutils
body = bpy.data.objects['SciRifle_Body']
bm = bmesh.new(); bm.from_mesh(body.data)
cut = [v for v in bm.verts if -0.01 <= v.co.y <= 1.71 and v.co.z > 1.125]
bmesh.ops.delete(bm, geom=cut, context='VERTS')

def mat_index(name, color, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0]
    b.inputs['Base Color'].default_value = color
    if 'Emission Strength' in b.inputs:
        b.inputs['Emission Strength'].default_value = strength
    if name not in [s.material.name for s in body.material_slots if s.material]:
        body.data.materials.append(m)
    return [s.material.name if s.material else '' for s in body.material_slots].index(name)

dark_mat = mat_index('holo_dark', (0.03, 0.03, 0.035, 1))       # -> SR_Dark (anima nera come il fucile)
shell_mat = mat_index('holo_shell', (0.63, 0.63, 0.63, 1))      # -> SR_White (gusci chiari come il fucile)
metal_mat = mat_index('holo_metal', (0.16, 0.16, 0.16, 1))      # -> SR_Gunmetal (cornice)
red_mat = mat_index('holo_red', (0.8, 0.12, 0.0, 1))            # -> SR_Red (righe come sul fucile)
glow_mat = mat_index('holo_glow', (1.0, 0.55, 0.08, 1), 5.0)    # -> SR_Glow

def finish(geom_verts, mi, bevel=0.007):
    faces = {f for v in geom_verts for f in v.link_faces}
    for f in faces:
        f.material_index = mi
    bmesh.ops.recalc_face_normals(bm, faces=list(faces))
    if bevel > 0:
        edges = list({e for f in faces for e in f.edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)

def prism_xz(pts, y0, y1, mi, bevel=0.007):
    """Prisma da un profilo (x, z) estruso lungo y (avanti-dietro)."""
    a = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    finish(a + b, mi, bevel)

def prism_yz(pts, x0, x1, mi, bevel=0.007):
    """Prisma da un profilo laterale (y, z) estruso lungo x."""
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    n = len(pts)
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    finish(a + b, mi, bevel)

def ring_xz(outer, inner, y0, y1, mi, bevel=0.006):
    """Cornice: anello fra due profili (x, z) con lo stesso numero di punti, estruso lungo y."""
    n = len(outer)
    oa = [bm.verts.new((x, y0, z)) for x, z in outer]; ob = [bm.verts.new((x, y1, z)) for x, z in outer]
    ia = [bm.verts.new((x, y0, z)) for x, z in inner]; ib = [bm.verts.new((x, y1, z)) for x, z in inner]
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((oa[k], oa[m], ia[m], ia[k]))      # faccia davanti
        bm.faces.new((ob[k], ib[k], ib[m], ob[m]))      # faccia dietro
        bm.faces.new((oa[k], ob[k], ob[m], oa[m]))      # esterno
        bm.faces.new((ia[k], ia[m], ib[m], ib[k]))      # interno
    finish(oa + ob + ia + ib, mi, bevel)

def octa(x, zb, zt, cb, ct):
    """Rettangolo con gli angoli smussati (cb in basso, ct in alto), in senso antiorario visto da dietro."""
    return [(-x + cb, zb), (x - cb, zb), (x, zb + cb), (x, zt - ct), (x - ct, zt), (-x + ct, zt), (-x, zt - ct), (-x, zb + cb)]

# misure in unità del modello (1 unità ~ 8,3 cm in gioco); la canna va verso -y, l'occhio sta verso +y
WX, ZB, ZT = 0.22, 1.275, 1.565  # finestra: mezza larghezza, basso, alto (~3,6 x 2,4 cm)
HY0, HY1 = 0.52, 0.94            # cappuccio (davanti, dietro)
# corpo: anima scura con il davanti inclinato come i pezzi del fucile, copre i resti del vecchio attacco
prism_yz([(0.08, 1.04), (1.64, 1.04), (1.64, 1.20), (1.50, 1.275), (0.42, 1.275), (0.16, 1.16)], -0.175, 0.175, dark_mat)
# gusci chiari sui fianchi, con il taglio inclinato davanti e lo scalino dietro (come il guscio del vecchio mirino)
for sx in (-1, 1):
    x0, x1 = (0.175, 0.205) if sx > 0 else (-0.205, -0.175)
    prism_yz([(0.26, 1.06), (1.22, 1.06), (1.36, 1.14), (1.36, 1.25), (0.50, 1.25), (0.30, 1.15)], x0, x1, shell_mat, 0.005)
    xr0, xr1 = (0.205, 0.211) if sx > 0 else (-0.211, -0.205)
    prism_yz([(0.62, 1.20), (0.98, 1.20), (0.98, 1.225), (0.62, 1.225)], xr0, xr1, red_mat, 0.0)      # riga rossa
    prism_yz([(1.02, 1.20), (1.10, 1.20), (1.10, 1.225), (1.02, 1.225)], xr0, xr1, red_mat, 0.0)      # tacca rossa
# cappuccio: cornice scura a otto lati + calotta chiara sopra gli smussi
outer = octa(WX + 0.045, ZB - 0.03, ZT + 0.05, 0.02, 0.09)
inner = octa(WX, ZB, ZT, 0.01, 0.06)
ring_xz(outer, inner, HY0, HY1, metal_mat)
cap = [(-(WX + 0.06), ZT - 0.05), (-(WX + 0.06) + 0.10, ZT + 0.065), ((WX + 0.06) - 0.10, ZT + 0.065), ((WX + 0.06), ZT - 0.05),
       ((WX + 0.045), ZT - 0.04), ((WX + 0.045) - 0.09, ZT + 0.05), (-(WX + 0.045) + 0.09, ZT + 0.05), (-(WX + 0.045), ZT - 0.04)]
prism_xz(cap, HY0 - 0.03, HY1 + 0.02, shell_mat, 0.005)
for sx in (-1, 1):   # righe rosse sui fianchi del cappuccio
    xa = sx * (WX + 0.046); xb = sx * (WX + 0.052)
    prism_yz([(HY0 + 0.05, 1.36), (HY1 - 0.05, 1.36), (HY1 - 0.05, 1.38), (HY0 + 0.05, 1.38)], min(xa, xb), max(xa, xb), red_mat, 0.0)
# emettitore dietro la finestra (luce ambra verso il vetro) e pulsanti sul fianco destro
prism_yz([(0.98, 1.275), (1.10, 1.275), (1.10, 1.30), (0.98, 1.30)], -0.07, 0.07, dark_mat, 0.004)
prism_xz([(-0.05, 1.30), (0.05, 1.30), (0.05, 1.315), (-0.05, 1.315)], 0.99, 1.005, glow_mat, 0.0)
for yb in (1.14, 1.24):
    prism_yz([(yb, 1.10), (yb + 0.07, 1.10), (yb + 0.07, 1.15), (yb, 1.15)], 0.205, 0.225, dark_mat, 0.004)
# morsetto della slitta sul fianco destro (manopola)
knob = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.055, radius2=0.055, depth=0.06)
for v in knob['verts']:
    v.co = mathutils.Vector((v.co.z + 0.235, v.co.x + 0.75, v.co.y + 1.08))
finish(knob['verts'], dark_mat, 0.0)
bm.to_mesh(body.data); bm.free()

def classify(m):
    bs = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'] if m.node_tree else []
    if not bs:
        return 'SR_Dark'
    b = bs[0]
    col = b.inputs['Base Color'].default_value
    es = b.inputs['Emission Strength'].default_value if 'Emission Strength' in b.inputs else 0
    if es > 0.5:
        return 'SR_Glow'
    r, g, bl = col[0], col[1], col[2]
    if r > 0.4 and g < 0.2:
        return 'SR_Red'
    if r > 0.6 and g > 0.3 and bl < 0.15:
        return 'SR_Brass'
    lum = 0.3 * r + 0.59 * g + 0.11 * bl
    if lum > 0.6: return 'SR_White'
    if lum > 0.3: return 'SR_Grey'
    if lum > 0.12: return 'SR_Gunmetal'
    return 'SR_Dark'

palette = {}
for o in keep:
    for slot in o.material_slots:
        if slot.material is None:
            continue
        n = classify(slot.material)
        if n not in palette:
            palette[n] = bpy.data.materials.new(n)
        slot.material = palette[n]
    # slot doppi con lo stesso materiale -> un solo slot (meno submesh = meno draw call)
    first = {}
    remap = []
    for i, slot in enumerate(o.material_slots):
        n = slot.material.name if slot.material else 'SR_Dark'
        first.setdefault(n, i)
        remap.append(first[n])
    for poly in o.data.polygons:
        poly.material_index = remap[poly.material_index]
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.material_slot_remove_unused()
    o.select_set(False)
print("PALETTE", sorted(palette))

# Vetro del mirino: oggetto a parte con il suo materiale (Unity ci mette il reticolo luminoso).
lm = bpy.data.meshes.new('SciRifle_Lens')
LY = 0.70
win = octa(WX - 0.002, ZB + 0.002, ZT - 0.002, 0.01, 0.06)
lm.from_pydata([(x, LY, z) for x, z in win], [], [list(range(len(win)))])
uv = lm.uv_layers.new(name='UVMap')
cz = 0.5 * (ZB + ZT); half = WX   # UV quadrate centrate sul vetro: il reticolo resta rotondo
for li, loop in enumerate(lm.loops):
    x, z = win[loop.vertex_index]
    uv.data[li].uv = (0.5 + 0.5 * x / half, 0.5 + 0.5 * (z - cz) / half)
lens = bpy.data.objects.new('SciRifle_Lens', lm)
bpy.context.scene.collection.objects.link(lens)
lens.data.materials.append(bpy.data.materials.new('SR_Lens'))
keep.append(lens)
for o in keep:
    print(o.name, "dims", tuple(round(x, 3) for x in o.dimensions), "slots", [s.material.name for s in o.material_slots])
bpy.ops.export_scene.fbx(filepath=out_path, object_types={'MESH'}, use_selection=False, apply_scale_options='FBX_SCALE_UNITS',
                         bake_anim=False, path_mode='STRIP', mesh_smooth_type='FACE')
print("EXPORTED", out_path)
