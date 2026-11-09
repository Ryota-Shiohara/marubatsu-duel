"""Build Unity's lightweight FBX from the preserved editable Blender model."""
from pathlib import Path
import bpy
import json

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Yagapon'
ASSET = ROOT / 'Assets/Marubatsu/Art/Yagapon'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'Yagapon.blend'))
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']

def triangle_count(objects):
    for ob in objects: ob.data.calc_loop_triangles()
    return sum(len(ob.data.loop_triangles) for ob in objects)

original = triangle_count(parts)
for ob in parts:
    budget = (2600 if ob.name == 'Body' else
              180 if ob.name.startswith('Eye') else
              160 if ob.name in {'HappyMouth', 'PinkTongue'} else
              240 if ob.name == 'ArrowShaft' else 90)
    count = triangle_count([ob])
    if count > budget:
        bpy.context.view_layer.objects.active = ob
        modifier = ob.modifiers.new('Unity triangle budget', 'DECIMATE')
        modifier.ratio = budget / count
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)

# One renderer, preserving six material groups. This is not a single draw call.
bpy.ops.object.select_all(action='DESELECT')
for ob in parts: ob.select_set(True)
bpy.context.view_layer.objects.active = next(o for o in parts if o.name == 'Body')
bpy.ops.object.join()
mesh = bpy.context.object
mesh.name = 'YagaponMesh'
triangles = triangle_count([mesh])
assert triangles <= 4600, triangles
root = bpy.data.objects.get('Yagapon')
root.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Yagapon.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, bake_space_transform=True, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE')
stats = {'source_triangles': original, 'triangles': triangles,
         'mesh_objects': 1, 'materials': len(mesh.data.materials),
         'nine_instances_triangles': triangles * 9, 'rigged': False}
(SOURCE/'unity_model_stats.json').write_text(json.dumps(stats, indent=2))
scene = bpy.context.scene
scene.render.filepath = str(SOURCE/'Yagapon_unity_preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Yagapon_Unity.blend'))
bpy.ops.render.render(write_still=True)
from mathutils import Vector
camera = scene.camera
camera.location = (0,-8,2.1)
camera.rotation_euler = (Vector((0,0,1.06))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath = str(SOURCE/'Yagapon_unity_side.png')
bpy.ops.render.render(write_still=True)
print('UNITY_OPTIMIZED', json.dumps(stats))
import sys
sys.dont_write_bytecode=True
sys.path.insert(0,str(ROOT/'Tools'))
from rig_characters import build_rig
build_rig('Yagapon',render=False)
