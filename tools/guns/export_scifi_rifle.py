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
for o in keep:
    print(o.name, "dims", tuple(round(x, 3) for x in o.dimensions), "slots", [s.material.name for s in o.material_slots])
bpy.ops.export_scene.fbx(filepath=out_path, object_types={'MESH'}, use_selection=False, apply_scale_options='FBX_SCALE_UNITS',
                         bake_anim=False, path_mode='STRIP', mesh_smooth_type='FACE')
print("EXPORTED", out_path)
