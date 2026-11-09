"""Create Yaguchi from the Rikoten official illustration; Blender 4.1."""
import bpy
import math
import json
import sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'ArtSource/Yaguchi'
ASSET=ROOT/'Assets/Marubatsu/Art/Yaguchi'
SOURCE.mkdir(parents=True,exist_ok=True)
ASSET.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts=[]

def mat(name,color):
    m=bpy.data.materials.new('Yaguchi_'+name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=.7
    return m
warm=mat('Warm',(.95,.18,.12))
cream=mat('Cream',(1,.78,.32))
ink=mat('Ink',(.10,.035,.02))
blue=mat('Blue',(.045,.23,.56))
red=mat('Mouth',(.87,.07,.12))
metal=mat('Metal',(.56,.62,.72))
# Small generated texture, used only for the official illustration's warm shading.
texture=bpy.data.images.new('YaguchiWarm',width=16,height=128)
stops=[(0,(.69,.20,.43)),(.45,(.96,.16,.19)),(1,(1,.64,.08))]
pixels=[]
for row in range(128):
    t=row/127
    a,b=(stops[0],stops[1]) if t<.45 else (stops[1],stops[2])
    f=(t-a[0])/(b[0]-a[0])
    color=[a[1][k]*(1-f)+b[1][k]*f for k in range(3)]
    for col in range(16):pixels.extend((*color,1))
texture.pixels=pixels
texture.filepath_raw=str(ASSET/'YaguchiWarm.png')
texture.file_format='PNG'
texture.save()
nodes=warm.node_tree.nodes
image=nodes.new('ShaderNodeTexImage');image.image=texture
warm.node_tree.links.new(image.outputs['Color'],next(n for n in nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])

def register(ob,name,material,bone):
    ob.name=name
    ob.data.materials.append(material)
    group=ob.vertex_groups.new(name='Bind_'+bone)
    group.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    for p in ob.data.polygons:p.use_smooth=True
    parts.append(ob)
    return ob

def sphere(name,loc,scale,material,bone,seg=28,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc)
    ob=bpy.context.object;ob.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    register(ob,name,material,bone)
    if material==warm:
        for loop in ob.data.loops:
            z=ob.data.vertices[loop.vertex_index].co.z
            ob.data.uv_layers.active.data[loop.index].uv=(.5,(z/scale[2]+1)/2)
    return ob

def tube(name,coords,radius,material,bone):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D'
    c.resolution_u=4;c.bevel_depth=radius;c.bevel_resolution=1;c.use_fill_caps=True
    s=c.splines.new('POLY');s.points.add(len(coords)-1)
    for p,co in zip(s.points,coords):p.co=(*co,1)
    ob=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True)
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
    return register(ob,name,material,bone)

def spring(name,start,end,radius,turns,bone):
    start=Vector(start);end=Vector(end);axis=(end-start).normalized()
    u=axis.cross(Vector((0,1,0))).normalized();v=axis.cross(u)
    points=[]
    steps=32
    for i in range(steps+1):
        t=i/steps;angle=t*turns*2*math.pi
        points.append(start.lerp(end,t)+radius*(math.cos(angle)*u+math.sin(angle)*v))
    verts=[];faces=[];sides=6
    for i,p in enumerate(points):
        tangent=(points[min(steps,i+1)]-points[max(0,i-1)]).normalized()
        n=tangent.cross(axis).normalized();b=tangent.cross(n).normalized()
        for j in range(sides):
            angle=j*2*math.pi/sides
            verts.append(p+.019*(math.cos(angle)*n+math.sin(angle)*b))
    for i in range(steps):
        for j in range(sides):
            a=i*sides+j;nextj=i*sides+(j+1)%sides
            faces.append((a,nextj,nextj+sides,a+sides))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    register(ob,name,metal,bone)
    tube(name+'Core',[start,end],.035,metal,bone)

head=sphere('Head',(0,0,1.73),(.79,.61,.57),warm,'Head',40,18)
head.data.materials.append(cream)
for p in head.data.polygons:
    center=p.center
    if abs(math.atan2(center.x/.79,-center.y/.61))<.62:p.material_index=1
# Flared blue shell: narrow neck, wide hem, softly rounded edges.
bpy.ops.mesh.primitive_cone_add(vertices=48,radius1=.55,radius2=.32,depth=.79,location=(0,0,.84))
body=bpy.context.object;body.scale.y=.72
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
register(body,'Body',blue,'Body')
bevel=body.modifiers.new('Soft rim','BEVEL');bevel.width=.045;bevel.segments=3
bpy.ops.object.modifier_apply(modifier=bevel.name)
for side,sign in [('L',1),('R',-1)]:
    start=(sign*.34,0,1.11);end=(sign*.83,-.025,.93)
    spring('ArmSpring_'+side,start,end,.060,4,'Arm_'+side)
    sphere('Hand_'+side,(sign*.91,-.03,.90),(.235,.22,.235),warm,'Arm_'+side)
    spring('LegSpring_'+side,(sign*.23,0,.46),(sign*.23,0,.18),.055,3,'Leg_'+side)
    shoe=sphere('Shoe_'+side,(sign*.34,-.075,.13),(.27,.25,.13),warm,'Leg_'+side)
    for v in shoe.data.vertices:v.co.z+=.06*max(0,sign*v.co.x/.27)**2

