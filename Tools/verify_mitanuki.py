"""Verify the actual Unity FBX by reimporting it, without touching source files."""
from pathlib import Path
import math
import json
import bpy
root=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Marubatsu/Art/Mitanuki/Mitanuki.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(meshes)==1
ob=meshes[0]
ob.data.calc_loop_triangles()
triangles=len(ob.data.loop_triangles)
stats=json.loads((root/'ArtSource/Mitanuki/unity_model_stats.json').read_text())
assert 0<triangles==stats['triangles']<=4600
assert len(ob.data.materials)==7
assert {p.material_index for p in ob.data.polygons}==set(range(7))
assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
assert all(p.area>0 for p in ob.data.polygons)
print('MITANUKI_FBX_OK',triangles,'triangles; one mesh; seven materials')
