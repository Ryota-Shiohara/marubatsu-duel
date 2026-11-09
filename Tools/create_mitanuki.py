"""Blender 4.1: --background --factory-startup --python Tools/create_mitanuki.py"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'ArtSource/Mitanuki'
ASSET = ROOT/'Assets/Marubatsu/Art/Mitanuki'
SOURCE.mkdir(parents=True, exist_ok=True)
ASSET.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts=[]

def mat(name, rgb):
    m=bpy.data.materials.new('Mitanuki_'+name)
    m.diffuse_color=(*rgb,1)
    m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=.78
    return m

tan=mat('Fur',(.49,.285,.105))
brown=mat('Brown',(.26,.125,.035))
white=mat('Cream',(.94,.925,.85))
ink=mat('Ink',(.009,.008,.012))
red=mat('Red',(.80,.015,.045))
navy=mat('Navy',(.012,.027,.12))
yellow=mat('Yellow',(.98,.71,.012))

def sphere(name,loc,scale,material,seg=28,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc)
    ob=bpy.context.object
    ob.name=name
    ob.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(material)
    for f in ob.data.polygons:f.use_smooth=True
    parts.append(ob)
    return ob

def tube(name, points, radius, material):
    c=bpy.data.curves.new(name,'CURVE')
    c.dimensions='3D'
    c.resolution_u=5
    c.bevel_depth=radius
    c.bevel_resolution=2
    c.use_fill_caps=True
    s=c.splines.new('BEZIER')
    s.bezier_points.add(len(points)-1)
    for p,co in zip(s.bezier_points,points):
        p.co=co
        p.handle_left_type=p.handle_right_type='AUTO'
    ob=bpy.data.objects.new(name,c)
    bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH')
    ob.data.materials.append(material)
    for p in ob.data.polygons:p.use_smooth=True
    parts.append(ob)
    return ob

# Front -Y, up Z. Two plump volumes match the illustration and costume photos.
body=sphere('Body',(0,0,.65),(.59,.57,.57),tan,32,20)
head=sphere('Head',(0,-.025,1.48),(.67,.64,.57),tan,40,24)
sphere('Belly',(0,-.383,.63),(.425,.075,.355),brown)
for sign in (-1,1):
    sphere('Foot', (sign*.33,-.08,.11),(.24,.28,.11),tan,24,12)
    arm=sphere('Arm',(sign*.62,-.015,.73),(.145,.16,.26),tan,24,14)
    arm.rotation_euler[1]=sign*.65
    # Rounded taper gives the ears a small triangular outline, not bear ears.
    ear=sphere('Ear',(sign*.45,-.005,1.92),(.17,.14,.255),tan,24,16)
    for v in ear.data.vertices:
        t=v.co.z/.255
        v.co.x*=1-.42*t
    ear.rotation_euler[1]=sign*.30

def face_y(x,z):
    return -.025-.48*math.sqrt(max(.01,1-(x/.67)**2-((z-1.48)/.57)**2))

for sign in (-1,1):
    x=sign*.27
    z=1.60
    mask=sphere('EyeMask',(x,face_y(x,z)-.008,z),(.145,.038,.193),brown,24,16)
    mask.rotation_euler[1]=-sign*.17
    sphere('Eye',(x,face_y(x,z)-.044,z+.018),(.083,.036,.111),ink,24,16)
    sphere('EyeShine',(x-.023,face_y(x,z)-.077,z+.056),(.033,.012,.043),white,16,10)
    x=sign*.49
    z=1.35
    cheek=sphere('Cheek',(x,face_y(x,z)-.015,z),(.105,.026,.103),red,24,14)
    cheek.rotation_euler[2]=sign*.55

# Broad pale muzzle, small button nose and gentle double smile.
sphere('Muzzle',(0,-.488,1.285),(.265,.092,.16),white,32,18)
sphere('Nose',(0,-.575,1.414),(.057,.026,.039),ink,20,12)
tube('SmileLeft',[(0,-.584,1.305),(-.046,-.587,1.262),(-.10,-.580,1.267),(-.126,-.565,1.305)],.009,ink)
tube('SmileRight',[(0,-.584,1.305),(.046,-.587,1.262),(.10,-.580,1.267),(.126,-.565,1.305)],.009,ink)
for sign in (-1,1):
    tube('BellyCross',[(sign*.047,-.454,.46),(-sign*.047,-.462,.55)],.012,yellow)

# A continuous striped tail around the neck, with its root on the lower back.
def sweep(name, centers, radii, stripe_length, closed=False, sides=10):
    verts=[]
    faces=[]
    n=len(centers)
    for i,co in enumerate(centers):
        tangent=Vector(centers[(i+1)%n if closed else min(n-1,i+1)])-Vector(centers[(i-1)%n if closed else max(0,i-1)])
        tangent.normalize()
        # A fixed transverse reference avoids sudden frame flips along the tail.
        axis=Vector((0,0,1)) if closed else Vector((0,1,0))
        u=tangent.cross(axis).normalized()
        v=tangent.cross(u).normalized()
        for j in range(sides):
            a=2*math.pi*j/sides
            verts.append(Vector(co)+radii[i]*(math.cos(a)*u+math.sin(a)*v))
    for i in range(n if closed else n-1):
        for j in range(sides):
            faces.append((i*sides+j,i*sides+(j+1)%sides,((i+1)%n)*sides+(j+1)%sides,((i+1)%n)*sides+j))
    if not closed:
        faces.extend([tuple(reversed(range(sides))),tuple((n-1)*sides+j for j in range(sides))])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    ob=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(ob)
    for m in (red,navy):mesh.materials.append(m)
    for f in mesh.polygons:
        f.material_index=(f.index//sides//stripe_length)%2
        f.use_smooth=True
    parts.append(ob)
    return ob

n=40
sweep('Scarf',[(.52*math.cos(2*math.pi*i/n),.48*math.sin(2*math.pi*i/n),1.075) for i in range(n)],[.105]*n,3,True,8)
# Viewed from behind, the tail descends on the right then curls across the rump.
centers=[]
for i in range(25):
    t=i/24
    centers.append((.48*math.sin(t*math.pi*.78)-.16*(1-t),.47+.12*math.sin(math.pi*t),.23+.84*t))
sweep('Tail',centers,[.065+.033*math.sin(math.pi*i/24) for i in range(25)],3,False,8)

root=bpy.data.objects.new('Mitanuki',None)
bpy.context.collection.objects.link(root)
for ob in parts:ob.parent=root
bpy.context.scene.unit_settings.system='METRIC'

def aim(ob,at=(0,0,1.05)):ob.rotation_euler=(Vector(at)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3,-8,2.7))
camera=bpy.context.object
aim(camera)
camera.data.type='ORTHO'
camera.data.ortho_scale=2.8
scene=bpy.context.scene
scene.camera=camera
for loc,power,size in [((-3,-4,6),550,5),((4,-2,3),300,4),((1,3,5),550,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    light=bpy.context.object
    light.data.energy=power
    light.data.shape='DISK'
    light.data.size=size
    aim(light)
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.world.color=(.18,.18,.18)
scene.render.resolution_x=900
scene.render.resolution_y=950
scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'

# Fit the feature meshes to the actual triangulated surface, not tangent planes.
# Retain the unmodified feature coordinates so both source and low-poly variants
# can be fitted independently to their own head/body geometry.
feature_templates={ob.name:[v.co.copy() for v in ob.data.vertices] for ob in parts}
def conform_features():
    bpy.context.view_layer.update()
    def fit(ob,target,depth,offset):
        inv=ob.matrix_world.inverted()
        target_inv=target.matrix_world.inverted()
        for v in ob.data.vertices:
            co=ob.matrix_world @ v.co
            local_depth=v.co.y
            origin=target_inv @ Vector((co.x,-3,co.z))
            direction=(target_inv.to_3x3() @ Vector((0,1,0))).normalized()
            hit,location,normal,index=target.ray_cast(origin,direction)
            if not hit:raise RuntimeError('Surface fit missed: '+ob.name)
            co.y=(target.matrix_world @ location).y-offset+local_depth*depth
            v.co=inv @ co
        ob.data.update()
    for ob in parts:
        name=ob.name.split('.')[0]
        if name=='EyeMask':fit(ob,head,.08,.009)
        elif name=='Eye':fit(ob,head,.10,.022)
        elif name=='EyeShine':fit(ob,head,.10,.031)
        elif name=='Cheek':fit(ob,head,.08,.010)
        elif name=='Belly':fit(ob,body,.12,.035)
        elif name=='Muzzle':fit(ob,head,.50,.048)
    muzzle=next(ob for ob in parts if ob.name=='Muzzle')
    belly=next(ob for ob in parts if ob.name=='Belly')
    for ob in parts:
        name=ob.name.split('.')[0]
        if name=='Nose':fit(ob,head,.65,.095)
        elif name in {'SmileLeft','SmileRight','BellyCross'}:
            # Curves were converted with world-space vertices: center their depth.
            middle=sum(v.co.y for v in ob.data.vertices)/len(ob.data.vertices)
            ob.location.y+=middle
            for v in ob.data.vertices:v.co.y-=middle
            bpy.context.view_layer.update()
            fit(ob,belly if name=='BellyCross' else muzzle,.45,.008)

conform_features()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Mitanuki.blend'))
# Restore the feature templates before decimation and refit to the final mesh.
for ob in parts:
    if ob.name.split('.')[0] in {'SmileLeft','SmileRight','BellyCross'}:
        ob.location.y=0
    for v,co in zip(ob.data.vertices,feature_templates[ob.name]):v.co=co
    ob.data.update()

def count(objects):
    for ob in objects:ob.data.calc_loop_triangles()
    return sum(len(ob.data.loop_triangles) for ob in objects)

original=count(parts)
protected=[ob for ob in parts if ob.name in {'Scarf','Tail'}]
fixed=count(protected)
ratio=min(1,(4450-fixed)/(original-fixed))
for ob in parts:
    if ob in protected:continue
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Unity budget','DECIMATE')
    mod.ratio=ratio
    mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
conform_features()
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
mesh=bpy.context.object
mesh.name='MitanukiMesh'
root.select_set(True)
triangles=count([mesh])
assert triangles<=4600,triangles
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Mitanuki.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    bake_space_transform=True,add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
stats={'source_triangles':original,'triangles':triangles,'mesh_objects':1,
       'materials':len(mesh.data.materials),'nine_instances_triangles':triangles*9,'rigged':False}
(SOURCE/'unity_model_stats.json').write_text(json.dumps(stats,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Mitanuki_Unity.blend'))
for label,loc in [('preview',(3,-8,2.7)),('front',(0,-8,2.1)),('back',(3,8,2.7)),('side',(8,0,2.1))]:
    camera.location=loc
    aim(camera)
    scene.render.filepath=str(SOURCE/('Mitanuki_'+label+'.png'))
    bpy.ops.render.render(write_still=True)
print('MITANUKI_COMPLETE',json.dumps(stats))
import sys
sys.dont_write_bytecode=True
sys.path.insert(0,str(ROOT/'Tools'))
from rig_characters import build_rig
build_rig('Mitanuki',render=False)