def front(x,z):return -.61*math.sqrt(max(.01,1-(x/.79)**2-((z-1.73)/.57)**2))
def fitted(ob,offset,flatten=.2):
    bpy.context.view_layer.update()
    inv=ob.matrix_world.inverted()
    for v in ob.data.vertices:
        co=ob.matrix_world@v.co
        co.y=front(co.x,co.z)-offset+v.co.y*flatten
        v.co=inv@co
for side,sign in [('L',1),('R',-1)]:
    eye=sphere('Eye_'+side,(sign*.23,0,1.77),(.037,.025,.043),ink,'Head',20,12)
    fitted(eye,.012)
    eye.vertex_groups.new(name='BlinkEye_'+side).add(list(range(len(eye.data.vertices))),1,'REPLACE')

# Rectangular open mouth with its pale lower half, matching the official drawing.
def mouth_patch(name,width,z0,z1,material,offset):
    verts=[];faces=[]
    nx=10;nz=8
    for j in range(nz+1):
        z=z0+(z1-z0)*j/nz
        for i in range(nx+1):
            x=width*(i/nx-.5)
            verts.append((x,front(x,z)-offset,z))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    return register(ob,name,material,'Head')
mouth_patch('MouthOutline',.40,1.27,1.65,ink,.012)
mouth_patch('MouthUpper',.36,1.46,1.63,red,.020)
mouth_patch('MouthLower',.36,1.29,1.45,cream,.020)

def body_y(x,z):
    radius=.55-(z-.445)/.79*.23
    return -.72*math.sqrt(max(.005,radius*radius-x*x))
for sign in (-1,1):
    x=sign*.205;z=1.04;y=body_y(x,z)-.018
    sphere('ChestBadge',(x,y,z),(.115,.025,.115),cream,'Body',24,14)
    for direction in (-1,1):
        bpy.ops.mesh.primitive_cube_add(size=1,location=(x,y-.030,z))
        bar=bpy.context.object;bar.scale=(.15,.020,.034)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        bar.rotation_euler[1]=direction*math.pi/4
        register(bar,'ChestCross',blue,'Body')
        for face in bar.data.polygons:face.use_smooth=False
coords=[]
corners=[(-.25,.48),(-.22,.79),(.22,.79),(.25,.48)]
for start,end in zip(corners,corners[1:]):
    for i in range(9):
        t=i/8;x=start[0]*(1-t)+end[0]*t;z=start[1]*(1-t)+end[1]*t
        coords.append((x,body_y(x,z)-.018,z))
tube('PocketTrim',coords,.014,cream,'Body')

root=bpy.data.objects.new('Yaguchi',None);bpy.context.collection.objects.link(root)
for ob in parts:ob.parent=root
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
bpy.ops.object.camera_add(location=(3,-8,2.8));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,1.15))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=2.95;scene.camera=camera
for loc,power in [((-3,-4,6),550),((4,-2,3),300),((1,3,5),550)]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object
    light.data.energy=power;light.data.size=4
    light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=950;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.world.color=(.18,.18,.18)
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Yaguchi.blend'))
def count(obs):
    for ob in obs:ob.data.calc_loop_triangles()
    return sum(len(ob.data.loop_triangles) for ob in obs)
original=count(parts)
protected=[ob for ob in parts if ob==head or ob.name.startswith('ChestCross') or ('Spring' in ob.name and 'Core' not in ob.name)]
fixed=count(protected)
ratio=min(1,(4450-fixed)/(original-fixed))
for ob in parts:
    if ob in protected:continue
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Unity budget','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
mesh=bpy.context.object;mesh.name='YaguchiMesh'
import bmesh
bm=bmesh.new();bm.from_mesh(mesh.data)
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
bm.to_mesh(mesh.data);bm.free();mesh.data.update()
triangles=count([mesh]);assert triangles<=4600
(SOURCE/'unity_model_stats.json').write_text(json.dumps({'source_triangles':original,'triangles':triangles,'mesh_objects':1,'materials':len(mesh.data.materials),'rigged':False},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Yaguchi_Unity.blend'))
for label,loc in [('preview',(3,-8,2.8)),('side',(8,0,2.3)),('back',(3,8,2.8))]:
    camera.location=loc;camera.rotation_euler=(Vector((0,0,1.15))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(SOURCE/f'Yaguchi_{label}.png');bpy.ops.render.render(write_still=True)
sys.dont_write_bytecode=True;sys.path.insert(0,str(ROOT/'Tools'))
from rig_characters import build_rig
build_rig('Yaguchi',render=False)
print('YAGUCHI_COMPLETE',triangles)
