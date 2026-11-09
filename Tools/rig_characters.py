"""Add deterministic lightweight rigs to the preserved Unity meshes (Blender 4.1)."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree
import sys
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from inspect_character_meshes import islands

ROOT=Path(__file__).resolve().parents[1]

def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)

def build_rig(name, render=True):
    source=ROOT/f'ArtSource/{name}'
    bpy.ops.wm.open_mainfile(filepath=str(source/f'{name}_Unity.blend'))
    ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    root=bpy.data.objects.get(name)
    # World-space rest vertices and identity mesh transforms simplify FBX skinning.
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    components=islands(ob.data)
    mats={v:ob.data.materials[p.material_index].name for p in ob.data.polygons for v in p.vertices}
    centers=[sum((ob.data.vertices[i].co for i in ids),Vector())/len(ids) for ids in components]
    mitanuki=name=='Mitanuki'
    yaguchi=name=='Yaguchi'
    front_facing=mitanuki or yaguchi
    tags={}
    if yaguchi:
        tags={v.index:[ob.vertex_groups[g.group].name for g in v.groups if g.weight>.5] for v in ob.data.vertices}
        for group in list(ob.vertex_groups):ob.vertex_groups.remove(group)
    definitions=[('Root',(0,0,0),(0,0,.25),None),
                 ('Body',(0,0,.55),(0,0,1.05),'Root'),
                 ('Head',(0,0,1.08),(0,0,1.75),'Body')]
    if yaguchi:
        for sign,side in [(1,'L'),(-1,'R')]:
            definitions.extend([(f'Arm_{side}',(sign*.34,0,1.11),(sign*.91,-.03,.90),'Body'),
                                (f'Leg_{side}',(sign*.23,0,.46),(sign*.30,-.075,.105),'Body')])
    elif mitanuki:
        for sign,side in [(1,'L'),(-1,'R')]:
            definitions.extend([(f'Arm_{side}',(sign*.48,0,.65),(sign*.77,0,.90),'Body'),
                                (f'Leg_{side}',(sign*.33,0,.24),(sign*.33,-.14,.08),'Body')])
        definitions.extend([('Tail_01',(-.16,.47,.23),(.18,.57,.51),'Body'),
                            ('Tail_02',(.18,.57,.51),(.37,.57,.79),'Tail_01'),
                            ('Tail_03',(.37,.57,.79),(.30,.47,1.07),'Tail_02')])
    else:
        definitions.extend([('Arm_L',(-.18,-.62,1.12),(-.46,-1.03,1.12),'Body'),
                            ('Arm_R',(-.02,.66,1.24),(-.04,1.23,1.70),'Body')])
        for sign,side in [(-1,'L'),(1,'R')]:
            definitions.append((f'Leg_{side}',(-.12,sign*.43,.55),(-.65,sign*.46,.09),'Body'))
        definitions.append(('Arrow',(0,0,.39),(-.7,0,.33),'Root'))
    arm=bpy.data.armatures.new(name+'Skeleton')
    rig=bpy.data.objects.new('Rig',arm)
    bpy.context.collection.objects.link(rig)
    rig.parent=root
    rig.show_in_front=True
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for key,head,tail,parent in definitions:
        bone=arm.edit_bones.new(key)
        bone.head=head;bone.tail=tail
        if parent:bone.parent=arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    groups={key:ob.vertex_groups.new(name=key) for key,_,_,_ in definitions}
    eyes={'L':[],'R':[]}
    highlights={'L':[],'R':[]}
    skin_ids=[]
    def weight(i,weights):
        weights={key:value for key,value in weights.items() if value>1e-6}
        total=sum(weights.values())
        for key,value in weights.items():groups[key].add([i],value/total,'REPLACE')
    for ids,c in zip(components,centers):
        material=mats[ids[0]].split('_',1)[1]
        if yaguchi:
            if len(ids)>300 and c.z>1.4:skin_ids+=ids
            for i in ids:
                bind=next(tag[5:] for tag in tags[i] if tag.startswith('Bind_'))
                weight(i,{bind:1})
                for side in ('L','R'):
                    if 'BlinkEye_'+side in tags[i]:eyes[side].append(i)
        elif mitanuki:
            side='L' if c.x>0 else 'R'
            if material=='Fur' and .95<c.z<1.65:skin_ids+=ids
            if material=='Ink' and c.z>1.5:eyes[side]+=ids
            if material=='Cream' and c.z>1.5:highlights[side]+=ids
            if material=='Fur' and abs(c.x)>.55 and c.z<1.1:bone='Arm_'+side
            elif material=='Fur' and c.z<.25:bone='Leg_'+side
            elif c.z>1.20:bone='Head'
            else:bone='Body'
            is_tail=c.y>.35 and c.z<.9 and material in ('Red','Navy')
            for i in ids:
                if is_tail:
                    z=ob.data.vertices[i].co.z
                    a=smooth(.36,.66,z);b=smooth(.68,.96,z)
                    weight(i,{'Tail_01':1-a,'Tail_02':a*(1-b),'Tail_03':b})
                else:weight(i,{bone:1})
        else:
            side='L' if c.y<0 else 'R'
            if material=='Ink' and c.z>1.6:eyes[side]+=ids
            if material=='White':
                skin_ids+=ids
                for i in ids:
                    p=ob.data.vertices[i].co
                    head=smooth(1.12,1.52,p.z)
                    wa=smooth(.64,1.0,abs(p.y))*smooth(.68,1.06,p.z)
                    wl=(1-smooth(.23,.57,p.z))*smooth(.20,.38,abs(p.y))
                    side='L' if p.y<0 else 'R'
                    rest=max(0,1-wa-wl)
                    weight(i,{'Arm_'+side:wa,'Leg_'+side:wl,'Head':rest*head,'Body':rest*(1-head)})
            else:
                bone='Head' if material in ('Ink','Tongue') else 'Arrow'
                for i in ids:weight(i,{bone:1})

    # Blink travels along the actual head surface so the closed eye never sinks
    # into the cheek. Keep the brown eye markings unchanged; shrink white shine.
    skin_set=set(skin_ids)
    polys=[tuple(p.vertices) for p in ob.data.polygons if all(i in skin_set for i in p.vertices)]
    surface=BVHTree.FromPolygons([v.co for v in ob.data.vertices],polys)
    axis=1 if front_facing else 0
    def front_surface(co):
        start=co.copy();start[axis]=-3
        direction=Vector((0,0,0));direction[axis]=1
        hit=surface.ray_cast(start,direction)
        assert hit[0] is not None,'Blink projection missed body'
        return hit[0][axis]
    ob.shape_key_add(name='Basis')
    targets={}
    for side in ('L','R'):
        assert eyes[side],name+' missing eye '+side
        center=sum((ob.data.vertices[i].co for i in eyes[side]),Vector())/len(eyes[side])
        target={}
        for i in eyes[side]+highlights[side]:
            co=ob.data.vertices[i].co.copy()
            is_shine=i in highlights[side]
            co.z=center.z+(co.z-center.z)*.06
            if is_shine:
                lateral=0 if front_facing else 1
                co[lateral]=center[lateral]+(co[lateral]-center[lateral])*.12
            # Offset is measured from the base eye to preserve the visible layer.
            old=ob.data.vertices[i].co
            offset=old[axis]-front_surface(old)
            co[axis]=front_surface(co)+(offset if not is_shine else .025)
            target[i]=co
        targets[side]=target
    for key,sides in [('Blink_L',('L',)),('Blink_R',('R',)),('Blink',('L','R'))]:
        shape=ob.shape_key_add(name=key)
        for side in sides:
            for i,co in targets[side].items():shape.data[i].co=co
    modifier=ob.modifiers.new('Character skin','ARMATURE')
    modifier.object=rig
    ob.parent=rig
    # No new geometry: only skin weights and shape-key deltas.
    ob.data.calc_loop_triangles()
    assert all(v.groups and abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in ob.data.vertices)
    assert max(len(v.groups) for v in ob.data.vertices)<=4
    bpy.ops.object.select_all(action='DESELECT')
    for item in (root,rig,ob):item.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(ROOT/f'Assets/Marubatsu/Art/{name}/{name}.fbx'),
        use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        bake_space_transform=False,add_leaf_bones=False,use_armature_deform_only=True,
        use_mesh_modifiers=False,bake_anim=False,mesh_smooth_type='FACE')
    stats=json.loads((source/'unity_model_stats.json').read_text())
    stats.update(rigged=True,bones=len(definitions),blend_shapes=['Blink_L','Blink_R','Blink'],
                 max_weights=max(len(v.groups) for v in ob.data.vertices))
    (source/'rig_stats.json').write_text(json.dumps(stats,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(source/f'{name}_Rigged.blend'))
    if render:
        scene=bpy.context.scene
        scene.cycles.samples=24
        scene.render.resolution_percentage=80
        camera=scene.camera
        camera.location=(3,-8,2.7) if front_facing else (-6,-8,3)
        camera.rotation_euler=(Vector((0,0,1.05))-camera.location).to_track_quat('-Z','Y').to_euler()
        def rotate(key,axis,deg):
            pb=rig.pose.bones[key]
            pb.rotation_mode='QUATERNION'
            pb.rotation_quaternion=Quaternion(pb.bone.matrix_local.to_3x3().inverted()@Vector(axis),math.radians(deg))
        for pose in ('wave','victory','blink'):
            for pb in rig.pose.bones:pb.rotation_quaternion=Quaternion()
            ob.data.shape_keys.key_blocks['Blink'].value=0
            if pose=='wave':
                rotate('Arm_L',(0,1,0) if front_facing else (1,0,0),-35 if front_facing else -30)
                rotate('Head',(0,1,0) if front_facing else (1,0,0),6)
            elif pose=='victory':
                rotate('Arm_L',(0,1,0) if front_facing else (1,0,0),-50 if front_facing else -35)
                rotate('Arm_R',(0,1,0) if front_facing else (1,0,0),50 if front_facing else 15)
                if mitanuki:rotate('Tail_02',(0,0,1),12)
            else:ob.data.shape_keys.key_blocks['Blink'].value=1
            bpy.context.view_layer.update()
            evaluated=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
            assert all(math.isfinite(c) for v in evaluated.data.vertices for c in v.co)
            scene.render.filepath=str(source/f'{name}_rig_{pose}.png')
            bpy.ops.render.render(write_still=True)
    print('RIG_READY',name,json.dumps(stats))

if __name__=='__main__':
    for name in ('Mitanuki','Yagapon','Yaguchi'):build_rig(name,render='--no-render' not in sys.argv)
