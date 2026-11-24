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
import bmesh
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

frame_mat = mat_index('holo_frame', (0.2, 0.2, 0.21, 1))
dark_mat = mat_index('holo_dark', (0.03, 0.03, 0.035, 1))
glow_mat = mat_index('holo_glow', (1.0, 0.55, 0.08, 1), 5.0)

def box(x0, x1, y0, y1, z0, z1, mi):
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r['verts']:
        v.co.x = x0 if v.co.x < 0 else x1
        v.co.y = y0 if v.co.y < 0 else y1
        v.co.z = z0 if v.co.z < 0 else z1
    faces = {f for v in r['verts'] for f in v.link_faces}
    for f in faces:
        f.material_index = mi

# misure in unità del modello (fucile lungo ~10,7: 1 unità ~ 9 cm in gioco)
Y0, Y1 = 0.50, 0.96            # profondità del cappuccio
WX, ZB, ZT = 0.27, 1.205, 1.615  # finestra: mezza larghezza, basso, alto (~4,5 x 3,4 cm in gioco)
T = 0.045                        # spessore della cornice
box(-0.27, 0.27, 0.30, 1.22, 1.10, 1.16, dark_mat)           # base sulla slitta
box(-0.16, 0.16, 0.36, 1.16, 1.16, ZB, frame_mat)             # corpo dell'emettitore
box(-WX - T, -WX, Y0, Y1, ZB - T, ZT + T, frame_mat)          # montante sinistro
box(WX, WX + T, Y0, Y1, ZB - T, ZT + T, frame_mat)            # montante destro
box(-WX, WX, Y0, Y1, ZT, ZT + T, frame_mat)                   # traversa alta (tra i montanti: niente facce sovrapposte)
box(-WX, WX, Y0, Y1, ZB - T, ZB, frame_mat)                   # traversa bassa
box(WX + T, WX + T + 0.012, 0.58, 0.88, 1.38, 1.41, glow_mat) # spia laterale
box(-0.12, 0.12, 0.92, 1.00, 1.16, 1.19, glow_mat)            # finestrella dell'emettitore
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
LY = 0.66
lm.from_pydata([(-WX, LY, ZB), (WX, LY, ZB), (WX, LY, ZT), (-WX, LY, ZT)], [], [(0, 1, 2, 3)])
uv = lm.uv_layers.new(name='UVMap')
k = 0.5 * (ZT - ZB) / (2 * WX)  # UV quadrate: il reticolo resta rotondo anche se il vetro è rettangolare
for i, c in enumerate([(0, 0.5 - k), (1, 0.5 - k), (1, 0.5 + k), (0, 0.5 + k)]):
    uv.data[i].uv = c
lens = bpy.data.objects.new('SciRifle_Lens', lm)
bpy.context.scene.collection.objects.link(lens)
lens.data.materials.append(bpy.data.materials.new('SR_Lens'))
keep.append(lens)
for o in keep:
    print(o.name, "dims", tuple(round(x, 3) for x in o.dimensions), "slots", [s.material.name for s in o.material_slots])
bpy.ops.export_scene.fbx(filepath=out_path, object_types={'MESH'}, use_selection=False, apply_scale_options='FBX_SCALE_UNITS',
                         bake_anim=False, path_mode='STRIP', mesh_smooth_type='FACE')
print("EXPORTED", out_path)
