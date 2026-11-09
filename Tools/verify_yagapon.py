"""Read exported FBX back in Blender and validate mesh geometry/materials."""
from pathlib import Path
import bpy
import math
root = Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Marubatsu/Art/Yagapon/Yagapon.fbx'))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, len(meshes)
assert len(bpy.data.materials) == 6, len(bpy.data.materials)
for ob in meshes:
    assert len(ob.data.polygons) > 0
    assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
    assert len(ob.data.materials) > 0
for ob in meshes: ob.data.calc_loop_triangles()
triangles = sum(len(ob.data.loop_triangles) for ob in meshes)
assert 0 < triangles <= 4600, triangles
assert {p.material_index for p in meshes[0].data.polygons} == set(range(6))
print('FBX_READBACK_OK', len(meshes), 'meshes',len(bpy.data.materials),'materials', triangles, 'triangles')
