"""Run with Blender 4.1 --background --python Tools/create_yagapon.py."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource' / 'Yagapon'
ASSET = ROOT / 'Assets' / 'Marubatsu' / 'Art' / 'Yagapon'
SOURCE.mkdir(parents=True, exist_ok=True)
ASSET.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = .72
    return m

white = material('Yagapon_White', (.94, .965, 1))
black = material('Yagapon_Ink', (.006, .008, .018))
pink = material('Yagapon_Tongue', (1, .29, .65))
gold = material('Yagapon_ArrowGold', (1, .66, .015))
red = material('Yagapon_ArrowRed', (.72, .035, .055))
navy = material('Yagapon_FeatherBlue', (.025, .035, .25))
parts = []

def sphere(name, loc, scale, mat, segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    ob = bpy.context.object
    ob.name = name
    ob.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(mat)
    for p in ob.data.polygons: p.use_smooth = True
    parts.append(ob)
    return ob

def tube(name, coords, radius, mat):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 8
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    curve.use_fill_caps = True
    s = curve.splines.new('BEZIER')
    s.bezier_points.add(len(coords)-1)
    for p, co in zip(s.bezier_points, coords):
        p.co = co
        p.handle_left_type = p.handle_right_type = 'AUTO'
    ob = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    ob.data.materials.append(mat)
    for p in ob.data.polygons: p.use_smooth = True
    parts.append(ob)
    return ob

def plate(name, points, y, depth, mat, bevel=.025):
    n = len(points)
    verts = [(x, y+d, z) for d in (-depth/2, depth/2) for x,z in points]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat)
    if bevel:
        mod = ob.modifiers.new('Soft edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = ob.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(ob)
    return ob

# Construct facing -Y, then turn the character to face along the arrow (-X).
# The reference is a front-left three-quarter view, not a frontal silhouette.
body = sphere('Body', (0,0,1.40), (.88,.80,.86), white, 48, 32)
for v in body.data.vertices:
    t = v.co.z/.86
    v.co.x *= 1-.07*t
    # Preserve the reference's narrower crown and full lower side silhouette,
    # independently of the rounded width across the character's shoulders.
    v.co.y = v.co.y*(1-.18*t) - .10*t
skin = [body]
skin.append(tube('RaisedArm', [(-.61,0,1.43),(-.89,-.02,1.65),(-1.14,-.04,1.78),(-1.25,-.03,1.96)], .095, white))
skin.append(sphere('RaisedPalm', (-1.14,-.04,1.78), (.16,.10,.10),white))
skin.append(tube('OpenArm',[(.53,-.29,1.37),(.79,-.46,1.28),(1.03,-.46,1.34)], .105, white))
skin.append(tube('OpenThumb',[(.80,-.46,1.28),(.79,-.47,1.13)],.075,white))
skin.append(tube('NearLeg',[(.43,-.12,.79),(.46,-.34,.48),(.46,-.59,.32)],.15,white))
skin.append(sphere('NearFoot',(.46,-.67,.31),(.16,.25,.115),white))
skin.append(tube('FarLeg',[(-.43,-.12,.79),(-.46,-.34,.48),(-.46,-.59,.32)],.15,white))
skin.append(sphere('FarFoot',(-.46,-.67,.31),(.16,.25,.115),white))
bpy.ops.object.select_all(action='DESELECT')
for ob in skin: ob.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
parts = [ob for ob in bpy.context.scene.objects if ob.type == 'MESH']
mod = body.modifiers.new('Unified soft silhouette', 'REMESH')
mod.mode = 'VOXEL'
mod.voxel_size = .025
bpy.ops.object.modifier_apply(modifier=mod.name)
mod = body.modifiers.new('Smooth clay', 'SMOOTH')
mod.factor = 1.2
mod.iterations = 5
bpy.ops.object.modifier_apply(modifier=mod.name)
mod = body.modifiers.new('Game mesh', 'DECIMATE')
mod.ratio = .32
bpy.ops.object.modifier_apply(modifier=mod.name)
for p in body.data.polygons: p.use_smooth = True
bpy.ops.object.select_all(action='DESELECT')

for name,x,z in [('EyeLeft',-.16,1.94),('EyeRight',.16,1.94)]:
    eye = sphere(name,(x,-.438,z),(.040,.026,.073),black,20,12)
plate('HappyMouth',[(-.30,1.80),(-.07,1.80),(-.075,1.75),(-.12,1.67),(-.17,1.64),(-.22,1.67),(-.28,1.74)],-.523,.045,black,.025)
plate('PinkTongue',[(-.235,1.727),(-.18,1.765),(-.10,1.765),(-.135,1.70),(-.17,1.678),(-.20,1.69)],-.553,.012,pink,.01)

# Fit the facial pieces to the round front rather than a flat image plane.
for ob in parts[1:]:
    is_mouth = ob.name in {'HappyMouth','PinkTongue'}
    for v in ob.data.vertices:
        co = ob.matrix_world @ v.co
        if is_mouth: co.x += .185
        t = (co.z-1.40)/.86
        surface = -.80*(1-.18*t)*math.sqrt(max(.001,1-t*t-(co.x/(.88*(1-.07*t)))**2)) - .10*t
        if is_mouth:
            baseline = -.553 if ob.name == 'PinkTongue' else -.523
            co.y = surface + (co.y-baseline) - (.045 if ob.name == 'PinkTongue' else .022)
        else:
            co.y = surface + (co.y-ob.location.y) - .013
        v.co = ob.matrix_world.inverted() @ co
for ob in parts:
    from mathutils import Matrix
    turn = Matrix.Rotation(-math.pi/2,4,'Z')
    ob.matrix_world = turn @ ob.matrix_world

# The arrow passes below the seated body; broad blue fletching has visible gaps.
tube('ArrowShaft',[(-1.49,0,.50),(-.60,0,.56),(0,0,.61),(.80,0,.78),(1.62,0,1.02)],.047,gold)
plate('ArrowHead',[(-1.96,.44),(-1.40,.75),(-1.35,.31)],.07,.105,red,.025)
for i in range(5):
    x=.92+i*.145
    z=.76+i*.055
    plate('FeatherUpper_%02d'%i,[(x,z+.035),(x+.13,z+.085),(x+.23,z+.48),(x+.10,z+.40)],.085,.055,navy,.012)
    plate('FeatherLower_%02d'%i,[(x,z-.025),(x+.13,z+.025),(x+.45,z-.13),(x+.29,z-.19)],.085,.055,navy,.012)

root = bpy.data.objects.new('Yagapon', None)
bpy.context.collection.objects.link(root)
for ob in parts: ob.parent = root
# Place the lowest point at zero, keeping the model centered on the body.
for ob in parts: ob.location.z -= .22
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for ob in parts: ob.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Yagapon.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, bake_space_transform=True, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE')
triangles = sum(len(ob.data.loop_triangles) for ob in parts)
for ob in parts: ob.data.calc_loop_triangles()
triangles = sum(len(ob.data.loop_triangles) for ob in parts)
(SOURCE/'model_stats.json').write_text(json.dumps({'triangles':triangles,'mesh_objects':len(parts),'rigged':False},indent=2))

def aim(ob, at): ob.rotation_euler = (Vector(at)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-6,-8,3.0))
camera = bpy.context.object
camera.name = 'PreviewCamera'
aim(camera,(0,0,1.06))
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 4.6
bpy.context.scene.camera = camera
for name,loc,power,size in [('Key',(-3,-4,6),650,5),('Fill',(4,-2,3),400,4),('Rim',(1,3,5),700,3)]:
    bpy.ops.object.light_add(type='AREA', location=loc)
    light = bpy.context.object
    light.name=name
    light.data.energy=power
    light.data.shape='DISK'
    light.data.size=size
    aim(light,(0,0,1))
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.world.color=(.18,.18,.18)
scene.render.resolution_x=1100
scene.render.resolution_y=850
scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(SOURCE/'Yagapon_preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Yagapon.blend'))
bpy.ops.render.render(write_still=True)
camera.location=(-8,0,2.1)
aim(camera,(0,0,1.06))
scene.render.filepath=str(SOURCE/'Yagapon_front.png')
bpy.ops.render.render(write_still=True)
camera.location=(0,-8,2.1)
aim(camera,(0,0,1.06))
scene.render.filepath=str(SOURCE/'Yagapon_side.png')
bpy.ops.render.render(write_still=True)
print('YAGAPON_COMPLETE',triangles)
import runpy
runpy.run_path(str(ROOT / 'Tools/optimize_yagapon.py'), run_name='__main__')
