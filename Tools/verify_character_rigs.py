"""Verify exported FBX skin and morph data, including evaluated deformation."""
from pathlib import Path
import bpy
import math
import json
from mathutils import Quaternion
ROOT=Path(__file__).resolve().parents[1]
characters=[('Mitanuki',4430,10),('Yagapon',4510,8)]
if (ROOT/'ArtSource/Yaguchi/rig_stats.json').exists():
    characters.append(('Yaguchi',json.loads((ROOT/'ArtSource/Yaguchi/rig_stats.json').read_text())['triangles'],7))
for name,triangles,bones in characters:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/f'Assets/Marubatsu/Art/{name}/{name}.fbx'))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(meshes)==len(rigs)==1
    ob=meshes[0];rig=rigs[0]
    ob.data.calc_loop_triangles()
    assert len(ob.data.loop_triangles)==triangles
    assert len(rig.data.bones)==bones
    assert all(v.groups and abs(sum(g.weight for g in v.groups)-1)<1e-4 for v in ob.data.vertices)
    assert max(len(v.groups) for v in ob.data.vertices)<=4
    assert any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers)
    keys=ob.data.shape_keys.key_blocks
    for key in ['Blink','Blink_L','Blink_R']:
        assert key in keys
        assert any((a.co-b.co).length>1e-5 for a,b in zip(keys[key].data,keys['Basis'].data))
    def snapshot():
        bpy.context.view_layer.update()
        evaluated=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
        return [v.co.copy() for v in evaluated.data.vertices]
    rest=snapshot()
    controls=['Head','Arm_L','Arm_R','Leg_L','Leg_R']
    if name=='Mitanuki':controls.append('Tail_02')
    if name=='Yagapon':controls.append('Arrow')
    for bone in controls:
        pb=rig.pose.bones[bone]
        pb.rotation_mode='QUATERNION'
        pb.rotation_quaternion=Quaternion((1,0,0),math.radians(15))
        posed=snapshot()
        assert all(math.isfinite(c) for v in posed for c in v)
        assert max((a-b).length for a,b in zip(rest,posed))>1e-5,bone
        pb.rotation_quaternion=Quaternion()
    keys['Blink'].value=1
    blink=snapshot()
    assert max((a-b).length for a,b in zip(rest,blink))>1e-5
    print('RIG_FBX_PASS',name,triangles,'triangles',bones,'bones; weights, blink and deformation checked')
